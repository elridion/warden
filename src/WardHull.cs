using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Visualizes a ward's radius: amber motes drift over the whole sphere, and while visible the
    /// sphere joins the haze wall (<see cref="WardWall"/>) that lights up near the local player.
    /// Kept as a separate root object so piece highlighting and placement tinting leave it alone.
    /// </summary>
    internal class WardHull : MonoBehaviour
    {
        private static readonly Color s_color = new Color(1f, 0.6f, 0.16f, 1f);

        private static readonly string[] s_shaderCandidates =
        {
            "Sprites/Default",
            "UI/Default",
            "Legacy Shaders/Particles/Alpha Blended",
        };

        private static Material s_hazeMaterial;
        private static Material s_moteMaterial;
        private static bool s_failed;

        private SpawnWard m_owner;
        private float m_radius = -1f;
        private ParticleSystem m_motes;
        // Long before any flash, so the wall does not start out bright.
        private float m_lastFlashTime = -1000f;

        public static Material HazeMaterial => s_hazeMaterial;

        public static WardHull Create(SpawnWard owner)
        {
            // Dedicated servers render nothing.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            if (!EnsureResources()) return null;

            var go = new GameObject("warden_hull");
            go.SetActive(false);

            var hull = go.AddComponent<WardHull>();
            hull.m_owner = owner;
            hull.m_motes = CreateMotes(go.transform);
            return hull;
        }

        public float Radius => m_radius;

        public float LastFlashTime => m_lastFlashTime;

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        public void SetRadius(float radius)
        {
            if (Mathf.Approximately(radius, m_radius)) return;
            m_radius = radius;

            if (m_motes)
            {
                var shape = m_motes.shape;
                shape.radius = radius;
                var emission = m_motes.emission;
                // Keep mote density roughly constant across radii (~45/s at 60 m).
                emission.rateOverTime = 45f * (radius * radius) / (60f * 60f);
            }
        }

        public void Flash()
        {
            m_lastFlashTime = Time.time;
        }

        private void OnEnable()
        {
            WardWall.Add(this);
        }

        private void OnDisable()
        {
            WardWall.Remove(this);
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

        // Tileable soft noise for faint grain in the haze; its clouds come from world-space noise in
        // WardWall, which also lines up where wards join.
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
                    float alpha = Mathf.Lerp(0.8f, 1f, Mathf.SmoothStep(0.25f, 0.75f, noise));
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
