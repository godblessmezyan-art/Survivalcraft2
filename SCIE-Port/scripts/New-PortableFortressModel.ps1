[CmdletBinding()]
param(
    [string]$ModelPath,
    [string]$PreviewDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ModelPath) { $ModelPath = Join-Path $repoRoot 'src\SCIEPort\Assets\Models\PortableFortress.dae' }
if (-not $PreviewDirectory) { $PreviewDirectory = Join-Path $repoRoot 'docs\portable-fortress\previews' }
$modelDirectory = Split-Path -Parent $ModelPath
New-Item -ItemType Directory -Force -Path $modelDirectory, $PreviewDirectory | Out-Null

$culture = [Globalization.CultureInfo]::InvariantCulture
function F([double]$value) { return $value.ToString('0.######', $culture) }

$groups = [ordered]@{}
$parts = New-Object System.Collections.Generic.List[object]

function Get-Group([string]$name) {
    if (-not $groups.Contains($name)) {
        $groups[$name] = [pscustomobject]@{
            Positions = New-Object System.Collections.Generic.List[double]
            Normals   = New-Object System.Collections.Generic.List[double]
            UVs       = New-Object System.Collections.Generic.List[double]
            Indices   = New-Object System.Collections.Generic.List[int]
            Triangles = 0
        }
    }
    return $groups[$name]
}

function Add-Box(
    [string]$group, [string]$name,
    [double]$cx, [double]$cy, [double]$cz,
    [double]$sx, [double]$sy, [double]$sz,
    [double]$yaw = 0
) {
    if ($sx -le 0 -or $sy -le 0 -or $sz -le 0) { throw "Invalid box size for $name" }
    $g = Get-Group $group
    $hx = $sx / 2; $hy = $sy / 2; $hz = $sz / 2
    $faces = @(
        @(@(-$hx,-$hy,-$hz),@(-$hx,$hy,-$hz),@($hx,$hy,-$hz),@($hx,-$hy,-$hz),@(0,0,-1)),
        @(@(-$hx,-$hy,$hz),@($hx,-$hy,$hz),@($hx,$hy,$hz),@(-$hx,$hy,$hz),@(0,0,1)),
        @(@(-$hx,-$hy,$hz),@(-$hx,$hy,$hz),@(-$hx,$hy,-$hz),@(-$hx,-$hy,-$hz),@(-1,0,0)),
        @(@($hx,-$hy,-$hz),@($hx,$hy,-$hz),@($hx,$hy,$hz),@($hx,-$hy,$hz),@(1,0,0)),
        @(@(-$hx,-$hy,$hz),@(-$hx,-$hy,-$hz),@($hx,-$hy,-$hz),@($hx,-$hy,$hz),@(0,-1,0)),
        @(@(-$hx,$hy,-$hz),@(-$hx,$hy,$hz),@($hx,$hy,$hz),@($hx,$hy,-$hz),@(0,1,0))
    )
    $radians = $yaw * [Math]::PI / 180.0
    $cos = [Math]::Cos($radians); $sin = [Math]::Sin($radians)
    $uv = @(@(0,0),@(0,1),@(1,1),@(1,0))
    foreach ($face in $faces) {
        $base = [int]($g.Positions.Count / 3)
        for ($i = 0; $i -lt 4; $i++) {
            $p = $face[$i]
            $x = $p[0] * $cos - $p[2] * $sin + $cx
            $z = $p[0] * $sin + $p[2] * $cos + $cz
            $g.Positions.Add($x); $g.Positions.Add($p[1] + $cy); $g.Positions.Add($z)
            $n = $face[4]
            $nx = $n[0] * $cos - $n[2] * $sin
            $nz = $n[0] * $sin + $n[2] * $cos
            $g.Normals.Add($nx); $g.Normals.Add([double]$n[1]); $g.Normals.Add($nz)
            $g.UVs.Add([double]$uv[$i][0]); $g.UVs.Add([double]$uv[$i][1])
        }
        foreach ($index in @($base,($base+1),($base+2),$base,($base+2),($base+3))) {
            $g.Indices.Add($index); $g.Indices.Add($index); $g.Indices.Add($index)
        }
        $g.Triangles += 2
    }
    $parts.Add([pscustomobject]@{ Group=$group; Name=$name; X=$cx; Y=$cy; Z=$cz; SX=$sx; SY=$sy; SZ=$sz; Yaw=$yaw })
}

