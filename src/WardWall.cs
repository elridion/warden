using System.Collections.Generic;
using UnityEngine;

namespace Warden
{
    /// <summary>
    /// The haze wall marking where ward protection ends near the local player: one mesh for all
    /// wards, built from rays cast from the camera to where they leave (or, from outside, enter) the
    /// joined spheres of every visible ward, revealed around the player. Overlapping wards are one smooth surface, rounded where
    /// they meet, so there is nothing to stitch between them. Everything that shapes the look comes
    /// from world positions, so the wall does not slide as the player moves.
    /// One per client, on its own root object, rebuilt from the registered <see cref="WardHull"/>s.
    /// </summary>
    internal class WardWall : MonoBehaviour
    {
        // The wall is animated per vertex on the CPU (no custom shaders without asset bundles), so
        // ray count and update rate are what it costs per frame. 5 degree steps put rays about
        // 2.5 m apart at the edge of the reveal.
        private const int Columns = 72;
        private const int Rows = 36;
        private const float UpdateInterval = 1f / 30f;

        // Reveal: full strength within RevealFull meters of the player, gone at RevealRadius.
        private const float RevealFull = 15f;
        private const float RevealRadius = 45f;
        // Fades the wall right at the camera, where its triangles would fill the screen.
        private const float NearFadeStart = 1.5f;
        private const float NearFadeEnd = 6f;
        // Fades where the rays skim the wall, toward a ward's outline as the camera sees it, where
        // the rays past it find nothing and the edge glow is strongest. Cosine of the angle between
        // ray and surface normal.
        private const float GrazingFade = 0.35f;

        // Edge glow: alpha rises where the surface turns away from the viewer. The base is high
        // enough that a wall faced head-on still shows.
        private const float BaseAlpha = 0.08f;
        private const float RimAlpha = 0.24f;
        private const float RimSharpness = 2.5f;
        // The glow factors multiply, so their peaks could stack into an opaque sheet.
        private const float MaxAlpha = 0.4f;

        // Bands of light drifting upward, wobbling sideways.
        private const float BandStrength = 0.35f;
        private const float BandSpacing = 5f;
        private const float BandSpeed = 0.9f;
        private const float BandWobble = 1.6f;
        private const float BandWobbleSpacing = 6f;

        // Surface waves: three travelling waves push the wall in and out and light their crests.
        // Wavelengths stay well above the ray spacing so the mesh can follow them.
        private const float WaveAmplitude = 0.4f;
        private const float WaveBrightness = 0.4f;
        private static readonly Vector4 s_wave1 = new Vector4(0.8f, 0f, 0.6f, 14f);
        private static readonly Vector4 s_wave2 = new Vector4(0f, 1f, 0f, 9f);
        private static readonly Vector4 s_wave3 = new Vector4(-0.5f, 0.3f, 0.81f, 21f);
        private const float WaveSpeed = 1.3f;

        // Slow breathing of the whole wall, so its strength alternates over time.
        private const float PulseStrength = 0.2f;
        private const float PulseSpeed = 0.7f;

        // Drifting clouds of world-space noise in the vertex alpha.
        private const float CloudScale = 0.1f;
        private const float CloudDrift = 0.25f;
        private const float CloudFloor = 0.45f;

        // The haze texture adds faint grain, mapped from world position in tiles of this many meters.
        private const float HazeTileMeters = 16f;

        // Joined wards are drawn as the smooth union of their spheres, rounded where they meet over
        // about JoinSmoothness meters; the edge glow fades out along the joint, which seen from
        // inside is a groove turned edge-on and would glow as a solid ribbon.
        private const float JoinSmoothness = 8f;

        // Ray marching: at least this many meters per step, then bisections to about 1.5 cm.
        private const float MinRayStep = 1f;
        private const int HitSteps = 6;

        // Brief brightening of a ward's part of the wall when it flashes on damage.
        private const float FlashBoost = 1.5f;
        private const float FlashDuration = 0.8f;

        private static readonly List<WardHull> s_hulls = new List<WardHull>();
        private static WardWall s_instance;
        private static Vector3[] s_directions;
        private static int[] s_quads;

        private Mesh m_mesh;
        private MeshRenderer m_renderer;
        private Vector3[] m_vertices;
        private Vector2[] m_uvs;
        private Color32[] m_colors;
        private readonly List<int> m_triangles = new List<int>();
        // Spheres near the player as (center, radius), with each one's last flash time.
        private readonly List<Vector4> m_spheres = new List<Vector4>();
        private readonly List<float> m_flashTimes = new List<float>();
        private float[] m_distances = new float[4];
        private float m_nextUpdate;
        private int m_lastScrollFrame = -1;

