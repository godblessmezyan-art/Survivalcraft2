param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\blueprints\owl_treehouse.json')
)

$ErrorActionPreference = 'Stop'

# Every entry is present in docs/blueprint-block-reference.md (v0.16.0).
$materials = @(
    @{ id = 0;  key = 'OakWoodBlock';       block = 'OakWoodBlock';       family = 'log' },
    @{ id = 1;  key = 'BirchWoodBlock';     block = 'BirchWoodBlock';     family = 'log' },
    @{ id = 2;  key = 'PlanksBlock';        block = 'PlanksBlock';        family = 'cube' },
    @{ id = 3;  key = 'StoneBrickBlock';    block = 'StoneBrickBlock';    family = 'cube' },
    @{ id = 4;  key = 'BricksBlock';        block = 'BricksBlock';        family = 'cube' },
    @{ id = 5;  key = 'GlassBlock';         block = 'GlassBlock';         family = 'cube' },
    @{ id = 6;  key = 'CoalBlock';          block = 'CoalBlock';          family = 'cube' },
    @{ id = 7;  key = 'WickerLampBlock';    block = 'WickerLampBlock';    family = 'cube' },
    @{ id = 8;  key = 'CopperBlock';        block = 'CopperBlock';        family = 'cube' },
    @{ id = 9;  key = 'DiamondBlock';       block = 'DiamondBlock';       family = 'cube' },
    @{ id = 10; key = 'MalachiteBlock';     block = 'MalachiteBlock';     family = 'cube' },
    @{ id = 11; key = 'MarbleBlock';        block = 'MarbleBlock';        family = 'cube' },
    @{ id = 12; key = 'BasaltBlock';        block = 'BasaltBlock';        family = 'cube' },
    @{ id = 13; key = 'FramedGlassBlock';   block = 'FramedGlassBlock';   family = 'cube' },
    @{ id = 14; key = 'IronBlock';          block = 'IronBlock';          family = 'cube' },
    @{ id = 15; key = 'WoodenStairsBlock';  block = 'WoodenStairsBlock';  family = 'stairs' },
    @{ id = 16; key = 'WoodenSlabBlock';    block = 'WoodenSlabBlock';    family = 'slab' },
    @{ id = 17; key = 'StoneSlabBlock';     block = 'StoneSlabBlock';     family = 'slab' },
    @{ id = 18; key = 'WoodenFenceBlock';   block = 'WoodenFenceBlock';   family = 'fence' },
    @{ id = 19; key = 'StoneFenceBlock';    block = 'StoneFenceBlock';    family = 'fence' },
    @{ id = 20; key = 'WoodenDoorBlock';    block = 'WoodenDoorBlock';    family = 'door' },
    @{ id = 21; key = 'CellDoorBlock';      block = 'CellDoorBlock';      family = 'door' },
    @{ id = 22; key = 'SpruceLeavesBlock';  block = 'SpruceLeavesBlock';  family = 'cube' }
)

$materialByKey = @{}
foreach ($material in $materials) { $materialByKey[$material.key] = $material }
$cells = @{}

function Set-Cell([int]$x, [int]$y, [int]$z, [string]$key, [int]$data = 0) {
    if (-not $script:materialByKey.ContainsKey($key)) { throw "Unknown material $key" }
    $material = $script:materialByKey[$key]
    $allowed = switch ($material.family) {
        'cube'   { $data -eq 0 }
        'log'    { $data -eq 0 }
        'stairs' { $data -ge 0 -and $data -le 7 }
        'slab'   { $data -ge 0 -and $data -le 1 }
        'fence'  { $data -ge 0 -and $data -le 15 }
        'door'   { $data -ge 0 -and $data -le 15 }
        default  { $false }
    }
    if (-not $allowed) { throw "Illegal DATA=$data for $key/$($material.family)" }
    $script:cells["$x,$y,$z"] = [pscustomobject]@{ x = $x; y = $y; z = $z; key = $key; data = $data }
}

function Remove-Cell([int]$x, [int]$y, [int]$z) {
    $script:cells.Remove("$x,$y,$z") | Out-Null
}

function Fill-Box([int]$x0, [int]$y0, [int]$z0, [int]$x1, [int]$y1, [int]$z1, [string]$key) {
    for ($y = $y0; $y -le $y1; $y++) {
        for ($z = $z0; $z -le $z1; $z++) {
            for ($x = $x0; $x -le $x1; $x++) { Set-Cell $x $y $z $key }
        }
    }
}

