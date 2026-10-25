[CmdletBinding()]
param([string]$ProjectRoot)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = Split-Path -Parent $PSScriptRoot }
$modelPath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Models\Helicopter.dae'
$texturePath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Textures\Helicopter.png'
$audioDirectory = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Audio'
$audioPath = Join-Path $audioDirectory 'SCIEPortHelicopterLoop.wav'
$invariant = [Globalization.CultureInfo]::InvariantCulture
$paletteCount = 12

function Format-Number([double]$value) { $value.ToString('0.######', $invariant) }
function New-Mesh([string]$name) {
    [pscustomobject]@{ Name=$name; Positions=[Collections.Generic.List[double]]::new(); Normals=[Collections.Generic.List[double]]::new(); UVs=[Collections.Generic.List[double]]::new(); Indices=[Collections.Generic.List[int]]::new(); Triangles=0 }
}
function Subtract-Vector($a,$b) { @((([double]$a[0])-([double]$b[0])),(([double]$a[1])-([double]$b[1])),(([double]$a[2])-([double]$b[2]))) }
function Cross-Vector($a,$b) { @((([double]$a[1])*$b[2]-([double]$a[2])*$b[1]),(([double]$a[2])*$b[0]-([double]$a[0])*$b[2]),(([double]$a[0])*$b[1]-([double]$a[1])*$b[0])) }
function Normalize-Vector($v) { [double]$x=$v[0]; [double]$y=$v[1]; [double]$z=$v[2]; [double]$length=[Math]::Sqrt($x*$x+$y*$y+$z*$z); if($length -lt 0.000001){return @(0.0,1.0,0.0)}; @(($x/$length),($y/$length),($z/$length)) }
function Add-Triangle($mesh,$a,$b,$c,[int]$material) {
    $normal=Normalize-Vector (Cross-Vector (Subtract-Vector $b $a) (Subtract-Vector $c $a)); $u=([double]$material+0.5)/$paletteCount
    $points=[object[]]::new(3); $points[0]=$a; $points[1]=$b; $points[2]=$c
    foreach($point in $points){ $index=[int]($mesh.Positions.Count/3); foreach($value in $point){$mesh.Positions.Add([double]$value)}; foreach($value in $normal){$mesh.Normals.Add([double]$value)}; $mesh.UVs.Add($u); $mesh.UVs.Add(0.5); $mesh.Indices.Add($index); $mesh.Indices.Add($index); $mesh.Indices.Add($index) }
    $mesh.Triangles++
}
function Add-Quad($mesh,$a,$b,$c,$d,[int]$material) { Add-Triangle $mesh $a $b $c $material; Add-Triangle $mesh $a $c $d $material }
function Add-OrientedBox($mesh,$center,$axisX,$axisY,$axisZ,[double]$halfX,[double]$halfY,[double]$halfZ,[int]$material) {
    $signTriples=@([pscustomobject]@{X=-1;Y=-1;Z=-1},[pscustomobject]@{X=1;Y=-1;Z=-1},[pscustomobject]@{X=1;Y=1;Z=-1},[pscustomobject]@{X=-1;Y=1;Z=-1},[pscustomobject]@{X=-1;Y=-1;Z=1},[pscustomobject]@{X=1;Y=-1;Z=1},[pscustomobject]@{X=1;Y=1;Z=1},[pscustomobject]@{X=-1;Y=1;Z=1})
    $vertices=[object[]]::new(8); $vertexIndex=0
    foreach($signs in $signTriples){ [double]$vx=([double]$center[0])+$signs.X*$halfX*$axisX[0]+$signs.Y*$halfY*$axisY[0]+$signs.Z*$halfZ*$axisZ[0]; [double]$vy=([double]$center[1])+$signs.X*$halfX*$axisX[1]+$signs.Y*$halfY*$axisY[1]+$signs.Z*$halfZ*$axisZ[1]; [double]$vz=([double]$center[2])+$signs.X*$halfX*$axisX[2]+$signs.Y*$halfY*$axisY[2]+$signs.Z*$halfZ*$axisZ[2]; $vertices[$vertexIndex]=@($vx,$vy,$vz); $vertexIndex++ }
    Add-Quad $mesh $vertices[0] $vertices[1] $vertices[2] $vertices[3] $material; Add-Quad $mesh $vertices[4] $vertices[7] $vertices[6] $vertices[5] $material; Add-Quad $mesh $vertices[0] $vertices[3] $vertices[7] $vertices[4] $material; Add-Quad $mesh $vertices[1] $vertices[5] $vertices[6] $vertices[2] $material; Add-Quad $mesh $vertices[0] $vertices[4] $vertices[5] $vertices[1] $material; Add-Quad $mesh $vertices[3] $vertices[2] $vertices[6] $vertices[7] $material
}
function Add-Box($mesh,$minimum,$maximum,[int]$material) { [double]$cx=(([double]$minimum[0])+$maximum[0])/2; [double]$cy=(([double]$minimum[1])+$maximum[1])/2; [double]$cz=(([double]$minimum[2])+$maximum[2])/2; $center=@($cx,$cy,$cz); Add-OrientedBox $mesh $center @(1,0,0) @(0,1,0) @(0,0,1) (([double]$maximum[0]-$minimum[0])/2) (([double]$maximum[1]-$minimum[1])/2) (([double]$maximum[2]-$minimum[2])/2) $material }
function Add-FrustumZ($mesh,[double]$z0,[double]$z1,$center0,$center1,$half0,$half1,[int]$material) {
    $v=[object[]]::new(8)
    $v[0]=@((([double]$center0[0])-$half0[0]),(([double]$center0[1])-$half0[1]),$z0); $v[1]=@((([double]$center0[0])+$half0[0]),(([double]$center0[1])-$half0[1]),$z0); $v[2]=@((([double]$center0[0])+$half0[0]),(([double]$center0[1])+$half0[1]),$z0); $v[3]=@((([double]$center0[0])-$half0[0]),(([double]$center0[1])+$half0[1]),$z0)
    $v[4]=@((([double]$center1[0])-$half1[0]),(([double]$center1[1])-$half1[1]),$z1); $v[5]=@((([double]$center1[0])+$half1[0]),(([double]$center1[1])-$half1[1]),$z1); $v[6]=@((([double]$center1[0])+$half1[0]),(([double]$center1[1])+$half1[1]),$z1); $v[7]=@((([double]$center1[0])-$half1[0]),(([double]$center1[1])+$half1[1]),$z1)
    Add-Quad $mesh $v[0] $v[1] $v[2] $v[3] $material; Add-Quad $mesh $v[4] $v[7] $v[6] $v[5] $material; Add-Quad $mesh $v[0] $v[3] $v[7] $v[4] $material; Add-Quad $mesh $v[1] $v[5] $v[6] $v[2] $material; Add-Quad $mesh $v[0] $v[4] $v[5] $v[1] $material; Add-Quad $mesh $v[3] $v[2] $v[6] $v[7] $material
}
function Add-EllipsoidBand($mesh,$center,$radius,[double]$latitudeMinimum,[double]$latitudeMaximum,[int]$rings,[int]$segments,[int]$material) {
    [double]$cx=$center[0]; [double]$cy=$center[1]; [double]$cz=$center[2]
    [double]$rx=$radius[0]; [double]$ry=$radius[1]; [double]$rz=$radius[2]
    for($ring=0;$ring -lt $rings;$ring++){
        [double]$lat0=$latitudeMinimum+($latitudeMaximum-$latitudeMinimum)*$ring/$rings
        [double]$lat1=$latitudeMinimum+($latitudeMaximum-$latitudeMinimum)*($ring+1)/$rings
        for($segment=0;$segment -lt $segments;$segment++){
            [double]$lon0=2.0*[Math]::PI*$segment/$segments; [double]$lon1=2.0*[Math]::PI*($segment+1)/$segments
            [double]$cosLat0=[Math]::Cos($lat0); [double]$sinLat0=[Math]::Sin($lat0); [double]$cosLat1=[Math]::Cos($lat1); [double]$sinLat1=[Math]::Sin($lat1)
            [double]$sinLon0=[Math]::Sin($lon0); [double]$cosLon0=[Math]::Cos($lon0); [double]$sinLon1=[Math]::Sin($lon1); [double]$cosLon1=[Math]::Cos($lon1)
            $p00=@(($cx+$rx*$cosLat0*$sinLon0),($cy+$ry*$sinLat0),($cz+$rz*$cosLat0*$cosLon0))
            $p01=@(($cx+$rx*$cosLat0*$sinLon1),($cy+$ry*$sinLat0),($cz+$rz*$cosLat0*$cosLon1))
            $p11=@(($cx+$rx*$cosLat1*$sinLon1),($cy+$ry*$sinLat1),($cz+$rz*$cosLat1*$cosLon1))
            $p10=@(($cx+$rx*$cosLat1*$sinLon0),($cy+$ry*$sinLat1),($cz+$rz*$cosLat1*$cosLon0))
            Add-Quad $mesh $p00 $p01 $p11 $p10 $material
        }
    }
}
function Add-RotorBox($mesh,[double]$angle,[double]$radius,[double]$width,[double]$height,[int]$material,[bool]$tail) {
    if($tail){ $axisLength=@(0,[Math]::Cos($angle),[Math]::Sin($angle)); $axisWidth=@(0,-[Math]::Sin($angle),[Math]::Cos($angle)); Add-OrientedBox $mesh @(0,0,0) @(1,0,0) $axisLength $axisWidth ($height/2) $radius ($width/2) $material }
    else { $axisLength=@([Math]::Cos($angle),0,[Math]::Sin($angle)); $axisWidth=@(-[Math]::Sin($angle),0,[Math]::Cos($angle)); Add-OrientedBox $mesh @(0,0,0) $axisLength @(0,1,0) $axisWidth $radius ($height/2) ($width/2) $material }
}
function Convert-ToLocal($mesh,$pivot) { for($i=0;$i -lt $mesh.Positions.Count;$i+=3){ $mesh.Positions[$i]-=$pivot[0]; $mesh.Positions[$i+1]-=$pivot[1]; $mesh.Positions[$i+2]-=$pivot[2] } }
function New-GeometryXml($mesh) {
    $positions=($mesh.Positions|ForEach-Object{Format-Number $_}) -join ' '; $normals=($mesh.Normals|ForEach-Object{Format-Number $_}) -join ' '; $uvs=($mesh.UVs|ForEach-Object{Format-Number $_}) -join ' '; $indices=($mesh.Indices|ForEach-Object{$_.ToString($invariant)}) -join ' '; $vertexCount=[int]($mesh.Positions.Count/3); $name=$mesh.Name
@"
    <geometry id="$name-mesh" name="$name"><mesh>
      <source id="$name-positions"><float_array id="$name-positions-array" count="$($mesh.Positions.Count)">$positions</float_array><technique_common><accessor source="#$name-positions-array" count="$vertexCount" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
      <source id="$name-normals"><float_array id="$name-normals-array" count="$($mesh.Normals.Count)">$normals</float_array><technique_common><accessor source="#$name-normals-array" count="$vertexCount" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
      <source id="$name-uv"><float_array id="$name-uv-array" count="$($mesh.UVs.Count)">$uvs</float_array><technique_common><accessor source="#$name-uv-array" count="$vertexCount" stride="2"><param name="S" type="float"/><param name="T" type="float"/></accessor></technique_common></source>
      <vertices id="$name-vertices"><input semantic="POSITION" source="#$name-positions"/></vertices><triangles count="$($mesh.Triangles)"><input semantic="VERTEX" source="#$name-vertices" offset="0"/><input semantic="NORMAL" source="#$name-normals" offset="1"/><input semantic="TEXCOORD" source="#$name-uv" offset="2" set="0"/><p>$indices</p></triangles>
    </mesh></geometry>
"@
}

