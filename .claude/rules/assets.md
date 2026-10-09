---
paths:
  - "assets/**"
  - "tools/**"
  - "Warden.csproj"
---

# Assets and generators

## Embedded resources

- Everything the mod loads from disk is embedded in `Warden.dll`; the shipped package contains only
  the DLL, icon, README, CHANGELOG and manifest. Do not add loose files the plugin reads at runtime.
- Each resource is an `<EmbeddedResource>` in `Warden.csproj` with an explicit
  `LogicalName="Warden.<name>"` (not always the file name: `greek_cup.obj` is
  `Warden.offering_cup.obj`), and code loads it by that exact name
  (`WardPieces.BannerTextureResource`, `OfferingCupModel.CupModelResource`). A mismatch only shows up
  in-game as a "not found" warning, so change both sides together.
- Load textures with `WardPieces.LoadEmbeddedTexture(name, linear)` (private; make it `internal` if
  another class needs it): `linear: true` for normal maps and other data textures, `false` for colour. Meshes go through `ObjLoader.LoadEmbedded`, which
  mirrors X to convert OBJ's right-handed space and reverses winding to match.
- Keep embedded files reasonably small; they inflate the DLL every player downloads.

## Third-party assets

- Only use assets whose license allows redistribution in a mod (CC0 preferred). Record the source
  and license in a `README.md` next to the asset (see `assets/models/offering_cup/README.md`) and in
  the Credits section of the root `README.md`.
- Keep the original source file (e.g. the FBX) beside the converted one that is embedded.

## Banner textures (`tools/make_banner_*.ps1`)

- `assets/textures/banner_runed_hide.png` and `banner_runed_hide_n.png` are generated. Never edit the
  PNGs by hand; change the script and regenerate.
- Run from the repo root in Windows PowerShell (they use `System.Drawing` and compile inline C# via
  `Add-Type`), in this order, because the normal map is derived from the albedo:
  ```
  powershell -File tools/make_banner_texture.ps1
  powershell -File tools/make_banner_normal.ps1
  ```
  Both take a `-Preview <path>` to write a review image; put previews in a scratch location, not in `assets/`.
- `make_banner_texture.ps1` is deterministic for a given `-Seed` (default 11). Changing the seed or
  the order of random draws reshuffles the whole design, so keep both stable unless a redesign is
  intended. `make_banner_normal.ps1` has no randomness; it is derived purely from the albedo.
- The texture is the **left half** of the vanilla banner cloth (default 340 x 1700 for 0.59 x 2.97 m);
  the game mirrors it onto the right half. The image's right edge is the banner's centre line: shapes
  meant to read whole are centred on it, and noise is sampled on distance from it so the seam matches.
- The top of the cloth is hidden behind the banner's crossbar in-game; keep important design elements
  below the frame top (`$frameTop`, currently y 130).
- Torn edges are cut into the alpha channel; the banner material alpha-clips them at 0.5 when the
  shader has a `_Cutoff` property.
- The normal map uses the DXT5nm-style layout (R = 1, G = Y, B = 1, A = X), matching
  `WardPieces.s_flatNormal`, is written at half the albedo's resolution (`-Scale 0.5`, 170 x 850),
  and is loaded as linear.
- Commit the regenerated PNGs together with the script change that produced them.