# --- Living trunk: 9x9 outer bark, 5x5 stair hall, front entrance tunnel. ---
for ($y = 0; $y -le 14; $y++) {
    for ($z = 9; $z -le 17; $z++) {
        for ($x = 16; $x -le 24; $x++) {
            $inside = $x -ge 18 -and $x -le 22 -and $z -ge 11 -and $z -le 15
            if (-not $inside) {
                $bark = if ((($x + $z + $y) % 7) -eq 0) { 'BirchWoodBlock' } else { 'OakWoodBlock' }
                Set-Cell $x $y $z $bark
            }
        }
    }
}
for ($y = 1; $y -le 3; $y++) {
    foreach ($z in 9,10) { Remove-Cell 20 $y $z }
}
Set-Cell 20 1 9 'WoodenDoorBlock' 2

# Rectangular spiral: ascending direction codes are +Z=2, -Z=0, +X=3, -X=1.
$spiral = @(
    @(18,11,3), @(19,11,3), @(20,11,3), @(21,11,3), @(22,11,2),
    @(22,12,2), @(22,13,2), @(22,14,2), @(22,15,1),
    @(21,15,1), @(20,15,1), @(19,15,1), @(18,15,0),
    @(18,14,0), @(18,13,0)
)
for ($i = 0; $i -lt $spiral.Count; $i++) {
    Set-Cell $spiral[$i][0] ($i + 1) $spiral[$i][1] 'WoodenStairsBlock' $spiral[$i][2]
}
# Warm stairwell lighting and a contrasting central spine.
for ($y = 2; $y -le 14; $y += 3) { Set-Cell 20 $y 13 'WickerLampBlock' }
for ($y = 0; $y -le 14; $y++) { Set-Cell 20 $y 17 'BirchWoodBlock' }

# Evergreen crown close to the wood shell (Chebyshev distance <= 3).
for ($y = 12; $y -le 14; $y++) {
    for ($z = 6; $z -le 20; $z++) {
        for ($x = 13; $x -le 27; $x++) {
            $outsideTrunk = $x -lt 16 -or $x -gt 24 -or $z -lt 9 -or $z -gt 17
            $softEdge = [Math]::Abs($x - 20) + [Math]::Abs($z - 13) -le (11 - ($y - 12))
            if ($outsideTrunk -and $softEdge -and (($x + 2 * $z + $y) % 3 -ne 0)) {
                Set-Cell $x $y $z 'SpruceLeavesBlock'
            }
        }
    }
}

# --- Owl body/head shell. It faces toward decreasing Z. ---
$layerWidths = @{
    15 = @(14,26); 16 = @(12,28); 17 = @(11,29); 18 = @(11,29)
    19 = @(11,29); 20 = @(11,29); 21 = @(11,29); 22 = @(9,31)
    23 = @(9,31); 24 = @(9,31); 25 = @(9,31); 26 = @(10,30)
    27 = @(11,29); 28 = @(12,28)
}
foreach ($y in 15..28) {
    $x0 = $layerWidths[$y][0]; $x1 = $layerWidths[$y][1]
    for ($z = 7; $z -le 21; $z++) {
        for ($x = $x0; $x -le $x1; $x++) {
            $isBottom = $y -eq 15
            $isTop = $y -eq 28
            $isWall = $x -eq $x0 -or $x -eq $x1 -or $z -eq 7 -or $z -eq 21
            if ($isBottom -or $isTop -or $isWall) {
                $key = 'PlanksBlock'
                if ($isBottom) { $key = 'StoneBrickBlock' }
                elseif ($isTop) { $key = 'BasaltBlock' }
                elseif ($z -eq 7 -and (($x + $y) % 5 -eq 0)) { $key = 'BricksBlock' }
                Set-Cell $x $y $z $key
            }
        }
    }
}

# Open the stairwell into the first floor and protect it with a railing.
for ($z = 11; $z -le 15; $z++) {
    for ($x = 18; $x -le 22; $x++) { Remove-Cell $x 15 $z }
}
foreach ($p in @(@(17,11),@(17,12),@(17,13),@(17,14),@(17,15),@(23,11),@(23,12),@(23,13),@(23,14),@(23,15),@(18,10),@(19,10),@(20,10),@(21,10),@(22,10),@(18,16),@(19,16),@(20,16),@(21,16),@(22,16))) {
    Set-Cell $p[0] 16 $p[1] 'WoodenFenceBlock' 15
}

# Partial loft: two sleeping wings and an open central atrium.
for ($z = 9; $z -le 19; $z++) {
    foreach ($x in 12..17) { Set-Cell $x 21 $z 'WoodenSlabBlock' 1 }
    foreach ($x in 23..28) { Set-Cell $x 21 $z 'WoodenSlabBlock' 1 }
}
foreach ($z in 10..18) {
    Set-Cell 18 22 $z 'WoodenFenceBlock' 15
    Set-Cell 22 22 $z 'WoodenFenceBlock' 15
}