$visual=New-Mesh 'VisualRoot'; $main=New-Mesh 'MainRotor'; $tail=New-Mesh 'TailRotor'; $mainBlur=New-Mesh 'MainRotorBlur'; $tailBlur=New-Mesh 'TailRotorBlur'
Add-EllipsoidBand $visual @(0,0.88,-0.72) @(0.94,0.72,1.32) (-[Math]::PI/2) 0.24 5 12 0
Add-FrustumZ $visual -1.82 -1.34 @(0,0.87) @(0,0.96) @(0.54,0.40) @(0.78,0.52) 0
Add-Quad $visual @(-0.54,0.82,-1.825) @(0.54,0.82,-1.825) @(0.67,1.37,-1.42) @(-0.67,1.37,-1.42) 2
Add-Box $visual @(-0.72,1.34,-1.38) @(0.72,1.48,0.18) 0
Add-Box $visual @(-0.88,0.48,-1.30) @(-0.80,1.34,0.18) 1; Add-Box $visual @(0.80,0.48,-1.30) @(0.88,1.34,0.18) 1
foreach($x in @(-0.83,0.83)){ Add-Box $visual @(($x-0.035),0.50,-0.95) @(($x+0.035),0.61,0.10) 1; Add-Box $visual @(($x-0.035),0.58,-1.03) @(($x+0.035),1.34,-0.95) 3; Add-Box $visual @(($x-0.035),0.58,0.10) @(($x+0.035),1.34,0.18) 3; Add-Box $visual @(($x-0.035),1.28,-0.95) @(($x+0.035),1.36,0.10) 3 }
foreach($x in @(-0.42,0.42)){ Add-Box $visual @(($x-0.27),0.48,-0.84) @(($x+0.27),0.61,-0.30) 6; Add-Box $visual @(($x-0.27),0.57,-0.24) @(($x+0.27),1.22,-0.10) 6; Add-Box $visual @(($x-0.24),0.61,-0.79) @(($x+0.24),0.66,-0.36) 3 }
Add-FrustumZ $visual -1.31 -0.97 @(0,0.92) @(0,1.03) @(0.66,0.18) @(0.61,0.23) 7
foreach($x in @(-0.32,0,0.32)){ Add-Box $visual @(($x-0.07),1.04,-1.335) @(($x+0.07),1.15,-1.30) 10 }
Add-FrustumZ $visual 0.16 2.42 @(0,0.96) @(0,1.15) @(0.30,0.28) @(0.12,0.13) 0; Add-Box $visual @(-0.04,0.92,0.22) @(0.04,1.04,2.34) 1
Add-FrustumZ $visual 2.16 2.53 @(0,1.25) @(0,1.56) @(0.10,0.52) @(0.075,0.25) 1; Add-Box $visual @(-0.54,1.10,2.18) @(0.54,1.18,2.46) 0; Add-Box $visual @(-0.08,1.49,-0.43) @(0.08,1.80,-0.27) 3
foreach($x in @(-0.98,0.98)){ Add-Box $visual @(($x-0.065),0.05,-1.37) @(($x+0.065),0.15,1.02) 4; Add-Box $visual @(($x-0.055),0.14,-0.72) @(($x+0.055),0.50,-0.57) 5; Add-Box $visual @(($x-0.055),0.14,0.28) @(($x+0.055),0.50,0.43) 5 }
Add-Box $visual @(-1.00,0.84,-0.22) @(-0.93,0.94,-0.10) 9; Add-Box $visual @(0.93,0.84,-0.22) @(1.00,0.94,-0.10) 8; Add-Box $visual @(-0.05,1.31,2.49) @(0.05,1.40,2.57) 10
Add-RotorBox $main 0 2.36 0.16 0.045 3 $false; Add-RotorBox $main ([Math]::PI/2) 2.36 0.16 0.045 3 $false; Add-OrientedBox $main @(0,0,0) @(1,0,0) @(0,1,0) @(0,0,1) 0.18 0.08 0.18 5
Add-RotorBox $tail 0 0.70 0.12 0.045 3 $true; Add-RotorBox $tail ([Math]::PI/2) 0.70 0.12 0.045 3 $true; Add-OrientedBox $tail @(0,0,0) @(1,0,0) @(0,1,0) @(0,0,1) 0.07 0.12 0.12 5
for($i=0;$i -lt 6;$i++){ Add-RotorBox $mainBlur ($i*[Math]::PI/6) 2.38 0.065 0.018 11 $false }; for($i=0;$i -lt 5;$i++){ Add-RotorBox $tailBlur ($i*[Math]::PI/5) 0.72 0.055 0.018 11 $true }

