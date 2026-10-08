# Generates assets/textures/banner_runed_hide.png: an aged hide writ inked with Elder Futhark runes
# in tin-grey, with red-ochre accents as on painted runestones. A braided spine runs down the centre
# between two text panels, framed by knot bands; the foot is cut as a swallowtail with torn edges in
# the alpha channel.
# Run from the repo root: powershell -File tools/make_banner_texture.ps1
param(
    # Left half of the vanilla banner cloth (0.59 x 2.97 m); the game mirrors it onto the right half.
    [int]$Width = 340,
    [int]$Height = 1700,
    [int]$Seed = 11,
    [string]$Out = "assets/textures/banner_runed_hide.png",
    # Optional: also write the full banner (both halves) for reviewing the design.
    [string]$Preview = ""
)

Add-Type -AssemblyName System.Drawing
$rng = New-Object System.Random $Seed
function Rand([double]$a, [double]$b) { return $a + $rng.NextDouble() * ($b - $a) }
function Clamp([double]$v) { return [int][Math]::Max(0, [Math]::Min(255, $v)) }

# Per-pixel passes are too slow in script; do them in C#.
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
public static class BannerPixels
{
    static double Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 144269504;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffffff) / (double)0xffffff;
        }
    }

    static double Noise(double x, double y, int seed)
    {
        int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
        double fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        double a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed);
        double c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    static double Fbm(double x, double y, int seed, int octaves)
    {
        double sum = 0, amp = 0.5, norm = 0;
        for (int o = 0; o < octaves; o++) { sum += amp * Noise(x, y, seed + o * 31); norm += amp; x *= 2.03; y *= 2.03; amp *= 0.5; }
        return sum / norm;
    }

    // Mottles the hide, scorches the rim and cuts the torn outline into the alpha channel. The outline
    // is the left/top/bottom tear walks plus a swallowtail notch rising toward the centre line.
    public static void Age(byte[] px, int width, int height, int stride, double[] left, double[] top, double[] bottom,
        double notchStart, double notchDepth, int seed)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * stride + x * 4;
                double t = Math.Max(0, (x - notchStart) / (width - notchStart));
                double foot = height - 1 - bottom[x] - notchDepth * t;
                double e = Math.Min(x - left[y], Math.Min(y - top[x], foot - y));
                if (e < 0)
                {
                    // Torn away: transparent, but keep a dark burnt colour in case the shader ignores alpha.
                    px[i] = 30; px[i + 1] = 45; px[i + 2] = 60; px[i + 3] = 0;
                    continue;
                }
                // Mirror-safe noise: sample on distance from the centre line so both halves match at the seam.
                double mx = width - x;
                double mottle = Fbm(mx / 90.0, y / 140.0, seed, 4) - 0.5;
                double grain = Fbm(mx / 5.0, y / 2.2, seed + 7, 2) - 0.5;
                double burn = Math.Max(0, 1 - e / 16);
                double nx = 1 - x / (double)width, ny = (y / (double)height - 0.5) * 2;
                double vignette = 1 - 0.25 * (nx * nx * 0.6 + ny * ny * 0.4);
                double k = vignette * (1 + mottle * 0.32 + grain * 0.12) * (1 - 0.78 * burn * burn);
                for (int c = 0; c < 3; c++) px[i + c] = (byte)Math.Max(0, Math.Min(255, px[i + c] * k));
                px[i + 3] = 255;
            }
        }
    }
}
'@

$bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$cx = $Width   # the banner's centre line is this texture's right edge

# --- Hide ------------------------------------------------------------------------------------
$g.Clear([System.Drawing.Color]::FromArgb(255, 178, 150, 106))

# Soft stains and lighter patches; the noise pass adds the fine mottling.
for ($i = 0; $i -lt 120; $i++) {
    $r = Rand 30 140
    $dark = $rng.NextDouble() -lt 0.65
    $c = if ($dark) { [System.Drawing.Color]::FromArgb((Clamp (Rand 5 12)), 110, 76, 36) } else { [System.Drawing.Color]::FromArgb((Clamp (Rand 5 11)), 240, 226, 190) }
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $c), (Rand (-$r) $Width), (Rand (-$r) $Height), $r * (Rand 1 2), $r * (Rand 0.6 1.4))
}
# A few water rings.
for ($i = 0; $i -lt 4; $i++) {
    $r = Rand 30 80; $x = Rand 0 $Width; $y = Rand 0 $Height
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(22, 110, 75, 35)), (Rand 2 4)
    $g.DrawEllipse($pen, $x - $r, $y - $r, $r * 2, $r * (Rand 1.6 2.2))
}
# Fold creases where the writ was once rolled: a dark line with a light lip below it.
foreach ($fy in @(($Height * 0.27), ($Height * 0.52), ($Height * 0.77))) {
    $y = $fy + (Rand -20 20)
    $pts = for ($x = 0; $x -le $Width; $x += 20) { New-Object System.Drawing.PointF $x, ($y + (Rand -2 2)) }
    $g.DrawCurve((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40, 80, 55, 25)), 2), [System.Drawing.PointF[]]$pts)
    $lip = $pts | ForEach-Object { New-Object System.Drawing.PointF $_.X, ($_.Y + 2.5) }
    $g.DrawCurve((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(30, 250, 238, 210)), 2), [System.Drawing.PointF[]]$lip)
}

