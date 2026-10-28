# Changelog

## 0.5.0

- Each warden stone now saves its radius, so other mods such as Weathering can respect warden
  stones in areas that are not loaded.
- Players on 0.4.x cannot play together with 0.5.0.

## 0.4.3

- Players on 0.4.x can still play together.

## 0.4.2

- The ward's hit sound plays again when something inside a warden stone is damaged. It now sounds
  where the damage happens instead of at the stone, which was out of earshot in larger wards.
  The vanilla ward's blue flash no longer plays: the amber haze brightening is the visual cue. Damage done by a player on 0.4.1 or older no longer flashes the stone for players on 0.4.2.
- Players on 0.4.x can still play together.

## 0.4.1

- The amber haze no longer covers the whole sphere. Within about 45 m of you it shows as a wall
  where the ward's sphere passes, so at a cliff edge, on a tower or in a valley you see where
  protection ends, without a bubble filling the view inside your base. Overlapping wards form one
  wall with a rounded joint where they meet. The wall ripples with slow waves, gently pulses and
  brightens when the stone flashes on damage. The motes still drift over the whole sphere, and haze
  and motes are a deeper amber.
- Twice as many ember motes drift over a ward's sphere.
- Players on 0.4.x can still play together.

## 0.4.0

- A warden stone now protects the sphere its amber haze shows, not a column of unlimited height. Near
  the edge of a large radius, ground far above or below the stone (a cliff top, a valley floor) is
  no longer protected; build the stone closer to the middle height of the area you want covered.
- An active warden stone now flashes like the vanilla ward when a building, rock or other object
  inside its radius takes damage, or when the stone itself is hit. Turn it off per client with the
  new `FlashOnDamage` setting.
- Items dropped inside an active warden stone's radius no longer despawn, as in a vanilla base near
  a workbench. The new server setting `KeepDroppedItems` turns this off.
- New server setting `PlayerBaseArea` (off by default): also marks an active warden stone's radius as a
  vanilla player base, for mods that only respect those. It also stops nests inside and counts
  toward raids; see the mod page.
- New server setting `AllowedCreatures`: a comma-separated list of creature prefab names (for example
  `Deer, Hare`) that still spawn inside an active warden stone's radius. Empty by default.
- Piece names, descriptions and messages are translated into every language Valheim offers except
  Abenaki, and follow the game's language setting.
- The warden stone, idol, offering cup and ritual candles now collide close to their visible shape
  instead of with one large box around each, so you no longer bump into or stand on empty air
  around them.
- Overlapping warden stones' hazes now meet in a clean seam along the circle where their spheres
  cross, instead of overlapping and fading out in a jagged band, most visible on large radii.
- Everyone on a server needs 0.4.x; players on 0.3.x cannot join.

## 0.3.5

- Bug reports and feedback now go to the #warden channel on Discord.
- Players on 0.3.x can still play together.

## 0.3.4

- New mod icon: an amber standing stone.

## 0.3.3

- Raid creatures now spawn outside an active warden stone's radius by default and have to walk in
  (`BlockEventSpawns` now defaults to on). Existing config files keep their current value; set it
  to `true` there to get the new behaviour.

## 0.3.2

- The warden stone has a new model: a standing rune stone ("Rune Stone Low Poly" by KaguKin,
  CC BY 4.0) in place of the vanilla ward's stone. Existing warden stones change look on their own.
- The runes carved into the stone's front glow amber while the ward is active and go dark when it
  is switched off.

## 0.3.1

First public release.

- Warden stone: a buildable ward that blocks random creature spawns inside its radius. Toggle it
  on and off by interacting; hover to see its level and radius.
- Upgrades raise the stone's level and radius, like crafting station extensions: Runed hide banner
  (Black Forest), Sacred idol (Swamp), Offering cup (Mountain) and Ritual candles (Plains).
  Ritual candles stack, up to `MaxStackedUpgrades` (4).
- Radius is 40 m at level 1 and grows by 20 m per upgrade, up to 180 m fully upgraded
  (`BaseRadius`, `RadiusPerUpgrade`).
- An animated amber haze sphere marks the protected area; overlapping wards merge into one hull
  (`ShowHull`: Always / OnHover / Off).
- Optional blocking of raid/event spawns (`BlockEventSpawns`, off by default).
- Gameplay settings are server-synced and admin-only.
