using System;
using HarmonyLib;
using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Upgrade pieces must be placed near a warden stone, like vanilla station extensions.
    /// </summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class PlacementPatches
    {
        private static readonly Action<Player, bool> s_setPlacementGhostValid =
            AccessTools.MethodDelegate<Action<Player, bool>>(AccessTools.Method(typeof(Player), "SetPlacementGhostValid"));

        private static void Postfix(Player __instance, GameObject ___m_placementGhost, ref Player.PlacementStatus ___m_placementStatus)
        {
            if (!___m_placementGhost) return;

            var extension = ___m_placementGhost.GetComponent<WardExtension>();
            if (!extension) return;

            var ward = SpawnWard.FindClosestWard(___m_placementGhost.transform.position, WardExtension.Range);
            if (ward)
            {
                extension.StartConnectionEffect(ward.ConnectionPoint);
                return;
            }

            extension.StopConnectionEffect();
            if (___m_placementStatus == Player.PlacementStatus.Valid)
            {
                LogRejection(___m_placementGhost);
                ___m_placementStatus = Player.PlacementStatus.ExtensionMissingStation;
                s_setPlacementGhostValid(__instance, false);
            }
        }

        private static float s_nextLogTime;

        // Throttled diagnostics: where the ghost is versus the nearest warden stone.
        private static void LogRejection(GameObject ghost)
        {
            if (Time.time < s_nextLogTime) return;
            s_nextLogTime = Time.time + 2f;

            var nearest = SpawnWard.FindClosestWard(ghost.transform.position, float.MaxValue);
            string distance = nearest ? Vector3.Distance(nearest.transform.position, ghost.transform.position).ToString("0.0") + " m" : "none loaded";
            Jotunn.Logger.LogInfo($"{ghost.name} not placeable: ghost at {ghost.transform.position}, nearest warden stone {distance}, range {WardExtension.Range} m");
        }
    }
}
