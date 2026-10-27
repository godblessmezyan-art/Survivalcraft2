param([string]$ProjectDir = (Join-Path $PSScriptRoot '..\src\SCIEPort'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$textureDir = Join-Path $ProjectDir 'Assets\Textures\Clothing'
$sharedTextureDir = Join-Path $ProjectDir 'Assets\Textures'
$resolvedProjectDir = [IO.Path]::GetFullPath($ProjectDir)
$moduleRoot = Split-Path (Split-Path $resolvedProjectDir -Parent) -Parent
$previewDir = Join-Path $moduleRoot 'docs\assets'
New-Item -ItemType Directory -Force -Path $textureDir, $sharedTextureDir, $previewDir | Out-Null

function C([string]$hex, [int]$alpha = 255) {
    $hex = $hex.TrimStart('#')
    return [Drawing.Color]::FromArgb($alpha,
        [Convert]::ToInt32($hex.Substring(0,2),16),
        [Convert]::ToInt32($hex.Substring(2,2),16),
        [Convert]::ToInt32($hex.Substring(4,2),16))
}

function New-Canvas([int]$width = 64, [int]$height = 64) {
    $bitmap = [Drawing.Bitmap]::new($width,$height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96,96)
    return $bitmap
}

function Fill([Drawing.Bitmap]$bitmap, [int]$x, [int]$y, [int]$w, [int]$h, [Drawing.Color]$color) {
    for ($py=$y; $py -lt $y+$h; $py++) {
        for ($px=$x; $px -lt $x+$w; $px++) { $bitmap.SetPixel($px,$py,$color) }
    }
}

function Frame([Drawing.Bitmap]$bitmap, [int]$x, [int]$y, [int]$w, [int]$h, [Drawing.Color]$color) {
    for ($px=$x; $px -lt $x+$w; $px++) { $bitmap.SetPixel($px,$y,$color); $bitmap.SetPixel($px,$y+$h-1,$color) }
    for ($py=$y; $py -lt $y+$h; $py++) { $bitmap.SetPixel($x,$py,$color); $bitmap.SetPixel($x+$w-1,$py,$color) }
}

function Line([Drawing.Bitmap]$bitmap, [int]$x0, [int]$y0, [int]$x1, [int]$y1, [Drawing.Color]$color) {
    $dx=[Math]::Abs($x1-$x0); $sx=if($x0 -lt $x1){1}else{-1}; $dy=-[Math]::Abs($y1-$y0); $sy=if($y0 -lt $y1){1}else{-1}; $err=$dx+$dy
    while($true) { $bitmap.SetPixel($x0,$y0,$color); if($x0 -eq $x1 -and $y0 -eq $y1){break}; $e2=2*$err; if($e2 -ge $dy){$err+=$dy;$x0+=$sx}; if($e2 -le $dx){$err+=$dx;$y0+=$sy} }
}

function Metal([Drawing.Bitmap]$bitmap, [int]$x, [int]$y, [int]$w, [int]$h, [Drawing.Color]$dark, [Drawing.Color]$mid, [Drawing.Color]$light, [int]$seed) {
    for ($py=$y; $py -lt $y+$h; $py++) {
        for ($px=$x; $px -lt $x+$w; $px++) {
            $n=(($px*37+$py*61+$seed*17) -bxor ($px*$py+$seed*13)) % 11
            $color=if($n -eq 0){$light}elseif($n -le 3){$dark}else{$mid}
            $bitmap.SetPixel($px,$py,$color)
        }
    }
}

function Scale([Drawing.Color]$color, [float]$factor) {
    return [Drawing.Color]::FromArgb($color.A,
        [Math]::Min(255,[int]($color.R*$factor)),
        [Math]::Min(255,[int]($color.G*$factor)),
        [Math]::Min(255,[int]($color.B*$factor)))
}

function Save-Png([Drawing.Bitmap]$bitmap, [string]$path) {
    $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
}

function New-Qinglong {
    $b=New-Canvas
    $deep=C '#12383A'; $jade=C '#17615F'; $bright=C '#3E8F83'; $bronze=C '#8D6C3C'; $gold=C '#D6B76A'; $rune=C '#9FE0CB'
    foreach($r in @(@(5,0,12,8),@(28,0,12,8),@(45,0,8,8),@(54,0,8,8))) { Metal $b $r[0] $r[1] $r[2] $r[3] $deep $jade $bright 31; Frame $b $r[0] $r[1] $r[2] $r[3] $bronze }
    # Crown ridges, paired scale plates and a compact forehead dragon rune.
    foreach($x in @(6,9,12,29,32,35)) { Line $b $x 2 ($x+1) 1 $gold; Line $b $x 3 ($x+1) 4 $bright }
    Line $b 31 5 33 2 $rune; Line $b 33 2 35 5 $rune; Line $b 31 5 35 5 $gold; $b.SetPixel(33,4,$rune)
    foreach($x in @(46,49,55,58)) { Line $b $x 6 ($x+1) 3 $gold }
    return $b
}

function New-Xuanwu {
    $b=New-Canvas
    $black=C '#171B22'; $iron=C '#292F38'; $ridge=C '#444B52'; $bronze=C '#806039'; $gold=C '#C29A55'; $jade=C '#497A70'
    foreach($r in @(@(4,13,14,22),@(27,13,14,22),@(46,12,7,23),@(54,12,7,23))) { Metal $b $r[0] $r[1] $r[2] $r[3] $black $iron $ridge 47; Frame $b $r[0] $r[1] $r[2] $r[3] $bronze }
    # Layered shell scutes on chest/back.
    foreach($x in @(6,10,14,29,33,37)) { Line $b $x 17 ($x+2) 14 $gold; Line $b $x 17 ($x+2) 20 $bronze }
    foreach($y in @(21,26,31)) { Line $b 5 $y 16 $y $bronze; Line $b 28 $y 39 $y $bronze }
    # Turtle/snake knot emblem in the forward centre panel.
    Line $b 31 24 34 21 $gold; Line $b 34 21 37 24 $gold; Line $b 37 24 34 28 $jade; Line $b 34 28 31 24 $jade
    Line $b 32 29 36 29 $gold; $b.SetPixel(37,28,$gold)
    # Heavy shoulder laminates.
    foreach($x in @(47,55)) { Line $b $x 15 ($x+4) 15 $gold; Line $b $x 19 ($x+4) 19 $bronze; Line $b $x 23 ($x+4) 23 $bronze }
    return $b
}

function New-Baihu {
    $b=New-Canvas
    $graphite=C '#272A2F'; $steel=C '#AEB7BD'; $silver=C '#E6ECEB'; $ice=C '#F8FFFF'; $shadow=C '#697278'; $gold=C '#B7904E'
    foreach($r in @(@(5,35,12,22),@(28,35,12,22),@(46,35,7,22),@(54,35,7,22))) { Metal $b $r[0] $r[1] $r[2] $r[3] $graphite $steel $silver 59; Frame $b $r[0] $r[1] $r[2] $r[3] $shadow }
    # Angular knee plates.
    foreach($x in @(6,29)) { Fill $b $x 42 10 7 $graphite; Frame $b $x 42 10 7 $silver; Line $b ($x+1) 48 ($x+5) 44 $gold; Line $b ($x+5) 44 ($x+8) 48 $gold }
    foreach($x in @(7,30,48,56)) { $b.SetPixel($x,36,$ice) }
    # Tiger stripes run diagonally and remain readable at native point sampling.
    foreach($offset in @(0,6)) { Line $b (6+$offset) 36 (9+$offset) 40 $graphite; Line $b (29+$offset) 36 (32+$offset) 40 $graphite; Line $b (6+$offset) 51 (9+$offset) 55 $graphite; Line $b (29+$offset) 51 (32+$offset) 55 $graphite }
    foreach($x in @(47,55)) { Line $b $x 37 ($x+4) 41 $graphite; Line $b $x 50 ($x+4) 54 $graphite }
    return $b
}

function New-Zhuque {
    $b=New-Canvas
    $crimson=C '#651E20'; $red=C '#A3352B'; $warm=C '#D85D32'; $gold=C '#D8A647'; $white=C '#FFE2A0'; $sole=C '#2A2022'
    foreach($r in @(@(5,57,12,7),@(28,57,12,7),@(46,57,7,7),@(54,57,7,7))) { Metal $b $r[0] $r[1] $r[2] $r[3] $crimson $red $warm 71; Frame $b $r[0] $r[1] $r[2] $r[3] $gold }
    foreach($x in @(6,29)) { Fill $b $x 62 10 2 $sole; Line $b ($x+1) 61 ($x+4) 58 $white; Line $b ($x+4) 58 ($x+6) 61 $gold; Line $b ($x+6) 61 ($x+8) 59 $warm }
    foreach($x in @(47,55)) { Line $b $x 61 ($x+2) 58 $white; Line $b ($x+2) 58 ($x+4) 61 $gold }
    return $b
}

function New-BaihuIcon {
    $b=New-Canvas 96 96; $g=[Drawing.Graphics]::FromImage($b); $g.SmoothingMode='AntiAlias'; $g.Clear([Drawing.Color]::Transparent)
    $gold=[Drawing.Pen]::new((C '#B78B43'),5); $silver=[Drawing.Pen]::new((C '#EDF2F1'),5); $dark=[Drawing.Pen]::new((C '#34383E'),4)
    $g.DrawEllipse($gold,6,6,84,84); $g.DrawEllipse($dark,12,12,72,72); $g.DrawEllipse($silver,16,16,64,64)
    $g.DrawArc($silver,24,23,48,43,205,130); $g.DrawLine($silver,27,57,39,72); $g.DrawLine($silver,69,57,57,72)
    $g.DrawLine($gold,29,34,42,44); $g.DrawLine($gold,67,34,54,44); $g.DrawLine($dark,38,28,44,39); $g.DrawLine($dark,58,28,52,39)
    $g.DrawLine($silver,42,54,48,61); $g.DrawLine($silver,54,54,48,61); $g.Dispose(); $gold.Dispose(); $silver.Dispose(); $dark.Dispose(); return $b
}

function New-EffectsAtlas {
    $b=New-Canvas 256 64; $g=[Drawing.Graphics]::FromImage($b); $g.SmoothingMode='AntiAlias'; $g.Clear([Drawing.Color]::Transparent)
    $colors=@((C '#7EDFC9'),(C '#D2AC65'),(C '#F0F5F3'),(C '#FFB04F'))
    for($i=0;$i -lt 4;$i++) { $cx=32+$i*64; $pen=[Drawing.Pen]::new($colors[$i],3); $faint=[Drawing.Pen]::new([Drawing.Color]::FromArgb(110,$colors[$i]),2); $g.DrawEllipse($faint,$cx-25,7,50,50); $g.DrawEllipse($pen,$cx-19,13,38,38)
        if($i -eq 0){$g.DrawArc($pen,$cx-12,18,24,27,205,285);$g.DrawLine($pen,$cx,19,$cx,46)}
        elseif($i -eq 1){$g.DrawPolygon($pen,[Drawing.Point[]]@([Drawing.Point]::new($cx,17),[Drawing.Point]::new($cx+15,28),[Drawing.Point]::new($cx+10,45),[Drawing.Point]::new($cx-10,45),[Drawing.Point]::new($cx-15,28)))}
        elseif($i -eq 2){$g.DrawArc($pen,$cx-16,18,32,30,200,140);$g.DrawLine($pen,$cx-14,24,$cx-4,33);$g.DrawLine($pen,$cx+14,24,$cx+4,33)}
        else{$g.DrawArc($pen,$cx-17,18,34,30,195,150);$g.DrawLine($pen,$cx,16,$cx-7,40);$g.DrawLine($pen,$cx,16,$cx+7,40)}
        $pen.Dispose(); $faint.Dispose()
    }
    $g.Dispose(); return $b
}

$textures=[ordered]@{
    FourSaintsQinglong = New-Qinglong
    FourSaintsXuanwu = New-Xuanwu
    FourSaintsBaihuLegs = New-Baihu
    FourSaintsZhuque = New-Zhuque
}
foreach($entry in $textures.GetEnumerator()) { Save-Png $entry.Value (Join-Path $textureDir ($entry.Key+'.png')) }
$icon=New-BaihuIcon; Save-Png $icon (Join-Path $sharedTextureDir 'FourSaintsBaihu.png'); $icon.Dispose()
$effects=New-EffectsAtlas; Save-Png $effects (Join-Path $sharedTextureDir 'FourSaintsEffects.png'); $effects.Dispose()

# Deterministic nearest-neighbour UV review sheet. It is an offline authoring aid, not an Android screenshot.
$preview=New-Canvas 1024 304; $pg=[Drawing.Graphics]::FromImage($preview); $pg.Clear((C '#181B20')); $pg.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor; $pg.PixelOffsetMode='Half'
$font=[Drawing.Font]::new('Arial',16,[Drawing.FontStyle]::Bold); $whiteBrush=[Drawing.SolidBrush]::new((C '#ECE8DA')); $names=@('Qinglong Crown','Xuanwu Armor','Baihu Greaves','Zhuque Boots'); $i=0
foreach($entry in $textures.GetEnumerator()) { $x=$i*256; $pg.DrawImage($entry.Value,[Drawing.Rectangle]::new($x,0,256,256),0,0,64,64,[Drawing.GraphicsUnit]::Pixel); $pg.DrawString($names[$i],$font,$whiteBrush,$x+12,266); $i++ }
$pg.Dispose(); $font.Dispose(); $whiteBrush.Dispose(); Save-Png $preview (Join-Path $previewDir 'FourSaintsV2-UV-preview.png'); $preview.Dispose()
foreach($bitmap in $textures.Values) { $bitmap.Dispose() }

Write-Host 'Generated original 64x64 Four Saints pixel armor, 256x64 effect glyph atlas, Baihu HUD emblem, and UV preview.'
