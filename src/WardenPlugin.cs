using System.IO;
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
        public const string PluginVersion = "0.4.3";

        private const string TranslationPrefix = "Warden.translations.";

        public enum HullVisibility { Always, OnHover, Off }

        public static ConfigEntry<float> BaseRadius;
        public static ConfigEntry<float> RadiusPerUpgrade;
        public static ConfigEntry<float> UpgradeRange;
        public static ConfigEntry<int> MaxStackedUpgrades;
        public static ConfigEntry<bool> BlockEventSpawns;
        public static ConfigEntry<string> AllowedCreatures;
        public static ConfigEntry<bool> KeepDroppedItems;
        public static ConfigEntry<bool> PlayerBaseArea;
        public static ConfigEntry<HullVisibility> ShowHull;
        public static ConfigEntry<bool> FlashOnDamage;

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
            BlockEventSpawns = Config.Bind("General", "BlockEventSpawns", true,
                new ConfigDescription("Also block raid/event spawns inside the radius, so raiders must spawn beyond it. " +
                    "With a large radius and players near the stone, raids may spawn few or no creatures.",
                    null, adminOnly));
            AllowedCreatures = Config.Bind("General", "AllowedCreatures", "",
                new ConfigDescription("Comma-separated creature prefab names (e.g. Deer, Hare, Neck) that may still spawn " +
                    "inside an active warden stone's radius, from the world's random and raid spawns. Not case-sensitive. " +
                    "With PlayerBaseArea on, vanilla's own base rules can still keep them out.",
                    null, adminOnly));
            KeepDroppedItems = Config.Bind("General", "KeepDroppedItems", true,
                new ConfigDescription("Dropped items inside an active warden stone's radius never despawn, " +
                    "as items inside a vanilla base (near a workbench or fire) don't.",
                    null, adminOnly));
            PlayerBaseArea = Config.Bind("General", "PlayerBaseArea", false,
                new ConfigDescription("Also mark an active warden stone's radius as a vanilla player base, like a workbench, " +
                    "for other mods that only respect that. Side effects: nests and other creature spawners inside stop, " +
                    "each stone adds to the base value raids need, and vanilla may keep raid spawns out even with BlockEventSpawns off.",
                    null, adminOnly));
            ShowHull = Config.Bind("Visuals", "ShowHull", HullVisibility.Always,
                "When to show the amber haze marking an active ward's radius. Client-side only.");
            FlashOnDamage = Config.Bind("Visuals", "FlashOnDamage", true,
                "Flash an active warden stone, as the vanilla ward does, when something inside its radius is damaged. Client-side only.");

            AddLocalizations();
            PrefabManager.OnVanillaPrefabsAvailable += WardPieces.Register;

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(WardenPlugin).Assembly);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        // Each embedded assets/translations/<Language>.json is named after a Valheim language. Jotunn
        // falls back to English for languages and keys without a translation.
        private static void AddLocalizations()
        {
            var localization = LocalizationManager.Instance.GetLocalization();
            var assembly = typeof(WardenPlugin).Assembly;
            foreach (string resource in assembly.GetManifestResourceNames())
            {
                if (!resource.StartsWith(TranslationPrefix) || !resource.EndsWith(".json")) continue;
                string language = resource.Substring(TranslationPrefix.Length, resource.Length - TranslationPrefix.Length - ".json".Length);
                using (var reader = new StreamReader(assembly.GetManifestResourceStream(resource)))
                    localization.AddJsonFile(language, reader.ReadToEnd());
            }
        }
    }
}