        public static void Add(WardHull hull)
        {
            if (!s_hulls.Contains(hull)) s_hulls.Add(hull);
            if (!s_instance) s_instance = Create();
        }

        public static void Remove(WardHull hull)
        {
            s_hulls.Remove(hull);
        }

        private static WardWall Create()
        {
            var material = WardHull.HazeMaterial;
            if (!material) return null;
            if (s_directions == null) BuildDirections();

            var go = new GameObject("warden_wall");
            var wall = go.AddComponent<WardWall>();
            int count = s_directions.Length;
            wall.m_vertices = new Vector3[count];
            wall.m_uvs = new Vector2[count];
            wall.m_colors = new Color32[count];
            wall.m_mesh = new Mesh { name = "warden_wall" };
            wall.m_mesh.MarkDynamic();

            go.AddComponent<MeshFilter>().sharedMesh = wall.m_mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            wall.m_renderer = renderer;
            return wall;
        }

        private void OnDestroy()
        {
            if (m_mesh) Destroy(m_mesh);
            if (s_instance == this) s_instance = null;
        }

        private void LateUpdate()
        {
            float time = Time.time;
            var material = m_renderer.sharedMaterial;
            if (m_lastScrollFrame != Time.frameCount)
            {
                m_lastScrollFrame = Time.frameCount;
                material.mainTextureOffset = new Vector2(time * 0.004f, -time * 0.006f);
            }

            if (time < m_nextUpdate) return;
            m_nextUpdate = Mathf.Max(m_nextUpdate + UpdateInterval, time);

            var cam = Camera.main;
            // The wall follows the player rather than the camera, which orbits a few meters away.
            var player = Player.m_localPlayer;
            Vector3 focus = player ? player.transform.position + Vector3.up : cam ? cam.transform.position : Vector3.zero;

            bool show = cam && CollectSpheres(focus);
            if (m_renderer.enabled != show) m_renderer.enabled = show;
            if (!show) return;

            // Vertices are relative to the focus, so they keep float precision far from the world origin.
            transform.position = focus;
            BuildWall(focus, cam.transform.position, time);
        }

        // The visible wards that contain the focus or whose surface may come within the reveal
        // radius of it. A containing ward always counts, however far its surface: without it the
        // player counts as outside, and the near side of a neighbouring ward's sphere, which lies
        // inside the coverage, was drawn right in front of them.
        private bool CollectSpheres(Vector3 focus)
        {
            m_spheres.Clear();
            m_flashTimes.Clear();
            foreach (var hull in s_hulls)
            {
                if (!hull || hull.Radius <= 0f) continue;

                Vector3 center = hull.transform.position;
                float radius = hull.Radius;
                float distance = Vector3.Distance(focus, center);
                if (distance - radius > RevealRadius + JoinSmoothness) continue;

                m_spheres.Add(new Vector4(center.x, center.y, center.z, radius));
                m_flashTimes.Add(hull.LastFlashTime);
            }
            return m_spheres.Count > 0;
        }

