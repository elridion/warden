# Generates the warden stone's embedded textures from the rune stone model's source maps in
# assets/textures/rune_stone/ (2048 x 2048, too big to embed as they are):
#   assets/textures/rune_stone_albedo.png  albedo with the ambient occlusion baked in, since the carved
#                                          runes read mostly through it and the copied vanilla
#                                          material's occlusion slot is kept neutral
#   assets/textures/rune_stone_n.png       tangent-space normals in the DXT5nm-style layout the banner
#                                          normal map uses (R = 1, G = Y, B = 1, A = X)
#   assets/textures/rune_stone_glow.png    emission mask of the front face's carved runes: grooves
#                                          are darker than their surroundings in the AO map
# Run from the repo root: powershell -File tools/make_rune_stone_textures.ps1
param(
    [string]$Source = "assets/textures/rune_stone",
    [string]$OutAlbedo = "assets/textures/rune_stone_albedo.png",
    [string]$OutNormal = "assets/textures/rune_stone_n.png",
    [string]$OutGlow = "assets/textures/rune_stone_glow.png",
    # Output edge length in pixels; the runes stay readable down to 512.
    [int]$Size = 512,
    # How strongly the occlusion darkens the albedo (0 = not at all, 1 = fully).
    [double]$Occlusion = 1.0,
    # Gain on the albedo. The source averages about 68/255, close to charcoal, which in game looks
    # black and barely shows firelight; 1.8 brings it near vanilla stone.
    [double]$Brightness = 1.8,
    # The source normals are OpenGL style (green = up), as Unity expects; use this if the carving
    # looks raised instead of cut in.
    [switch]$FlipY,
    # Rune glow mask. The seed is a UV point (v up) inside the front face's island; the island is
    # shrunk by GlowErode source pixels so its bevelled edges, as dark in the AO as the carving,
    # stay unlit. A groove is how much darker the AO is than its GlowRadius neighbourhood, times
    # GlowGain; anything below GlowThreshold is dropped as surface noise.
    [double]$GlowSeedU = 0.2,
    [double]$GlowSeedV = 0.5,
    [int]$GlowErode = 24,
    [int]$GlowRadius = 6,
    [double]$GlowGain = 6.0,
    [double]$GlowThreshold = 0.15,
    # Optional: also write a lit greyscale render of the normal map for review.
    [string]$Preview = ""
)

Add-Type -AssemblyName System.Drawing

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
public static class RuneStoneMaps
{
    // Box-filters a BGRA image down by an integer factor, per channel.
    static double[] Downsample(byte[] px, int w, int outSize, int channel)
    {
        int f = w / outSize;
        var dst = new double[outSize * outSize];
        for (int y = 0; y < outSize; y++)
            for (int x = 0; x < outSize; x++)
            {
                double sum = 0;
                for (int sy = y * f; sy < (y + 1) * f; sy++)
                    for (int sx = x * f; sx < (x + 1) * f; sx++) sum += px[(sy * w + sx) * 4 + channel];
                dst[y * outSize + x] = sum / (f * f * 255.0);
            }
        return dst;
    }

    public static byte[] Albedo(byte[] albedo, byte[] ao, int w, int outSize, double occlusion, double brightness)
    {
        var b = Downsample(albedo, w, outSize, 0);
        var g = Downsample(albedo, w, outSize, 1);
        var r = Downsample(albedo, w, outSize, 2);
        var o = Downsample(ao, w, outSize, 1);
        var px = new byte[outSize * outSize * 4];
        for (int i = 0; i < outSize * outSize; i++)
        {
            double k = (1 - occlusion * (1 - o[i])) * brightness;
            px[i * 4] = (byte)Math.Round(Math.Min(1, b[i] * k) * 255);
            px[i * 4 + 1] = (byte)Math.Round(Math.Min(1, g[i] * k) * 255);
            px[i * 4 + 2] = (byte)Math.Round(Math.Min(1, r[i] * k) * 255);
            px[i * 4 + 3] = 255;
        }
        return px;
    }

    // The normal map's magenta fill outside the UV islands. It is pure magenta give or take a few
    // levels; a looser match also catches real normals tilted about 45 degrees toward the lower right.
    static bool IsFill(byte[] px, int i)
    {
        return px[i * 4 + 2] >= 240 && px[i * 4 + 1] <= 4 && px[i * 4] >= 248;
    }

