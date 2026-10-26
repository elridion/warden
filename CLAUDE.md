# Warden

Valheim mod (BepInEx + Jotunn) adding the **Warden stone**: a buildable ward that blocks random
creature spawns inside its radius, plus upgrade pieces that raise its level and radius. Player-facing
description, costs and config table live in `package/README.md` (the mod page shipped in the package);
the root `README.md` is the GitHub page for developers; release history in `CHANGELOG.md`.

## Stack

- C# targeting `net462`, `LangVersion` latest, nullable reference types disabled.
- `JotunnLib` 2.30.2 from NuGet. Jotunn's prebuild generates publicized Valheim assemblies in
  `Valheim_Data/Managed/publicized_assemblies`; Valheim is located through the Steam registry entry
  (override with `-p:VALHEIM_INSTALL="<path>"`).
- Harmony (via BepInEx) for patches. No Unity project and no asset bundles: every mesh, material,
  texture and effect is built or cloned at runtime in code.

## Layout

| Path | Purpose |
| --- | --- |
| `src/WardenPlugin.cs` | BepInEx entry point: GUID/version, config binding, English localization, Harmony `PatchAll` |
| `src/WardPieces.cs` | Builds and registers the stone and the four upgrade pieces from vanilla prefabs |
| `src/SpawnWard.cs` | Ward component: enabled state (ZDO), level/radius, hover/interact, hull visibility |
| `src/WardExtension.cs` | Upgrade component: counts upgrades in range of a stone, connection beam effect |
| `src/WardHull.cs` | Runtime-generated animated haze sphere; merges with overlapping hulls |
| `src/SpawnPatches.cs` | `SpawnSystem` patches that reject spawn points inside active wards |
| `src/ItemPatches.cs` | `ItemDrop.IsInsideBase` postfix: dropped items inside active wards don't despawn |
| `src/FlashPatches.cs` | `PrivateArea.OnObjectDamaged` postfix that flashes the closest active ward when something inside it is damaged |
| `src/PlacementPatches.cs` | Placement rule: upgrades need a warden stone within `UpgradeRange` |
| `src/ObjLoader.cs`, `src/OfferingCupModel.cs` | Minimal OBJ reader and the offering cup model (with lathed fallback) |
| `assets/` | Embedded resources (banner and rune stone textures, cup and rune stone OBJs); FBX and 2048 px rune stone maps kept as source only |
| `tools/` | PowerShell generators for the banner albedo and normal map, the rune stone texture converter, and the mod icon (`make_icon.ps1`, game-icons.net "menhir", CC BY 3.0) |
| `package/README.md`, `package/icon.png` | Mod page and icon shipped in the package (Thunderstore/Hexium) |
| `docs/` | Design notes (crafting-station idea) |
| `.github/workflows/build.yml` | CI: builds the package on push/PR, publishes a GitHub release and the Hexium upload (shared action `elridion/publish-hexium`) on `v*` tags |
| `hexium.json` | Hexium package name, listing categories and install location, read by the shared publish action |

## Commands

Run from the repo root (Windows, PowerShell).

```
dotnet build -c Release                           # build bin/Release/Warden.dll, touches nothing outside the repo
dotnet build -c Release -t:Package                # also writes dist/Warden-<version>.zip
powershell -File tools/make_banner_texture.ps1    # regenerate the banner albedo
powershell -File tools/make_banner_normal.ps1     # then regenerate its normal map
powershell -File tools/make_rune_stone_textures.ps1  # regenerate the warden stone's textures
powershell -File tools/make_icon.ps1              # regenerate package/icon.png
```

- The build never copies anything into the game. The user tests by importing the packaged zip into
  their mod manager (Gale), which installs its own copy; never copy the DLL into a game or Gale
  profile by hand, since the mod manager would overwrite it again.
- A clean build prints one known warning, `MSB3245` for `UnityEngine.ProfilerModule`. It is harmless;
  any other warning is new.

## Verification

There is no test project. "Done" means: `dotnet build -c Release` succeeds with no
new warnings, and behaviour that needs the game (placement, spawning, visuals, multiplayer sync) is
called out as untested-in-game unless the user has confirmed it in Valheim. Never claim in-game
behaviour works on the strength of a successful build.

## Invariants that must not break

- **Saved-world compatibility.** These identifiers are persisted in players' worlds or synced over the
  network; renaming one silently breaks existing builds or multiplayer:
  prefab names `piece_spawnward` and `piece_spawnward_ext1`..`ext4`, the ZDO key `spawnward_enabled`,
  the RPC name `SpawnWard_Toggle`, the plugin GUID `elridion.warden`, and config section/key names
  (renaming a key resets users' settings).
- **Upgrade tiers** are numbered in `WardPieces.Register`: 1 banner, 2 idol, 3 offering cup,
  4 ritual candles (`StackableTier`). Non-stackable tiers count once each; stackable pieces count per
  piece up to `MaxStackedUpgrades`. Tier numbers are part of the prefab names, so never renumber.
- **Vanilla assets are shared.** Never modify a vanilla prefab's materials, meshes or components in
  place; clone first (see `.claude/rules/valheim-modding.md`).
- **Every client and the server need the mod** (`NetworkCompatibility(EveryoneMustHaveMod, VersionStrictness.Minor)`);
  peers whose major or minor version differs cannot connect.
- **Docs follow code.** Costs, radii, config keys/defaults/ranges and tiers appear in `package/README.md`;
  keep its tables in sync with `WardPieces.Register` and `WardenPlugin.Awake` in the same change.
  The root `README.md` carries no tables, only build and project information.

## Commits

History uses plain imperative, sentence-case subjects with no type prefix and no trailing period
(e.g. "Stack ritual candles, lower the banner header, fix the ward glow"), and a wrapped body that
explains why and any non-obvious in-game reason behind the change. User-visible changes update
`CHANGELOG.md` (and `package/README.md` where relevant) in the same commit. Do not commit `bin/`, `obj/` or `dist/`.

## Detailed rules

Path-scoped rules in `.claude/rules/` load when matching files are read or edited:

- `code-style.md` - C# conventions for `src/`
- `valheim-modding.md` - Unity/Valheim/Jotunn/Harmony patterns and pitfalls for `src/`
- `assets.md` - embedded resources and the banner texture generators
- `release.md` - versioning, packaging, README and CHANGELOG
