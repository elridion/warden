using HarmonyLib;
using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Flashes a warden stone when something inside its radius is damaged, like the vanilla ward.
    /// Vanilla already reports every damaged building piece, destructible and mine rock to the static
    /// <c>PrivateArea.OnObjectDamaged</c>, even with no vanilla ward around, so one postfix covers them all.
    /// </summary>
    [HarmonyPatch(typeof(PrivateArea))]
    internal static class FlashPatches
    {
        // PrivateArea also has a private instance overload of the same name, hence the argument types.
        [HarmonyPostfix]
        [HarmonyPatch(nameof(PrivateArea.OnObjectDamaged), typeof(Vector3), typeof(Character), typeof(bool))]
        private static void OnObjectDamaged_Postfix(Vector3 point)
        {
            SpawnWard.FlashWardAt(point);
        }
    }
}
