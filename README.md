# Warden

Valheim mod that adds the **Warden stone**, a buildable ward that blocks creature spawns inside its radius,
and upgrade pieces that extend its reach.

## Features

- Warden stone and its upgrades in the hammer's Base defense tab. The stone requires a workbench
  nearby; upgrades only need a warden stone within range.
  Cost: 15 Stone, 10 Greydwarf Eye, 2 Surtling Core.
- While active, random world spawns inside the radius are blocked.
- Toggle on/off by interacting with it. Hover to see its level, radius and connected upgrades.
- An animated amber haze sphere marks the protected area while the ward is active.
  Overlapping wards merge into one common hull.
- Optional: also block raid/event spawns (off by default; raiders can still walk in from outside).

## Upgrades

Like crafting station extensions: build them within 5 m of a warden stone. Each distinct upgrade raises
the stone's level by one and its radius by 15 m; a second banner, idol or cup adds nothing. Ritual
candles stack: every cluster in range counts, up to 4, so a fully upgraded stone reaches 145 m.

| Level | Upgrade | Biome | Cost | Radius |
| --- | --- | --- | --- | --- |
| 1 | Warden stone | Black Forest | 15 Stone, 10 Greydwarf Eye, 2 Surtling Core | 40 m |
| 2 | Runed hide banner | Black Forest | 6 Leather Scraps, 2 Fine Wood, 4 Coal, 5 Tin | 55 m |
| 3 | Sacred idol | Swamp | 20 Stone, 5 Iron, 3 Withered Bone | 70 m |
| 4 | Offering cup | Mountain | 5 Silver, 5 Obsidian | 85 m |
| 5-8 | Ritual candles (each) | Plains | 10 Tar, 10 Resin, 5 Candle Wick | 100-145 m |

## Configuration

`BepInEx/config/elridion.warden.cfg`. Everything under `General` is server-synced and admin-only;
`ShowHull` is per client.

| Key | Default | Description |
| --- | --- | --- |
| `BaseRadius` | 40 | Radius in meters without upgrades (4-128) |
| `RadiusPerUpgrade` | 15 | Meters added per upgrade (0-64) |
| `UpgradeRange` | 5 | Max distance between an upgrade and its stone (2-20) |
| `MaxStackedUpgrades` | 4 | How many ritual candle clusters count per stone (1-16) |
| `BlockEventSpawns` | false | Also block raid/event spawns |
| `ShowHull` | Always | Haze visibility: `Always`, `OnHover` or `Off` |

## Credits

- Offering cup model: [The Base Mesh](https://www.thebasemesh.com/) (CC0).

## Requirements

- BepInEx for Valheim
- [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/)

Every client and the server need the mod installed.

## Building

Requires the .NET SDK (8+). The build locates Valheim via the Steam registry entry; override with
`-p:VALHEIM_INSTALL="<path>"` if needed.

```
dotnet build -c Release              # build and copy to BepInEx/plugins/Warden
dotnet build -c Release -t:Package   # also produce dist/Warden-<version>.zip for Thunderstore/Hexium
```

The first build generates publicized Valheim assemblies in `Valheim_Data/Managed/publicized_assemblies`.
Disable the copy into the game with `-p:DeployToGame=false`.