# --- Inks ------------------------------------------------------------------------------------
# Tin ink: a soft dark bleed under a cool grey stroke with a thin highlight, so it reads as metal.
# Ochre: the red paint runestones were coloured with, flat and matte.
function Ink-Pens([float]$w, [string]$ink = "tin", [double]$fade = 1) {
    if ($ink -eq "ochre") {
        $layers = @(@(90, 70, 25, 15, ($w + 1.6)), @(235, 156, 54, 36, $w), @(70, 200, 110, 80, ([Math]::Max(1, $w * 0.3))))
    } else {
        $layers = @(@(95, 50, 42, 36, ($w + 1.6)), @(240, 112, 118, 126, $w), @(130, 196, 202, 208, ([Math]::Max(1, $w * 0.3))))
    }
    foreach ($l in $layers) {
        $p = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb((Clamp ($l[0] * $fade)), $l[1], $l[2], $l[3])), $l[4]
        $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round'
        $p
    }
}

# Draws a list of point arrays with each ink layer in turn, so later strokes don't cover outlines.
function Draw-Paths($paths, $pens, [bool]$closed = $false) {
    foreach ($pen in $pens) {
        foreach ($p in $paths) {
            if ($closed) { $g.DrawClosedCurve($pen, [System.Drawing.PointF[]]$p) } else { $g.DrawLines($pen, [System.Drawing.PointF[]]$p) }
        }
    }
}

function Pt([double]$x, [double]$y) { New-Object System.Drawing.PointF ([float]$x), ([float]$y) }

# --- Runes -----------------------------------------------------------------------------------
# Elder Futhark as strokes in a unit box (x right, y down).
$runes = @(
    @(@(0.3,1,0.3,0), @(0.3,0.35,0.8,0.05), @(0.3,0.6,0.8,0.3)),                       # fehu
    @(@(0.25,1,0.25,0), @(0.25,0,0.75,0.3), @(0.75,0.3,0.75,1)),                       # uruz
    @(@(0.3,1,0.3,0), @(0.3,0.25,0.72,0.5), @(0.72,0.5,0.3,0.75)),                     # thurisaz
    @(@(0.3,1,0.3,0), @(0.3,0,0.75,0.3), @(0.3,0.3,0.75,0.6)),                         # ansuz
    @(@(0.3,1,0.3,0), @(0.3,0,0.72,0.25), @(0.72,0.25,0.3,0.5), @(0.3,0.5,0.75,1)),   # raidho
    @(@(0.7,0.2,0.3,0.5), @(0.3,0.5,0.7,0.8)),                                         # kaunan
    @(@(0.15,0.05,0.85,0.95), @(0.85,0.05,0.15,0.95)),                                 # gebo
    @(@(0.3,1,0.3,0), @(0.3,0,0.72,0.22), @(0.72,0.22,0.3,0.45)),                      # wunjo
    @(@(0.25,1,0.25,0), @(0.75,1,0.75,0), @(0.25,0.35,0.75,0.6)),                      # hagalaz
    @(@(0.5,1,0.5,0), @(0.25,0.35,0.75,0.6)),                                          # naudiz
    @(,@(0.5,1,0.5,0)),                                                                # isa (leading comma keeps the one stroke nested)
    @(@(0.5,0.05,0.2,0.3), @(0.2,0.3,0.5,0.55), @(0.5,0.45,0.8,0.7), @(0.8,0.7,0.5,0.95)), # jera
    @(@(0.5,1,0.5,0), @(0.5,0,0.8,0.2), @(0.5,1,0.2,0.8)),                             # eihwaz
    @(@(0.3,1,0.3,0), @(0.3,0,0.7,0.25), @(0.3,1,0.7,0.75)),                           # perthro
    @(@(0.5,1,0.5,0), @(0.5,0.45,0.12,0.05), @(0.5,0.45,0.88,0.05)),                   # algiz
    @(@(0.75,0,0.25,0.4), @(0.25,0.4,0.75,0.6), @(0.75,0.6,0.25,1)),                   # sowilo
    @(@(0.5,1,0.5,0), @(0.5,0,0.15,0.35), @(0.5,0,0.85,0.35)),                         # tiwaz
    @(@(0.3,1,0.3,0), @(0.3,0,0.72,0.25), @(0.72,0.25,0.3,0.5), @(0.3,0.5,0.72,0.75), @(0.72,0.75,0.3,1)), # berkanan
    @(@(0.2,1,0.2,0), @(0.8,1,0.8,0), @(0.2,0,0.5,0.35), @(0.5,0.35,0.8,0)),          # ehwaz
    @(@(0.2,1,0.2,0), @(0.8,1,0.8,0), @(0.2,0,0.8,0.45), @(0.8,0,0.2,0.45)),          # mannaz
    @(@(0.35,1,0.35,0), @(0.35,0,0.75,0.3)),                                           # laguz
    @(@(0.5,0,0.85,0.5), @(0.85,0.5,0.5,1), @(0.5,1,0.15,0.5), @(0.15,0.5,0.5,0)),    # ingwaz
    @(@(0.15,1,0.15,0), @(0.85,1,0.85,0), @(0.15,0,0.85,1), @(0.85,0,0.15,1)),        # dagaz
    @(@(0.5,0,0.85,0.35), @(0.85,0.35,0.5,0.7), @(0.5,0.7,0.15,0.35), @(0.15,0.35,0.5,0), @(0.5,0.7,0.85,1), @(0.5,0.7,0.15,1)) # othala
)

