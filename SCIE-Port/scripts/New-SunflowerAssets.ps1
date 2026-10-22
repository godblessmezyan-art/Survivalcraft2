# Generates a clean-room 512x512 RGBA atlas "Textures/Sunflower" for the SCIE-Port
# sunflower crop. The atlas uses the confirmed block-face grid: 16 tiles per row,
# 32x32 pixel tiles, so texture slot N lives at pixel rect ((N%16)*32, (N/16)*32).
#   slots 0..7 = sunflower growth stages (small sprout -> tall stalk -> yellow head)
#   slot 8     = harvested sunflower seed pile (the edible crop)
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

# Sunflower palette
$stemR = [byte]60;  $stemG = [byte]140; $stemB = [byte]60
$leafR = [byte]82;  $leafG = [byte]162; $leafB = [byte]74
$petR  = [byte]240; $petG  = [byte]200; $petB  = [byte]48    # golden petals
$petLR = [byte]250; $petLG = [byte]226; $petLB = [byte]96    # petal highlight
$cenR  = [byte]110; $cenG  = [byte]70;  $cenB  = [byte]35    # brown disc florets
$cenD  = [byte]78;  $cenDG  = [byte]48; $cenDB = [byte]24

# --- Growth stages 0..7: a single taproot stalk that gets taller and leafier, then
#     tops out in the iconic yellow flower head. ---
for ($s = 0; $s -le 7; $s++) {
    $ox = TileX $s; $oy = TileY $s
    $top = 29 - ($s * 3)
    if ($top -lt 6) { $top = 6 }

    if ($s -le 1) {
        # Sprout: two tiny seedling leaves fanning from the soil line.
        Rect $bmp $ox $oy 15 27 16 31 $stemR $stemG $stemB 255
        Rect $bmp $ox $oy 12 25 14 27 $leafR $leafG $leafB 255
        Rect $bmp $ox $oy 17 25 19 27 $leafR $leafG $leafB 255
        continue
    }

    # Stem (one cell wide, growing upward as the stage advances).
    Rect $bmp $ox $oy 15 $top 16 31 $stemR $stemG $stemB 255

    # A pair of broad leaves partway up the stalk once it has some height.
    if ($s -ge 3) {
        $ly = $top + [int](($s - 3) * 2) + 4
        if ($ly -gt 29) { $ly = 29 }
        Rect $bmp $ox $oy 10 $ly 14 ($ly + 1) $leafR $leafG $leafB 255
        Rect $bmp $ox $oy 17 $ly 21 ($ly + 1) $leafR $leafG $leafB 255
    }

    # The flower head appears at maturity (stage 6+): golden petals around a brown disc.
    if ($s -ge 6) {
        $hy = $top
        # petal ring
        Rect $bmp $ox $oy 12 ($hy - 1) 19 ($hy + 6) $petR $petG $petB 255
        # lighter petal tips
        Rect $bmp $ox $oy 12 ($hy - 1) 19 ($hy) $petLR $petLG $petLB 255
        Rect $bmp $ox $oy 12 ($hy + 5) 19 ($hy + 6) $petLR $petLG $petLB 255
        # brown seed disc in the middle
        Rect $bmp $ox $oy 14 ($hy + 2) 17 ($hy + 3) $cenR $cenG $cenB 255
        Rect $bmp $ox $oy 14 ($hy + 2) 14 ($hy + 2) $cenD $cenDG $cenDB 255
        Rect $bmp $ox $oy 17 ($hy + 3) 17 ($hy + 3) $cenD $cenDG $cenDB 255
    }
}

# --- Slot 8: harvested sunflower seed pile ---
$ox = TileX 8; $oy = TileY 8
$seedR = [byte]44; $seedG = [byte]44; $seedB = [byte]50
$stripeR = [byte]150; $stripeG = [byte]138; $stripeB = [byte]110
# mound outline of dark hulled seeds with a light edge stripe
foreach ($cell in @(
    @{x=9;y=20}, @{x=12;y=20}, @{x=15;y=20}, @{x=18;y=20}, @{x=21;y=20},
    @{x=8;y=22}, @{x=11;y=22}, @{x=14;y=22}, @{x=17;y=22}, @{x=20;y=22}, @{x=23;y=22},
    @{x=9;y=24}, @{x=12;y=24}, @{x=15;y=24}, @{x=18;y=24}, @{x=21;y=24},
    @{x=11;y=26}, @{x=14;y=26}, @{x=17;y=26}, @{x=20;y=26})) {
    Rect $bmp $ox $oy $cell.x ($cell.y) ($cell.x + 1) ($cell.y + 1) $seedR $seedG $seedB 255
    Px $bmp $ox $oy $cell.x $cell.y $stripeR $stripeG $stripeB 255
}
# a few seeds resting on top
Rect $bmp $ox $oy 13 17 15 18 $seedR $seedG $seedB 255
Px $bmp $ox $oy 13 17 $stripeR $stripeG $stripeB 255

$out = Join-Path (Get-Location) 'src\SCIEPort\Assets\Textures\Sunflower.png'
$out = [IO.Path]::GetFullPath($out)
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$check = [System.Drawing.Bitmap]::FromFile($out)
if ($check.Width -ne 512 -or $check.Height -ne 512) { $check.Dispose(); throw 'Sunflower.png must be 512x512.' }
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
