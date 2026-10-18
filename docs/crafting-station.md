# Idea: the warden stone as a vanilla crafting station

Status: under consideration, nothing implemented.

Today the warden stone (`SpawnWard`) and its upgrades (`WardExtension`) reimplement most of what
vanilla's `CraftingStation` / `StationExtension` pair already does: counting upgrades in range,
the connection beam, and the "needs a station nearby" placement rule (`PlacementPatches`). This note
collects what switching to the vanilla components would gain and cost.

## What vanilla offers

Fields on `StationExtension` in the current game assembly:

| Field | Meaning |
| --- | --- |
| `m_craftingStation` | The station this extension belongs to |
| `m_maxStationDistance` | How close the station must be |
| `m_stack` | Extension counts once per piece instead of once per kind (fits ritual candles) |
| `m_connectionPrefab`, `m_connectionOffset`, `m_continousConnection` | Connection beam |
| `m_hoverOffset` | Hover text offset |

The `CraftingStation` fields were not listed; that type failed to load through reflection outside
the game. Check it in the publicized assembly before starting.

## Gains

- Vanilla handles level counting, the connection beam, the placement check with its own message,
  and the station level shown in the UI. Most of `WardExtension` and all of `PlacementPatches` go away.
- Stackable extensions are supported natively through `m_stack`.
- Other mods that look for crafting stations would see the stone.

## Costs and risks

- **Interact conflict.** `CraftingStation.Interact` opens the crafting menu; the stone uses Interact
  to toggle the ward on and off. Needs a patch or a different key for the toggle.
- **Build range.** A station grants building range (`m_rangeBuild`), like a workbench. Set it to 0,
  or treat it as a feature (the stone doubles as a workbench for its area).
- **Upgrade range no longer live.** `m_maxStationDistance` is a per-prefab value set when the prefab
  is built, so the `UpgradeRange` config would stop following live changes and server sync unless
  extra code updates every instance.
- **Stack cap.** Unverified whether vanilla limits how many `m_stack` extensions count. Keeping
  `MaxStackedUpgrades` may need a patch on the level calculation.
- **Side effects in other mods.** The stone would appear in station lists of crafting-from-containers
  mods and similar, which may be unwanted.
- **Hover text.** The station's hover text and the ward's (state, level, radius) would need merging.

## Compatibility

Saved worlds and multiplayer are safe either way: prefab names (`piece_spawnward`,
`piece_spawnward_ext1`..`ext4`), the ZDO key `spawnward_enabled` and the RPC `SpawnWard_Toggle` stay
unchanged. Adding components to the prefabs does not affect existing saves. Peers still need the same
minor version.

## Possible directions

1. **Upgrades only.** Convert the upgrade pieces to `StationExtension` and give the stone a
   `CraftingStation` with crafting and build range neutralised. Main work: the Interact conflict and
   the upgrade-range config.
2. **Full station.** Make the stone a real workbench-like station: build range, possibly its own
   recipes (ward-themed items). Larger design change; needs gameplay decisions first.
3. **Keep the current custom components.** They work and support live config; only revisit if the
   duplicated logic becomes a maintenance problem.
