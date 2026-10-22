# Generates a clean-room 512x512 RGBA atlas "Textures/Corn" for the SCIE-Port
# corn crop. The atlas uses the confirmed block-face grid: 16 tiles per row,
# 32x32 pixel tiles, so texture slot N lives at pixel rect ((N%16)*32, (N/16)*32).
#   slots 0..7 = corn stalk growth stages (green stalk -> tall stalk with yellow ear)
#   slot 8     = corn seed kernels
#   slot 9     = harvested corn ear (produce / future fodder)
# Everything else stays fully transparent (cross-face blocks render with alpha test).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$W = 512; $H = 512; $TILE = 32
$bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

# Fully transparent background.
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::Transparent)
$g.Dispose()

function TileX([int]$slot) { return [int](($slot % 16) * $TILE) }
function TileY([int]$slot) { return [int]([Math]::Floor($slot / 16.0) * $TILE) }

function Px($bmp, [int]$ox, [int]$oy, [int]$x, [int]$y, [byte]$r, [byte]$gg, [byte]$b, [byte]$a) {
    if ($x -lt 0 -or $y -lt 0 -or $x -ge $TILE -or $y -ge $TILE) { return }
    $bmp.SetPixel($ox + $x, $oy + $y, [System.Drawing.Color]::FromArgb($a, $r, $gg, $b))
}

function Rect($bmp, [int]$ox, [int]$oy, [int]$x0, [int]$y0, [int]$x1, [int]$y1, [byte]$r, [byte]$gg, [byte]$b, [byte]$a) {
    for ($y = $y0; $y -le $y1; $y++) { for ($x = $x0; $x -le $x1; $x++) { Px $bmp $ox $oy $x $y $r $gg $b $a } }
}

# --- Stalk growth stages 0..7 ---
for ($s = 0; $s -le 7; $s++) {
    $ox = TileX $s; $oy = TileY $s
    # stalk height grows from a stub to the full tile
    $top = 30 - ($s * 3)         # smaller $top = taller stalk
    if ($top -lt 3) { $top = 3 }
    $stalkR = [byte]76; $stalkG = [byte]130; $stalkB = [byte]50
    # central vertical stalk (2px wide), from bottom up
    Rect $bmp $ox $oy 15 $top 16 31 $stalkR $stalkG $stalkB 255
    # leaves branching off the stalk as it matures
    if ($s -ge 1) { Rect $bmp $ox $oy 11 ($top + 6) 14 ($top + 7) $stalkR $stalkG $stalkB 255 }
    if ($s -ge 2) { Rect $bmp $ox $oy 17 ($top + 4) 20 ($top + 5) $stalkR $stalkG $stalkB 255 }
    if ($s -ge 3) { Rect $bmp $ox $oy 10 ($top + 12) 13 ($top + 13) $stalkR $stalkG $stalkB 255 }
    if ($s -ge 4) { Rect $bmp $ox $oy 18 ($top + 10) 21 ($top + 11) $stalkR $stalkG $stalkB 255 }
    # a mature (stage 7) stalk sprouts a small yellow ear near the top
    if ($s -ge 6) {
        Rect $bmp $ox $oy 17 ($top + 2) 20 ($top + 8) 230 200 60 255
        Rect $bmp $ox $oy 17 ($top + 1) 20 ($top + 1) 150 180 60 255  # husk tip
    }
    if ($s -eq 7) {
        Rect $bmp $ox $oy 12 ($top + 1) 14 ($top + 9) 150 180 60 255  # extra husk leaf
    }
}

# --- Slot 8: corn seed kernels ---
$ox = TileX 8; $oy = TileY 8
foreach ($k in @( @(11,20,3), @(16,21,3), @(21,20,3), @(13,24,3), @(19,24,3) )) {
    Rect $bmp $ox $oy $k[0] $k[1] ($k[0] + $k[2]) ($k[1] + $k[2]) 235 205 70 255
    Rect $bmp $ox $oy $k[0] ($k[1] + $k[2]) ($k[0] + $k[2]) ($k[1] + $k[2]) 200 165 50 255
}

# --- Slot 9: harvested corn ear (produce) ---
$ox = TileX 9; $oy = TileY 9
# cob body
Rect $bmp $ox $oy 11 8 20 26 235 205 70 255
# kernel rows (vertical darker lines)
for ($x = 12; $x -le 20; $x += 3) { Rect $bmp $ox $oy $x 8 $x 26 205 170 55 255 }
for ($y = 10; $y -le 26; $y += 4) { Rect $bmp $ox $oy 11 $y 20 $y 205 170 55 255 }
# green husk peeled down the base
Rect $bmp $ox $oy 9 24 22 29 150 180 60 255
Rect $bmp $ox $oy 10 22 12 26 120 160 50 255

$out = Join-Path (Get-Location) 'src\SCIEPort\Assets\Textures\Corn.png'
$out = [IO.Path]::GetFullPath($out)
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

# Self-check dimensions.
$check = [System.Drawing.Bitmap]::FromFile($out)
if ($check.Width -ne 512 -or $check.Height -ne 512) { $check.Dispose(); throw 'Corn.png must be 512x512.' }
$fmt = $check.PixelFormat
$check.Dispose()
Write-Host ("Wrote " + $out + "  format=" + $fmt)
