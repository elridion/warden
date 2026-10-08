using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Translucent lattice dome that visualizes a ward's radius. Kept as a separate root object
    /// so piece highlighting and placement tinting don't touch its material.
    /// </summary>
    internal static class WardHull
    {
        private const int Segments = 96;
        private const int Rings = 24;
        private const float LatticeColumns = 48f;
        private const float LatticeRows = 10f;

        private static readonly string[] s_shaderCandidates =
        {
            "Sprites/Default",
            "UI/Default",
            "Legacy Shaders/Particles/Alpha Blended",
        };

        private static Mesh s_mesh;
        private static Material s_material;
        private static bool s_failed;

        public static GameObject Create(string name)
        {
            // Dedicated servers render nothing.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            if (!EnsureResources()) return null;

            var hull = new GameObject(name);
            hull.AddComponent<MeshFilter>().sharedMesh = s_mesh;
            var renderer = hull.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = s_material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            hull.SetActive(false);
            return hull;
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

            s_mesh = BuildDome();
            s_material = new Material(shader)
            {
                name = "warden_hull",
                mainTexture = BuildLatticeTexture(),
                color = new Color(1f, 0.72f, 0.3f, 1f),
            };
            return true;
        }

        // Unit hemisphere with vertex alpha fading toward the top.
        private static Mesh BuildDome()
        {
            var vertices = new Vector3[(Segments + 1) * (Rings + 1)];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[Segments * Rings * 6];

            for (int ring = 0; ring <= Rings; ring++)
            {
                float v = (float)ring / Rings;
                float lat = v * Mathf.PI * 0.5f;
                float y = Mathf.Sin(lat);
                float r = Mathf.Cos(lat);

                for (int seg = 0; seg <= Segments; seg++)
                {
                    float u = (float)seg / Segments;
                    float lon = u * Mathf.PI * 2f;
                    int i = ring * (Segments + 1) + seg;
                    vertices[i] = new Vector3(r * Mathf.Cos(lon), y, r * Mathf.Sin(lon));
                    uvs[i] = new Vector2(u * LatticeColumns, v * LatticeRows);
                    colors[i] = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.35f, v));
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

            var mesh = new Mesh
            {
                name = "warden_hull_dome",
                vertices = vertices,
                uv = uvs,
                colors = colors,
                triangles = triangles,
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        // One lattice cell: faint fill with soft bright edges, tiled across the dome.
        private static Texture2D BuildLatticeTexture()
        {
            const int size = 64;
            const float fillAlpha = 0.05f;
            const float lineAlpha = 0.55f;
            const float lineWidth = 2.5f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "warden_hull_lattice",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    float line = 1f - Mathf.Clamp01(edge / lineWidth);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Lerp(fillAlpha, lineAlpha, line));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }
    }
}