# Hand-drawn feel: jitter every endpoint and lean the whole glyph a little.
function Draw-Rune([int]$index, [float]$x, [float]$y, [float]$w, [float]$h, $pens, [float]$jitter) {
    $lean = Rand -0.07 0.07
    $paths = @(foreach ($l in $runes[$index]) {
        ,@(
            (Pt ($x + ($l[0] + (Rand -$jitter $jitter) + $lean * (1 - $l[1])) * $w) ($y + ($l[1] + (Rand -$jitter $jitter)) * $h)),
            (Pt ($x + ($l[2] + (Rand -$jitter $jitter) + $lean * (1 - $l[3])) * $w) ($y + ($l[3] + (Rand -$jitter $jitter)) * $h))
        )
    })
    Draw-Paths $paths $pens
}

# --- Layout ----------------------------------------------------------------------------------
$border = 30          # outer frame line, inside the torn edge
$spine = 22           # half-width of the braided spine on the centre line
$textLeft = $border + 16
$textRight = $cx - $spine - 10

# Two strands of a cosine wave; each mirrors onto the other across the centre line, so the spine
# reads as a braid on the finished banner.
function Spine([double]$y0, [double]$y1) {
    $strand = for ($y = $y0; $y -le $y1; $y += 3) { Pt ($cx - $spine * 0.5 + $spine * 0.42 * [Math]::Sin(($y - $y0) / 22)) $y }
    Draw-Paths @(,$strand) (Ink-Pens 3.2)
}

# A horizontal two-strand cable from the frame to the centre line, symmetric about it.
function Knot-Band([double]$y) {
    $period = 36
    $a = for ($x = $border; $x -le $cx; $x += 2) { Pt $x ($y + 7 * [Math]::Cos(($cx - $x) * 2 * [Math]::PI / $period)) }
    $b = for ($x = $border; $x -le $cx; $x += 2) { Pt $x ($y - 7 * [Math]::Cos(($cx - $x) * 2 * [Math]::PI / $period)) }
    Draw-Paths @($a, $b) (Ink-Pens 3 "ochre")
    Draw-Paths @(,@((Pt $border ($y - 13)), (Pt $cx ($y - 13)))) (Ink-Pens 1.6)
    Draw-Paths @(,@((Pt $border ($y + 13)), (Pt $cx ($y + 13)))) (Ink-Pens 1.6)
}

