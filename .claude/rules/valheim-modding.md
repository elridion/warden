---
paths:
  - "src/**/*.cs"
---

# Valheim, Jotunn and Harmony patterns

Lessons this codebase already paid for. Follow them when adding or changing pieces, components or patches.

## Registering content

- All pieces are built in `WardPieces.Register`, subscribed to `PrefabManager.OnVanillaPrefabsAvailable`
  in `WardenPlugin.Awake`; it unsubscribes itself so it runs once. New pieces go there too.
- Look vanilla prefabs up with `PrefabManager.Cache.GetPrefab<GameObject>(name)` and guard with
  `RequirePrefab`, so a renamed vanilla prefab skips one piece with a warning instead of breaking the mod.
- Piece configs come from the `Config(...)` helper: hammer piece table, vanilla `Misc` category with the `Defense` usage only
  (a custom category or an empty usage adds extra hammer tags),
  `$`-prefixed localization tokens. Only the warden stone needs a crafting station (workbench);
  upgrades must not have one, since "near a warden stone" is their requirement.
- Requirements use the `Req(item, amount)` helper with vanilla item prefab names (`"GreydwarfEye"`,
  `"CandleWick"`), and are recoverable on deconstruction.
- New player-facing strings go into `WardenPlugin.AddLocalizations` (English only). Reuse vanilla
  tokens where the meaning is identical (the stone uses `$piece_guardstone_active` etc.).

## Building prefabs at runtime

- **Never mutate shared vanilla assets.** Changing a `sharedMaterial`, `sharedMesh` or component on a
  vanilla prefab changes every vanilla instance in the world. Clone materials with
  `new Material(source) { name = source.name + "_warden" }` and assign the copies back through
  `renderer.sharedMaterials`; cloning a prefab via `new CustomPiece(name, basePrefab, config)` or
  `Object.Instantiate` is what makes its components safe to edit.
- Model-only pieces start from `CreateBarePrefab` (strips the primitive cube Jotunn's empty prefab
  comes with) and finish with `SetupLoosePiece` (Piece, place effect, `piece` layer on every child,
  persistent `ZNetView`).
- Strip instantiated vanilla props to visuals with `StripToVisual` / `StripToVisualAndEffects`. These
  loop three passes because Unity refuses to destroy a component another one requires.
- Give placeable pieces an enabled **convex collider** (a fitted `BoxCollider`). Vanilla placement
  starts the ghost about 50 m out along the surface normal and settles it with
  `Collider.ClosestPoint`, which skips disabled and non-convex mesh colliders; a ghost floating
  about 50 m up means it has no usable collider.
- **Vanilla prefabs are not necessarily stored at the origin.** Reset `localPosition` to zero before
  measuring bounds of an instantiated child, and use `MeshBounds`/`FitToHeight` so the piece origin,
  which placement and range checks use, matches the visible base.
- Use `Object.DestroyImmediate` when editing prefabs during registration; `Destroy` is deferred and
  leaves the component on the prefab when it gets cloned.
- Icons: `RenderManager.Instance.Render(prefab, RenderManager.IsometricRotation)` through `RenderIcon`,
  which falls back to the ward's icon. Prefer a vanilla icon when rendering misses something (flames).
- Texture parameters on Valheim shaders: set explicit `Texture2D.blackTexture` rather than clearing a
  slot, because an empty slot falls back to the shader default, which can be white. Check
  `material.HasProperty` before every `Set*`.
- The runed hide banner keeps the vanilla cloth mesh because MagicaCloth simulates it from the
  original; never edit its mesh or UVs. `ApplyBannerTexture` instead places the design into an atlas
  at the UV rect the cloth samples. It finds the cloth renderer by its main texture name containing
  "banner" and needs a readable mesh for the UV rect; if a Valheim update breaks either, the banner
  silently keeps or misplaces its texture (with a warning in the log).
- `WardHull` is deliberately its own root GameObject, not a child of the ward, so piece highlighting
  and the placement ghost tint leave it alone. It follows the ward in `LateUpdate`, and `SpawnWard`
  destroys it explicitly in `OnDestroy`. Treat any new detached visual the same way.

## Components and networking

- Persistent state belongs in the ZDO (`m_nview.GetZDO()`), keyed by a `GetStableHashCode()` hash
  held in a static field. Change it only on the owner (`if (!m_nview.IsOwner()) return;`), driven by
  an RPC registered in `Awake`.
- A component whose `ZNetView` is missing or invalid is the **placement ghost**. It must not register
  RPCs or join the static instance lists (`s_instances`), and anything per-frame for it stays cheap.
- Both `SpawnWard` and `WardExtension` keep a static `s_instances` list, added in `Awake` and removed
  in `OnDestroy`. Range queries iterate those lists; skip destroyed entries with `if (!x) continue;`.
- Prefer squared distances (`sqrMagnitude`) in loops that run often. Spawn blocking is a horizontal
  (XZ) circle; hull merging and upgrade range are full 3D distances. Keep each check consistent with
  its counterpart rather than mixing the two.
- Dedicated servers have no graphics device: anything visual (`WardHull.Create`) returns null there,
  so callers null-check visual objects. Never make spawn logic depend on a visual object.
- Config values are read live through the static `ConfigEntry` fields on `WardenPlugin`
  (`WardenPlugin.BaseRadius.Value`), never cached, so server-synced changes take effect.

## Config

- Gameplay settings go in section `General`, bound with
  `new ConfigurationManagerAttributes { IsAdminOnly = true }` so Jotunn syncs them from the server.
  Client-only visuals go in `Visuals` without that attribute.
- Give numeric settings an `AcceptableValueRange` and a description that states the unit.
- A new or changed setting updates the configuration table in `package/README.md` (Key, Default, and Description
  with the range in parentheses, e.g. "(4-128)").

## Harmony patches

- Patches are `internal static class ...Patch(es)` with `[HarmonyPatch]` attributes, applied by
  `PatchAll` on the plugin assembly; new patch classes need no registration.
- The build compiles against Jotunn's publicized Valheim assemblies, so private vanilla members would
  compile if accessed directly. Don't: keep to the existing Harmony-style access. Target private
  methods by string name (`[HarmonyPatch("UpdateSpawnList")]`), public ones with `nameof`. Read
  private fields through triple-underscore parameters (`___m_placementGhost`), and call private
  methods through a cached `AccessTools.MethodDelegate` (see `PlacementPatches`).
- Postfixes that veto vanilla behaviour return early when vanilla already said no (`if (!__result) return;`)
  and only ever turn a yes into a no.
- Patches run often: the placement postfix every frame while building, the spawn postfix on every
  spawn attempt. Bail out on the cheapest check first (for example "is the ghost a `WardExtension`?")
  and avoid allocations.
- When reusing a vanilla status or message (the placement rejection reuses
  `PlacementStatus.ExtensionMissingStation`), rewrite its text only for our own pieces.