    // Separable box blur, clamped at the borders.
    static double[] Blur(double[] src, int w, int r)
    {
        var tmp = new double[w * w];
        var dst = new double[w * w];
        for (int y = 0; y < w; y++)
            for (int x = 0; x < w; x++)
            {
                double sum = 0;
                for (int k = -r; k <= r; k++) sum += src[y * w + Math.Max(0, Math.Min(w - 1, x + k))];
                tmp[y * w + x] = sum / (2 * r + 1);
            }
        for (int y = 0; y < w; y++)
            for (int x = 0; x < w; x++)
            {
                double sum = 0;
                for (int k = -r; k <= r; k++) sum += tmp[Math.Max(0, Math.Min(w - 1, y + k)) * w + x];
                dst[y * w + x] = sum / (2 * r + 1);
            }
        return dst;
    }

    // The UV island containing the seed pixel. All islands are eroded first (except along the
    // texture's outermost row and column, which the front face does not touch), so the face's bevelled
    // edges fall outside and the thin bridges where neighbouring islands touch the front face break;
    // then a flood fill from the seed keeps the front face alone.
    static bool[] Island(byte[] normal, int w, int seedX, int seedY, int erode)
    {
        var solid = new bool[w * w];
        for (int i = 0; i < w * w; i++) solid[i] = !IsFill(normal, i);
        for (int pass = 0; pass < erode; pass++)
        {
            var next = (bool[])solid.Clone();
            for (int y = 1; y < w - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    if (solid[i] && (!solid[i - 1] || !solid[i + 1] || !solid[i - w] || !solid[i + w])) next[i] = false;
                }
            solid = next;
        }

        int seed = seedY * w + seedX;
        if (!solid[seed]) throw new ArgumentException("The glow seed lies outside the eroded UV islands.");
        var inside = new bool[w * w];
        var stack = new Stack<int>();
        stack.Push(seed);
        inside[seed] = true;
        while (stack.Count > 0)
        {
            int i = stack.Pop(), x = i % w, y = i / w;
            foreach (int n in new[] { x > 0 ? i - 1 : -1, x < w - 1 ? i + 1 : -1, y > 0 ? i - w : -1, y < w - 1 ? i + w : -1 })
            {
                if (n < 0 || inside[n] || !solid[n]) continue;
                inside[n] = true;
                stack.Push(n);
            }
        }
        return inside;
    }

    public static bool[] FrontIsland(byte[] normal, int w, double seedU, double seedV, int erode)
    {
        return Island(normal, w, (int)(seedU * (w - 1)), (int)((1 - seedV) * (w - 1)), erode);
    }

    // White where the front face's runes are cut in, black elsewhere.
    public static byte[] Glow(byte[] ao, bool[] island, int w, int outSize, int radius, double gain, double threshold)
    {
        var v = new double[w * w];
        for (int i = 0; i < w * w; i++) v[i] = ao[i * 4 + 1] / 255.0;
        var low = Blur(v, w, radius);

        int f = w / outSize;
        var px = new byte[outSize * outSize * 4];
        for (int y = 0; y < outSize; y++)
            for (int x = 0; x < outSize; x++)
            {
                double m = 0;
                for (int sy = y * f; sy < (y + 1) * f; sy++)
                    for (int sx = x * f; sx < (x + 1) * f; sx++)
                    {
                        int i = sy * w + sx;
                        if (!island[i]) continue;
                        double groove = (low[i] - v[i]) * gain;
                        m += Math.Max(0, Math.Min(1, (groove - threshold) / (1 - threshold)));
                    }
                byte b = (byte)Math.Round(m / (f * f) * 255);
                int o = (y * outSize + x) * 4;
                px[o] = b; px[o + 1] = b; px[o + 2] = b; px[o + 3] = 255;
            }
        return px;
    }

