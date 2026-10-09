# Changelog

## 0.3.0

- Warden stones can be upgraded like crafting stations. Build upgrade pieces within 5 m to raise
  the level and radius: Runed hide banner (Black Forest), Sacred idol (Swamp), Offering cup (Mountain),
  Ritual candles (Plains).
- Radius now scales with level: 40 m base, +20 m per upgrade (180 m fully upgraded) (`BaseRadius`, `RadiusPerUpgrade`).
  Replaces the old `Radius` setting.
- Hulls of wards with different radii merge correctly.
- Ward hulls are much cheaper to draw: several wards no longer cost a noticeable amount of frame rate.
- The placement preview's hull no longer stays behind after switching to remove mode or removing a ward.
- Hover text shows the ward's level and radius.
- Offering cup uses a chalice model from The Base Mesh (CC0).
- Ritual candles: a cluster of always-lit resin candles of different heights. Unlike the other
  upgrades they stack: each cluster in range counts, up to `MaxStackedUpgrades` (4).
- Upgrades need no workbench, only a warden stone within range, and say so when placed too far.
- Runed hide banner has its own texture: a torn, swallowtailed hide writ with tin-inked runes in two
  panels around a braided spine, red-ochre knot bands, and an Algiz medallion at the head.
  A normal map presses the runes into the hide and rounds its torn edge.
- Runed hide banner has its own build menu icon showing its design, instead of the vanilla banner's.
- All Warden pieces are listed only under the hammer's Defense tab, next to the vanilla ward; upgrades show the build menu upgrade arrow.
- Warden stone recolored: grey stone, so it reads apart from the vanilla ward. Its runes, light and
  sparks glow like the vanilla ward's instead of glaring.
- Ritual candles burn again: their flames and light were switched off.

## 0.2.0

- Animated amber haze sphere visualizes the protected area (`ShowHull`: Always / OnHover / Off):
  drifting light bands, swirling haze and floating motes.
- Overlapping wards merge into one common hull.
- Default radius raised from 32 m to 60 m.

## 0.1.0

- Initial release: Warden Stone, a buildable ward that blocks random creature spawns inside its radius.
- Server-synced config for radius and optional raid/event spawn blocking.
