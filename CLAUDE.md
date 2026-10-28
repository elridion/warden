# Warden

Valheim mod adding the **Warden stone**: a buildable ward that blocks random creature spawns inside
its radius, plus four upgrade pieces that raise its level and radius. Stack, build, verification
and git workflow are shared by all mods: see `../CLAUDE.md` and `../.claude/rules/`.

## Where things are

- `src/WardPieces.cs` builds every piece; `src/SpawnWard.cs` is the ward component,
  `src/WardExtension.cs` the upgrade component, `src/WardHull.cs` a ward's radius motes and `src/WardWall.cs` the haze wall near the player, one mesh for all wards; `*Patches.cs` are
  the Harmony patches (spawns, dropped items, damage flash, placement).
- `assets/` embedded textures and OBJs (FBX and 2048 px rune stone maps kept as source only);
  `tools/` PowerShell generators for the banner, rune stone textures and the icon.
- `docs/` design notes. `hexium.json` Hexium listing metadata.

## Invariants that must not break

- **Saved-world compatibility.** Persisted or networked identifiers: prefab names
  `piece_spawnward` and `piece_spawnward_ext1`..`ext4`, ZDO keys `spawnward_enabled` and `spawnward_radius` (float, the
  stone's current radius, read by the Weathering mod), RPC
  `SpawnWard_Toggle`, plugin GUID `elridion.warden`, and config section/key names (renaming a key
  resets users' settings).
- **Upgrade tiers** are numbered in `WardPieces.Register`: 1 banner, 2 idol, 3 offering cup,
  4 ritual candles (`StackableTier`). Non-stackable tiers count once each; stackable pieces count
  per piece up to `MaxStackedUpgrades`. Tier numbers are part of the prefab names: never renumber.
- **Docs follow code.** `package/README.md` keeps in sync with the code in the same change: the
  upgrade table (level, piece, biome, cost, radius) and the stone's cost with `WardPieces.Register`,
  the config table with `WardenPlugin.Awake`, the stacking note with `MaxStackedUpgrades`. Radius
  per level is `BaseRadius + RadiusPerUpgrade * (level - 1)`; with the defaults (40 m, +20 m, four
  candle clusters) a full stone is level 8 with 180 m.

## Art and licensing

The icon is game-icons.net "menhir" (CC BY 3.0, `tools/make_icon.ps1`). Third-party models and
textures carry their source and license in a `README.md` next to them under `assets/models/`;
asset rules are in `.claude/rules/assets.md`.
