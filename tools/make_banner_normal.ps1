# Generates assets/textures/banner_runed_hide_n.png, the banner's normal map, from its albedo so the
# relief always lines up with what is painted: hide grain and stains from the brightness, inked
# strokes pressed into the hide, and a rounded rim along the torn edge in the alpha channel.
# Run from the repo root after make_banner_texture.ps1: powershell -File tools/make_banner_normal.ps1
param(
    [string]$In = "assets/textures/banner_runed_hide.png",
    [string]$Out = "assets/textures/banner_runed_hide_n.png",
    # Output size as a fraction of the albedo; the relief is soft, so half resolution is plenty.
    [double]$Scale = 0.5,
    # Height per unit of brightness detail (0..255) and of the rim, in albedo pixels.
    [double]$Detail = 0.045,
    [double]$Rim = 3.0,
    [double]$Strength = 1.0,
    # Optional: also write a lit greyscale render of the relief for review.
    [string]$Preview = ""
)

Add-Type -AssemblyName System.Drawing

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
public static class BannerRelief
{
    // Separable box blur, clamped at the borders.
    static double[] Blur(double[] src, int w, int h, int r)
    {
        var tmp = new double[w * h];
        var dst = new double[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double sum = 0;
                for (int k = -r; k <= r; k++) sum += src[y * w + Math.Max(0, Math.Min(w - 1, x + k))];
                tmp[y * w + x] = sum / (2 * r + 1);
            }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double sum = 0;
                for (int k = -r; k <= r; k++) sum += tmp[Math.Max(0, Math.Min(h - 1, y + k)) * w + x];
                dst[y * w + x] = sum / (2 * r + 1);
            }
        return dst;
    }

    // px is BGRA. Returns a height field at the albedo's size.
    public static double[] Height(byte[] px, int w, int h, double detail, double rim)
    {
        var lum = new double[w * h];
        var mask = new double[w * h];
        for (int i = 0; i < w * h; i++)
        {
            mask[i] = px[i * 4 + 3] / 255.0;
            lum[i] = 0.114 * px[i * 4] + 0.587 * px[i * 4 + 1] + 0.299 * px[i * 4 + 2];
        }

        // Fill the torn-away area with the average hide brightness so the high-pass does not ring
        // along the edge.
        double sum = 0, n = 0;
        for (int i = 0; i < w * h; i++) { sum += lum[i] * mask[i]; n += mask[i]; }
        double mean = n > 0 ? sum / n : 128;
        for (int i = 0; i < w * h; i++) lum[i] = lum[i] * mask[i] + mean * (1 - mask[i]);

        var low = Blur(lum, w, h, 10);
        var edge = Blur(Blur(mask, w, h, 4), w, h, 4);
        var height = new double[w * h];
        for (int i = 0; i < w * h; i++)
        {
            double e = Math.Min(1, edge[i] * 2);
            height[i] = (lum[i] - low[i]) * detail * mask[i] + e * e * (3 - 2 * e) * rim;
        }
        return height;
    }

    // Box-filters the height field down to the output size and encodes tangent-space normals as
    // DXT5nm-style RGBA (R = 1, G = Y, B = 1, A = X), which both Unity unpack paths read correctly.
    public static byte[] Normals(double[] h, int width, int height, int outW, int outH, double strength)
    {
        var s = new double[outW * outH];
        double fx = width / (double)outW, fy = height / (double)outH;
        for (int y = 0; y < outH; y++)
            for (int x = 0; x < outW; x++)
            {
                double sum = 0; int n = 0;
                for (int sy = (int)(y * fy); sy < (int)((y + 1) * fy); sy++)
                    for (int sx = (int)(x * fx); sx < (int)((x + 1) * fx); sx++) { sum += h[sy * width + sx]; n++; }
                s[y * outW + x] = n > 0 ? sum / n : 0;
            }

        // Heights are in albedo pixels and one output pixel spans fx of them.
        var px = new byte[outW * outH * 4];
        for (int y = 0; y < outH; y++)
            for (int x = 0; x < outW; x++)
            {
                // The right edge is the centre line; the mirrored half continues it.
                double l = s[y * outW + Math.Max(0, x - 1)], r = s[y * outW + Math.Min(outW - 1, x + 1)];
                double u = s[Math.Max(0, y - 1) * outW + x], d = s[Math.Min(outH - 1, y + 1) * outW + x];
                // Image rows run down but texture v runs up.
                double dx = (r - l) * 0.5 * strength / fx, dy = (u - d) * 0.5 * strength / fx;
                double len = Math.Sqrt(dx * dx + dy * dy + 1);
                int i = (y * outW + x) * 4;   // BGRA
                px[i] = 255;
                px[i + 1] = (byte)Math.Round((-dy / len * 0.5 + 0.5) * 255);
                px[i + 2] = 255;
                px[i + 3] = (byte)Math.Round((-dx / len * 0.5 + 0.5) * 255);
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

function Read-Pixels([System.Drawing.Bitmap]$bmp) {
    $rect = New-Object System.Drawing.Rectangle 0, 0, $bmp.Width, $bmp.Height
    $data = $bmp.LockBits($rect, 'ReadOnly', ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
    $px = New-Object byte[] ($bmp.Width * $bmp.Height * 4)
    [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $px, 0, $px.Length)
    $bmp.UnlockBits($data)
    return ,$px
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

$albedo = New-Object System.Drawing.Bitmap (Resolve-Path $In).Path
$srcW = $albedo.Width
$srcH = $albedo.Height
$px = Read-Pixels $albedo
$albedo.Dispose()

$outW = [int][Math]::Round($srcW * $Scale)
$outH = [int][Math]::Round($srcH * $Scale)
$heights = [BannerRelief]::Height($px, $srcW, $srcH, $Detail, $Rim)
$normals = [BannerRelief]::Normals($heights, $srcW, $srcH, $outW, $outH, $Strength)

$outPath = Join-Path (Get-Location) $Out
Write-Pixels $normals $outW $outH $outPath
Write-Host "Wrote $outPath ($outW x $outH)"

if ($Preview) {
    Write-Pixels ([BannerRelief]::Shade($normals, $outW, $outH)) $outW $outH $Preview
    Write-Host "Wrote $Preview"
}