$visualPivot=@(0.0,1.0,-0.55); Convert-ToLocal $visual $visualPivot
$geometries=(@($visual,$main,$tail,$mainBlur,$tailBlur)|ForEach-Object{New-GeometryXml $_}) -join "`n"
$document=@"
<?xml version="1.0" encoding="utf-8"?>
<!-- Project-created clean-room V0.4 two-seat helicopter. Six bones, five merged meshes, opaque palette UVs. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1"><asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-HelicopterAssets.ps1</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset><library_geometries>$geometries</library_geometries><library_visual_scenes><visual_scene id="Scene" name="Scene"><node id="HelicopterRoot" name="HelicopterRoot" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix><node id="VisualRoot" name="VisualRoot" type="NODE"><translate>0 1 -0.55</translate><instance_geometry url="#VisualRoot-mesh" name="VisualRoot"/><node id="MainRotor" name="MainRotor" type="NODE"><translate>0 0.80 0.25</translate><instance_geometry url="#MainRotor-mesh" name="MainRotor"/></node><node id="TailRotor" name="TailRotor" type="NODE"><translate>0.30 0.20 2.92</translate><instance_geometry url="#TailRotor-mesh" name="TailRotor"/></node><node id="MainRotorBlur" name="MainRotorBlur" type="NODE"><translate>0 0.80 0.25</translate><instance_geometry url="#MainRotorBlur-mesh" name="MainRotorBlur"/></node><node id="TailRotorBlur" name="TailRotorBlur" type="NODE"><translate>0.30 0.20 2.92</translate><instance_geometry url="#TailRotorBlur-mesh" name="TailRotorBlur"/></node></node></node></visual_scene></library_visual_scenes><scene><instance_visual_scene url="#Scene"/></scene></COLLADA>
"@
[IO.File]::WriteAllText($modelPath,$document,[Text.UTF8Encoding]::new($false))

