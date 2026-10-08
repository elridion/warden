using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Soft amber haze sphere that visualizes a ward's radius. Kept as a separate root object
    /// so piece highlighting and placement tinting don't touch its material.
    /// </summary>
    internal class WardHull : MonoBehaviour
    {
        private const int Segments = 64;
        private const int Rings = 32;
        private const float NoiseTilesAround = 3f;
        private const float NoiseTilesVertical = 1.5f;

        // Edge glow: alpha rises where the surface turns away from the viewer.
        private const float BaseAlpha = 0.02f;
        private const float RimAlpha = 0.32f;
        private const float RimSharpness = 2.5f;
        private const float PulseAmount = 0.12f;
        private const float PulseSpeed = 0.7f;

        private static readonly string[] s_shaderCandidates =
        {
            "Sprites/Default",
            "UI/Default",
            "Legacy Shaders/Particles/Alpha Blended",
        };

        private static Mesh s_templateMesh;
        private static Vector3[] s_normals;
        private static Material s_material;
        private static bool s_failed;

        private Mesh m_mesh;
        private Color32[] m_colors;

        public static GameObject Create(string name)
        {
            // Dedicated servers render nothing.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            if (!EnsureResources()) return null;

            var hull = new GameObject(name);
            hull.SetActive(false);
            var component = hull.AddComponent<WardHull>();
            component.m_mesh = Instantiate(s_templateMesh);
            component.m_colors = new Color32[s_normals.Length];

            hull.AddComponent<MeshFilter>().sharedMesh = component.m_mesh;
            var renderer = hull.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = s_material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return hull;
        }

        private void OnDestroy()
        {
            if (m_mesh) Destroy(m_mesh);
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;

            float time = Time.time;
            s_material.mainTextureOffset = new Vector2(time * 0.004f, time * 0.0025f);

            Vector3 center = transform.position;
            float radius = transform.localScale.x;
            Vector3 camPos = cam.transform.position;
            float pulse = 1f - PulseAmount + PulseAmount * Mathf.Sin(time * PulseSpeed);

            for (int i = 0; i < s_normals.Length; i++)
            {
                Vector3 normal = s_normals[i];
                Vector3 toVertex = center + normal * radius - camPos;
                float facing = Mathf.Abs(Vector3.Dot(normal, toVertex.normalized));
                float rim = Mathf.Pow(1f - facing, RimSharpness);
                float alpha = (BaseAlpha + RimAlpha * rim) * pulse;
                m_colors[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
            }

            m_mesh.colors32 = m_colors;
        }

        private static bool EnsureResources()
        {
            if (s_material) return true;
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

            s_templateMesh = BuildSphere(out s_normals);
            s_material = new Material(shader)
            {
                name = "warden_hull",
                mainTexture = BuildHazeTexture(),
                color = new Color(1f, 0.66f, 0.25f, 1f),
            };
            return true;
        }

        // Unit UV sphere; vertex positions double as normals.
        private static Mesh BuildSphere(out Vector3[] normals)
        {
            var vertices = new Vector3[(Segments + 1) * (Rings + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Segments * Rings * 6];

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
                    vertices[i] = new Vector3(r * Mathf.Cos(lon), y, r * Mathf.Sin(lon));
                    uvs[i] = new Vector2(u * NoiseTilesAround, v * NoiseTilesVertical);
                }
            }

            int t = 0;
            for (int ring = 0; ring < Rings; ring++)
            {
                for (int seg = 0; seg < Segments; seg++)
                {
                    int a = ring * (Segments + 1) + seg;
                    int b = a + Segments + 1;
                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = b;
                    triangles[t++] = b + 1;
                }
            }

            normals = vertices;
            var mesh = new Mesh
            {
                name = "warden_hull_sphere",
                vertices = vertices,
                uv = uvs,
                colors32 = new Color32[vertices.Length],
                triangles = triangles,
            };
            mesh.RecalculateBounds();
            mesh.MarkDynamic();
            return mesh;
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
    }
}
