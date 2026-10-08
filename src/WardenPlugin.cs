using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Warden
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class WardenPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "elridion.warden";
        public const string PluginName = "Warden";
        public const string PluginVersion = "0.2.0";

        public const string PiecePrefabName = "piece_spawnward";
        private const string BasePrefabName = "guard_stone";

        public enum HullVisibility { Always, OnHover, Off }

        public static ConfigEntry<float> Radius;
        public static ConfigEntry<bool> BlockEventSpawns;
        public static ConfigEntry<HullVisibility> ShowHull;

        private Harmony _harmony;

        private void Awake()
        {
            var adminOnly = new ConfigurationManagerAttributes { IsAdminOnly = true };

            Radius = Config.Bind("General", "Radius", 60f,
                new ConfigDescription("Radius in meters inside which creature spawns are blocked.",
                    new AcceptableValueRange<float>(4f, 128f), adminOnly));
            BlockEventSpawns = Config.Bind("General", "BlockEventSpawns", false,
                new ConfigDescription("Also block raid/event spawns inside the radius. Event creatures can still walk in from outside.",
                    null, adminOnly));
            ShowHull = Config.Bind("Visuals", "ShowHull", HullVisibility.Always,
                "When to show the translucent dome marking an active ward's radius. Client-side only.");

            AddLocalizations();
            PrefabManager.OnVanillaPrefabsAvailable += AddPieces;

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(WardenPlugin).Assembly);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private static void AddLocalizations()
        {
            LocalizationManager.Instance.GetLocalization().AddTranslation("English", new Dictionary<string, string>
            {
                { "piece_spawnward", "Warden Stone" },
                { "piece_spawnward_description", "Wards the surrounding land against wild creatures. Nothing spawns inside its radius while active." },
            });
        }

        private static void AddPieces()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= AddPieces;

            var config = new PieceConfig
            {
                Name = "$piece_spawnward",
                Description = "$piece_spawnward_description",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Misc,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("Stone", 15, 0, true),
                    new RequirementConfig("GreydwarfEye", 10, 0, true),
                    new RequirementConfig("SurtlingCore", 2, 0, true),
                },
            };

            var piece = new CustomPiece(PiecePrefabName, BasePrefabName, config);
            ConvertToSpawnWard(piece.PiecePrefab);
            PieceManager.Instance.AddPiece(piece);
        }

        // Swaps the cloned vanilla PrivateArea for SpawnWard, reusing its visuals and effects.
        private static void ConvertToSpawnWard(GameObject prefab)
        {
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
        }
    }
}
