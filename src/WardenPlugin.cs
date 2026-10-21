using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace Warden
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class WardenPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "elridion.warden";
        public const string PluginName = "Warden";
        public const string PluginVersion = "0.3.1";

        public enum HullVisibility { Always, OnHover, Off }

        public static ConfigEntry<float> BaseRadius;
        public static ConfigEntry<float> RadiusPerUpgrade;
        public static ConfigEntry<float> UpgradeRange;
        public static ConfigEntry<int> MaxStackedUpgrades;
        public static ConfigEntry<bool> BlockEventSpawns;
        public static ConfigEntry<HullVisibility> ShowHull;

        private Harmony _harmony;

        private void Awake()
        {
            var adminOnly = new ConfigurationManagerAttributes { IsAdminOnly = true };

            BaseRadius = Config.Bind("General", "BaseRadius", 40f,
                new ConfigDescription("Radius in meters of a warden stone without upgrades.",
                    new AcceptableValueRange<float>(4f, 128f), adminOnly));
            RadiusPerUpgrade = Config.Bind("General", "RadiusPerUpgrade", 20f,
                new ConfigDescription("Radius in meters each distinct upgrade piece adds.",
                    new AcceptableValueRange<float>(0f, 64f), adminOnly));
            UpgradeRange = Config.Bind("General", "UpgradeRange", 5f,
                new ConfigDescription("Max distance in meters between an upgrade piece and its warden stone.",
                    new AcceptableValueRange<float>(2f, 20f), adminOnly));
            MaxStackedUpgrades = Config.Bind("General", "MaxStackedUpgrades", 4,
                new ConfigDescription("How many ritual candles around one warden stone count; each adds RadiusPerUpgrade. " +
                    "Other upgrades count once each.",
                    new AcceptableValueRange<int>(1, 16), adminOnly));
            BlockEventSpawns = Config.Bind("General", "BlockEventSpawns", false,
                new ConfigDescription("Also block raid/event spawns inside the radius. Event creatures can still walk in from outside.",
                    null, adminOnly));
            ShowHull = Config.Bind("Visuals", "ShowHull", HullVisibility.Always,
                "When to show the amber haze marking an active ward's radius. Client-side only.");

            AddLocalizations();
            PrefabManager.OnVanillaPrefabsAvailable += WardPieces.Register;

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
                { "piece_spawnward", "Warden stone" },
                { "piece_spawnward_description", "Wild things will not rise where this stone keeps watch." },
                { "piece_spawnward_ext1", "Runed hide banner" },
                { "piece_spawnward_ext1_description", "Runes of truce, inked in tin. The warden stone's watch reaches further." },
                { "piece_spawnward_ext2", "Sacred idol" },
                { "piece_spawnward_ext2_description", "An iron heart beats in the stone. The spirits of the land take notice." },
                { "piece_spawnward_ext3", "Offering cup" },
                { "piece_spawnward_ext3_description", "Filled for the spirits, never emptied." },
                { "piece_spawnward_ext4", "Ritual candles" },
                { "piece_spawnward_ext4_description", "Wax and tar, burned down together. They never gutter, and nothing creeps close." },
                { "spawnward_level", "Level" },
                { "spawnward_connected", "Strengthens a warden stone" },
                { "spawnward_notconnected", "No warden stone nearby" },
                { "spawnward_needstone", "Must be placed within $1 m of a warden stone" },
            });
        }
    }
}