# Front owl face: white eye discs, dark masks, blue glass pupils, warm lamp cores.
foreach ($eyeX in 14,26) {
    for ($dy = -2; $dy -le 2; $dy++) {
        for ($dx = -3; $dx -le 3; $dx++) {
            if (($dx * $dx + $dy * $dy) -le 9) {
                $key = if (($dx * $dx + $dy * $dy) -ge 6) { 'CoalBlock' } else { 'MarbleBlock' }
                Set-Cell ($eyeX + $dx) (24 + $dy) 7 $key
            }
        }
    }
    Set-Cell $eyeX 24 6 'WickerLampBlock'
    Set-Cell ($eyeX - 1) 24 6 'DiamondBlock'
    Set-Cell ($eyeX + 1) 24 6 'FramedGlassBlock'
}
# Brows, copper beak, cheek color and chest-feather mosaic.
foreach ($x in 11..17) {
    $browY = if ($x -le 14) { 27 } else { 26 }
    Set-Cell $x $browY 6 'BasaltBlock'
}
foreach ($x in 23..29) {
    $browY = if ($x -ge 26) { 27 } else { 26 }
    Set-Cell $x $browY 6 'BasaltBlock'
}
Set-Cell 20 23 6 'CopperBlock'; Set-Cell 19 22 6 'CopperBlock'; Set-Cell 20 22 6 'CopperBlock'; Set-Cell 21 22 6 'CopperBlock'; Set-Cell 20 21 5 'CopperBlock'
foreach ($p in @(@(12,21),@(13,20),@(14,19),@(28,21),@(27,20),@(26,19))) { Set-Cell $p[0] $p[1] 6 'MalachiteBlock' }
for ($y = 16; $y -le 20; $y++) {
    for ($x = 15; $x -le 25; $x++) {
        if ((($x + $y) % 4) -eq 0) {
            $key = @('BricksBlock','CopperBlock','MalachiteBlock','MarbleBlock')[($x + 2 * $y) % 4]
            Set-Cell $x $y 7 $key
        }
    }
}

# Ear tufts and roof feather ridges.
for ($y = 29; $y -le 31; $y++) {
    $inset = $y - 29
    foreach ($x in (10 + $inset)..(14 - $inset)) { Set-Cell $x $y 10 'BasaltBlock' }
    foreach ($x in (26 + $inset)..(30 - $inset)) { Set-Cell $x $y 10 'BasaltBlock' }
}
foreach ($x in 12..28) {
    $ridgeDirection = if ($x -lt 20) { 3 } else { 1 }
    Set-Cell $x 29 13 'WoodenStairsBlock' $ridgeDirection
}

# Rear and side windows; they remain sealed with glass.
foreach ($x in 14..17) { foreach ($y in 18..19) { Set-Cell $x $y 21 'FramedGlassBlock' } }
foreach ($x in 23..26) { foreach ($y in 18..19) { Set-Cell $x $y 21 'FramedGlassBlock' } }
foreach ($z in 11..14) { foreach ($y in 18..19) { Set-Cell 11 $y $z 'GlassBlock'; Set-Cell 29 $y $z 'GlassBlock' } }

# --- Spread wings as usable terraces. ---
for ($z = 9; $z -le 19; $z++) {
    $taper = [Math]::Abs($z - 14) / 2
    $leftMin = [int][Math]::Floor($taper)
    $rightMax = 40 - $leftMin
    for ($x = $leftMin; $x -le 10; $x++) { Set-Cell $x 18 $z 'WoodenSlabBlock' 0 }
    for ($x = 30; $x -le $rightMax; $x++) { Set-Cell $x 18 $z 'WoodenSlabBlock' 0 }
    Set-Cell $leftMin 19 $z 'StoneFenceBlock' 15
    Set-Cell $rightMax 19 $z 'StoneFenceBlock' 15
}
foreach ($x in 2..10) { Set-Cell $x 19 9 'WoodenFenceBlock' 15; Set-Cell $x 19 19 'WoodenFenceBlock' 15 }
foreach ($x in 30..38) { Set-Cell $x 19 9 'WoodenFenceBlock' 15; Set-Cell $x 19 19 'WoodenFenceBlock' 15 }
# Wing-to-house doors; only bottom halves are authored.
foreach ($y in 19,20) { Remove-Cell 11 $y 14; Remove-Cell 29 $y 14 }
Set-Cell 11 19 14 'WoodenDoorBlock' 3
Set-Cell 29 19 14 'WoodenDoorBlock' 1
# Vertical wood supports keep the wings visibly tied to the tree without unsupported horizontal logs.
foreach ($p in @(@(7,12),@(7,17),@(33,12),@(33,17))) {
    for ($y = 13; $y -le 17; $y++) { Set-Cell $p[0] $y $p[1] 'OakWoodBlock' }
}

