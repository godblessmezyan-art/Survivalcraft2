# Generates a clean-room 512x512 RGBA atlas "Textures/Bamboo" for the SCIE-Port
# bamboo crop. The atlas uses the confirmed block-face grid: 16 tiles per row,
# 32x32 pixel tiles, so texture slot N lives at pixel rect ((N%16)*32, (N/16)*32).
#   slot 0 = planted bamboo base cross face (short culm with a node ring + roots)
#   slot 1 = stacked upper culm cross face (taller internode with node ring + sprig)
#   slot 2 = solid bamboo building-block cube face (a full panel of side-by-side culms)
#   slot 3 = fresh shoot (visible in destruction debris)
# Everything else stays fully transparent (cross-face blocks render with alpha test;
# the cube face is fully opaque so its SubsetOpaque tiles have coverage).
param([string]$OutPath = (Join-Path $PSScriptRoot '../src/SCIEPort/Assets/Textures/Bamboo.png'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$W = 512; $H = 512; $TILE = 32
$bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

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

# Bamboo culm palette
$culmR = [byte]120; $culmG = [byte]170; $culmB = [byte]70      # mid green cane
$darkR = [byte]92;  $darkG = [byte]132; $darkB = [byte]52      # node ring / shading
$liteR = [byte]150; $liteG = [byte]196; $liteB = [byte]96      # highlight edge
$leafR = [byte]78;  $leafG = [byte]130; $leafB = [byte]50      # sprig / leaves

# --- Slot 0: planted base cross face (short stubby culm with node + soil tuft) ---
$ox = TileX 0; $oy = TileY 0
# main vertical culm (columns 14..17), lower half of the tile only
Rect $bmp $ox $oy 14 12 17 30 $culmR $culmG $culmB 255
Rect $bmp $ox $oy 14 12 14 30 $liteR $liteG $liteB 255   # lit left edge
Rect $bmp $ox $oy 17 12 17 30 $darkR $darkG $darkB 255   # shaded right edge
# node ring near the top of the base
Rect $bmp $ox $oy 13 14 18 15 $darkR $darkG $darkB 255
# small roots / base flare into the ground
Rect $bmp $ox $oy 12 29 19 30 $darkR $darkG $darkB 255
# two little leaves sprouting from the node
Rect $bmp $ox $oy 9 16 12 17 $leafR $leafG $leafB 255
Rect $bmp $ox $oy 19 17 22 18 $leafR $leafG $leafB 255

# --- Slot 1: upper culm cross face (taller internode, node ring, side sprig) ---
$ox = TileX 1; $oy = TileY 1
Rect $bmp $ox $oy 14 2 17 30 $culmR $culmG $culmB 255
Rect $bmp $ox $oy 14 2 14 30 $liteR $liteG $liteB 255
Rect $bmp $ox $oy 17 2 17 30 $darkR $darkG $darkB 255
# two node rings splitting the internode
Rect $bmp $ox $oy 13 9 18 10 $darkR $darkG $darkB 255
Rect $bmp $ox $oy 13 21 18 22 $darkR $darkG $darkB 255
# side sprig with leaves from the lower node
Rect $bmp $ox $oy 8 18 13 19 $leafR $leafG $leafB 255
Rect $bmp $ox $oy 6 17 8 18 $leafR $leafG $leafB 255
Rect $bmp $ox $oy 18 8 22 9 $leafR $leafG $leafB 255

# --- Slot 2: solid bamboo building-block cube face (full opaque culm panel) ---
$ox = TileX 2; $oy = TileY 2
# fully opaque background so SubsetOpaque cube faces always have coverage
Rect $bmp $ox $oy 0 0 31 31 $culmR $culmG $culmB 255
# four vertical culms with dark seam shading between them
foreach ($cx in @(2, 9, 16, 23)) {
    Rect $bmp $ox $oy $cx 0 ($cx + 4) 31 $culmR $culmG $culmB 255
    Rect $bmp $ox $oy $cx 0 $cx 31 $liteR $liteG $liteB 255
    Rect $bmp $ox $oy ($cx + 4) 0 ($cx + 5) 31 $darkR $darkG $darkB 255
}
# horizontal node rings crossing every culm at two heights
Rect $bmp $ox $oy 0 7 31 8 $darkR $darkG $darkB 255
Rect $bmp $ox $oy 0 20 31 21 $darkR $darkG $darkB 255
# right-most culm column to fill edge
Rect $bmp $ox $oy 30 0 31 31 $culmR $culmG $culmB 255

# Slot 3: tapered shoot with overlapping brown and olive sheaths.
$ox = TileX 3; $oy = TileY 3
for ($y = 5; $y -le 29; $y++) {
    $halfWidth = [int][Math]::Floor(($y - 4) * .33)
    for ($x = 17 - $halfWidth; $x -le 17 + $halfWidth; $x++) {
        if ($y -lt 10) {
            Px $bmp $ox $oy $x $y 105 141 62 255
        } elseif ((($y + [int][Math]::Floor($x / 3)) % 5) -le 1 -or $x -eq 17 - $halfWidth) {
            Px $bmp $ox $oy $x $y 106 91 47 255
        } elseif ($x -lt 18) {
            Px $bmp $ox $oy $x $y 169 149 70 255
        } else {
            Px $bmp $ox $oy $x $y 141 122 62 255
        }
    }
}

$out = [IO.Path]::GetFullPath($OutPath)
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$check = [System.Drawing.Bitmap]::FromFile($out)
if ($check.Width -ne 512 -or $check.Height -ne 512) { $check.Dispose(); throw 'Bamboo.png must be 512x512.' }
$fmt = $check.PixelFormat
# verify the four authored slots have opaque pixels and a spare slot is clear
function Opaque($check, [int]$slot) {
    $bx = TileX $slot; $by = TileY $slot
    $n = 0
    for ($y = 0; $y -lt $TILE; $y++) { for ($x = 0; $x -lt $TILE; $x++) {
        if ($check.GetPixel($bx + $x, $by + $y).A -gt 0) { $n++ } } }
    return $n
}
$c0 = Opaque $check 0; $c1 = Opaque $check 1; $c2 = Opaque $check 2; $c3 = Opaque $check 3; $c5 = Opaque $check 5
$check.Dispose()
Write-Host ("Wrote " + $out + "  format=" + $fmt)
Write-Host ("opaque slot0=" + $c0 + " slot1=" + $c1 + " slot2=" + $c2 + " slot3=" + $c3 + " slot5(spare)=" + $c5)
if ($c0 -le 0 -or $c1 -le 0 -or $c2 -ne 1024 -or $c3 -le 0 -or $c5 -ne 0) { throw 'Bamboo atlas tile coverage is invalid.' }
