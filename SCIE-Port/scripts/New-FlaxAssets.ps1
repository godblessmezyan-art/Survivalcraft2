# Generates a clean-room 512x512 RGBA atlas "Textures/Flax" for the SCIE-Port flax
# crop. The atlas uses the confirmed block-face grid: 16 tiles per row, 32x32 pixel
# tiles, so texture slot N lives at pixel rect ((N%16)*32, (N/16)*32).
#   slots 0..7 = flax plant growth stages (short green sprout -> tall stalk with blue flowers)
#   slot 8     = harvested flax fiber bundle (textile product / future rope)
# Everything else stays fully transparent (cross-face blocks render with alpha test).
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

# Flax palette
$stalkR = [byte]92;  $stalkG = [byte]150; $stalkB = [byte]72
$blossR = [byte]72;  $blossG = [byte]108; $blossB = [byte]200   # flax blue flower
$blosLite = [byte]150

# --- Plant growth stages 0..7 (a small cluster of thin culms that grows tall) ---
for ($s = 0; $s -le 7; $s++) {
    $ox = TileX $s; $oy = TileY $s
    $top = 29 - ($s * 3)
    if ($top -lt 4) { $top = 4 }
    # three thin culms (like flax's grassy stems)
    foreach ($cx in @(13, 16, 19)) {
        Rect $bmp $ox $oy $cx $top ($cx + 1) 31 $stalkR $stalkG $stalkB 255
    }
    # as it matures the middle culm blooms a blue flax flower at its tip
    if ($s -ge 5) {
        Rect $bmp $ox $oy 15 ($top - 1) 17 ($top + 1) $blossR $blossG $blossB 255
        Px $bmp $ox $oy 16 ($top - 2) $blossR $blossG $blossB 255
        Px $bmp $ox $oy 16 ($top - 1) $blosLite $blosLite 250 255
    }
    if ($s -eq 7) {
        # side blooms on outer culms when fully ripe
        Rect $bmp $ox $oy 12 ($top + 2) 13 ($top + 3) $blossR $blossG $blossB 255
        Rect $bmp $ox $oy 20 ($top + 3) 21 ($top + 4) $blossR $blossG $blossB 255
    }
}

# --- Slot 8: harvested flax fiber bundle ---
$ox = TileX 8; $oy = TileY 8
# a tied sheaf of pale fibers (vertical strands with a binding knot)
$fibR = [byte]206; $fibG = [byte]200; $fibB = [byte]170
$fibD = [byte]168; $fibDg = [byte]160; $fibDb = [byte]126
foreach ($cx in @(10, 13, 16, 19, 22)) {
    Rect $bmp $ox $oy $cx 6 ($cx) 27 $fibR $fibG $fibB 255
    Rect $bmp $ox $oy ($cx + 1) 7 ($cx + 1) 26 $fibD $fibDg $fibDb 255
}
# frayed tops and bound bottoms
Rect $bmp $ox $oy 9 5 23 5 $fibR $fibG $fibB 255
Rect $bmp $ox $oy 9 27 23 28 $fibD $fibDg $fibDb 255
# binding tie across the middle
Rect $bmp $ox $oy 8 15 24 17 120 92 52 255

$out = Join-Path (Get-Location) 'src\SCIEPort\Assets\Textures\Flax.png'
$out = [IO.Path]::GetFullPath($out)
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$check = [System.Drawing.Bitmap]::FromFile($out)
if ($check.Width -ne 512 -or $check.Height -ne 512) { $check.Dispose(); throw 'Flax.png must be 512x512.' }
$fmt = $check.PixelFormat
function Opaque($check, [int]$slot) {
    $bx = TileX $slot; $by = TileY $slot
    $n = 0
    for ($y = 0; $y -lt $TILE; $y++) { for ($x = 0; $x -lt $TILE; $x++) {
        if ($check.GetPixel($bx + $x, $by + $y).A -gt 0) { $n++ } } }
    return $n
}
$counts = @()
for ($s = 0; $s -le 8; $s++) { $counts += ("slot" + $s + "=" + (Opaque $check $s)) }
$spare = Opaque $check 12
$check.Dispose()
Write-Host ("Wrote " + $out + "  format=" + $fmt)
Write-Host (($counts -join " ") + " spare slot12=" + $spare)