# Overall envelope: X [-5.5,5.5], Z [-6.5,6.5], Y [0,9]. Front is -Z.
# Foundation and lower armored footprint: full-length center spine, broad side shoulders and chamfer braces.
Add-Box Foundation CenterKeel 0 0.18 0 8.0 0.36 13.0
Add-Box Foundation ShoulderL -4.75 0.18 0 1.5 0.36 9.6
Add-Box Foundation ShoulderR 4.75 0.18 0 1.5 0.36 9.6
foreach ($z in @(-5.55,5.55)) {
    Add-Box Foundation "ChamferL$z" -4.35 0.18 $z 1.55 0.36 0.85 $(if($z -lt 0){45}else{-45})
    Add-Box Foundation "ChamferR$z" 4.35 0.18 $z 1.55 0.36 0.85 $(if($z -lt 0){-45}else{45})
}

# Lower shell. Rear door opening is 2.5 wide x 2.75 high. Corner panels make a faceted silhouette.
Add-Box LowerShell RearWallL -3.35 1.9 6.35 4.3 3.1 0.30
Add-Box LowerShell RearWallR 3.35 1.9 6.35 4.3 3.1 0.30
Add-Box LowerShell RearDoorHeader 0 3.25 6.35 2.4 0.4 0.30
Add-Box LowerShell SideWallL -5.35 1.9 0 0.30 3.1 9.6
Add-Box LowerShell SideWallR 5.35 1.9 0 0.30 3.1 9.6
Add-Box LowerShell FrontWall 0 1.9 -6.35 7.6 3.1 0.30
foreach ($side in @(-1,1)) {
    Add-Box LowerShell "FrontChamfer$side" ($side*4.65) 1.9 -5.55 2.05 3.1 0.30 ($side*-45)
    Add-Box LowerShell "RearChamfer$side" ($side*4.65) 1.9 5.55 2.05 3.1 0.30 ($side*45)
    Add-Box Armor "LowerButtress$side" ($side*5.30) 1.45 0 0.40 2.5 1.25
}

# First-floor fixed zoning guides and equipment volumes. Central 4x4 activity square remains empty.
Add-Box InteriorLower AirlockWallL -1.45 1.75 5.05 0.16 2.75 2.35
Add-Box InteriorLower AirlockWallR 1.45 1.75 5.05 0.16 2.75 2.35
Add-Box InteriorLower AirlockInnerHeader 0 3.05 3.85 2.9 0.24 0.18
Add-Box InteriorLower HeatingPartition -3.0 1.75 2.75 0.16 2.75 3.1
Add-Box InteriorLower HeaterPlinth -4.0 0.55 3.1 1.6 0.75 1.6
Add-Box InteriorLower StorageRackA 3.75 1.35 3.2 1.5 2.0 2.0
Add-Box InteriorLower StorageRackB 3.75 1.35 1.0 1.5 2.0 2.0
Add-Box InteriorLower Workbench 3.65 0.85 -3.15 2.0 1.0 1.25
Add-Box InteriorLower BunkLower -3.95 0.75 0.1 1.55 0.65 2.7
Add-Box InteriorLower BunkUpper -3.95 1.95 0.1 1.55 0.40 2.7

# Eight-step staircase, 2.1 wide, 4.0 run, 3.6 rise. Final collision should use terrain stairs or matching proxies.
for ($i = 0; $i -lt 8; $i++) {
    $height = 0.45 * ($i + 1)
    Add-Box Stair "Step$($i+1)" -3.25 ($height/2 + 0.36) (-3.75 + $i*0.5) 2.1 $height 0.50
}

