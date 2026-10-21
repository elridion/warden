# Warden

Valheim mod (BepInEx + Jotunn) that adds the **Warden stone**: a buildable ward that blocks random
creature spawns inside its radius, plus upgrade pieces that extend its reach.

Players: install it from a mod manager. Features, upgrade costs and configuration are on the
mod page, whose source is [`package/README.md`](package/README.md).

## Building

Requires the .NET SDK (8+) and a Valheim install. The build locates Valheim through the Steam
registry entry; override with `-p:VALHEIM_INSTALL="<path>"`.

```
dotnet build -c Release                           # build and copy to BepInEx/plugins/Warden
dotnet build -c Release -p:DeployToGame=false     # build only
dotnet build -c Release -t:Package                # also write dist/Warden-<version>.zip
```

The first build generates publicized Valheim assemblies in `Valheim_Data/Managed/publicized_assemblies`.

The deploy step copies the DLL into the [Gale](https://github.com/Kesomannen/gale) profile
`Default` when it exists (pick another with `-p:GaleProfile=<name>`), otherwise into
`<Valheim>/BepInEx`. Point it anywhere with `-p:DeployBepInEx="<BepInEx folder>"`.

## Project layout

| Path | Purpose |
| --- | --- |
| `src/` | Plugin source: pieces, ward and upgrade components, spawn and placement patches, hull effect |
| `assets/` | Resources embedded in the DLL (banner textures, offering cup model) |
| `tools/` | PowerShell generators for the banner texture and normal map |
| `package/` | Mod page README and icon shipped in the package |
| `docs/` | Design notes |

No Unity project or asset bundles: every mesh, material and effect is built at runtime.

## Releases

CI (`.github/workflows/build.yml`) builds the package on every push and pull request. Pushing a
`vX.Y.Z` tag checks that the tag matches the version in `Warden.csproj` and `src/WardenPlugin.cs`
and has a `CHANGELOG.md` section, then creates a GitHub release and publishes to Hexium.

## License

[MIT](LICENSE). The offering cup model is from [The Base Mesh](https://www.thebasemesh.com/) (CC0).