    // Averages the decoded normal vectors rather than the bytes, and treats the magenta fill outside
    // the UV islands as flat, so it does not bleed tilted normals into the island edges.
    public static byte[] Normals(byte[] src, int w, int outSize, bool flipY)
    {
        int f = w / outSize;
        var px = new byte[outSize * outSize * 4];
        for (int y = 0; y < outSize; y++)
            for (int x = 0; x < outSize; x++)
            {
                double nx = 0, ny = 0, nz = 0;
                for (int sy = y * f; sy < (y + 1) * f; sy++)
                    for (int sx = x * f; sx < (x + 1) * f; sx++)
                    {
                        int i = (sy * w + sx) * 4;   // BGRA
                        byte b = src[i], g = src[i + 1], r = src[i + 2];
                        if (IsFill(src, sy * w + sx)) { nz += 1; continue; }
                        nx += r / 255.0 * 2 - 1;
                        ny += g / 255.0 * 2 - 1;
                        nz += b / 255.0 * 2 - 1;
                    }
                if (flipY) ny = -ny;
                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-6) { nx = 0; ny = 0; nz = 1; len = 1; }
                int o = (y * outSize + x) * 4;
                px[o] = 255;
                px[o + 1] = (byte)Math.Round((ny / len * 0.5 + 0.5) * 255);
                px[o + 2] = 255;
                px[o + 3] = (byte)Math.Round((nx / len * 0.5 + 0.5) * 255);
            }
        return px;
    }

    // Lambert shading of a normal map under a light from the upper left, for review.
    public static byte[] Shade(byte[] n, int w, int h)
    {
        double lx = -0.5, ly = 0.5, lz = 0.7, ll = Math.Sqrt(lx * lx + ly * ly + lz * lz);
        var px = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            double nx = n[i * 4 + 3] / 255.0 * 2 - 1, ny = n[i * 4 + 1] / 255.0 * 2 - 1;
            double nz = Math.Sqrt(Math.Max(0, 1 - nx * nx - ny * ny));
            byte v = (byte)Math.Round(Math.Max(0, (nx * lx + ny * ly + nz * lz) / ll) * 255);
            px[i * 4] = v; px[i * 4 + 1] = v; px[i * 4 + 2] = v; px[i * 4 + 3] = 255;
        }
        return px;
    }
}
'@

function Read-Pixels([string]$path) {
    $bmp = New-Object System.Drawing.Bitmap $path
    $rect = New-Object System.Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height
    $data = $bmp.LockBits($rect, 'ReadOnly', ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $px = New-Object byte[] ($bmp.Width * $bmp.Height * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $px, 0, $px.Length)
    $bmp.UnlockBits($data)
    $w = $bmp.Width
    $h = $bmp.Height
    $bmp.Dispose()
    if ($w -ne $h -or $w % $Size -ne 0) { throw "$path is $w x $h; expected a square a multiple of $Size wide." }
    return @{ Pixels = $px; Width = $w }
}

function Write-Pixels([byte[]]$px, [int]$w, [int]$h, [string]$path) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $data = $bmp.LockBits($rect, 'WriteOnly', ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    [System.Runtime.InteropServices.Marshal]::Copy($px, 0, $data.Scan0, $px.Length)
    $bmp.UnlockBits($data)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# The file names contain brackets, which PowerShell paths treat as wildcards.
$dir = (Resolve-Path $Source).Path
$albedo = Read-Pixels "$dir\pPlane2_[AlbedoM].tga.png"
$ao = Read-Pixels "$dir\pPlane2_[AO].tga.png"
$normal = Read-Pixels "$dir\pPlane2_[Normal].tga.png"
if ($ao.Width -ne $albedo.Width) { throw "The AO map and the albedo differ in size." }
if ($normal.Width -ne $ao.Width) { throw "The normal map and the AO map differ in size." }
# Find the front face before writing anything, so a bad seed leaves the existing textures intact.
$island = [RuneStoneMaps]::FrontIsland($normal.Pixels, $normal.Width, $GlowSeedU, $GlowSeedV, $GlowErode)

$albedoPath = Join-Path (Get-Location) $OutAlbedo
Write-Pixels ([RuneStoneMaps]::Albedo($albedo.Pixels, $ao.Pixels, $albedo.Width, $Size, $Occlusion, $Brightness)) $Size $Size $albedoPath
Write-Host "Wrote $albedoPath ($Size x $Size)"

$normals = [RuneStoneMaps]::Normals($normal.Pixels, $normal.Width, $Size, $FlipY.IsPresent)
$normalPath = Join-Path (Get-Location) $OutNormal
Write-Pixels $normals $Size $Size $normalPath
Write-Host "Wrote $normalPath ($Size x $Size)"

$glow = [RuneStoneMaps]::Glow($ao.Pixels, $island, $ao.Width, $Size, $GlowRadius, $GlowGain, $GlowThreshold)
$glowPath = Join-Path (Get-Location) $OutGlow
Write-Pixels $glow $Size $Size $glowPath
Write-Host "Wrote $glowPath ($Size x $Size)"

if ($Preview) {
    Write-Pixels ([RuneStoneMaps]::Shade($normals, $Size, $Size)) $Size $Size $Preview
    Write-Host "Wrote $Preview"
}