# Second-floor deck has a true 2.1 x 4.25 stairwell opening at X[-4.3,-2.2], Z[-4,0.25].
Add-Box UpperDeck UpperDeckMain 1.025 3.72 -0.5 6.45 0.24 10.9
Add-Box UpperDeck UpperDeckLeftFront -3.25 3.72 -4.975 2.1 0.24 1.95
Add-Box UpperDeck UpperDeckLeftRear -3.25 3.72 2.6 2.1 0.24 4.7

# Upper shell narrows to 8.8 wide. Side walls are segmented to leave two real window openings per side.
foreach ($side in @(-1,1)) {
    $x = $side * 4.25
    Add-Box UpperShell "SideSill$side" $x 4.28 0 0.25 0.85 9.6
    Add-Box UpperShell "SideHeader$side" $x 6.82 0 0.25 0.75 9.6
    foreach ($z in @(-4.65,-2.45,-0.35,1.85,4.65)) { Add-Box UpperShell "SidePillar$side-$z" $x 5.55 $z 0.25 1.8 0.34 }
}
Add-Box UpperShell RearUpperL -2.75 5.55 4.95 3.1 3.3 0.25
Add-Box UpperShell RearUpperR 2.75 5.55 4.95 3.1 3.3 0.25
Add-Box UpperShell RearUpperHeader 0 6.82 4.95 2.4 0.75 0.25

# Panoramic observation nose: front and diagonal frames only, with actual empty apertures.
Add-Box Observation FrontSill 0 4.30 -6.35 6.4 0.82 0.24
Add-Box Observation FrontHeader 0 6.82 -6.35 6.4 0.75 0.24
foreach ($x in @(-3.15,0,3.15)) { Add-Box Observation "FrontPillar$x" $x 5.55 -6.35 0.24 1.8 0.24 }
foreach ($side in @(-1,1)) {
    Add-Box Observation "NoseSill$side" ($side*3.85) 4.30 -5.65 1.95 0.82 0.24 ($side*-42)
    Add-Box Observation "NoseHeader$side" ($side*3.85) 6.82 -5.65 1.95 0.75 0.24 ($side*-42)
    Add-Box Observation "NoseOuterPillar$side" ($side*4.42) 5.55 -4.98 0.24 1.8 0.24 ($side*-42)
}
Add-Box InteriorUpper CommandDesk 0 4.35 -3.95 4.0 0.85 0.9
Add-Box InteriorUpper RestBenchL -3.35 4.35 2.5 1.25 0.75 3.2
Add-Box InteriorUpper RestBenchR 3.35 4.35 2.5 1.25 0.75 3.2
# Reserved roof-access volume X[2.325,3.975] Z[2.725,4.375], deck (3.84) to roof (7.15).
# The whitebox filled it with one solid box, which made the reserved ladder volume
# impassable. The volume now carries a real rung stack plus two side rails so the
# hatch above is reachable; envelope, program and every other part are unchanged.
foreach ($rung in 0..9) {
    $ry = 4.05 + $rung * 0.31
    Add-Box InteriorUpper "RoofAccessRung$rung" 3.15 $ry 3.55 1.20 0.09 0.14
}
foreach ($x in @(2.50,3.80)) { Add-Box InteriorUpper "RoofAccessRail$x" $x 5.60 3.55 0.10 3.30 0.10 }

