using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Offering cup: a silver chalice with a faint glow inside. The chalice comes from the embedded
    /// OBJ model, with a lathed fallback if it fails to load.
    /// </summary>
    internal static class OfferingCupModel
    {
        // The authored model is ~0.11 m tall; scale it to ~0.5 m.
        private const string CupModelResource = "Warden.offering_cup.obj";
        private const float CupModelScale = 4.5f;
        public const float Height = 0.5f;
        public const float Width = 0.45f;

        private const int LatheSegments = 32;

        // (radius, height) outline of the fallback chalice, revolved around the Y axis.
        private static readonly Vector2[] s_fallbackProfile =
        {
            new Vector2(0f, 0f), new Vector2(0.16f, 0f), new Vector2(0.16f, 0.02f), new Vector2(0.06f, 0.05f),
            new Vector2(0.035f, 0.1f), new Vector2(0.03f, 0.2f), new Vector2(0.05f, 0.24f), new Vector2(0.03f, 0.27f),
            new Vector2(0.05f, 0.3f), new Vector2(0.14f, 0.36f), new Vector2(0.17f, 0.45f), new Vector2(0.165f, 0.5f),
            new Vector2(0.15f, 0.5f), new Vector2(0.12f, 0.4f), new Vector2(0f, 0.37f),
        };

        public static void Build(Transform parent, Material silver)
        {
            Mesh model = null;
            try
            {
                model = ObjLoader.LoadEmbedded(CupModelResource, "warden_cup_chalice");
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogWarning($"Offering cup model failed to load; using the fallback chalice: {e.Message}");
            }
            if (model) AddPart(parent, "cup", model, silver, CupModelScale);
            else AddPart(parent, "cup", Lathe("warden_cup_chalice", s_fallbackProfile), silver, 1f);

            // Faint ember glow pooled inside the bowl.
            var glow = new GameObject("glow");
            glow.transform.SetParent(parent, false);
            glow.transform.localPosition = new Vector3(0f, Height - 0.05f, 0f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.25f);
            light.range = 2.5f;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;
        }

        private static void AddPart(Transform parent, string name, Mesh mesh, Material material, float scale)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localScale = Vector3.one * scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            if (material) renderer.sharedMaterial = material;
        }

        private static Mesh Lathe(string name, Vector2[] profile)
        {
            int rows = profile.Length;
            var vertices = new Vector3[rows * (LatheSegments + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[(rows - 1) * LatheSegments * 6];

            for (int row = 0; row < rows; row++)
            {
                for (int seg = 0; seg <= LatheSegments; seg++)
                {
                    float u = (float)seg / LatheSegments;
                    float angle = u * Mathf.PI * 2f;
                    int i = row * (LatheSegments + 1) + seg;
                    vertices[i] = new Vector3(profile[row].x * Mathf.Cos(angle), profile[row].y, profile[row].x * Mathf.Sin(angle));
                    uvs[i] = new Vector2(u, (float)row / (rows - 1));
                }
            }

            int t = 0;
            for (int row = 0; row < rows - 1; row++)
            {
                for (int seg = 0; seg < LatheSegments; seg++)
                {
                    int a = row * (LatheSegments + 1) + seg;
                    int b = a + LatheSegments + 1;
                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = b;
                    triangles[t++] = b + 1;
                }
            }

            var mesh = new Mesh { name = name, vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
