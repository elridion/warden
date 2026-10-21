# Changelog

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