# A ring centred on the centre line; the mirror completes it.
function Medallion([double]$cy, [double]$r) {
    $outer = for ($t = -90; $t -le 90; $t += 4) { Pt ($cx - $r * [Math]::Cos($t * [Math]::PI / 180)) ($cy + $r * [Math]::Sin($t * [Math]::PI / 180)) }
    $inner = for ($t = -90; $t -le 90; $t += 4) { Pt ($cx - ($r - 8) * [Math]::Cos($t * [Math]::PI / 180)) ($cy + ($r - 8) * [Math]::Sin($t * [Math]::PI / 180)) }
    Draw-Paths @($outer, $inner) (Ink-Pens 2.6)
    # Dots between the rings, like rivets on a shield boss.
    $dot = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(220, 156, 54, 36))
    for ($t = -75; $t -le 75; $t += 30) {
        $g.FillEllipse($dot, [float]($cx - ($r - 4) * [Math]::Cos($t * [Math]::PI / 180) - 2.5), [float]($cy + ($r - 4) * [Math]::Sin($t * [Math]::PI / 180) - 2.5), 5, 5)
    }
}

# Mirror-symmetric runes read whole across the seam; others are used sparingly.
$symmetricRunes = @(6, 8, 10, 14, 16, 18, 19, 21, 22, 23)
function Pick-Rune { if ($rng.NextDouble() -lt 0.7) { return $symmetricRunes[$rng.Next(0, $symmetricRunes.Count)] } return $rng.Next(0, $runes.Count) }

# Runestone-style text: words of runes split by double dots, ink fading word by word.
function Text-Block([double]$top, [double]$bottom) {
    $glyphH = 26; $lineHeight = 42
    $y = $top
    while ($y + $glyphH -le $bottom) {
        $x = $textLeft + (Rand 0 6)
        while ($true) {
            # Shorten the last word of a line to fill it out to the spine.
            $room = [Math]::Floor(($textRight - $x) / 21)
            if ($room -lt 1) { break }
            $wordLen = [Math]::Min($rng.Next(2, 6), $room)
            $widths = @(for ($k = 0; $k -lt $wordLen; $k++) { Rand 13 16 })
            $pens = Ink-Pens 2.4 "tin" (Rand 0.7 1)
            foreach ($w in $widths) {
                Draw-Rune (Pick-Rune) $x ($y + (Rand -1.2 1.2)) $w $glyphH $pens 0.04
                $x += $w + 4.5
            }
            # Divider only between words, not at the end of a line.
            if (($textRight - $x - 12) / 21 -lt 1) { break }
            $dot = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(210, 120, 124, 130))
            $g.FillEllipse($dot, $x + 1, $y + $glyphH * 0.28, 4, 4)
            $g.FillEllipse($dot, $x + 1, $y + $glyphH * 0.62, 4, 4)
            $x += 12
        }
        $y += $lineHeight + (Rand -1.5 1.5)
    }
}

# Frame line along the outer edge, notched with small ticks like a carved border. It is capped at the
# top and runs down into the lower knot band, whose rules close it at the bottom.
$frameTop = 40; $frameBottom = 1421
Draw-Paths @(,@((Pt $cx $frameTop), (Pt $border $frameTop), (Pt $border $frameBottom))) (Ink-Pens 2.2)
Draw-Paths @(,@((Pt $cx ($frameTop + 7)), (Pt ($border + 7) ($frameTop + 7)), (Pt ($border + 7) $frameBottom))) (Ink-Pens 1.4)
for ($y = $frameTop + 10; $y -lt $frameBottom; $y += 24) {
    Draw-Paths @(,@((Pt ($border - 1) $y), (Pt ($border + 8) ($y + 7)))) (Ink-Pens 1.2 "ochre")
}

# Header: algiz, the rune of protection, inside a medallion on the centre line.
$headCy = 150
Medallion $headCy 92
Draw-Rune 14 ($cx - 52) ($headCy - 58) 104 116 (Ink-Pens 8 "ochre") 0
Knot-Band 270

# Upper text panel, centre medallion with dagaz, lower text panel.
$midCy = 840
Spine 290 ($midCy - 60)
Text-Block 304 ($midCy - 70)
Medallion $midCy 56
Draw-Rune 22 ($cx - 30) ($midCy - 26) 60 52 (Ink-Pens 5 "ochre") 0
Spine ($midCy + 60) 1380
Text-Block ($midCy + 74) 1390
Knot-Band 1408

# Borromean rings at the foot, a knot found on Norse carvings, centred on the centre line.
$ringCy = 1478; $ringR = 30; $spread = 20
$centres = foreach ($dir in @(270, 30, 150)) { ,@(($cx + [Math]::Cos($dir * [Math]::PI / 180) * $spread), ($ringCy + [Math]::Sin($dir * [Math]::PI / 180) * $spread)) }
function Arc($c, [double]$from, [double]$to) {
    ,@(for ($t = $from; $t -le $to; $t += 3) { Pt ($c[0] + $ringR * [Math]::Cos($t * [Math]::PI / 180)) ($c[1] + $ringR * [Math]::Sin($t * [Math]::PI / 180)) })
}
$ringPens = Ink-Pens 5
Draw-Paths @(foreach ($c in $centres) { Arc $c 0 360 }) $ringPens

