using System.Collections.Generic;
using UnityEngine;

namespace Warden
{
    /// <summary>
    /// Buildable ward that blocks random creature spawns inside its radius while enabled.
    /// Nearby upgrade pieces (<see cref="WardExtension"/>) raise its level and radius.
    /// </summary>
    public class SpawnWard : MonoBehaviour, Hoverable, Interactable
    {
        private static readonly List<SpawnWard> s_instances = new List<SpawnWard>();
        private static readonly int s_enabledHash = "spawnward_enabled".GetStableHashCode();
        private const string RpcToggle = "SpawnWard_Toggle";
        private static readonly int s_emissionColor = Shader.PropertyToID("_EmissionColor");
        // The vanilla ward's amber rune glow; above 1 so the runes bloom.
        private static readonly Color s_runeGlow = new Color(1.934f, 1.017f, 0.185f);
        // Dedicated servers render nothing, so the glow is not worth setting there.
        private static readonly bool s_headless = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;

        public string m_name = "$piece_spawnward";
        public GameObject m_enabledEffect;
        public MeshRenderer m_model;
        public CircleProjector m_areaMarker;
        public EffectList m_activateEffect = new EffectList();
        public EffectList m_deactivateEffect = new EffectList();
        public float m_hoverOffset = 1f;

        private ZNetView m_nview;
        private bool m_lastEnabled;
        private bool m_statusInitialized;
        private bool m_isGhost;
        private bool m_hovered;
        private int m_level = 1;
        private WardHull m_hull;

        // Read from config every time, so server-synced radius changes apply at once. The level
        // follows placed and removed upgrades once per second (UpdateStatus).
        public float Radius => WardenPlugin.BaseRadius.Value + WardenPlugin.RadiusPerUpgrade.Value * (m_level - 1);
        public Vector3 ConnectionPoint => transform.position + Vector3.up * 1.2f;

        public static bool IsInsideActiveWard(Vector3 point)
        {
            foreach (var ward in s_instances)
            {
                if (!ward || !ward.IsEnabled()) continue;

                Vector3 pos = ward.transform.position;
                float dx = pos.x - point.x;
                float dz = pos.z - point.z;
                float radius = ward.Radius;
                if (dx * dx + dz * dz <= radius * radius) return true;
            }
            return false;
        }

        public static SpawnWard FindClosestWard(Vector3 point, float maxDistance)
        {
            SpawnWard closest = null;
            float best = maxDistance;
            foreach (var ward in s_instances)
            {
                if (!ward) continue;
                float distance = Vector3.Distance(ward.transform.position, point);
                if (distance < best)
                {
                    best = distance;
                    closest = ward;
                }
            }
            return closest;
        }

        private void Awake()
        {
            m_nview = GetComponent<ZNetView>();
            m_isGhost = !m_nview || !m_nview.IsValid();
            m_hull = WardHull.Create(this);
            RefreshLevel();

            if (m_isGhost)
            {
                // Placement ghost: show the area so the player can see the coverage.
                if (m_areaMarker) m_areaMarker.gameObject.SetActive(true);
                UpdateHull();
                return;
            }

            if (m_areaMarker) m_areaMarker.gameObject.SetActive(false);
            m_nview.Register(RpcToggle, RPC_Toggle);
            s_instances.Add(this);
            InvokeRepeating(nameof(UpdateStatus), 0f, 1f);
        }

        private void OnDestroy()
        {
            s_instances.Remove(this);
            if (m_hull) Destroy(m_hull.gameObject);
        }

        private void Update()
        {
            // The ghost moves every frame, so its level follows the upgrades around the cursor.
            if (m_isGhost)
            {
                RefreshLevel();
                UpdateHull();
            }
        }

        private void RefreshLevel()
        {
            m_level = 1 + WardExtension.CountUpgrades(transform.position);
            if (m_areaMarker) m_areaMarker.m_radius = Radius;
        }

