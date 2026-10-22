# Rune stone model

`Rune_stone_LowPoly.obj`: "Rune Stone Low Poly" (https://skfb.ly/6pxsp) by KaguKin is licensed under
[Creative Commons Attribution 4.0](http://creativecommons.org/licenses/by/4.0/). Attribution is
required, and so is stating that the work was modified; both are in the Credits section of the mod
README and the License section of the root README. Modifications: the textures are converted as
described below, and the model is rescaled and converted to Unity's coordinate space at runtime.

The OBJ is the unmodified Sketchfab download (centimeter units). It is embedded into `Warden.dll` at
build time as `Warden.rune_stone.obj` (see `Warden.csproj`), loaded by `src/ObjLoader.cs` and scaled
to the vanilla ward's height in `WardPieces.ReplaceWardModel`.

Its texture set from the same download is kept as source in `assets/textures/rune_stone/` (2048 x 2048).
`tools/make_rune_stone_textures.ps1` converts it into three embedded 512 x 512 textures in
`assets/textures/`:

- `rune_stone_albedo.png`: the albedo brightened 1.8x (the source is near charcoal in game), with the
  AO baked in.
- `rune_stone_n.png`: the normal map in the banner's layout.
- `rune_stone_glow.png`: the emission mask of the front face's carved runes, taken from the AO map and
  limited to the front face's UV island. `SpawnWard` lights it while the ward is active.

Roughness and metalness are not used: the metalness map is black and the material uses a single
glossiness value. The textures go on a copy of the vanilla ward
stone's material (Unity's Standard shader); Valheim's piece materials are no use as a base, since for
example the stone wall's first renderer is a snow overlay whose shader ignores the main texture.
