using System.Collections.Generic;
using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Animated amber haze sphere that visualizes a ward's radius. Overlapping hulls hide the
    /// parts of themselves that lie inside each other, so chained wards read as one shape.
    /// Kept as a separate root object so piece highlighting and placement tinting leave it alone.
    /// </summary>
    internal class WardHull : MonoBehaviour
    {
        // The haze is animated per vertex on the CPU (no custom shaders without asset bundles), so
        // the vertex count and update rate are what each visible hull costs per frame.
        private const int Segments = 64;
        private const int Rings = 32;
        private const float UpdateInterval = 1f / 30f;
        private const float NoiseTilesAround = 3f;
        private const float NoiseTilesVertical = 1.5f;

        // Edge glow: alpha rises where the surface turns away from the viewer.
        private const float BaseAlpha = 0.02f;
        private const float RimAlpha = 0.32f;
        private const float RimSharpness = 2.5f;

        // Bands of light drifting upward, wobbling around the sphere.
        private const float BandStrength = 0.35f;
        private const float BandCount = 7f;
        private const float BandSpeed = 0.9f;
        private const float BandWobble = 1.6f;

        // Haze swirl: per-vertex UV warp on top of the shared texture scroll.
        private const float SwirlAmount = 0.06f;
        private const float SwirlSpeed = 0.35f;

        // Width in meters of the soft seam where chained hulls meet.
        private const float MergeFade = 4f;

        private static readonly Color s_color = new Color(1f, 0.66f, 0.25f, 1f);

        private static readonly string[] s_shaderCandidates =
        {
            "Sprites/Default",
            "UI/Default",
            "Legacy Shaders/Particles/Alpha Blended",
        };

        private static Vector3[] s_unitSphere;
        private static Vector2[] s_baseUvs;
        private static int[] s_triangles;
        // Per-vertex sines and cosines of latitude/longitude terms, so the animation only needs
        // the time-dependent parts each update (sin(a + t) = sin a cos t + cos a sin t).
        private static float[] s_bandLat;
        private static float[] s_sinLon3, s_cosLon3;
        private static float[] s_sinLat4, s_cosLat4;
        private static float[] s_sinLon2, s_cosLon2;
        private static Material s_hazeMaterial;
        private static Material s_moteMaterial;
        private static bool s_failed;
        private static int s_lastScrollFrame = -1;

        private SpawnWard m_owner;
        private Mesh m_mesh;
        private Vector3[] m_vertices;
        private Vector2[] m_uvs;
        private Color32[] m_colors;
        private float m_radius = -1f;
        private ParticleSystem m_motes;
        private MeshRenderer m_renderer;
        private float m_nextUpdate;
        private readonly List<Vector4> m_mergeSpheres = new List<Vector4>();

        // Merge fading depends only on where the hulls are, so it is cached per vertex and
        // recomputed when this hull or an overlapping one moves or resizes.
        private float[] m_merge;
        private readonly List<Vector4> m_cachedSpheres = new List<Vector4>();
        private Vector4 m_cachedSelf = new Vector4(float.NaN, 0f, 0f, 0f);

        public static WardHull Create(SpawnWard owner)
        {
            // Dedicated servers render nothing.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            if (!EnsureResources()) return null;

            var go = new GameObject("warden_hull");
            go.SetActive(false);

            var hull = go.AddComponent<WardHull>();
            hull.m_owner = owner;
            hull.m_vertices = new Vector3[s_unitSphere.Length];
            hull.m_uvs = new Vector2[s_unitSphere.Length];
            hull.m_colors = new Color32[s_unitSphere.Length];
            hull.m_merge = new float[s_unitSphere.Length];
            // Spread the updates of many hulls over different frames.
            hull.m_nextUpdate = Time.time + Random.value * UpdateInterval;
            hull.m_mesh = new Mesh { name = "warden_hull_sphere" };
            hull.m_mesh.MarkDynamic();

            go.AddComponent<MeshFilter>().sharedMesh = hull.m_mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = s_hazeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            hull.m_renderer = renderer;

            hull.m_motes = CreateMotes(go.transform);
            return hull;
        }

        public bool IsVisible => gameObject.activeSelf;

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        public void SetRadius(float radius)
        {
            if (Mathf.Approximately(radius, m_radius)) return;
            m_radius = radius;

            for (int i = 0; i < s_unitSphere.Length; i++) m_vertices[i] = s_unitSphere[i] * radius;
            m_mesh.vertices = m_vertices;
            m_mesh.uv = s_baseUvs;
            m_mesh.colors32 = m_colors;
            if (m_mesh.triangles.Length == 0) m_mesh.triangles = s_triangles;
            m_mesh.RecalculateBounds();

            if (m_motes)
            {
                var shape = m_motes.shape;
                shape.radius = radius;
                var emission = m_motes.emission;
                // Keep mote density roughly constant across radii (~45/s at 60 m).
                emission.rateOverTime = 45f * (radius * radius) / (60f * 60f);
            }
        }

        private void OnDestroy()
        {
            if (m_mesh) Destroy(m_mesh);
        }

        private void LateUpdate()
        {
            if (!m_owner)
            {
                Destroy(gameObject);
                return;
            }

            // The hull is not parented to its ward, so it does not hide with it. Vanilla deactivates
            // the placement ghost (remove mode, nothing under the cursor) instead of destroying it;
            // without this its hull stayed behind after the ward was gone.
            if (!m_owner.isActiveAndEnabled)
            {
                SetVisible(false);
                return;
            }

            transform.position = m_owner.transform.position;

            float time = Time.time;
            if (s_lastScrollFrame != Time.frameCount)
            {
                s_lastScrollFrame = Time.frameCount;
                s_hazeMaterial.mainTextureOffset = new Vector2(time * 0.004f, -time * 0.006f);
            }

            if (time < m_nextUpdate || !m_renderer.isVisible) return;
            m_nextUpdate = Mathf.Max(m_nextUpdate + UpdateInterval, time);

            var cam = Camera.main;
            if (!cam) return;

            Vector3 center = transform.position;
            UpdateMerge(center);

            Vector3 camPos = cam.transform.position;
            float radius = m_radius;

            // Time-dependent halves of the band and swirl terms, shared by every vertex.
            float wobbleSin = Mathf.Sin(time * 0.25f), wobbleCos = Mathf.Cos(time * 0.25f);
            float swirlXSin = Mathf.Sin(time * SwirlSpeed), swirlXCos = Mathf.Cos(time * SwirlSpeed);
            float swirlYSin = Mathf.Sin(-time * SwirlSpeed * 0.8f), swirlYCos = Mathf.Cos(-time * SwirlSpeed * 0.8f);
            float bandPhase = time * BandSpeed;

            for (int i = 0; i < s_unitSphere.Length; i++)
            {
                float merge = m_merge[i];
                if (merge <= 0f)
                {
                    m_colors[i] = new Color32(255, 255, 255, 0);
                    continue;
                }

                Vector3 normal = s_unitSphere[i];
                Vector3 toVertex = center + normal * radius - camPos;
                float facing = Mathf.Abs(Vector3.Dot(normal, toVertex)) / toVertex.magnitude;
                float rim = Mathf.Pow(1f - facing, RimSharpness);

                float wobble = s_sinLon3[i] * wobbleCos + s_cosLon3[i] * wobbleSin;
                float band = Mathf.Sin(s_bandLat[i] - bandPhase + wobble * BandWobble);
                float bands = 1f + BandStrength * band;

                float alpha = (BaseAlpha + RimAlpha * rim) * bands * merge;
                m_colors[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));

                Vector2 uv = s_baseUvs[i];
                uv.x += SwirlAmount * (s_sinLat4[i] * swirlXCos + s_cosLat4[i] * swirlXSin);
                uv.y += SwirlAmount * (s_sinLon2[i] * swirlYCos + s_cosLon2[i] * swirlYSin);
                m_uvs[i] = uv;
            }

            m_mesh.colors32 = m_colors;
            m_mesh.uv = m_uvs;
        }

        private void UpdateMerge(Vector3 center)
        {
            m_owner.CollectMergeSpheres(m_mergeSpheres);

            var self = new Vector4(center.x, center.y, center.z, m_radius);
            if (self == m_cachedSelf && SameSpheres(m_mergeSpheres, m_cachedSpheres)) return;
            m_cachedSelf = self;
            m_cachedSpheres.Clear();
            m_cachedSpheres.AddRange(m_mergeSpheres);

            for (int i = 0; i < s_unitSphere.Length; i++)
            {
                Vector3 worldPos = center + s_unitSphere[i] * m_radius;
                float merge = 1f;
                foreach (var other in m_mergeSpheres)
                {
                    float outside = Vector3.Distance(worldPos, (Vector3)other) - other.w;
                    merge = Mathf.Min(merge, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-MergeFade, MergeFade * 0.5f, outside)));
                    if (merge <= 0f) break;
                }
                m_merge[i] = merge;
            }
        }

        private static bool SameSpheres(List<Vector4> a, List<Vector4> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private static ParticleSystem CreateMotes(Transform parent)
        {
            var go = new GameObject("warden_hull_motes");
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            main.startColor = s_color;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            // Wards overlap and every hull is drawn while the camera is inside it, so keep the
            // transparent mote overdraw per hull bounded.
            main.maxParticles = 200;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radiusThickness = 0f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.3f;
            noise.scrollSpeed = 0.2f;

            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.25f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = fade;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = s_moteMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            ps.Play();
            return ps;
        }

        private static bool EnsureResources()
        {
            if (s_hazeMaterial) return true;
            if (s_failed) return false;

            Shader shader = null;
            foreach (var candidate in s_shaderCandidates)
            {
                shader = Shader.Find(candidate);
                if (shader) break;
            }

            if (!shader)
            {
                s_failed = true;
                Jotunn.Logger.LogWarning("No transparent shader found; ward hull disabled.");
                return false;
            }

            BuildSphere();
            s_hazeMaterial = new Material(shader)
            {
                name = "warden_hull",
                mainTexture = BuildHazeTexture(),
                color = s_color,
            };
            s_moteMaterial = new Material(shader)
            {
                name = "warden_hull_mote",
                mainTexture = BuildMoteTexture(),
            };
            return true;
        }

        private static void BuildSphere()
        {
            s_unitSphere = new Vector3[(Segments + 1) * (Rings + 1)];
            s_baseUvs = new Vector2[s_unitSphere.Length];
            s_triangles = new int[Segments * Rings * 6];
            int count = s_unitSphere.Length;
            s_bandLat = new float[count];
            s_sinLon3 = new float[count];
            s_cosLon3 = new float[count];
            s_sinLat4 = new float[count];
            s_cosLat4 = new float[count];
            s_sinLon2 = new float[count];
            s_cosLon2 = new float[count];

            for (int ring = 0; ring <= Rings; ring++)
            {
                float v = (float)ring / Rings;
                float lat = (v - 0.5f) * Mathf.PI;
                float y = Mathf.Sin(lat);
                float r = Mathf.Cos(lat);

                for (int seg = 0; seg <= Segments; seg++)
                {
                    float u = (float)seg / Segments;
                    float lon = u * Mathf.PI * 2f;
                    int i = ring * (Segments + 1) + seg;
                    s_unitSphere[i] = new Vector3(r * Mathf.Cos(lon), y, r * Mathf.Sin(lon));
                    s_baseUvs[i] = new Vector2(u * NoiseTilesAround, v * NoiseTilesVertical);

                    // The animation uses sin(latitude) (the vertex height) and the longitude
                    // atan2 would return, which wraps to the negative half past pi.
                    float animLon = Mathf.Atan2(Mathf.Sin(lon), Mathf.Cos(lon));
                    s_bandLat[i] = y * BandCount;
                    s_sinLon3[i] = Mathf.Sin(animLon * 3f);
                    s_cosLon3[i] = Mathf.Cos(animLon * 3f);
                    s_sinLat4[i] = Mathf.Sin(y * 4f);
                    s_cosLat4[i] = Mathf.Cos(y * 4f);
                    s_sinLon2[i] = Mathf.Sin(animLon * 2f);
                    s_cosLon2[i] = Mathf.Cos(animLon * 2f);
                }
            }

            int t = 0;
            for (int ring = 0; ring < Rings; ring++)
            {
                for (int seg = 0; seg < Segments; seg++)
                {
                    int a = ring * (Segments + 1) + seg;
                    int b = a + Segments + 1;
                    s_triangles[t++] = a;
                    s_triangles[t++] = b;
                    s_triangles[t++] = a + 1;
                    s_triangles[t++] = a + 1;
                    s_triangles[t++] = b;
                    s_triangles[t++] = b + 1;
                }
            }
        }

        // Tileable soft noise so the haze drifts instead of reading as a flat shell.
        private static Texture2D BuildHazeTexture()
        {
            const int size = 128;
            const float scale = 4f / size;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "warden_hull_haze",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = TileableNoise(x, y, size, scale) * 0.65f
                                + TileableNoise(x * 2 % size, y * 2 % size, size, scale) * 0.35f;
                    float alpha = Mathf.Lerp(0.45f, 1f, Mathf.SmoothStep(0.25f, 0.75f, noise));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        // Blends four offset Perlin samples so opposite edges match.
        private static float TileableNoise(int x, int y, int size, float scale)
        {
            float fx = (float)x / size;
            float fy = (float)y / size;
            float a = Mathf.PerlinNoise(x * scale, y * scale);
            float b = Mathf.PerlinNoise((x - size) * scale, y * scale);
            float c = Mathf.PerlinNoise(x * scale, (y - size) * scale);
            float d = Mathf.PerlinNoise((x - size) * scale, (y - size) * scale);
            return a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy;
        }

        // Soft round glow for the floating motes.
        private static Texture2D BuildMoteTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "warden_hull_mote",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float glow = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, glow);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
