# Warden

Adds the **Warden stone**, a buildable ward that stops creatures from spawning around your base,
and upgrade pieces that extend its reach.

## Features

- Warden stone and its upgrades in the hammer's Defense tab. The stone requires a workbench
  nearby; upgrades only need a warden stone within range.
  Cost: 15 Stone, 10 Greydwarf Eye, 2 Surtling Core.
- While active, random world spawns inside the radius are blocked.
- Toggle on/off by interacting with it. Hover to see its level, radius and connected upgrades.
- An animated amber haze sphere marks the protected area while the ward is active.
  Overlapping wards merge into one common hull.
- Optional: also block raid/event spawns (off by default; raiders can still walk in from outside).

## Upgrades

Like crafting station extensions: build them within 5 m of a warden stone. Each distinct upgrade raises
the stone's level by one and its radius by 20 m; a second banner, idol or cup adds nothing. Ritual
candles stack: every cluster in range counts, up to 4, so a fully upgraded stone reaches 180 m.

| Level | Upgrade | Biome | Cost | Radius |
| --- | --- | --- | --- | --- |
| 1 | Warden stone | Black Forest | 15 Stone, 10 Greydwarf Eye, 2 Surtling Core | 40 m |
| 2 | Runed hide banner | Black Forest | 6 Leather Scraps, 2 Fine Wood, 4 Coal, 5 Tin | 60 m |
| 3 | Sacred idol | Swamp | 20 Stone, 5 Iron, 3 Withered Bone | 80 m |
| 4 | Offering cup | Mountain | 5 Silver, 5 Obsidian | 100 m |
| 5-8 | Ritual candles (each) | Plains | 10 Tar, 10 Resin, 5 Candle Wick | 120-180 m |

## Configuration

`BepInEx/config/elridion.warden.cfg`. Everything under `General` is server-synced and admin-only;
`ShowHull` is per client.

| Key | Default | Description |
| --- | --- | --- |
| `BaseRadius` | 40 | Radius in meters without upgrades (4-128) |
| `RadiusPerUpgrade` | 20 | Meters added per upgrade (0-64) |
| `UpgradeRange` | 5 | Max distance between an upgrade and its stone (2-20) |
| `MaxStackedUpgrades` | 4 | How many ritual candle clusters count per stone (1-16) |
| `BlockEventSpawns` | false | Also block raid/event spawns |
| `ShowHull` | Always | Haze visibility: `Always`, `OnHover` or `Off` |

## Requirements

- BepInEx for Valheim
- [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/)

Every client and the server need the mod installed, at the same minor version.

## Credits

- Offering cup model: [The Base Mesh](https://www.thebasemesh.com/) (CC0).

## Source and issues

Source code (MIT license) and bug reports: [github.com/elridion/warden](https://github.com/elridion/warden).
