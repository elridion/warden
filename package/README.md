# Warden

Spawn-proof your base without a ring of workbenches.

In vanilla Valheim, creatures only stop spawning close to player structures, and the vanilla ward
only keeps other players out, not creatures. Keeping a larger base free of spawns means
scattering workbenches and fires across it and along its edges, just for their side effect.

Warden replaces that chore with one dedicated piece: the **Warden stone**, a rune stone that keeps
random creature spawns out of its radius while active. Upgrade pieces built next to it, like
crafting station extensions, widen the protected area as you progress.

## Vanilla in spirit

- Built with the hammer from ordinary materials, next to a workbench, like any other piece.
- Its reach grows with progression: each upgrade needs materials from the next biome, from the
  Black Forest to the Plains.
- It only stops creatures from appearing out of nowhere. Raids still happen, but their creatures
  spawn outside the radius and walk in, creatures from outside can still walk in, and fixed spawners such as greydwarf nests and dungeon
  spawns keep working, so your walls and defenses still matter.

## Features

- Warden stone and its upgrades in the hammer's Defense tab. The stone requires a workbench
  nearby; upgrades only need a warden stone within range.
  Cost: 15 Stone, 10 Greydwarf Eye, 2 Surtling Core.
- While active, random world spawns inside the radius are blocked. The radius reaches up and down
  as well as outwards: the protected area is the sphere the haze shows, so build the stone near the
  middle height of the area you want covered.
- Toggle on/off by interacting with it. Hover to see its level, radius and connected upgrades.
- An animated amber haze sphere marks the protected area while the ward is active.
  Overlapping wards merge into one common hull.
- Items dropped inside an active stone's radius never despawn, as in a vanilla base (near a
  workbench or fire). Turn `KeepDroppedItems` off for vanilla despawning.
- Like the vanilla ward, an active stone flashes when something inside its radius takes damage, so
  you notice when creatures attack your walls.
- Raid/event spawns are kept out of the radius too, so raiders spawn beyond it and walk in. With a
  large radius and everyone near the stone, a raid may find nowhere to spawn and bring few or no
  creatures; turn `BlockEventSpawns` off to let raiders spawn inside the radius as in vanilla.
- Creatures you want around anyway, such as deer or hares, can be let through: list their prefab
  names in `AllowedCreatures` (for example `Deer, Hare`) and they spawn inside the radius as usual.

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
`Visuals` (`ShowHull`, `FlashOnDamage`) is per client.

| Key | Default | Description |
| --- | --- | --- |
| `BaseRadius` | 40 | Radius in meters without upgrades (4-128) |
| `RadiusPerUpgrade` | 20 | Meters added per upgrade (0-64) |
| `UpgradeRange` | 5 | Max distance between an upgrade and its stone (2-20) |
| `MaxStackedUpgrades` | 4 | How many ritual candle clusters count per stone (1-16) |
| `BlockEventSpawns` | true | Also keep raid/event spawns out of the radius; a large radius can stop raids near its center |
| `AllowedCreatures` | (empty) | Comma-separated creature prefab names that may still spawn inside the radius, e.g. `Deer, Hare` |
| `KeepDroppedItems` | true | Dropped items inside an active stone's radius never despawn |
| `PlayerBaseArea` | false | Also mark the radius as a vanilla player base, for compatibility (see below) |
| `ShowHull` | Always | Haze visibility: `Always`, `OnHover` or `Off` |
| `FlashOnDamage` | true | Flash the stone when something inside its radius is damaged |

### PlayerBaseArea

Warden blocks spawns with its own check, so the game and other mods don't see a warden stone as a
player base the way they see a workbench or fire. Mods that only respect vanilla player bases (for
example custom spawners) therefore ignore it. Turn `PlayerBaseArea` on to also mark each active
stone's radius as a vanilla player base. That brings vanilla's other base effects with it:

- Greydwarf nests and other creature spawners inside the radius stop spawning.
- Each stone counts toward the base value raids need, wherever you stand inside its radius.
- Vanilla may keep raid creatures out of the radius even with `BlockEventSpawns` off.
- Dropped items inside never despawn, even with `KeepDroppedItems` off.

## Languages

Piece names, descriptions and messages follow the game's language setting, in every language
Valheim offers except Abenaki, which shows English. The translations have not been checked by
native speakers yet; corrections are welcome on GitHub (see below). Configuration descriptions stay
in English.

## Requirements

- BepInEx for Valheim
- [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/)

Every client and the server need the mod installed, at the same minor version.

## Credits

- Warden stone model: "Rune Stone Low Poly" (https://skfb.ly/6pxsp) by KaguKin, licensed under
  [Creative Commons Attribution 4.0](http://creativecommons.org/licenses/by/4.0/). Modified: textures
  resized, ambient occlusion baked in and brightened, rune glow mask derived; model rescaled in game.
- Offering cup model: [The Base Mesh](https://www.thebasemesh.com/) (CC0).
- Mod icon: "Menhir" by Delapouite from [game-icons.net](https://game-icons.net), licensed under
  [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/). Recoloured and outlined.

## Source and issues

Bug reports and feedback: the [#warden channel on Discord](https://discord.gg/wqUA7vvvp2).

Source code (MIT license): [github.com/elridion/warden](https://github.com/elridion/warden).