Add-Type -AssemblyName System.Drawing
$bitmap=New-Object Drawing.Bitmap 96,16,([Drawing.Imaging.PixelFormat]::Format32bppArgb)
try { $palette=@([Drawing.Color]::FromArgb(255,232,235,232),[Drawing.Color]::FromArgb(255,184,35,38),[Drawing.Color]::FromArgb(255,22,38,50),[Drawing.Color]::FromArgb(255,47,50,53),[Drawing.Color]::FromArgb(255,24,25,26),[Drawing.Color]::FromArgb(255,132,142,148),[Drawing.Color]::FromArgb(255,74,30,32),[Drawing.Color]::FromArgb(255,18,23,25),[Drawing.Color]::FromArgb(255,60,220,95),[Drawing.Color]::FromArgb(255,255,55,55),[Drawing.Color]::FromArgb(255,240,230,160),[Drawing.Color]::FromArgb(255,78,83,86)); for($x=0;$x -lt $bitmap.Width;$x++){ $color=$palette[[Math]::Min([int]($x/8),$palette.Count-1)]; for($y=0;$y -lt $bitmap.Height;$y++){$bitmap.SetPixel($x,$y,$color)} }; $bitmap.Save($texturePath,[Drawing.Imaging.ImageFormat]::Png) } finally { $bitmap.Dispose() }