# --- Interior: dining table, chairs, reading desks, lamps and loft beds. ---
foreach ($p in @(@(15,17),@(16,17),@(17,17),@(23,17),@(24,17),@(25,17))) {
    Set-Cell $p[0] 16 $p[1] 'WoodenFenceBlock' 15
    Set-Cell $p[0] 17 $p[1] 'WoodenSlabBlock' 1
}
foreach ($p in @(@(14,17,3),@(18,17,1),@(22,17,3),@(26,17,1),@(16,15,2),@(24,15,2))) {
    Set-Cell $p[0] 16 $p[1] 'WoodenStairsBlock' $p[2]
}
# Central low table with a warm light feature.
foreach ($x in 19..21) { foreach ($z in 18..19) { Set-Cell $x 16 $z 'StoneSlabBlock' 1 } }
Set-Cell 20 17 19 'WickerLampBlock'
# Loft sleeping platforms and bedside lamps (decorative blocks only, no entities).
foreach ($x in 13..16) { Set-Cell $x 22 15 'MarbleBlock'; Set-Cell $x 23 15 'BricksBlock' }
foreach ($x in 24..27) { Set-Cell $x 22 15 'MarbleBlock'; Set-Cell $x 23 15 'MalachiteBlock' }
Set-Cell 13 23 16 'WickerLampBlock'; Set-Cell 27 23 16 'WickerLampBlock'
# Interior color panels and ceiling lights.
foreach ($p in @(@(12,18,18,'CopperBlock'),@(12,19,18,'DiamondBlock'),@(28,18,18,'MalachiteBlock'),@(28,19,18,'MarbleBlock'))) {
    Set-Cell $p[0] $p[1] $p[2] $p[3]
}
foreach ($p in @(@(15,20,10),@(25,20,10),@(15,20,18),@(25,20,18),@(20,26,14))) { Set-Cell $p[0] $p[1] $p[2] 'WickerLampBlock' }

# Normalize all coordinates to a zero minimum and emit deterministic y/z/x order.
$values = @($cells.Values)
$minX = ($values | Measure-Object x -Minimum).Minimum
$minY = ($values | Measure-Object y -Minimum).Minimum
$minZ = ($values | Measure-Object z -Minimum).Minimum
$normalized = foreach ($cell in $values) {
    [pscustomobject]@{ x = $cell.x - $minX; y = $cell.y - $minY; z = $cell.z - $minZ; key = $cell.key; data = $cell.data }
}
$normalized = @($normalized | Sort-Object y,z,x)
$maxX = ($normalized | Measure-Object x -Maximum).Maximum
$maxY = ($normalized | Measure-Object y -Maximum).Maximum
$maxZ = ($normalized | Measure-Object z -Maximum).Maximum
if ($normalized.Count -gt 10000) { throw "Blueprint exceeds 10000 cells: $($normalized.Count)" }
if (($maxX + 1) -gt 64 -or ($maxY + 1) -gt 64 -or ($maxZ + 1) -gt 64) { throw 'Blueprint exceeds 64 cells on an axis.' }

$jsonMaterials = foreach ($material in $materials) {
    [ordered]@{ id = $material.id; key = $material.key; block = $material.block }
}
$jsonCells = foreach ($cell in $normalized) {
    $material = $materialByKey[$cell.key]
    $out = [ordered]@{ x = $cell.x; y = $cell.y; z = $cell.z; m = $material.id }
    if ($material.family -ne 'cube') { $out.f = $material.family }
    if ($cell.data -ne 0) { $out.d = $cell.data }
    $out
}
$document = [ordered]@{
    schemaVersion = 2
    kind = 'builderassistant.blueprint'
    name = 'Owl Treehouse - Wingspan Roost'
    size = [ordered]@{ x = $maxX + 1; y = $maxY + 1; z = $maxZ + 1 }
    materials = @($jsonMaterials)
    cells = @($jsonCells)
}

$absoluteOutput = if ([IO.Path]::IsPathRooted($OutputPath)) {
    [IO.Path]::GetFullPath($OutputPath)
} else {
    [IO.Path]::GetFullPath((Join-Path ([string](Get-Location)) $OutputPath))
}
$outputDirectory = [IO.Path]::GetDirectoryName($absoluteOutput)
if (-not [IO.Directory]::Exists($outputDirectory)) { [IO.Directory]::CreateDirectory($outputDirectory) | Out-Null }
$json = $document | ConvertTo-Json -Depth 8
[IO.File]::WriteAllText($absoluteOutput, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

$familyCounts = $jsonCells | Group-Object { $materials[$_.m].family } | Sort-Object Name
Write-Host "Wrote $absoluteOutput"
Write-Host "Cells: $($normalized.Count); size: $($maxX + 1)x$($maxY + 1)x$($maxZ + 1)"
Write-Host (($familyCounts | ForEach-Object { "$($_.Name)=$($_.Count)" }) -join ', ')
