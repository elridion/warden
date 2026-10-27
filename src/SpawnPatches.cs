using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Warden
{
    [HarmonyPatch(typeof(SpawnSystem))]
    internal static class SpawnPatches
    {
        // IsSpawnPointGood has no event flag, so track which spawn list is being processed.
        private static bool s_processingEventSpawns;

        // Creatures from AllowedCreatures, parsed whenever the setting changes (also when the server
        // syncs it). Reading a prefab's name allocates, so the verdict is cached per prefab.
        private static readonly HashSet<string> s_allowedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<GameObject, bool> s_allowedPrefabs = new Dictionary<GameObject, bool>();
        private static string s_parsedSetting;

        [HarmonyPrefix]
        [HarmonyPatch("UpdateSpawnList")]
        private static void UpdateSpawnList_Prefix(bool eventSpawners)
        {
            s_processingEventSpawns = eventSpawners;
        }

        [HarmonyPostfix]
        [HarmonyPatch("UpdateSpawnList")]
        private static void UpdateSpawnList_Postfix()
        {
            s_processingEventSpawns = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch("IsSpawnPointGood")]
        private static void IsSpawnPointGood_Postfix(ref bool __result, SpawnSystem.SpawnData spawn, ref Vector3 spawnPoint)
        {
            if (!__result) return;
            if (s_processingEventSpawns && !WardenPlugin.BlockEventSpawns.Value) return;
            if (IsAllowed(spawn)) return;
            if (SpawnWard.IsInsideActiveWard(spawnPoint)) __result = false;
        }

        private static bool IsAllowed(SpawnSystem.SpawnData spawn)
        {
            string setting = WardenPlugin.AllowedCreatures.Value;
            if (!ReferenceEquals(setting, s_parsedSetting)) Parse(setting);
            if (s_allowedNames.Count == 0 || spawn == null || !spawn.m_prefab) return false;

            if (!s_allowedPrefabs.TryGetValue(spawn.m_prefab, out bool allowed))
            {
                allowed = s_allowedNames.Contains(spawn.m_prefab.name);
                s_allowedPrefabs[spawn.m_prefab] = allowed;
            }
            return allowed;
        }

        private static void Parse(string setting)
        {
            s_parsedSetting = setting;
            s_allowedNames.Clear();
            s_allowedPrefabs.Clear();
            if (string.IsNullOrEmpty(setting)) return;
            foreach (string name in setting.Split(','))
            {
                string trimmed = name.Trim();
                if (trimmed.Length > 0) s_allowedNames.Add(trimmed);
            }
        }
    }
}