New-Item -ItemType Directory -Force -Path $audioDirectory | Out-Null
$sampleRate=22050; $sampleCount=$sampleRate; $dataLength=$sampleCount*2; $stream=[IO.File]::Open($audioPath,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None); $writer=[IO.BinaryWriter]::new($stream)
try { $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $writer.Write(36+$dataLength); $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt ')); $writer.Write(16); $writer.Write([int16]1); $writer.Write([int16]1); $writer.Write($sampleRate); $writer.Write($sampleRate*2); $writer.Write([int16]2); $writer.Write([int16]16); $writer.Write([Text.Encoding]::ASCII.GetBytes('data')); $writer.Write($dataLength); for($i=0;$i -lt $sampleCount;$i++){ $t=[double]$i/$sampleRate; $pulse=0.46*[Math]::Sin(2*[Math]::PI*13*$t)+0.20*[Math]::Sin(2*[Math]::PI*26*$t); $engine=0.42*[Math]::Sin(2*[Math]::PI*55*$t)+0.18*[Math]::Sin(2*[Math]::PI*110*$t)+0.08*[Math]::Sin(2*[Math]::PI*220*$t); $sample=[Math]::Max(-1,[Math]::Min(1,0.42*($pulse+$engine))); $writer.Write([int16][Math]::Round($sample*32767)) } } finally { $writer.Dispose(); $stream.Dispose() }

Write-Host "Generated: $modelPath"; Write-Host "Generated: $texturePath"; Write-Host "Generated: $audioPath"