        private void BuildWall(Vector3 focus, Vector3 camPos, float time)
        {
            // Rays start at the camera, so the wall is exactly the surface the camera sees and its
            // outline is the camera's own; cast from the player, which the camera orbits, it ended
            // short of parts of the ward the camera could see. The reveal still centres on the player.
            bool inside = UnionDistance(camPos, out _) < 0f;
            // With n spheres the exponential smooth minimum lies up to JoinSmoothness * ln n inside
            // the nearest one.
            float swell = JoinSmoothness * Mathf.Log(m_spheres.Count);
            float reach = RevealRadius + Vector3.Distance(camPos, focus);
            float pulse = 1f + PulseStrength * Mathf.Sin(time * PulseSpeed);
            float wavePhase = time * WaveSpeed;
            float bandPhase = time * BandSpeed;
            float wobblePhase = time * 0.25f;

            for (int i = 0; i < s_directions.Length; i++)
            {
                Vector3 direction = s_directions[i];
                float hit = CastRay(camPos, direction, inside, swell, reach, out float closest);
                if (hit < 0f)
                {
                    // A ray that found no wall still gets a vertex, transparent, where it passed
                    // closest to it. The wall then fades out across one cell along a smooth line;
                    // dropping these triangles left an edge following the ray grid.
                    m_vertices[i] = camPos + direction * closest - focus;
                    m_colors[i] = new Color32(255, 255, 255, 0);
                    continue;
                }

                Vector3 surface = camPos + direction * hit;
                float distance = UnionDistance(surface, out Vector3 normal, out float jointGap, out int nearest);

                float wave = (Wave(s_wave1, surface, wavePhase)
                            + Wave(s_wave2, surface, -wavePhase * 0.7f)
                            + Wave(s_wave3, surface, wavePhase * 0.5f)) / 3f;
                Vector3 world = surface + normal * (WaveAmplitude * wave - distance);
                m_vertices[i] = world - focus;

                // A fixed world mapping: the grain does not slide as the rays move with the player.
                m_uvs[i] = new Vector2(
                    (world.x * 0.8f - world.z * 0.6f) / HazeTileMeters,
                    (world.y + (world.x * 0.6f + world.z * 0.8f) * 0.3f) / HazeTileMeters);

                Vector3 toVertex = world - camPos;
                float cameraDistance = toVertex.magnitude;
                float facing = Mathf.Abs(Vector3.Dot(normal, toVertex)) / Mathf.Max(cameraDistance, 0.01f);
                float rim = Mathf.Pow(1f - facing, RimSharpness) * Smooth(0f, JoinSmoothness, jointGap);

                float wobble = Mathf.Sin((world.x * 0.6f + world.z * 0.8f) / BandWobbleSpacing + wobblePhase);
                float bands = 1f + BandStrength * Mathf.Sin(world.y / BandSpacing - bandPhase + wobble * BandWobble);
                float crest = 1f + WaveBrightness * wave;

                float flash = Mathf.Clamp01(1f - (time - m_flashTimes[nearest]) / FlashDuration);
                float strength = (1f + FlashBoost * flash * flash) * pulse;
                float fade = (1f - Smooth(RevealFull, RevealRadius, Vector3.Distance(world, focus)))
                           * Smooth(NearFadeStart, NearFadeEnd, cameraDistance)
                           * Smooth(0f, GrazingFade, Mathf.Abs(Vector3.Dot(normal, direction)));

                float alpha = (BaseAlpha + RimAlpha * rim) * bands * crest * Cloud(surface, time) * strength * fade;
                m_colors[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp(alpha, 0f, MaxAlpha) * 255f));
            }

            // Fully transparent triangles are skipped, so the rays that find nothing cost no overdraw.
            m_triangles.Clear();
            for (int q = 0; q < s_quads.Length; q += 3)
            {
                int a = s_quads[q], b = s_quads[q + 1], c = s_quads[q + 2];
                if (m_colors[a].a == 0 && m_colors[b].a == 0 && m_colors[c].a == 0) continue;
                m_triangles.Add(a);
                m_triangles.Add(b);
                m_triangles.Add(c);
            }

            m_mesh.vertices = m_vertices;
            m_mesh.uv = m_uvs;
            m_mesh.colors32 = m_colors;
            m_mesh.SetTriangles(m_triangles, 0, true);
        }

        // Distance along the ray to where it first crosses the rounded union, or -1 within reach;
        // closest is where along the ray it came nearest the surface. Marches in steps that cannot
        // skip the surface, then bisects. Everything (inside or not, the hit, the normal) comes from
        // that one surface: mixing in the plain spheres left gaps and fans of triangles where the
        // rounding bulges past them.
        private float CastRay(Vector3 origin, Vector3 direction, bool inside, float swell, float reach, out float closest)
        {
            float previous = 0f, along = 0f, nearest = float.MaxValue;
            closest = 0f;
            while (along <= reach)
            {
                float distance = UnionDistance(origin + direction * along, out float exact);
                if (Mathf.Abs(distance) < nearest)
                {
                    nearest = Mathf.Abs(distance);
                    closest = along;
                }
                if ((distance < 0f) != inside)
                {
                    float low = previous, high = along;
                    for (int step = 0; step < HitSteps; step++)
                    {
                        float mid = (low + high) * 0.5f;
                        if ((UnionDistance(origin + direction * mid, out _) < 0f) == inside) low = mid;
                        else high = mid;
                    }
                    return (low + high) * 0.5f;
                }

                // Safe steps: from inside the spheres, leaving the union takes at least the depth
                // in the deepest one; from outside, the rounding brings the surface up to swell
                // meters closer than the nearest sphere.
                previous = along;
                along += Mathf.Max(inside ? -exact : exact - swell, MinRayStep);
            }
            return -1f;
        }

