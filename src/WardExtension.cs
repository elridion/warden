using System.Collections.Generic;
using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Upgrade piece for a warden stone. Each distinct tier within range raises the stone's level,
    /// the same way crafting station extensions work; stackable tiers count once per piece, up to
    /// <see cref="WardenPlugin.MaxStackedUpgrades"/>.
    /// </summary>
    public class WardExtension : MonoBehaviour, Hoverable
    {
        private static readonly List<WardExtension> s_instances = new List<WardExtension>();

        public int m_tier;
        public bool m_stackable;
        public GameObject m_connectionPrefab;
        public Vector3 m_connectionOffset = Vector3.up;
        public float m_hoverOffset;

        private Piece m_piece;
        private GameObject m_connection;

        public static float Range => WardenPlugin.UpgradeRange.Value;

        /// <summary>Number of upgrades within range of a ward position: one per distinct tier, plus
        /// one per stackable piece up to the stacking cap.</summary>
        public static int CountUpgrades(Vector3 wardPos)
        {
            int mask = 0, stacked = 0;
            float rangeSqr = Range * Range;
            foreach (var ext in s_instances)
            {
                if (!ext || (ext.transform.position - wardPos).sqrMagnitude >= rangeSqr) continue;
                if (ext.m_stackable) stacked++;
                else mask |= 1 << ext.m_tier;
            }

            int count = Mathf.Min(stacked, WardenPlugin.MaxStackedUpgrades.Value);
            for (; mask != 0; mask &= mask - 1) count++;
            return count;
        }

        public static void PokeConnectionsTo(Vector3 wardPos, Vector3 target)
        {
            float rangeSqr = Range * Range;
            foreach (var ext in s_instances)
            {
                if (ext && (ext.transform.position - wardPos).sqrMagnitude < rangeSqr) ext.StartConnectionEffect(target);
            }
        }

        private void Awake()
        {
            m_piece = GetComponent<Piece>();
            var nview = GetComponent<ZNetView>();
            if (nview && nview.GetZDO() != null) s_instances.Add(this);
        }

        private void OnDestroy()
        {
            s_instances.Remove(this);
            StopConnectionEffect();
        }

        public string GetHoverText()
        {
            var ward = SpawnWard.FindClosestWard(transform.position, Range);
            if (ward) StartConnectionEffect(ward.ConnectionPoint);

            string status = ward ? "$spawnward_connected" : "$spawnward_notconnected";
            return Localization.instance.Localize($"{GetHoverName()}\n<color=#c0c0c0>{status}</color>");
        }

        public string GetHoverName() => m_piece ? Localization.instance.Localize(m_piece.m_name) : "";

        public float GetHoverOffset() => m_hoverOffset;

        public void StartConnectionEffect(Vector3 target, float timeout = 1f)
        {
            if (!m_connectionPrefab) return;

            Vector3 from = transform.TransformPoint(m_connectionOffset);
            if (!m_connection) m_connection = Instantiate(m_connectionPrefab, from, Quaternion.identity);

            Vector3 delta = target - from;
            m_connection.transform.position = from;
            m_connection.transform.rotation = Quaternion.LookRotation(delta.normalized);
            m_connection.transform.localScale = new Vector3(1f, 1f, delta.magnitude);
            CancelInvoke(nameof(StopConnectionEffect));
            Invoke(nameof(StopConnectionEffect), timeout);
        }

        public void StopConnectionEffect()
        {
            if (m_connection) Destroy(m_connection);
            m_connection = null;
        }
    }
}
