using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Builds and registers the warden stone and its four upgrade pieces.
    /// </summary>
    internal static class WardPieces
    {
        public const string WardPrefabName = "piece_spawnward";

        // A custom category name adds its own hammer tag, and an empty Usage lets Jotunn guess more
        // tags from each piece's components and name. Vanilla Misc plus an explicit Defense usage
        // lists every piece under Defense only, next to the vanilla ward.
        private static readonly string s_buildCategory = PieceCategories.Misc;
        private static readonly string[] s_buildUsage = { PieceUsages.Defense };

        private const string WardBasePrefab = "guard_stone";
        private static readonly Color s_wardStoneTint = new Color(0.66f, 0.66f, 0.64f);
        private const string BannerBasePrefab = "piece_banner01";
        private const string BannerTextureResource = "Warden.banner_runed_hide.png";
        private const string BannerNormalResource = "Warden.banner_runed_hide_n.png";
        private static readonly Color32 s_atlasOutside = new Color32(60, 45, 30, 0);
        // Flat normal in the DXT5nm-style layout the banner normal map uses (X in alpha, Y in green).
        private static readonly Color32 s_flatNormal = new Color32(255, 128, 255, 128);
        private static readonly Color s_parchmentTint = new Color(0.8f, 0.78f, 0.75f);
        // Banner build-menu icon: the mirrored design hanging from a crossbar, in a square sprite.
        private const int BannerIconSize = 128;
        private const int BannerIconMargin = 4;
        // Share of the design's top cut off; in game that strip is behind the crossbar too.
        private const float BannerIconTopCrop = 0.06f;
        private const int BannerIconBarHeight = 7;
        private const int BannerIconBarOverhang = 9;
        private static readonly Color32 s_bannerIconWood = new Color32(92, 64, 40, 255);
        private static readonly Color32 s_bannerIconWoodEdge = new Color32(48, 32, 20, 255);
        private const string IdolModelPrefab = "StatueDeer";
        private const float IdolHeight = 0.9f;
        private static readonly Color s_blackMetalTint = new Color(0.2f, 0.21f, 0.23f);
        // Ritual candles stack: each cluster counts, up to MaxStackedUpgrades.
        private const int StackableTier = 4;
        private const string CandlePrefab = "Candle_resin";
        // Candle cluster: (x, z, scale) per candle, tallest near the middle.
        private static readonly Vector3[] s_candles =
        {
            new Vector3(0f, 0f, 1.35f),
            new Vector3(0.09f, 0.05f, 1.1f),
            new Vector3(-0.08f, 0.07f, 1.0f),
            new Vector3(-0.06f, -0.09f, 0.9f),
            new Vector3(0.1f, -0.08f, 0.8f),
            new Vector3(0.17f, 0.12f, 0.65f),
            new Vector3(-0.17f, -0.02f, 0.7f),
            new Vector3(0.02f, 0.17f, 0.6f),
        };
        private const string SilverMaterialPrefab = "Silver";
        private const string ConnectionSourcePrefab = "piece_workbench_ext1";

        private static GameObject s_connectionPrefab;
        private static Piece s_wardPiece;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Register;

            var connectionSource = PrefabManager.Cache.GetPrefab<GameObject>(ConnectionSourcePrefab);
            s_connectionPrefab = connectionSource ? connectionSource.GetComponent<StationExtension>()?.m_connectionPrefab : null;

            AddWard();
            AddExtension(1, CreateBanner, Req("LeatherScraps", 6), Req("FineWood", 2), Req("Coal", 4), Req("Tin", 5));
            AddExtension(2, CreateIdol, Req("Stone", 20), Req("Iron", 5), Req("WitheredBone", 3));
            AddExtension(3, CreateOfferingCup, Req("Silver", 5), Req("Obsidian", 5));
            AddExtension(4, CreateRitualCandles, Req("Tar", 10), Req("Resin", 10), Req("CandleWick", 5));
        }

        private static RequirementConfig Req(string item, int amount) => new RequirementConfig(item, amount, 0, true);

        // Upgrades need no crafting station: being near a warden stone is their requirement.
        private static PieceConfig Config(string name, RequirementConfig[] requirements, string craftingStation = null) => new PieceConfig
        {
            Name = "$" + name,
            Description = "$" + name + "_description",
            PieceTable = PieceTables.Hammer,
            Category = s_buildCategory,
            Usage = s_buildUsage,
            CraftingStation = craftingStation,
            Requirements = requirements,
        };

        private static void AddWard()
        {
            if (!RequirePrefab(WardBasePrefab)) return;
            try
            {
                BuildWard();
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogError($"Failed to build {WardPrefabName}: {e}");
            }
        }

        private static void BuildWard()
        {
            var config = Config(WardPrefabName, new[] { Req("Stone", 15), Req("GreydwarfEye", 10), Req("SurtlingCore", 2) }, CraftingStations.Workbench);
            var piece = new CustomPiece(WardPrefabName, WardBasePrefab, config);
            var prefab = piece.PiecePrefab;
            s_wardPiece = prefab.GetComponent<Piece>();

            // Swap the cloned vanilla PrivateArea for SpawnWard, reusing its visuals and effects.
            var privateArea = prefab.GetComponent<PrivateArea>();
            var ward = prefab.AddComponent<SpawnWard>();
            if (privateArea)
            {
                ward.m_enabledEffect = privateArea.m_enabledEffect;
                ward.m_areaMarker = privateArea.m_areaMarker;
                ward.m_activateEffect = privateArea.m_activateEffect;
                ward.m_deactivateEffect = privateArea.m_deactivateEffect;
                ward.m_hoverOffset = privateArea.m_hoverOffset;
                Object.DestroyImmediate(privateArea);
            }

            RecolorWard(prefab, ward);

            PieceManager.Instance.AddPiece(piece);
        }

        // Grey stone, so the warden stone reads apart from the vanilla ward. The runes, light and
        // sparks keep the vanilla ward's amber glow: it is already amber, and recolouring the
        // sparks lost their faint per-particle alpha and made the stone glare.
        // Works on copies of the materials; the area marker keeps its look.
        private static void RecolorWard(GameObject prefab, SpawnWard ward)
        {
            var marker = ward.m_areaMarker ? ward.m_areaMarker.transform : null;

            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                if (marker && renderer.transform.IsChildOf(marker)) continue;
                if (renderer is ParticleSystemRenderer) continue;

                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (!source) continue;

                    var copy = new Material(source) { name = source.name + "_warden" };
                    if (copy.HasProperty("_Color")) copy.SetColor("_Color", copy.GetColor("_Color") * s_wardStoneTint);
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }

        private delegate CustomPiece PieceFactory(string name, PieceConfig config);

        private static void AddExtension(int tier, PieceFactory factory, params RequirementConfig[] requirements)
        {
            string name = $"{WardPrefabName}_ext{tier}";
            CustomPiece piece;
            try
            {
                piece = factory(name, Config(name, requirements));
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogError($"Failed to build {name}: {e}");
                return;
            }
            if (piece == null) return;

            // Shows the upgrade arrow on the build menu icon, like vanilla station extensions.
            piece.PiecePrefab.GetComponent<Piece>().m_isUpgrade = true;

            var extension = piece.PiecePrefab.AddComponent<WardExtension>();
            extension.m_tier = tier;
            extension.m_stackable = tier == StackableTier;
            extension.m_connectionPrefab = s_connectionPrefab;
            extension.m_connectionOffset = new Vector3(0f, 1f, 0f);

            PieceManager.Instance.AddPiece(piece);
        }

        private static CustomPiece CreateBanner(string name, PieceConfig config)
        {
            if (!RequirePrefab(BannerBasePrefab)) return null;
            var design = LoadEmbeddedTexture(BannerTextureResource, false);
            // Without an icon the clone keeps the vanilla banner's, which shows a different design.
            // Rendering the prefab doesn't help: the 3 m cloth strip comes out a thin sliver at icon
            // size, so the icon is drawn flat from the design instead.
            config.Icon = BuildBannerIcon(design);
            var piece = new CustomPiece(name, BannerBasePrefab, config);
            ApplyBannerTexture(piece.PiecePrefab, design);
            return piece;
        }

        // Swaps the banner cloth's texture for the runed parchment, on fresh material copies so vanilla
        // banners keep theirs. The cloth is simulated by MagicaCloth from the original mesh, so instead of
        // touching its UVs we place the design into an atlas at the strip of UV space the cloth samples.
        private static void ApplyBannerTexture(GameObject prefab, Texture2D design)
        {
            if (!design) return;
            var normalDesign = LoadEmbeddedTexture(BannerNormalResource, true);

            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var materials = r.sharedMaterials;
                bool isBannerCloth = materials.Length > 0 && materials[0] && materials[0].mainTexture &&
                    materials[0].mainTexture.name.IndexOf("banner", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (!isBannerCloth) continue;

                if (!TryGetUvRect(r, out var uvRect))
                {
                    Jotunn.Logger.LogWarning($"Banner cloth '{r.name}' has no readable UVs; using the design unmapped.");
                    uvRect = new Rect(0f, 0f, 1f, 1f);
                }
                var atlas = BuildAtlas(design, uvRect, s_atlasOutside, false);
                var normalAtlas = normalDesign ? BuildAtlas(normalDesign, uvRect, s_flatNormal, true) : FlatNormalTexture();

                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i]) continue;
                    materials[i] = MakeParchmentMaterial(materials[i], atlas, normalAtlas);
                }
                r.sharedMaterials = materials;
            }
        }

        // The design is only the banner's left half; mirror it at its right edge to get the whole cloth.
        // Each icon pixel box-filters the design area it covers: point or bilinear samples at this
        // shrink (about 14:1) would alias the runes into speckle.
        private static Sprite BuildBannerIcon(Texture2D design)
        {
            if (!design) return null;

            int halfW = design.width, srcH = Mathf.RoundToInt(design.height * (1f - BannerIconTopCrop));
            int fullW = halfW * 2;
            var src = design.GetPixels32();

            int bannerTop = BannerIconMargin + BannerIconBarHeight / 2;
            int bannerH = BannerIconSize - BannerIconMargin - bannerTop;
            int bannerW = Mathf.RoundToInt(bannerH * (float)fullW / srcH);
            int bannerX = (BannerIconSize - bannerW) / 2;
            float scale = (float)srcH / bannerH;

            var pixels = new Color32[BannerIconSize * BannerIconSize];
            for (int row = 0; row < bannerH; row++)
            {
                // Rows count down from the banner's top; textures store the bottom row first.
                int y = BannerIconSize - 1 - bannerTop - row;
                int sy0 = srcH - 1 - Mathf.FloorToInt((row + 1) * scale - 0.001f);
                int sy1 = srcH - 1 - Mathf.FloorToInt(row * scale);
                for (int col = 0; col < bannerW; col++)
                {
                    int sx0 = Mathf.FloorToInt(col * scale), sx1 = Mathf.Min(Mathf.FloorToInt((col + 1) * scale - 0.001f), fullW - 1);
                    pixels[y * BannerIconSize + bannerX + col] = AverageMirrored(src, halfW, sx0, sx1, Mathf.Max(sy0, 0), sy1);
                }
            }

            int barX0 = Mathf.Max(bannerX - BannerIconBarOverhang, 0);
            int barX1 = Mathf.Min(bannerX + bannerW + BannerIconBarOverhang, BannerIconSize);
            for (int i = 0; i < BannerIconBarHeight; i++)
            {
                int y = BannerIconSize - 1 - BannerIconMargin - i;
                bool edge = i == 0 || i == BannerIconBarHeight - 1;
                for (int x = barX0; x < barX1; x++)
                    pixels[y * BannerIconSize + x] = edge || x == barX0 || x == barX1 - 1 ? s_bannerIconWoodEdge : s_bannerIconWood;
            }

            var texture = new Texture2D(BannerIconSize, BannerIconSize, TextureFormat.RGBA32, false)
            {
                name = "warden_banner_icon",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, BannerIconSize, BannerIconSize), new Vector2(0.5f, 0.5f));
        }

        // Alpha-weighted average of a block of the mirrored design, tinted like the in-game parchment.
        // Weighting by alpha keeps the colour of fully cut-away pixels out of the torn edge.
        private static Color32 AverageMirrored(Color32[] src, int halfW, int x0, int x1, int y0, int y1)
        {
            float r = 0f, g = 0f, b = 0f, a = 0f;
            int count = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var c = src[y * halfW + (x < halfW ? x : 2 * halfW - 1 - x)];
                    r += c.r * c.a;
                    g += c.g * c.a;
                    b += c.b * c.a;
                    a += c.a;
                    count++;
                }
            }
            if (a <= 0f) return new Color32(0, 0, 0, 0);
            return new Color32(
                (byte)(r / a * s_parchmentTint.r),
                (byte)(g / a * s_parchmentTint.g),
                (byte)(b / a * s_parchmentTint.b),
                (byte)(a / count));
        }

        private static bool TryGetUvRect(Renderer renderer, out Rect rect)
        {
            rect = default;
            Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (!mesh || !mesh.isReadable) return false;

            var uvs = mesh.uv;
            if (uvs.Length == 0) return false;
            Vector2 min = uvs[0], max = uvs[0];
            foreach (var uv in uvs)
            {
                min = Vector2.Min(min, uv);
                max = Vector2.Max(max, uv);
            }
            if (max.x <= min.x || max.y <= min.y) return false;

            rect = Rect.MinMaxRect(Mathf.Clamp01(min.x), Mathf.Clamp01(min.y), Mathf.Clamp01(max.x), Mathf.Clamp01(max.y));
            return true;
        }

        // Atlas sized so the design keeps its proportions inside uvRect; outside it is transparent.
        // The design keeps its own resolution: bilinear sampling can't shrink it without dropping thin strokes.
        private static Texture2D BuildAtlas(Texture2D design, Rect uvRect, Color32 outside, bool linear)
        {
            int rectH = design.height;
            int rectW = Mathf.RoundToInt(rectH * (float)design.width / design.height);
            int width = Mathf.Clamp(Mathf.RoundToInt(rectW / uvRect.width), rectW, 4096);
            int height = Mathf.Clamp(Mathf.RoundToInt(rectH / uvRect.height), rectH, 4096);

            var pixels = new Color32[width * height];
            int x0 = Mathf.FloorToInt(uvRect.xMin * width), x1 = Mathf.CeilToInt(uvRect.xMax * width);
            int y0 = Mathf.FloorToInt(uvRect.yMin * height), y1 = Mathf.CeilToInt(uvRect.yMax * height);

            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f - y0) / (y1 - y0);
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f - x0) / (x1 - x0);
                    pixels[y * width + x] = u < 0f || u > 1f || v < 0f || v > 1f ? outside : (Color32)design.GetPixelBilinear(u, v);
                }
            }

            var atlas = new Texture2D(width, height, TextureFormat.RGBA32, true, linear)
            {
                name = design.name + "_atlas",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            atlas.SetPixels32(pixels);
            atlas.Apply(true, true);
            return atlas;
        }

        // Stands in for the banner normal map if it fails to load: a cleared _BumpMap slot falls back
        // to whatever default the shader declares.
        private static Texture2D FlatNormalTexture()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true) { name = "warden_flat_normal" };
            texture.SetPixels32(new[] { s_flatNormal });
            texture.Apply(false, true);
            return texture;
        }

        // Matte parchment: swap the cloth's normal map for ours (the vanilla one doesn't line up with
        // our design), remove sheen, and alpha-clip the torn edges when the shader supports it.
        private static Material MakeParchmentMaterial(Material source, Texture2D atlas, Texture2D normalAtlas)
        {
            var material = new Material(source) { name = "warden_banner_parchment", mainTexture = atlas };
            if (material.HasProperty("_Color")) material.SetColor("_Color", s_parchmentTint);
            // The vanilla atlas also drives emission and metalness; left in, the parchment glows.
            // Use explicit black textures: a cleared slot falls back to the shader's default, which can be white.
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", normalAtlas);
            if (material.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", 1f);
            if (material.HasProperty("_EmissiveTex")) material.SetTexture("_EmissiveTex", Texture2D.blackTexture);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
            if (material.HasProperty("_MetalTex")) material.SetTexture("_MetalTex", Texture2D.blackTexture);
            if (material.HasProperty("_USEMETALMAP")) material.SetFloat("_USEMETALMAP", 0f);
            material.DisableKeyword("_EMISSION");
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.05f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.05f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Cutoff"))
            {
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
            }
            return material;
        }

        // Normal maps must load as linear: sRGB decoding would bend the encoded vectors.
        private static Texture2D LoadEmbeddedTexture(string resourceName, bool linear)
        {
            using (var stream = typeof(WardPieces).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Jotunn.Logger.LogWarning($"Embedded texture '{resourceName}' not found.");
                    return null;
                }

                // Stream.Read may return fewer bytes than asked for; read until the buffer is full.
                var bytes = new byte[stream.Length];
                int read = 0, count;
                while (read < bytes.Length && (count = stream.Read(bytes, read, bytes.Length - read)) > 0) read += count;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { name = resourceName };
                if (!Jotunn.Utils.AssetUtils.LoadImage(texture, bytes))
                {
                    Jotunn.Logger.LogWarning($"Embedded texture '{resourceName}' failed to decode.");
                    Object.Destroy(texture);
                    return null;
                }
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Trilinear;
                return texture;
            }
        }

        // A cluster of vanilla resin candles of different heights.
        private static CustomPiece CreateRitualCandles(string name, PieceConfig config)
        {
            if (!RequirePrefab(CandlePrefab)) return null;

            var prefab = CreateBarePrefab(name);
            var candleSource = PrefabManager.Cache.GetPrefab<GameObject>(CandlePrefab);
            for (int i = 0; i < s_candles.Length; i++)
            {
                var spot = s_candles[i];
                var candle = Object.Instantiate(candleSource, prefab.transform, false);
                candle.name = "candle" + i;
                LightFireplace(candle);
                StripToVisualAndEffects(candle);
                candle.transform.localScale *= spot.z;
                candle.transform.localRotation = Quaternion.Euler(0f, i * 47f, 0f);
                // Stand the candle's base on the ground, whatever its pivot. The vanilla prefab isn't
                // stored at the origin, so start from zero before measuring.
                candle.transform.localPosition = Vector3.zero;
                var bounds = MeshBounds(candle.transform, prefab.transform, true);
                candle.transform.localPosition = new Vector3(spot.x, -bounds.min.y, spot.y);
            }

            var collider = prefab.AddComponent<BoxCollider>();
            var all = MeshBounds(prefab.transform, prefab.transform, true);
            collider.center = all.center;
            collider.size = all.size;
            SetupLoosePiece(prefab);

            // A rendered icon misses the flames; the vanilla candle's icon reads better.
            var candleIcon = candleSource.GetComponent<Piece>()?.m_icon;
            config.Icon = candleIcon ? candleIcon : RenderIcon(prefab);
            return new CustomPiece(prefab, true, config);
        }

        // Keep a flame burning without fuel: freeze the fireplace's lit state, then drop it.
        // The resin candle has no m_enabledObject; its flame and light are the high/low quality
        // variants, of which vanilla shows one by graphics setting. Keep the high one.
        private static void LightFireplace(GameObject root)
        {
            foreach (var fireplace in root.GetComponentsInChildren<Fireplace>(true))
            {
                SetActive(fireplace.m_enabledObject, true);
                SetActive(fireplace.m_enabledObjectLow, false);
                SetActive(fireplace.m_enabledObjectHigh, true);
                SetActive(fireplace.m_fullObject, true);
                SetActive(fireplace.m_halfObject, false);
                SetActive(fireplace.m_emptyObject, false);
                SetActive(fireplace.m_playerBaseObject, false);
                Object.DestroyImmediate(fireplace);
            }
        }

        // Like StripToVisual, but keeps flames: particles, lights and their flicker. Colliders go too;
        // the piece gets one box around everything.
        private static void StripToVisualAndEffects(GameObject root)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component is Transform || component is MeshFilter || component is Renderer ||
                        component is LODGroup || component is ParticleSystem || component is Light ||
                        component is LightFlicker || component is LightLod)
                        continue;
                    Object.DestroyImmediate(component);
                }
            }
        }

        private static CustomPiece CreateIdol(string name, PieceConfig config)
        {
            if (!RequirePrefab(IdolModelPrefab)) return null;

            var prefab = CreateBarePrefab(name);
            var model = Object.Instantiate(PrefabManager.Cache.GetPrefab<GameObject>(IdolModelPrefab), prefab.transform, false);
            model.name = "model";
            StripToVisual(model);
            var bounds = FitToHeight(model, IdolHeight);
            TintMaterials(model, s_blackMetalTint);

            // Placement only snaps to convex colliders; with just the statue's mesh collider the ghost
            // floats 50 m up. Replace it with a box around the scaled model.
            foreach (var meshCollider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(meshCollider);
            var box = prefab.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;

            SetupLoosePiece(prefab);

            config.Icon = RenderIcon(prefab);
            return new CustomPiece(prefab, true, config);
        }

        private static CustomPiece CreateOfferingCup(string name, PieceConfig config)
        {
            var prefab = CreateBarePrefab(name);
            OfferingCupModel.Build(prefab.transform, FindMaterial(SilverMaterialPrefab));

            var collider = prefab.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, OfferingCupModel.Height * 0.5f, 0f);
            collider.size = new Vector3(OfferingCupModel.Width, OfferingCupModel.Height, OfferingCupModel.Width);
            SetupLoosePiece(prefab);

            config.Icon = RenderIcon(prefab);
            return new CustomPiece(prefab, true, config);
        }

        // Scales a model uniformly to the given height, centers it on the piece origin and stands its
        // base on y = 0, so the origin (used for placement and range checks) matches what you see.
        // Returns the model's resulting bounds in its parent's space.
        private static Bounds FitToHeight(GameObject model, float height)
        {
            var parent = model.transform.parent;
            var local = MeshBounds(model.transform, parent);
            if (local.size.y <= 0f) return local;

            model.transform.localScale *= height / local.size.y;
            var scaled = MeshBounds(model.transform, parent);
            model.transform.localPosition -= new Vector3(scaled.center.x, scaled.min.y, scaled.center.z);
            return MeshBounds(model.transform, parent);
        }

        // Combined mesh bounds of everything under root, expressed in space's local coordinates.
        // With visibleOnly, meshes on disabled branches below root (e.g. a fireplace's unlit state) are skipped.
        private static Bounds MeshBounds(Transform root, Transform space, bool visibleOnly = false)
        {
            var toSpace = space.worldToLocalMatrix;
            bool any = false;
            var result = new Bounds();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                if (visibleOnly && !ActiveBelow(filter.transform, root)) continue;
                var renderer = filter.GetComponent<Renderer>();
                if (visibleOnly && (!renderer || !renderer.enabled)) continue;
                var b = filter.sharedMesh.bounds;
                var toLocal = toSpace * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = toLocal.MultiplyPoint3x4(new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (any) result.Encapsulate(corner);
                    else result = new Bounds(corner, Vector3.zero);
                    any = true;
                }
            }
            return result;
        }

        // Whether t and its parents up to root are active, ignoring root's own (possibly inactive) parents.
        private static bool ActiveBelow(Transform t, Transform root)
        {
            for (; t && t != root; t = t.parent)
                if (!t.gameObject.activeSelf) return false;
            return root.gameObject.activeSelf;
        }

        // Darkens copies of a model's materials, e.g. to read as black metal.
        private static void TintMaterials(GameObject model, Color tint)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i]) continue;
                    var copy = new Material(materials[i]) { name = materials[i].name + "_warden" };
                    if (copy.HasProperty("_Color")) copy.SetColor("_Color", tint);
                    if (copy.HasProperty("_Metallic")) copy.SetFloat("_Metallic", 0.6f);
                    if (copy.HasProperty("_Glossiness")) copy.SetFloat("_Glossiness", 0.45f);
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }

        // Jotunn's empty prefab is a primitive cube; strip the cube so only our model shows.
        private static GameObject CreateBarePrefab(string name)
        {
            var prefab = PrefabManager.Instance.CreateEmptyPrefab(name, true);
            Object.DestroyImmediate(prefab.GetComponent<Collider>());
            Object.DestroyImmediate(prefab.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(prefab.GetComponent<MeshFilter>());
            return prefab;
        }

        // Adds the piece plumbing a model-only prefab needs: Piece, effects, and piece-layer colliders.
        private static void SetupLoosePiece(GameObject prefab)
        {
            var piece = prefab.GetComponent<Piece>() ?? prefab.AddComponent<Piece>();
            if (s_wardPiece)
            {
                piece.m_placeEffect = s_wardPiece.m_placeEffect;
            }

            int pieceLayer = LayerMask.NameToLayer("piece");
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = pieceLayer;

            var nview = prefab.GetComponent<ZNetView>() ?? prefab.AddComponent<ZNetView>();
            nview.m_persistent = true;
        }

        // Removes everything but rendering and collision from an instantiated vanilla prop.
        private static void StripToVisual(GameObject root)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component is Transform || component is MeshFilter || component is Renderer ||
                        component is LODGroup || component is Collider)
                        continue;
                    Object.DestroyImmediate(component);
                }
            }
        }

        private static Sprite RenderIcon(GameObject prefab)
        {
            // Dedicated servers have no graphics device to render with, and never show icons.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
            try
            {
                return RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation);
            }
            catch (System.Exception e)
            {
                Jotunn.Logger.LogWarning($"Icon render failed for {prefab.name}: {e.Message}");
                return s_wardPiece ? s_wardPiece.m_icon : null;
            }
        }

        private static Material FindMaterial(string prefabName)
        {
            var prefab = PrefabManager.Cache.GetPrefab<GameObject>(prefabName);
            var renderer = prefab ? prefab.GetComponentInChildren<MeshRenderer>(true) : null;
            if (!renderer) Jotunn.Logger.LogWarning($"No material source '{prefabName}'.");
            return renderer ? renderer.sharedMaterial : null;
        }

        private static bool RequirePrefab(string prefabName)
        {
            if (PrefabManager.Cache.GetPrefab<GameObject>(prefabName)) return true;
            Jotunn.Logger.LogWarning($"Vanilla prefab '{prefabName}' not found; skipping the piece built from it.");
            return false;
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go) go.SetActive(active);
        }
    }
}
