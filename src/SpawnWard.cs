using System.Collections.Generic;
using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Buildable ward that blocks random creature spawns inside its radius while enabled.
    /// </summary>
    public class SpawnWard : MonoBehaviour, Hoverable, Interactable
    {
        private static readonly List<SpawnWard> s_instances = new List<SpawnWard>();
        private static readonly int s_enabledHash = "spawnward_enabled".GetStableHashCode();
        private const string RpcToggle = "SpawnWard_Toggle";

        public string m_name = "$piece_spawnward";
        public GameObject m_enabledEffect;
        public CircleProjector m_areaMarker;
        public EffectList m_activateEffect = new EffectList();
        public EffectList m_deactivateEffect = new EffectList();
        public float m_hoverOffset = 1f;

        private ZNetView m_nview;
        private bool m_lastEnabled;
        private bool m_statusInitialized;

        public static float Radius => WardenPlugin.Radius.Value;

        public static bool IsInsideActiveWard(Vector3 point)
        {
            float radiusSqr = Radius * Radius;
            foreach (var ward in s_instances)
            {
                if (!ward || !ward.IsEnabled()) continue;

                Vector3 pos = ward.transform.position;
                float dx = pos.x - point.x;
                float dz = pos.z - point.z;
                if (dx * dx + dz * dz <= radiusSqr) return true;
            }
            return false;
        }

        private void Awake()
        {
            if (m_areaMarker)
            {
                m_areaMarker.m_radius = Radius;
                m_areaMarker.gameObject.SetActive(false);
            }

            m_nview = GetComponent<ZNetView>();
            if (!m_nview || !m_nview.IsValid())
            {
                // Placement ghost: show the area so the player can see the coverage.
                if (m_areaMarker) m_areaMarker.gameObject.SetActive(true);
                return;
            }

            m_nview.Register(RpcToggle, RPC_Toggle);
            s_instances.Add(this);
            InvokeRepeating(nameof(UpdateStatus), 0f, 1f);
        }

        private void OnDestroy()
        {
            s_instances.Remove(this);
        }

        public bool IsEnabled()
        {
            return m_nview && m_nview.IsValid() && m_nview.GetZDO().GetBool(s_enabledHash, true);
        }

        private void UpdateStatus()
        {
            bool enabled = IsEnabled();
            if (m_enabledEffect) m_enabledEffect.SetActive(enabled);
            if (m_areaMarker) m_areaMarker.m_radius = Radius;

            if (m_statusInitialized && enabled != m_lastEnabled)
            {
                var effects = enabled ? m_activateEffect : m_deactivateEffect;
                effects?.Create(transform.position, transform.rotation);
            }

            m_lastEnabled = enabled;
            m_statusInitialized = true;
        }

        private void RPC_Toggle(long sender)
        {
            if (!m_nview.IsOwner()) return;
            m_nview.GetZDO().Set(s_enabledHash, !IsEnabled());
            UpdateStatus();
        }

        public string GetHoverText()
        {
            if (!m_nview || !m_nview.IsValid()) return "";

            ShowAreaMarker();
            bool enabled = IsEnabled();
            string state = enabled ? "$piece_guardstone_active" : "$piece_guardstone_inactive";
            string action = enabled ? "$piece_guardstone_deactivate" : "$piece_guardstone_activate";
            return Localization.instance.Localize(
                $"{m_name} ( {state} )\n[<color=yellow><b>$KEY_Use</b></color>] {action}");
        }

        public string GetHoverName() => m_name;

        public float GetHoverOffset() => m_hoverOffset;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;
            if (!PrivateArea.CheckAccess(transform.position)) return true;

            m_nview.InvokeRPC(RpcToggle);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        private void ShowAreaMarker()
        {
            if (!m_areaMarker) return;
            m_areaMarker.gameObject.SetActive(true);
            CancelInvoke(nameof(HideMarker));
            Invoke(nameof(HideMarker), 0.5f);
        }

        private void HideMarker()
        {
            if (m_areaMarker) m_areaMarker.gameObject.SetActive(false);
        }
    }
}
