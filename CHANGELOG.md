# Changelog

## 0.3.5

- Bug reports and feedback now go to the #warden channel on Discord: https://discord.gg/TsJGEmKj6V
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
