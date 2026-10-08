# Changelog

## 0.3.0

- Warden stones can be upgraded like crafting stations. Build upgrade pieces within 5 m to raise
  the level and radius: Runed hide banner (Black Forest), Sacred idol (Swamp), Offering cup (Mountain),
  Tar lamp (Plains).
- Radius now scales with level: 40 m base, +15 m per upgrade (`BaseRadius`, `RadiusPerUpgrade`).
  Replaces the old `Radius` setting.
- Hulls of wards with different radii merge correctly.
- Hover text shows the ward's level and radius.
- Offering cup uses a chalice model from The Base Mesh (CC0).
- Runed hide banner has its own texture: torn parchment with silver-inked runes around an Algiz.
- All Warden pieces moved to the hammer's Base defense tab; upgrades show the build menu upgrade arrow.

## 0.2.0

- Animated amber haze sphere visualizes the protected area (`ShowHull`: Always / OnHover / Off):
  drifting light bands, swirling haze and floating motes.
- Overlapping wards merge into one common hull.
- Default radius raised from 32 m to 60 m.

## 0.1.0

- Initial release: Warden Stone, a buildable ward that blocks random creature spawns inside its radius.
- Server-synced config for radius and optional raid/event spawn blocking.