# Roof deck is split around the true hatch opening X[2.3,4], Z[2.7,4.4].
# The beacon datum reaches exactly Y=9.
Add-Box Roof RoofDeckMain -0.9875 7.28 -0.45 6.575 0.26 10.9
Add-Box Roof RoofDeckRightFront 3.1375 7.28 -1.6 1.675 0.26 8.6
Add-Box Roof RoofDeckRightRear 3.1375 7.28 4.7 1.675 0.26 0.6
Add-Box Roof HatchCurbFront 3.15 7.55 2.70 1.8 0.28 0.18
Add-Box Roof HatchCurbRear 3.15 7.55 4.40 1.8 0.28 0.18
Add-Box Roof HatchCurbL 2.30 7.55 3.55 0.18 0.28 1.8
Add-Box Roof HatchCurbR 4.00 7.55 3.55 0.18 0.28 1.8
Add-Box Roof EquipmentPad 0 7.48 2.35 3.8 0.20 3.0
foreach ($x in @(-4.1,4.1)) { foreach ($z in @(-5.55,-1.8,1.8,4.55)) { Add-Box RoofRails "RailPost$x-$z" $x 8.05 $z 0.16 1.1 0.16 } }
Add-Box RoofRails RailFront 0 8.47 -5.55 8.35 0.16 0.16
Add-Box RoofRails RailRear -1.05 8.47 4.55 6.25 0.16 0.16
Add-Box RoofRails RailLeft -4.1 8.47 -0.5 0.16 0.16 10.1
Add-Box RoofRails RailRight 4.1 8.47 -0.5 0.16 0.16 10.1
Add-Box Roof BeaconDatum 0 8.90 2.35 0.35 0.20 0.35

$xml = New-Object Text.StringBuilder
[void]$xml.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$xml.AppendLine('<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">')
[void]$xml.AppendLine('  <asset><contributor><authoring_tool>SCIE-Port Portable Fortress Model Generator</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>')
[void]$xml.AppendLine('  <library_effects><effect id="WhiteboxEffect"><profile_COMMON><technique sid="common"><lambert><diffuse><color>0.72 0.75 0.78 1</color></diffuse></lambert></technique></profile_COMMON></effect></library_effects>')
[void]$xml.AppendLine('  <library_materials><material id="WhiteboxMaterial" name="WhiteboxMaterial"><instance_effect url="#WhiteboxEffect"/></material></library_materials>')
[void]$xml.AppendLine('  <library_geometries>')
foreach ($entry in $groups.GetEnumerator()) {
    $name = $entry.Key; $g = $entry.Value
    $positions = ($g.Positions | ForEach-Object { F $_ }) -join ' '
    $normals = ($g.Normals | ForEach-Object { F $_ }) -join ' '
    $uvs = ($g.UVs | ForEach-Object { F $_ }) -join ' '
    $indices = ($g.Indices | ForEach-Object { $_.ToString($culture) }) -join ' '
    $vertexCount = [int]($g.Positions.Count / 3)
    [void]$xml.AppendLine(('    <geometry id="{0}-mesh" name="{0}"><mesh>' -f $name))
    [void]$xml.AppendLine(('      <source id="{0}-positions"><float_array id="{0}-positions-array" count="{1}">{2}</float_array><technique_common><accessor source="#{0}-positions-array" count="{3}" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>' -f $name,$g.Positions.Count,$positions,$vertexCount))
    [void]$xml.AppendLine(('      <source id="{0}-normals"><float_array id="{0}-normals-array" count="{1}">{2}</float_array><technique_common><accessor source="#{0}-normals-array" count="{3}" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>' -f $name,$g.Normals.Count,$normals,$vertexCount))
    [void]$xml.AppendLine(('      <source id="{0}-uv"><float_array id="{0}-uv-array" count="{1}">{2}</float_array><technique_common><accessor source="#{0}-uv-array" count="{3}" stride="2"><param name="S" type="float"/><param name="T" type="float"/></accessor></technique_common></source>' -f $name,$g.UVs.Count,$uvs,$vertexCount))
    [void]$xml.AppendLine(('      <vertices id="{0}-vertices"><input semantic="POSITION" source="#{0}-positions"/></vertices>' -f $name))
    [void]$xml.AppendLine(('      <triangles material="WhiteboxMaterial-symbol" count="{1}"><input semantic="VERTEX" source="#{0}-vertices" offset="0"/><input semantic="NORMAL" source="#{0}-normals" offset="1"/><input semantic="TEXCOORD" source="#{0}-uv" offset="2" set="0"/><p>{2}</p></triangles>' -f $name,$g.Triangles,$indices))
    [void]$xml.AppendLine('    </mesh></geometry>')
}
[void]$xml.AppendLine('  </library_geometries>')
[void]$xml.AppendLine('  <library_visual_scenes><visual_scene id="Scene" name="Scene"><node id="PortableFortress" name="PortableFortress" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>')
foreach ($name in $groups.Keys) { [void]$xml.AppendLine(('    <instance_geometry url="#{0}-mesh" name="{0}"><bind_material><technique_common><instance_material symbol="WhiteboxMaterial-symbol" target="#WhiteboxMaterial"/></technique_common></bind_material></instance_geometry>' -f $name)) }
[void]$xml.AppendLine('  </node></visual_scene></library_visual_scenes><scene><instance_visual_scene url="#Scene"/></scene></COLLADA>')
[IO.File]::WriteAllText($ModelPath, $xml.ToString(), (New-Object Text.UTF8Encoding($false)))

