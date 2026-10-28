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
- Solid colliders follow the visible shape, never one box around the whole model. Solid models get
  `AddHullColliders`: `HullBands` horizontal bands, each a convex `MeshCollider` cooked from the
  triangles reaching into it (convex, because placement only settles the ghost on convex colliders).
  It returns false on an unreadable mesh; the caller then falls back to `AddBoxCollider` around the
  bounds. Clusters of separate parts (the ritual candles) get one fitted box per part instead, since
  a hull or box around the cluster fills the gaps. The vanilla banner keeps its own colliders, and
  the ward's triggers are never replaced.
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
- The haze wall is one `WardWall` per client, not one per ward: rays cast from the camera to the
  smooth union of every visible hull's sphere, revealed around the local player, rebuilt at 30 Hz.
  Rays that find nothing keep a transparent vertex: dropping their triangles drew grid-shaped edges. Per-ward patches joined at a
  seam were tried and always showed it (different texture mapping, grid and glow on each side), so
  keep every look term a function of world position and keep the wall a single mesh.
- The optional `PlayerBase` area (`SpawnWard.UpdateBaseArea`, config `PlayerBaseArea`) is detached the
  same way, on the `character_trigger` layer (vanilla finds `EffectArea`s only through physics
  queries there), and built inactive because `EffectArea.Awake` reads the collider. It is for other
  mods' compatibility only: Warden's own checks never read it.
- Ward coverage (spawn blocking, kept items, damage flash) is a 3D sphere of `Radius` around the
  stone, the same sphere the hull draws and the `PlayerBase` area fills; hull merging and upgrade
  range are 3D distances too. Keep new checks consistent with that sphere.
- Each stone saves its radius in the ZDO (`spawnward_radius`, owner only, on change) because the
  level comes from upgrade pieces that exist only while loaded; Weathering reads it to protect
  ground around stones in unloaded zones. It is written only when `ZNetScene.IsAreaReady` holds for
  the four corners of the stone's upgrade range: near a zone border, or just after an ownership
  hand-off, the upgrades' zone may not be loaded yet and the level would save too small a radius.
- `SpawnWard` and `WardExtension` each keep a static `s_instances` list that range queries iterate.
- Gameplay config is in section `General` (admin-only, synced), client-only visuals in `Visuals`.
- The placement postfix runs every frame while building and the spawn postfix on every spawn
  attempt: bail out on the cheapest check first (is the ghost a `WardExtension`?). The placement
  rejection reuses `PlacementStatus.ExtensionMissingStation` and rewrites its text only for upgrades.
- `AllowedCreatures` (comma-separated prefab names, case-insensitive) exempts creatures from the
  spawn block, using the `SpawnData` vanilla passes to `IsSpawnPointGood`. `SpawnPatches` reparses
  it whenever the config string changes (server sync included) and caches the verdict per prefab,
  since reading `GameObject.name` allocates on the spawn path; an empty list skips the lookup.
