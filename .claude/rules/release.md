---
paths:
  - "README.md"
  - "package/README.md"
  - "CHANGELOG.md"
  - "Warden.csproj"
  - "src/WardenPlugin.cs"
  - "package/**"
---

# Versioning, docs and packaging

## Version bumps

The version lives in two places that must always match:

- `<Version>` in `Warden.csproj` (used for the assembly, the package file name and `manifest.json`)
- `PluginVersion` in `src/WardenPlugin.cs` (what BepInEx and Jotunn's version check see)

Add a matching `## x.y.z` section at the top of `CHANGELOG.md` in the same change. Bump only when the
user asks for a release or version change.

Because of `VersionStrictness.Minor`, a minor or major bump means clients and servers on different
versions refuse to connect; a patch bump does not. Use a patch bump only for changes that keep
gameplay and network data identical between peers.

## Jotunn version

The Jotunn version appears twice in `Warden.csproj`: the `JotunnLib` `PackageReference` and
`PackageDependencies` (`ValheimModding-Jotunn-x.y.z`, the Thunderstore dependency string). Update both together.

## README files

There are two, for two audiences:

- `package/README.md` is shipped inside the package as its `README.md` and is the mod's store page
  on Thunderstore and Hexium, so write it for players. Store pages render images only from absolute
  URLs (e.g. raw.githubusercontent.com), never relative paths.
- The root `README.md` is the GitHub page for developers: build, layout, releases, license. It links
  to `package/README.md` instead of repeating its tables.

Keep `package/README.md` in sync with the code in the same change:

- the upgrade table (level, piece, biome, cost, radius) with `WardPieces.Register` and the config defaults;
- the Features cost line for the warden stone;
- the configuration table (Key, Default, Description with the range in parentheses) with `WardenPlugin.Awake`;
- the stacking note (up to `MaxStackedUpgrades`, max radius) when radii or stacking change.

Radius per level is `BaseRadius + RadiusPerUpgrade * (level - 1)`; with the defaults 40 m, +20 m, and
four stacking candle clusters, a fully upgraded stone has level 8 and 180 m.

## CHANGELOG.md

- Newest version first, `## x.y.z` headings, one bullet per player-visible change, written for players
  (what changed in game, which setting), not as a commit log.
- Unreleased work goes into the topmost section only if that version has not been released yet; ask
  when unsure whether it has.
- Mention renamed or removed config keys explicitly (e.g. "Replaces the old `Radius` setting").

## Packaging

- `dotnet build -c Release -t:Package` stages the DLL, `package/icon.png`, `package/README.md`,
  `CHANGELOG.md` and a generated `manifest.json` in `obj/package/`, then zips them to
  `dist/Warden-<version>.zip`. `dist/` is gitignored.
- `package/icon.png` is the Thunderstore icon (256 x 256 PNG).
- Publishing the zip to Thunderstore or another mod site is outward-facing; only the user does that.

## CI (`.github/workflows/build.yml`)

- Every push to `main` and every pull request builds the package on Ubuntu against the Valheim
  dedicated server (SteamCMD, anonymous) and the latest BepInExPack_Valheim, and uploads the zip as
  the `warden-package` artifact.
- Pushing a tag `vX.Y.Z` also fails unless the tag, `<Version>` and `PluginVersion` match and
  `CHANGELOG.md` has a `## X.Y.Z` section, then creates a GitHub release with the zip and that
  section as notes. Tagging and pushing is a release; only do it when the user asks.
- After the GitHub release, the `hexium` job publishes the same zip to Hexium as team `el_team`
  with the shared action `elridion/publish-hexium@v1` (local clone `../publish-hexium`), using the
  `HEXIUM_TOKEN` secret and the `hexium` GitHub environment. The listing's categories and install
  location (`both`, shown as "Client & Server") live in `hexium.json` at the repo root, with the
  package name; the action sets them right after submitting, because submit applies neither.
  Categories edited on the site are overwritten by the next release; change them in `hexium.json`.
  Change the publish flow in `../publish-hexium`, never by adding a script here. Hexium's API has
  no license field; the listing's license is set in the Hexium web UI. API research:
  `../publish-hexium/docs/hexium-api.md`.
