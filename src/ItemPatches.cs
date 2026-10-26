using HarmonyLib;
using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Dropped items inside an active warden stone count as inside a base, so they never despawn,
    /// like items near a workbench. Vanilla deletes a dropped item after an hour unless it is in a
    /// base or a player is near; the base check is the only part changed here.
    /// </summary>
    [HarmonyPatch(typeof(ItemDrop))]
    internal static class ItemPatches
    {
        // Vanilla's own height cut-off in IsInsideBase: items below it (sunk in the sea) still despawn.
        private const float MinBaseHeight = 28f;

        [HarmonyPostfix]
        [HarmonyPatch("IsInsideBase")]
        private static void IsInsideBase_Postfix(ItemDrop __instance, ref bool __result)
        {
            if (__result || !WardenPlugin.KeepDroppedItems.Value) return;

            Vector3 position = __instance.transform.position;
            if (position.y > MinBaseHeight && SpawnWard.IsInsideActiveWard(position)) __result = true;
        }
    }
}
