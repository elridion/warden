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
        // Saved for other mods (Weathering) that protect ground around stones in unloaded zones.
        private static readonly int s_radiusHash = "spawnward_radius".GetStableHashCode();
        private const string RpcToggle = "SpawnWard_Toggle";
        // Renamed from SpawnWard_Flash when it gained the hit point, so an older client's call without
        // it finds no handler instead of failing to read the missing argument.
        private const string RpcFlash = "SpawnWard_FlashAt";
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
        // Only the sound of the vanilla flash: its blue light clashes with the amber haze, which
        // brightens instead. Played where the damage happened rather than at the stone, since a
        // warden's radius reaches far past the range a vanilla sound carries.
        public EffectList m_flashSoundEffect = new EffectList();
        public float m_hoverOffset = 1f;

        private ZNetView m_nview;
        private bool m_flashAvailable = true;
        private bool m_lastEnabled;
        private bool m_statusInitialized;
        private bool m_isGhost;
        private bool m_hovered;
        private int m_level = 1;
        private WardHull m_hull;
        private SphereCollider m_baseArea;

        // Read from config every time, so server-synced radius changes apply at once. The level
        // follows placed and removed upgrades once per second (UpdateStatus).
        public float Radius => WardenPlugin.BaseRadius.Value + WardenPlugin.RadiusPerUpgrade.Value * (m_level - 1);
        public Vector3 ConnectionPoint => transform.position + Vector3.up * 1.2f;

        // The ward covers a sphere around the stone, the one the hull shows and the optional PlayerBase
        // area fills, so ground far above or below the stone near the rim is outside it.
        public static bool IsInsideActiveWard(Vector3 point)
        {
            foreach (var ward in s_instances)
            {
                if (!ward || !ward.IsEnabled()) continue;

                float radius = ward.Radius;
                if ((ward.transform.position - point).sqrMagnitude <= radius * radius) return true;
            }
            return false;
        }

        // Flashes the closest active ward covering the point, as the vanilla ward does when something
        // inside it is damaged. Only the closest, so overlapping wards don't all flash for one hit.
        public static void FlashWardAt(Vector3 point)
        {
            SpawnWard closest = null;
            float best = float.MaxValue;
            foreach (var ward in s_instances)
            {
                if (!ward || !ward.IsEnabled()) continue;

                float distanceSqr = (ward.transform.position - point).sqrMagnitude;
                float radius = ward.Radius;
                if (distanceSqr > radius * radius || distanceSqr >= best) continue;

                best = distanceSqr;
                closest = ward;
            }
            if (closest) closest.Flash(point);
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
            m_nview.Register<Vector3>(RpcFlash, RPC_Flash);
            var wearNTear = GetComponent<WearNTear>();
            if (wearNTear) wearNTear.m_onDamaged += OnDamaged;
            s_instances.Add(this);
            InvokeRepeating(nameof(UpdateStatus), 0f, 1f);
        }

        private void OnDestroy()
        {
            s_instances.Remove(this);
            if (m_hull) Destroy(m_hull.gameObject);
            if (m_baseArea) Destroy(m_baseArea.gameObject);
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
            // At most one flash per second, so a creature chewing on a wall doesn't flash every hit.
            m_flashAvailable = true;
            UpdateRuneGlow(enabled);
            RefreshLevel();
            SaveRadius();
            UpdateBaseArea(enabled);

            if (m_statusInitialized && enabled != m_lastEnabled)
            {
                var effects = enabled ? m_activateEffect : m_deactivateEffect;
                effects?.Create(transform.position, transform.rotation);
            }

            m_lastEnabled = enabled;
            m_statusInitialized = true;
            UpdateHull();
        }

        // Only the owner writes, and only on a real change, so the ZDO gets no revision every second.
        private void SaveRadius()
        {
            if (!m_nview.IsOwner()) return;

            var zdo = m_nview.GetZDO();
            float radius = Radius;
            if (Mathf.Abs(zdo.GetFloat(s_radiusHash, -1f) - radius) > 0.01f) zdo.Set(s_radiusHash, radius);
        }

        // Lights the runes while the ward is active, as the vanilla ward does. MaterialMan keeps the
        // value per renderer, so the shared material stays as it is. Set every status tick because
        // hover highlighting resets the emission when it ends.
        private void UpdateRuneGlow(bool enabled)
        {
            if (s_headless || !m_model || !MaterialMan.instance) return;
            MaterialMan.instance.SetValue(m_model.gameObject, s_emissionColor, enabled ? s_runeGlow : Color.black);
        }

        // Optional vanilla PlayerBase effect area, for other mods and vanilla systems that only know
        // that marker (nests, raid base value, item despawning). Spawn blocking never depends on it.
        // The same sphere as the hull, so it covers what the player sees as the ward.
        // Its own root object, like WardHull: a child collider would join the stone's WearNTear and
        // hover handling. Physics queries find it only on the character_trigger layer.
        private void UpdateBaseArea(bool enabled)
        {
            if (!enabled || !WardenPlugin.PlayerBaseArea.Value)
            {
                if (m_baseArea) m_baseArea.gameObject.SetActive(false);
                return;
            }

            if (!m_baseArea)
            {
                var area = new GameObject("warden_playerbase");
                // Inactive while being built: EffectArea.Awake reads its collider and type.
                area.SetActive(false);
                area.layer = LayerMask.NameToLayer("character_trigger");
                area.transform.position = transform.position;
                m_baseArea = area.AddComponent<SphereCollider>();
                m_baseArea.isTrigger = true;
                area.AddComponent<EffectArea>().m_type = EffectArea.Type.PlayerBase;
            }

            // Resizing a static collider rebuilds it, so only when the radius actually changed.
            float radius = Radius;
            if (!Mathf.Approximately(m_baseArea.radius, radius)) m_baseArea.radius = radius;
            m_baseArea.gameObject.SetActive(true);
        }

        private void RPC_Toggle(long sender)
        {
            if (!m_nview.IsOwner()) return;
            m_nview.GetZDO().Set(s_enabledHash, !IsEnabled());
            UpdateStatus();
        }

        // Damage to the stone itself, raised by WearNTear on the stone's owner.
        private void OnDamaged()
        {
            if (IsEnabled()) Flash(transform.position);
        }

        // Runs on whichever peer applied the damage, so the flash is sent to everybody.
        private void Flash(Vector3 point)
        {
            if (!m_flashAvailable || !m_nview || !m_nview.IsValid()) return;
            m_flashAvailable = false;
            m_nview.InvokeRPC(ZNetView.Everybody, RpcFlash, point);
        }

        private void RPC_Flash(long sender, Vector3 point)
        {
            if (!WardenPlugin.FlashOnDamage.Value) return;
            m_flashSoundEffect?.Create(point, Quaternion.identity);
            if (m_hull) m_hull.Flash();
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