# Weave the top ring with the left one: over at their upper crossing, under at the lower one. The
# mirror weaves the right ring the same way; the left/right crossings sit on the centre line.
# Each over-pass is redrawn on a band of bare hide that cuts the strand beneath.
$top = $centres[0]; $left = $centres[2]
$dx = $left[0] - $top[0]; $dy = $left[1] - $top[1]; $d = [Math]::Sqrt($dx * $dx + $dy * $dy)
$mx = $top[0] + $dx / 2; $my = $top[1] + $dy / 2; $h = [Math]::Sqrt($ringR * $ringR - $d * $d / 4)
$crossings = @(@(($mx - $dy * $h / $d), ($my + $dx * $h / $d)), @(($mx + $dy * $h / $d), ($my - $dx * $h / $d))) | Sort-Object { $_[1] }
$gap = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 178, 150, 106)), 11
$gap.StartCap = 'Flat'; $gap.EndCap = 'Flat'
foreach ($pass in @(@($top, $crossings[0]), @($left, $crossings[1]))) {
    $c = $pass[0]; $p = $pass[1]
    $a = [Math]::Atan2($p[1] - $c[1], $p[0] - $c[0]) * 180 / [Math]::PI
    $g.DrawLines($gap, [System.Drawing.PointF[]](Arc $c ($a - 12) ($a + 12)))
    Draw-Paths @(Arc $c ($a - 18) ($a + 18)) $ringPens
}

$g.Dispose()

# --- Aging and torn edges --------------------------------------------------------------------
# Jagged outline from random walks: the outer (left) edge, top and a swallowtail foot whose notch
# rises toward the centre line. The right edge is the centre line, so it stays intact.
function Walk([int]$n, [double]$base, [double]$step, [double]$min, [double]$max) {
    $v = New-Object double[] $n; $cur = $base
    for ($i = 0; $i -lt $n; $i++) {
        $cur = [Math]::Max($min, [Math]::Min($max, $cur + (Rand (-$step) $step)))
        if ($rng.NextDouble() -lt 0.02) { $cur = [Math]::Min($max, $cur + (Rand 4 12)) }   # occasional bite
        $v[$i] = $cur
    }
    # A raw walk changes every pixel and reads as a comb of hairs; blur it into ragged bays and
    # tongues, then add back a little fine roughness.
    for ($pass = 0; $pass -lt 2; $pass++) {
        $s = New-Object double[] $n
        for ($i = 0; $i -lt $n; $i++) {
            $sum = 0; $cnt = 0
            for ($j = [Math]::Max(0, $i - 4); $j -le [Math]::Min($n - 1, $i + 4); $j++) { $sum += $v[$j]; $cnt++ }
            $s[$i] = $sum / $cnt
        }
        $v = $s
    }
    for ($i = 0; $i -lt $n; $i++) { $v[$i] += Rand -0.6 0.6 }
    return $v
}
$leftTear = Walk $Height 8 2.2 2 22
$topTear = Walk $Width 6 1.8 1 14
$bottomTear = Walk $Width 10 2.4 2 30

$rect = New-Object System.Drawing.Rectangle 0, 0, $Width, $Height
$data = $bmp.LockBits($rect, 'ReadWrite', [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes = New-Object byte[] ($data.Stride * $Height)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
[BannerPixels]::Age($bytes, $Width, $Height, $data.Stride, $leftTear, $topTear, $bottomTear, ($Width * 0.3), 120, $Seed)
[System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
$bmp.UnlockBits($data)

$dir = Split-Path $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
$bmp.Save((Join-Path (Get-Location) $Out), [System.Drawing.Imaging.ImageFormat]::Png)
"Wrote $Out"

if ($Preview) {
    $full = New-Object System.Drawing.Bitmap ($Width * 2), $Height
    $pg = [System.Drawing.Graphics]::FromImage($full)
    $pg.Clear([System.Drawing.Color]::FromArgb(255, 40, 44, 40))
    $pg.DrawImage($bmp, 0, 0, $Width, $Height)
    $bmp.RotateFlip('RotateNoneFlipX')
    $pg.DrawImage($bmp, $Width, 0, $Width, $Height)
    $pg.Dispose()
    $full.Save($Preview, [System.Drawing.Imaging.ImageFormat]::Png)
    $full.Dispose()
    "Wrote $Preview"
}
$bmp.Dispose()