        // Other visible hulls close enough to overlap this one, as (center, radius), so the hull
        // can hide the parts of itself inside them. Coincident wards: only the lower instance id stays visible.
        internal void CollectMergeSpheres(List<Vector4> spheres)
        {
            spheres.Clear();
            Vector3 pos = transform.position;
            float radius = Radius;

            foreach (var other in s_instances)
            {
                if (!other || other == this || !other.m_hull || !other.m_hull.IsVisible) continue;

                Vector3 otherPos = other.transform.position;
                float otherRadius = other.Radius;
                float distance = Vector3.Distance(pos, otherPos);
                if (distance >= radius + otherRadius) continue;
                if (distance < 0.1f && !m_isGhost && other.GetInstanceID() > GetInstanceID()) continue;

                spheres.Add(new Vector4(otherPos.x, otherPos.y, otherPos.z, otherRadius));
            }
        }

        private void UpdateHull()
        {
            if (!m_hull) return;

            bool show;
            switch (WardenPlugin.ShowHull.Value)
            {
                case WardenPlugin.HullVisibility.Always: show = m_isGhost || IsEnabled(); break;
                case WardenPlugin.HullVisibility.OnHover: show = m_isGhost || m_hovered; break;
                default: show = false; break;
            }

            m_hull.SetRadius(Radius);
            m_hull.SetVisible(show);
        }

        public bool IsEnabled()
        {
            return m_nview && m_nview.IsValid() && m_nview.GetZDO().GetBool(s_enabledHash, true);
        }

        private void UpdateStatus()
        {
            bool enabled = IsEnabled();
            if (m_enabledEffect) m_enabledEffect.SetActive(enabled);
            UpdateRuneGlow(enabled);
            RefreshLevel();

            if (m_statusInitialized && enabled != m_lastEnabled)
            {
                var effects = enabled ? m_activateEffect : m_deactivateEffect;
                effects?.Create(transform.position, transform.rotation);
            }

            m_lastEnabled = enabled;
            m_statusInitialized = true;
            UpdateHull();
        }

        // Lights the runes while the ward is active, as the vanilla ward does. MaterialMan keeps the
        // value per renderer, so the shared material stays as it is. Set every status tick because
        // hover highlighting resets the emission when it ends.
        private void UpdateRuneGlow(bool enabled)
        {
            if (s_headless || !m_model || !MaterialMan.instance) return;
            MaterialMan.instance.SetValue(m_model.gameObject, s_emissionColor, enabled ? s_runeGlow : Color.black);
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
            WardExtension.PokeConnectionsTo(transform.position, ConnectionPoint);

            bool enabled = IsEnabled();
            string state = enabled ? "$piece_guardstone_active" : "$piece_guardstone_inactive";
            string action = enabled ? "$piece_guardstone_deactivate" : "$piece_guardstone_activate";
            return Localization.instance.Localize(
                $"{m_name} ( {state} )\n<color=#c0c0c0>$spawnward_level {m_level} · {Radius:0} m</color>\n" +
                $"[<color=yellow><b>$KEY_Use</b></color>] {action}");
        }

        public string GetHoverName() => m_name;

        public float GetHoverOffset() => m_hoverOffset;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !m_nview || !m_nview.IsValid()) return false;
            if (!PrivateArea.CheckAccess(transform.position)) return true;

            m_nview.InvokeRPC(RpcToggle);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        private void ShowAreaMarker()
        {
            if (m_areaMarker) m_areaMarker.gameObject.SetActive(true);
            if (!m_hovered)
            {
                m_hovered = true;
                UpdateHull();
            }
            CancelInvoke(nameof(HideMarker));
            Invoke(nameof(HideMarker), 0.5f);
        }

        private void HideMarker()
        {
            if (m_areaMarker) m_areaMarker.gameObject.SetActive(false);
            m_hovered = false;
            UpdateHull();
        }
    }
}
