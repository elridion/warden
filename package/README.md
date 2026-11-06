# Warden

An upgradeable spawn-proofing piece for your base. Build the Warden stone and grow its reach with upgrades instead of a ring of workbenches.

## Features

- The Warden stone blocks random creature spawns within its radius while it is active. Interact
  with it to turn it on or off, and hover over it to see its level, radius and upgrades.
- Raids still happen, but their creatures spawn outside the radius and walk in. Nests and dungeon
  spawners keep working, so your walls still matter.
- Amber motes show the protected area, and a haze wall lights up near its edge. The area is a
  sphere, so place the stone near the middle height of your base.
- The stone flashes when something inside its radius takes damage, as the vanilla ward does.
- Dropped items inside the radius never despawn, as in a vanilla base.
- Creatures you want around, such as deer, can still be allowed to spawn.

## How to use

Build the stone with the hammer (Defense tab) next to a workbench. Build upgrades within 5 m of
the stone. Each different upgrade adds a level and 20 m of radius; a second banner, idol or cup
adds nothing. Up to four ritual candle clusters count, so a fully upgraded stone reaches 180 m.

| Level | Piece | Cost | Radius |
| --- | --- | --- | --- |
| 1 | Warden stone | 15 Stone, 10 Greydwarf Eye, 2 Surtling Core | 40 m |
| 2 | Runed hide banner | 6 Leather Scraps, 2 Fine Wood, 4 Coal, 5 Tin | 60 m |
| 3 | Sacred idol | 20 Stone, 5 Iron, 3 Withered Bone | 80 m |
| 4 | Offering cup | 5 Silver, 5 Obsidian | 100 m |
| 5-8 | Ritual candles (each) | 10 Tar, 10 Resin, 5 Candle Wick | 120-180 m |

## Installation

- Requires BepInEx and [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
- Every player and the server need the mod, at the same minor version.
- Translated into every language Valheim offers except Abenaki, which shows English.
- After uninstalling, the stones and upgrades disappear and their materials are not refunded, so
  deconstruct them first. Spawns then work as in vanilla again.

## Configuration

Settings are in `BepInEx/config/elridion.warden.cfg` (or a config manager). `General` settings
are synced from the server and admin-only; `Visuals` are per player.

| Section | Key | Default | Description |
| --- | --- | --- | --- |
| General | `BaseRadius` | 40 | Radius in metres without upgrades (4-128). |
| General | `RadiusPerUpgrade` | 20 | Metres added per upgrade (0-64). |
| General | `UpgradeRange` | 5 | Max distance between an upgrade and its stone (2-20). |
| General | `MaxStackedUpgrades` | 4 | How many ritual candle clusters count per stone (1-16). |
| General | `BlockEventSpawns` | true | Keep raid creatures from spawning inside the radius. A large radius can stop raids near its centre. |
| General | `AllowedCreatures` | (empty) | Creature prefab names that may still spawn inside, e.g. `Deer, Hare`. |
| General | `KeepDroppedItems` | true | Dropped items inside the radius never despawn. |
| General | `PlayerBaseArea` | false | Also mark the radius as a vanilla player base, for mods that only respect those. Nests inside then stop too. |
| Visuals | `ShowHull` | Always | When the haze shows: `Always`, `OnHover` or `Off`. |
| Visuals | `FlashOnDamage` | true | Flash the stone when something inside its radius is damaged. |

## Credits

- Warden stone model: "Rune Stone Low Poly" (https://skfb.ly/6pxsp) by KaguKin, licensed under
  [Creative Commons Attribution 4.0](http://creativecommons.org/licenses/by/4.0/). Modified: textures
  resized, ambient occlusion baked in and brightened, rune glow mask derived; model rescaled in game.
- Offering cup model: [The Base Mesh](https://www.thebasemesh.com/) (CC0).
- Mod icon: "Menhir" by Delapouite from [game-icons.net](https://game-icons.net), licensed under
  [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/). Recoloured and outlined.
