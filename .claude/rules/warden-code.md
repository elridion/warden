---
paths:
  - "src/**/*.cs"
---

# Warden code specifics

Adds to the workspace-wide `code-style.md` and `valheim-modding.md` in `../.claude/rules/`.

## Pieces

- Every piece is built in `WardPieces.Register`, guarded by `RequirePrefab` plus a try/catch in
  `AddWard` / `AddExtension`. New pieces go there and are guarded the same way.
- Piece configs come from the `Config(...)` helper: hammer table, vanilla `Misc` category with only
  the `Defense` usage (a custom category or empty usage adds extra hammer tags), `$`-prefixed tokens.
  Only the warden stone needs a workbench; upgrades must not have a station, since "near a warden
  stone" is their requirement (`PlacementPatches`, `UpgradeRange`). Requirements use `Req(item, amount)`.
- Model-only pieces start from `CreateBarePrefab` (strips the cube Jotunn's empty prefab comes with)
  and finish with `SetupLoosePiece` (Piece, place effect, `piece` layer on every child, persistent
  `ZNetView`). Strip vanilla props with `StripToVisual` / `StripToVisualAndEffects`; size them with
  `MeshBounds` / `FitToHeight`; icons through `RenderIcon`, which falls back to the ward's icon.
- Materials are cloned with the `_warden` name suffix. The stone reuses vanilla tokens
  (`$piece_guardstone_active` etc.); new keys are `piece_spawnward*` for piece names/descriptions and
  `spawnward_*` for other UI strings.
- The runed hide banner keeps the vanilla cloth mesh; `ApplyBannerTexture` places the design into an
  atlas at the UV rect the cloth samples. It finds the cloth by its main texture name containing
  "banner" and needs a readable mesh; if a Valheim update breaks either, the banner keeps or
  misplaces its texture with a warning in the log.

## Ward behaviour

- `WardHull` is its own root GameObject, not a child of the ward, so piece highlighting and the
  ghost tint leave it alone; it follows the ward in `LateUpdate` and `SpawnWard.OnDestroy` destroys
  it. Treat any new detached visual the same way.
- The optional `PlayerBase` area (`SpawnWard.UpdateBaseArea`, config `PlayerBaseArea`) is detached the
  same way, on the `character_trigger` layer (vanilla finds `EffectArea`s only through physics
  queries there), and built inactive because `EffectArea.Awake` reads the collider. It is for other
  mods' compatibility only: Warden's own checks never read it.
- Ward coverage (spawn blocking, kept items, damage flash) is a 3D sphere of `Radius` around the
  stone, the same sphere the hull draws and the `PlayerBase` area fills; hull merging and upgrade
  range are 3D distances too. Keep new checks consistent with that sphere.
- `SpawnWard` and `WardExtension` each keep a static `s_instances` list that range queries iterate.
- Gameplay config is in section `General` (admin-only, synced), client-only visuals in `Visuals`.
- The placement postfix runs every frame while building and the spawn postfix on every spawn
  attempt: bail out on the cheapest check first (is the ghost a `WardExtension`?). The placement
  rejection reuses `PlacementStatus.ExtensionMissingStation` and rewrites its text only for upgrades.
