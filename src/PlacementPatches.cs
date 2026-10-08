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
                ___m_placementStatus = Player.PlacementStatus.ExtensionMissingStation;
                s_setPlacementGhostValid(__instance, false);
            }
        }

    }

    /// <summary>
    /// The rejection reuses vanilla's "missing station" status, whose message talks about crafting
    /// stations; for upgrade pieces, say a warden stone is what's missing.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.Message))]
    internal static class PlacementMessagePatch
    {
        private static void Prefix(Player __instance, ref string msg, GameObject ___m_placementGhost)
        {
            if (msg != "$msg_extensionmissingstation" || !___m_placementGhost) return;
            if (!___m_placementGhost.GetComponent<WardExtension>()) return;
            msg = Localization.instance.Localize("$spawnward_needstone", WardExtension.Range.ToString("0"));
        }
    }
}
