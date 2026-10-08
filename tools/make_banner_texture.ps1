# Generates assets/textures/banner_runed_hide.png: an aged parchment writ covered in silver-inked
# Elder Futhark runes, with torn edges in the alpha channel and interlocked rings at the foot.
# Run from the repo root: powershell -File tools/make_banner_texture.ps1
param(
    # Left half of the vanilla banner cloth (0.59 x 2.97 m); the game mirrors it onto the right half.
    [int]$Width = 340,
    [int]$Height = 1700,
    [int]$Seed = 11,
    [string]$Out = "assets/textures/banner_runed_hide.png"
)

Add-Type -AssemblyName System.Drawing
$rng = New-Object System.Random $Seed
function Rand([double]$a, [double]$b) { return $a + $rng.NextDouble() * ($b - $a) }
function Clamp([double]$v) { return [int][Math]::Max(0, [Math]::Min(255, $v)) }

$bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'

# --- Parchment -------------------------------------------------------------------------------
$g.Clear([System.Drawing.Color]::FromArgb(255, 172, 146, 104))

# Large soft stains and lighter patches.
for ($i = 0; $i -lt 260; $i++) {
    $r = Rand 20 120
    $dark = $rng.NextDouble() -lt 0.7
    $c = if ($dark) { [System.Drawing.Color]::FromArgb((Clamp (Rand 6 16)), 120, 84, 40) } else { [System.Drawing.Color]::FromArgb((Clamp (Rand 6 14)), 245, 232, 200) }
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $c), (Rand (-$r) $Width), (Rand (-$r) $Height), $r * (Rand 1 2), $r * (Rand 0.6 1.4))
}
# Water rings.
for ($i = 0; $i -lt 6; $i++) {
    $r = Rand 30 90; $x = Rand 0 $Width; $y = Rand 0 $Height
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(28, 110, 75, 35)), (Rand 2 5)
    $g.DrawEllipse($pen, $x - $r, $y - $r, $r * 2, $r * (Rand 1.6 2.2))
}
# Fibres and grain.
for ($i = 0; $i -lt 9000; $i++) {
    $c = [System.Drawing.Color]::FromArgb((Clamp (Rand 8 26)), 90, 62, 30)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush $c), (Rand 0 $Width), (Rand 0 $Height), (Rand 1 3), 1)
}

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
    @(@(0.5,1,0.5,0)),                                                                 # isa
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

# Silver ink: a dark outline under a cool grey stroke with a thin highlight, so it reads as metal.
function Ink-Pens([float]$w) {
    $shadow = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(170, 45, 40, 38)), ($w + 2.5)
    $body = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(245, 128, 134, 142)), $w
    $shine = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(150, 205, 210, 216)), ([Math]::Max(1, $w * 0.35))
    foreach ($p in @($shadow, $body, $shine)) { $p.StartCap = 'Round'; $p.EndCap = 'Round'; $p.LineJoin = 'Round' }
    return @($shadow, $body, $shine)
}

# Hand-drawn feel: jitter every endpoint and lean the whole glyph a little.
function Draw-Rune([int]$index, [float]$x, [float]$y, [float]$w, [float]$h, [float]$stroke, [float]$jitter) {
    $lean = Rand -0.08 0.08
    $lines = foreach ($l in $runes[$index]) {
        ,@(
            ($x + ($l[0] + (Rand -$jitter $jitter) + $lean * (1 - $l[1])) * $w), ($y + ($l[1] + (Rand -$jitter $jitter)) * $h),
            ($x + ($l[2] + (Rand -$jitter $jitter) + $lean * (1 - $l[3])) * $w), ($y + ($l[3] + (Rand -$jitter $jitter)) * $h)
        )
    }
    foreach ($pen in (Ink-Pens $stroke)) {
        foreach ($l in $lines) { $g.DrawLine($pen, [float]$l[0], [float]$l[1], [float]$l[2], [float]$l[3]) }
    }
}

$margin = 30
$textTop = 70
$textBottom = $Height * 0.74

# The texture is the banner's left half; the game mirrors it across the right edge (the centre line).
# Keep the text mostly to runes that read the same mirrored, and centre motifs on that line.
$symmetricRunes = @(6, 10, 14, 16, 18, 19, 21, 22, 23)
function Pick-Rune { if ($rng.NextDouble() -lt 0.8) { return $symmetricRunes[$rng.Next(0, $symmetricRunes.Count)] } return $rng.Next(0, $runes.Count) }