        // Signed distance to the rounded union only, for marching, and to the nearest plain sphere.
        private float UnionDistance(Vector3 point, out float exact)
        {
            int count = m_spheres.Count;
            if (m_distances.Length < count) m_distances = new float[count];

            exact = float.MaxValue;
            for (int s = 0; s < count; s++)
            {
                var sphere = m_spheres[s];
                float distance = Vector3.Distance(point, (Vector3)sphere) - sphere.w;
                m_distances[s] = distance;
                exact = Mathf.Min(exact, distance);
            }

            float sum = 0f;
            for (int s = 0; s < count; s++)
            {
                sum += Mathf.Exp((exact - m_distances[s]) / JoinSmoothness);
            }
            return exact - JoinSmoothness * Mathf.Log(sum);
        }

        // Signed distance to the smooth union (exponential smooth minimum) of the spheres, with its
        // gradient, how much nearer the nearest sphere is than the second nearest (small along a
        // joint), and the nearest sphere's index. Where two spheres are about equally near the union
        // swells outward by up to JoinSmoothness * ln 2, rounding the joint.
        private float UnionDistance(Vector3 point, out Vector3 gradient, out float jointGap, out int nearest)
        {
            float first = float.MaxValue, second = float.MaxValue;
            nearest = 0;
            for (int s = 0; s < m_spheres.Count; s++)
            {
                var sphere = m_spheres[s];
                float distance = Vector3.Distance(point, (Vector3)sphere) - sphere.w;
                if (distance < first)
                {
                    second = first;
                    first = distance;
                    nearest = s;
                }
                else if (distance < second)
                {
                    second = distance;
                }
            }
            jointGap = second - first;

            // Weights relative to the nearest sphere, so the exponentials stay in range.
            float sum = 0f;
            gradient = Vector3.zero;
            for (int s = 0; s < m_spheres.Count; s++)
            {
                var sphere = m_spheres[s];
                Vector3 offset = point - (Vector3)sphere;
                float length = offset.magnitude;
                float weight = Mathf.Exp((first - (length - sphere.w)) / JoinSmoothness);
                sum += weight;
                gradient += offset * (weight / Mathf.Max(length, 1e-3f));
            }
            gradient /= sum;
            if (gradient.sqrMagnitude > 1e-6f) gradient.Normalize();
            return first - JoinSmoothness * Mathf.Log(sum);
        }

        // Drifting cloud density at a world point, from CloudFloor to 1. Two Perlin planes crossing
        // at right angles, so walls facing any horizontal direction get clouds.
        private static float Cloud(Vector3 world, float time)
        {
            float drift = time * CloudDrift;
            float a = Mathf.PerlinNoise(world.x * CloudScale + drift, world.y * CloudScale - drift * 0.6f);
            float b = Mathf.PerlinNoise(world.z * CloudScale - drift * 0.8f, world.y * CloudScale + 31.7f);
            return Mathf.Lerp(CloudFloor, 1f, Smooth(0.3f, 0.7f, (a + b) * 0.5f));
        }

        // A travelling wave along the direction xyz with wavelength w in meters, from -1 to 1.
        private static float Wave(Vector4 wave, Vector3 world, float phase)
        {
            float along = wave.x * world.x + wave.y * world.y + wave.z * world.z;
            return Mathf.Sin(along * (2f * Mathf.PI) / wave.w - phase);
        }

        // Hermite step from 0 at edge0 to 1 at edge1 (Mathf.SmoothStep interpolates instead).
        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // Ray directions on a latitude/longitude grid around the player, and the grid's triangles.
        private static void BuildDirections()
        {
            s_directions = new Vector3[(Columns + 1) * (Rows + 1)];
            for (int row = 0; row <= Rows; row++)
            {
                float lat = ((float)row / Rows - 0.5f) * Mathf.PI;
                float y = Mathf.Sin(lat), r = Mathf.Cos(lat);
                for (int col = 0; col <= Columns; col++)
                {
                    float lon = (float)col / Columns * Mathf.PI * 2f;
                    s_directions[row * (Columns + 1) + col] = new Vector3(r * Mathf.Cos(lon), y, r * Mathf.Sin(lon));
                }
            }

            s_quads = new int[Columns * Rows * 6];
            int t = 0;
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    int a = row * (Columns + 1) + col;
                    int b = a + Columns + 1;
                    s_quads[t++] = a;
                    s_quads[t++] = b;
                    s_quads[t++] = a + 1;
                    s_quads[t++] = a + 1;
                    s_quads[t++] = b;
                    s_quads[t++] = b + 1;
                }
            }
        }
    }
}
