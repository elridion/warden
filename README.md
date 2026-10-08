# Warden

Valheim mod that adds the **Warden Stone**, a buildable ward that blocks creature spawns inside its radius.

## Features

- New hammer piece (Misc tab), requires a workbench nearby.
  Cost: 15 Stone, 10 Greydwarf Eye, 2 Surtling Core.
- While active, random world spawns inside the radius are blocked.
- Toggle on/off by interacting with it. Hover to show its radius.
- An animated amber haze sphere marks the protected area while the ward is active.
  Overlapping wards merge into one common hull.
- Optional: also block raid/event spawns (off by default; raiders can still walk in from outside).

## Configuration

`BepInEx/config/elridion.warden.cfg`. `Radius` and `BlockEventSpawns` are server-synced and admin-only;
`ShowHull` is per client.

| Key | Default | Description |
| --- | --- | --- |
| `Radius` | 60 | Block radius in meters (4-128) |
| `BlockEventSpawns` | false | Also block raid/event spawns |
| `ShowHull` | Always | Haze visibility: `Always`, `OnHover` or `Off` |

## Requirements

- BepInEx for Valheim
- [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/)

Every client and the server need the mod installed.

## Building

Requires the .NET SDK (8+). The build locates Valheim via the Steam registry entry; override with
`-p:VALHEIM_INSTALL="<path>"` if needed.

```
dotnet build -c Release
```

The first build generates publicized Valheim assemblies in `Valheim_Data/Managed/publicized_assemblies`.
Each build copies `Warden.dll` into `BepInEx/plugins/Warden`. Disable with `-p:DeployToGame=false`.