# Large algiz heading on the centre line; the mirror completes it.
Draw-Rune 14 ($Width - 35) $textTop 70 112 7 0
$textTop += 135
$initialBottom = 0

$lineHeight = 34
$glyphH = 24
$y = $textTop
while ($y -lt $textBottom) {
    $x = if ($y -lt $initialBottom) { $margin + 84 } else { $margin }
    # Lines run on into the mirrored half across the centre line.
    $right = $Width - 12
    while ($x -lt $right) {
        $wordLen = $rng.Next(2, 6)
        for ($k = 0; $k -lt $wordLen -and $x -lt $right; $k++) {
            $glyphW = Rand 12 17
            Draw-Rune (Pick-Rune) $x ($y + (Rand -1.5 1.5)) $glyphW $glyphH 2.3 0.05
            $x += $glyphW + (Rand 3 6)
        }
        # Word divider dot, as in runestone inscriptions.
        $dot = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(230, 120, 126, 134))
        $g.FillEllipse($dot, $x + 1, $y + $glyphH * 0.45, 4, 4)
        $x += Rand 10 15
    }
    $y += $lineHeight + (Rand -2 2)
}

# Borromean rings at the foot, a knot found on Norse carvings, centred on the centre line.
$cx = $Width; $cy = $Height * 0.84; $ringR = 44; $spread = 30
foreach ($pen in (Ink-Pens 6)) {
    foreach ($dir in @(270, 30, 150)) {
        $ox = $cx + [Math]::Cos($dir * [Math]::PI / 180) * $spread
        $oy = $cy + [Math]::Sin($dir * [Math]::PI / 180) * $spread
        $g.DrawEllipse($pen, [float]($ox - $ringR), [float]($oy - $ringR), [float]($ringR * 2), [float]($ringR * 2))
    }
}

# --- Aging and torn edges --------------------------------------------------------------------
$g.Dispose()

# Jagged outline from random walks: the outer (left) edge, top and bottom. The right edge is the
# banner's centre line, so it stays intact.
function Walk([int]$n, [double]$base, [double]$step, [double]$min, [double]$max) {
    $v = New-Object double[] $n; $cur = $base
    for ($i = 0; $i -lt $n; $i++) {
        $cur = [Math]::Max($min, [Math]::Min($max, $cur + (Rand (-$step) $step)))
        if ($rng.NextDouble() -lt 0.02) { $cur = [Math]::Min($max, $cur + (Rand 4 14)) }   # occasional bite
        $v[$i] = $cur
    }
    return $v
}
$leftTear = Walk $Height 8 1.6 2 26
$topTear = Walk $Width 6 1.2 1 14
$bottomTear = Walk $Width 24 2.5 6 70

$rect = New-Object System.Drawing.Rectangle 0, 0, $Width, $Height
$data = $bmp.LockBits($rect, 'ReadWrite', [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes = New-Object byte[] ($data.Stride * $Height)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)

for ($py = 0; $py -lt $Height; $py++) {
    for ($px = 0; $px -lt $Width; $px++) {
        $edge = [Math]::Min($px - $leftTear[$py], [Math]::Min($py - $topTear[$px], $Height - 1 - $py - $bottomTear[$px]))
        $i = $py * $data.Stride + $px * 4
        if ($edge -lt 0) {
            # Torn away: transparent, but keep a dark burnt colour in case the shader ignores alpha.
            $bytes[$i] = 30; $bytes[$i + 1] = 45; $bytes[$i + 2] = 60; $bytes[$i + 3] = 0
            continue
        }
        # Scorched rim fading into the page, plus an overall vignette.
        $burn = [Math]::Max(0, 1 - $edge / 18)
        $nx = 1 - $px / $Width; $ny = ($py / $Height - 0.5) * 2
        $vignette = 1 - 0.22 * ($nx * $nx * 0.6 + $ny * $ny * 0.4)
        $k = $vignette * (1 - 0.75 * $burn * $burn)
        $bytes[$i] = Clamp ($bytes[$i] * $k)
        $bytes[$i + 1] = Clamp ($bytes[$i + 1] * $k)
        $bytes[$i + 2] = Clamp ($bytes[$i + 2] * $k)
        $bytes[$i + 3] = 255
    }
}
[System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
$bmp.UnlockBits($data)

$dir = Split-Path $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
$bmp.Save((Join-Path (Get-Location) $Out), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"Wrote $Out"