Add-Type -AssemblyName System.Drawing
$palette = @{
    Foundation=[Drawing.Color]::FromArgb(75,86,96); LowerShell=[Drawing.Color]::FromArgb(95,110,122); Armor=[Drawing.Color]::FromArgb(119,91,57)
    InteriorLower=[Drawing.Color]::FromArgb(194,142,70); Stair=[Drawing.Color]::FromArgb(190,170,110); UpperDeck=[Drawing.Color]::FromArgb(75,86,96)
    UpperShell=[Drawing.Color]::FromArgb(104,121,134); Observation=[Drawing.Color]::FromArgb(84,154,174); InteriorUpper=[Drawing.Color]::FromArgb(194,142,70)
    Roof=[Drawing.Color]::FromArgb(75,86,96); RoofRails=[Drawing.Color]::FromArgb(151,112,61)
}
function New-Canvas([string]$file, [string]$title, [scriptblock]$draw) {
    $bitmap = New-Object Drawing.Bitmap 1400,1000
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(23,28,33)); $graphics.SmoothingMode = 'AntiAlias'
        $font = [Drawing.Font]::new('Arial',22,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
        $small = [Drawing.Font]::new('Arial',14,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
        $graphics.DrawString($title,$font,[Drawing.Brushes]::White,35,25)
        $graphics.DrawString('Portable Fortress Mk.I whitebox | 11 W x 13 L x 9 H | front = -Z',$small,[Drawing.Brushes]::LightGray,35,62)
        & $draw $graphics $small
        $bitmap.Save($file,[Drawing.Imaging.ImageFormat]::Png)
        $font.Dispose(); $small.Dispose()
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
function Draw-Grid($g,[double]$ox,[double]$oy,[double]$scale,[int]$w,[int]$h) {
    $pen = New-Object Drawing.Pen ([Drawing.Color]::FromArgb(45,58,66)),1
    for($i=0;$i -le $w;$i++){ $g.DrawLine($pen,[float]($ox+$i*$scale),[float]$oy,[float]($ox+$i*$scale),[float]($oy+$h*$scale)) }
    for($i=0;$i -le $h;$i++){ $g.DrawLine($pen,[float]$ox,[float]($oy+$i*$scale),[float]($ox+$w*$scale),[float]($oy+$i*$scale)) }
    $pen.Dispose()
}

New-Canvas (Join-Path $PreviewDirectory 'front.png') 'FRONT ELEVATION' {
    param($g,$font) $s=82; $ox=700; $base=900; Draw-Grid $g 249 162 $s 11 9
    foreach($p in $parts){ if([Math]::Abs($p.Z) -lt 4.9 -and $p.Group -notmatch 'Interior'){continue}; $x=$ox+($p.X-$p.SX/2)*$s; $y=$base-($p.Y+$p.SY/2)*$s; $b=New-Object Drawing.SolidBrush $palette[$p.Group]; $g.FillRectangle($b,[float]$x,[float]$y,[float]($p.SX*$s),[float]($p.SY*$s)); $b.Dispose() }
    $g.DrawString('Panoramic window openings are empty geometry, not painted glass.',$font,[Drawing.Brushes]::LightCyan,390,940)
}
New-Canvas (Join-Path $PreviewDirectory 'side.png') 'RIGHT SIDE ELEVATION' {
    param($g,$font) $s=72; $oz=700; $base=900; Draw-Grid $g 232 162 $s 13 9
    foreach($p in $parts){ if([Math]::Abs($p.X) -lt 3.9 -and $p.Group -notmatch 'Interior|Stair'){continue}; $x=$oz+($p.Z-$p.SZ/2)*$s; $y=$base-($p.Y+$p.SY/2)*$s; $b=New-Object Drawing.SolidBrush $palette[$p.Group]; $g.FillRectangle($b,[float]$x,[float]$y,[float]($p.SZ*$s),[float]($p.SY*$s)); $b.Dispose() }
    $g.DrawString('Lower armored mass / recessed upper deck / roof guard rail',$font,[Drawing.Brushes]::LightGray,420,940)
}
New-Canvas (Join-Path $PreviewDirectory 'top.png') 'TOP / ROOF PLAN' {
    param($g,$font) $s=66; $ox=700; $oz=520; Draw-Grid $g 337 91 $s 11 13
    foreach($p in $parts){ if($p.Y -lt 7.1){continue}; $points=[Drawing.PointF[]]::new(4); $hx=$p.SX/2; $hz=$p.SZ/2; $a=$p.Yaw*[Math]::PI/180; $c=[Math]::Cos($a);$sn=[Math]::Sin($a);$corners=@(@(-$hx,-$hz),@(-$hx,$hz),@($hx,$hz),@($hx,-$hz)); for($i=0;$i -lt 4;$i++){ $xx=$corners[$i][0]*$c-$corners[$i][1]*$sn+$p.X; $zz=$corners[$i][0]*$sn+$corners[$i][1]*$c+$p.Z; $points[$i]=[Drawing.PointF]::new([float]($ox+$xx*$s),[float]($oz+$zz*$s)) }; $b=New-Object Drawing.SolidBrush $palette[$p.Group]; $g.FillPolygon($b,$points);$b.Dispose() }
    $g.DrawString('Equipment reserve 3.8 x 3.0',$font,[Drawing.Brushes]::Orange,555,650); $g.DrawString('Roof hatch',$font,[Drawing.Brushes]::LightCyan,850,750)
}
New-Canvas (Join-Path $PreviewDirectory 'section.png') 'LONGITUDINAL SECTION / PROGRAM' {
    param($g,$font) $pen=New-Object Drawing.Pen ([Drawing.Color]::LightGray),4; $g.DrawRectangle($pen,165,150,1070,720); $g.DrawLine($pen,165,440,1235,440); $g.DrawLine($pen,165,720,1235,720)
    $g.DrawString('ROOF: guarded observation platform + equipment reserve + hatch',$font,[Drawing.Brushes]::LightGoldenrodYellow,250,205)
    $g.DrawString('LEVEL 2: command/rest | panoramic nose | side windows | roof access',$font,[Drawing.Brushes]::LightCyan,250,500)
    $g.DrawString('LEVEL 1: airlock | bunks | heater | 48-slot store | workbench | 4x4 clear area | stair',$font,[Drawing.Brushes]::Wheat,205,780)
    $g.DrawString('Front -Z',$font,[Drawing.Brushes]::OrangeRed,170,900); $g.DrawString('Rear +Z',$font,[Drawing.Brushes]::OrangeRed,1110,900); $pen.Dispose()
}

$totalTriangles = ($groups.Values | Measure-Object -Property Triangles -Sum).Sum
Write-Host "Generated: $ModelPath"
Write-Host "Groups/Meshes: $($groups.Count)"
Write-Host "Parts: $($parts.Count)"
Write-Host "Triangles: $totalTriangles"
Write-Host "Previews: $PreviewDirectory"
