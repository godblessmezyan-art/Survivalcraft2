# Generates a clean-room 512x512 RGBA atlas "Textures/Apple" for the SCIE-Port apple
# plant. The atlas uses the confirmed block-face grid: 16 tiles per row, 32x32 pixel
# tiles, so texture slot N lives at pixel rect ((N%16)*32, (N/16)*32).
#   slots 0..7 = apple growth stages (sprout -> sapling -> woody bush -> leafy canopy)
#   slot 8     = ripe/fruiting variant (the mature canopy hung with red apples)
#   slot 9     = harvested apple (the edible fruit item)
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

# Apple palette
$barkR = [byte]96;  $barkG = [byte]64;  $barkB = [byte]40      # woody trunk/branch
$barkD = [byte]70;  $barkDG = [byte]46; $barkDB = [byte]30     # bark shadow
$leafR = [byte]66;  $leafG = [byte]148; $leafB = [byte]58      # canopy leaf
$leafL = [byte]108; $leafLG = [byte]184; $leafLB = [byte]92    # leaf highlight
$aplR  = [byte]198; $aplG  = [byte]42;  $aplB  = [byte]42      # apple red
$aplHl = [byte]236; $aplHg  = [byte]120; $aplHb = [byte]110    # apple highlight
$aplDk = [byte]150; $aplDg  = [byte]24;  $aplDb = [byte]28     # apple shadow

# --- Growth stages 0..7: a woody central stalk thickens and rises while a rounded
#     leafy canopy balloons out of it. ---
for ($s = 0; $s -le 7; $s++) {
    $ox = TileX $s; $oy = TileY $s
    $top = 28 - ($s * 3)
    if ($top -lt 6) { $top = 6 }

    if ($s -eq 0) {
        # Sprout: two seedling leaves on a short green shoot.
        Rect $bmp $ox $oy 15 26 16 31 $leafR $leafG $leafB 255
        Rect $bmp $ox $oy 12 24 14 26 $leafR $leafG $leafB 255
        Rect $bmp $ox $oy 17 24 19 26 $leafR $leafG $leafB 255
        continue
    }

    # Woody trunk (widens slightly with maturity).
    $tw = if ($s -ge 5) { 2 } else { 1 }
    Rect $bmp $ox $oy (15 - [int][Math]::Floor(($tw - 1) / 2)) $top (15 + [int]($tw / 2) + 1) 31 $barkR $barkG $barkB 255
    Px $bmp $ox $oy 15 ($top + 1) $barkD $barkDG $barkDB 255

    # Branch stubs on the taller stages.
    if ($s -ge 4) {
        Rect $bmp $ox $oy 12 ($top + 3) 14 ($top + 3) $barkR $barkG $barkB 255
        Rect $bmp $ox $oy 17 ($top + 4) 19 ($top + 4) $barkR $barkG $barkB 255
    }

    # Leafy canopy; radius grows with the stage, rounding into a crown by maturity.
    $cr = [int](2 + $s * 1.4)          # canopy radius
    $cx = 16; $cy = $top - 1
    for ($dy = -$cr; $dy -le $cr; $dy++) {
        for ($dx = -$cr; $dx -le $cr; $dx++) {
            if (($dx * $dx + $dy * $dy) -le ($cr * $cr)) {
                $edge = (($dx + $dy) -lt (-$cr + 1))
                if ($edge) {
                    Px $bmp $ox $oy ($cx + $dx) ($cy + $dy) $leafL $leafLG $leafLB 255
                } else {
                    Px $bmp $ox $oy ($cx + $dx) ($cy + $dy) $leafR $leafG $leafB 255
                }
            }
        }
    }
    # Punch the trunk through the canopy base so the woody stem reads.
    Rect $bmp $ox $oy 15 ($cy + $cr - 2) 16 ($cy + $cr) $barkR $barkG $barkB 255
}

