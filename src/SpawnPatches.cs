using HarmonyLib;
using UnityEngine;

namespace Warden
{
    [HarmonyPatch(typeof(SpawnSystem))]
    internal static class SpawnPatches
    {
        // IsSpawnPointGood has no event flag, so track which spawn list is being processed.
        private static bool s_processingEventSpawns;

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
        private static void IsSpawnPointGood_Postfix(ref bool __result, ref Vector3 spawnPoint)
        {
            if (!__result) return;
            if (s_processingEventSpawns && !WardenPlugin.BlockEventSpawns.Value) return;
            if (SpawnWard.IsInsideActiveWard(spawnPoint)) __result = false;
        }
    }
}