# --- Slot 8: fruiting variant = the mature (stage-7) canopy hung with red apples ---
$ox = TileX 8; $oy = TileY 8
# Redraw the mature crown (same shape as stage 7) so fruiting reads as a full tree.
$top = 28 - (7 * 3); if ($top -lt 6) { $top = 6 }
Rect $bmp $ox $oy 14 $top 17 31 $barkR $barkG $barkB 255
$cr = [int](2 + 7 * 1.4); $cx = 16; $cy = $top - 1
for ($dy = -$cr; $dy -le $cr; $dy++) {
    for ($dx = -$cr; $dx -le $cr; $dx++) {
        if (($dx * $dx + $dy * $dy) -le ($cr * $cr)) {
            if (($dx + $dy) -lt (-$cr + 1)) { Px $bmp $ox $oy ($cx + $dx) ($cy + $dy) $leafL $leafLG $leafLB 255 }
            else { Px $bmp $ox $oy ($cx + $dx) ($cy + $dy) $leafR $leafG $leafB 255 }
        }
    }
}
# Apples dotted across the crown.
foreach ($cell in @(
    @{x=10;y=8}, @{x=16;y=6}, @{x=22;y=9}, @{x=12;y=13},
    @{x=19;y=13}, @{x=15;y=10}, @{x=9;y=12}, @{x=23;y=13})) {
    Rect $bmp $ox $oy $cell.x $cell.y ($cell.x + 2) ($cell.y + 2) $aplR $aplG $aplB 255
    Px $bmp $ox $oy $cell.x ($cell.y) $aplHl $aplHg $aplHb 255
    Px $bmp $ox $oy ($cell.x + 2) ($cell.y + 2) $aplDk $aplDg $aplDb 255
}

# --- Slot 9: harvested apple (a single fruit with a stalk + leaf) ---
$ox = TileX 9; $oy = TileY 9
# body
for ($dy = 0; $dy -lt 12; $dy++) {
    for ($dx = 0; $dx -lt 14; $dx++) {
        $mx = $dx - 7; $my = $dy - 6
        if (($mx * $mx) / 52 + ($my * $my) / 40 -le 1) {
            $r = $aplR; $gg = $aplG; $b = $aplB
            if ($mx -lt -3 -and $my -lt -1) { $r = $aplHl; $gg = $aplHg; $b = $aplHb }
            if ($mx -gt 3 -and $my -gt 1) { $r = $aplDk; $gg = $aplDg; $b = $aplDb }
            Px $bmp $ox $oy (9 + $dx) (14 + $dy) $r $gg $b 255
        }
    }
}
# stalk
Rect $bmp $ox $oy 15 11 16 13 $barkR $barkG $barkB 255
# leaf
Rect $bmp $ox $oy 17 11 20 12 $leafR $leafG $leafB 255

$out = Join-Path (Get-Location) 'src\SCIEPort\Assets\Textures\Apple.png'
$out = [IO.Path]::GetFullPath($out)
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$check = [System.Drawing.Bitmap]::FromFile($out)
if ($check.Width -ne 512 -or $check.Height -ne 512) { $check.Dispose(); throw 'Apple.png must be 512x512.' }
$fmt = $check.PixelFormat
function Opaque($check, [int]$slot) {
    $bx = TileX $slot; $by = TileY $slot
    $n = 0
    for ($y = 0; $y -lt $TILE; $y++) { for ($x = 0; $x -lt $TILE; $x++) {
        if ($check.GetPixel($bx + $x, $by + $y).A -gt 0) { $n++ } } }
    return $n
}
$counts = @()
for ($s = 0; $s -le 9; $s++) { $counts += ("slot" + $s + "=" + (Opaque $check $s)) }
$spare = Opaque $check 12
$check.Dispose()
Write-Host ("Wrote " + $out + "  format=" + $fmt)
Write-Host (($counts -join " ") + " spare slot12=" + $spare)
