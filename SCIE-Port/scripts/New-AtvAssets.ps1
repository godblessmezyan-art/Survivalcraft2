[CmdletBinding()]
param(
    [string]$ProjectRoot = $(Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$modelPath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Models\ATV.dae'
$texturePath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Textures\ATV.png'
$invariant = [Globalization.CultureInfo]::InvariantCulture

$parts = @(
    @{ Name = 'ATVBody';            Min = @(-0.58, 0.40, -0.82); Max = @( 0.58, 0.78,  0.75); U = 0.125 },
    @{ Name = 'ATVNose';            Min = @(-0.50, 0.62, -1.02); Max = @( 0.50, 0.90, -0.55); U = 0.125 },
    @{ Name = 'ATVSeat';            Min = @(-0.38, 0.79, -0.05); Max = @( 0.38, 0.98,  0.58); U = 0.625 },
    @{ Name = 'ATVHandlebarStem';   Min = @(-0.08, 0.73, -0.72); Max = @( 0.08, 1.12, -0.62); U = 0.875 },
    @{ Name = 'ATVHandlebar';       Min = @(-0.55, 1.06, -0.75); Max = @( 0.55, 1.14, -0.64); U = 0.875 },
    @{ Name = 'ATVWheelFrontLeft';  Min = @(-0.82, 0.08, -0.86); Max = @(-0.58, 0.62, -0.34); U = 0.375 },
    @{ Name = 'ATVWheelFrontRight'; Min = @( 0.58, 0.08, -0.86); Max = @( 0.82, 0.62, -0.34); U = 0.375 },
    @{ Name = 'ATVWheelRearLeft';   Min = @(-0.82, 0.08,  0.31); Max = @(-0.58, 0.62,  0.83); U = 0.375 },
    @{ Name = 'ATVWheelRearRight';  Min = @( 0.58, 0.08,  0.31); Max = @( 0.82, 0.62,  0.83); U = 0.375 }
)

function Format-Number([double]$value) {
    return $value.ToString('0.######', $invariant)
}

function New-CuboidGeometry($part) {
    $x0 = Format-Number $part.Min[0]; $y0 = Format-Number $part.Min[1]; $z0 = Format-Number $part.Min[2]
    $x1 = Format-Number $part.Max[0]; $y1 = Format-Number $part.Max[1]; $z1 = Format-Number $part.Max[2]
    $u = Format-Number $part.U
    $name = $part.Name
    return @"
    <geometry id="$name-mesh" name="$name">
      <mesh>
        <source id="$name-positions"><float_array id="$name-positions-array" count="24">$x0 $y0 $z0 $x1 $y0 $z0 $x1 $y1 $z0 $x0 $y1 $z0 $x0 $y0 $z1 $x1 $y0 $z1 $x1 $y1 $z1 $x0 $y1 $z1</float_array><technique_common><accessor source="#$name-positions-array" count="8" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
        <source id="$name-normals"><float_array id="$name-normals-array" count="18">0 0 -1 0 0 1 -1 0 0 1 0 0 0 -1 0 0 1 0</float_array><technique_common><accessor source="#$name-normals-array" count="6" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
        <source id="$name-uv"><float_array id="$name-uv-array" count="2">$u 0.5</float_array><technique_common><accessor source="#$name-uv-array" count="1" stride="2"><param name="S" type="float"/><param name="T" type="float"/></accessor></technique_common></source>
        <vertices id="$name-vertices"><input semantic="POSITION" source="#$name-positions"/></vertices>
        <triangles count="12"><input semantic="VERTEX" source="#$name-vertices" offset="0"/><input semantic="NORMAL" source="#$name-normals" offset="1"/><input semantic="TEXCOORD" source="#$name-uv" offset="2" set="0"/><p>0 0 0 1 0 0 2 0 0 0 0 0 2 0 0 3 0 0 4 1 0 6 1 0 5 1 0 4 1 0 7 1 0 6 1 0 0 2 0 3 2 0 7 2 0 0 2 0 7 2 0 4 2 0 1 3 0 5 3 0 6 3 0 1 3 0 6 3 0 2 3 0 0 4 0 4 4 0 5 4 0 0 4 0 5 4 0 1 4 0 3 5 0 2 5 0 6 5 0 3 5 0 6 5 0 7 5 0</p></triangles>
      </mesh>
    </geometry>
"@
}

$geometries = ($parts | ForEach-Object { New-CuboidGeometry $_ }) -join "`n"
$nodes = ($parts | ForEach-Object {
    "      <node id=`"$($_.Name)`" name=`"$($_.Name)`" type=`"NODE`"><matrix sid=`"transform`">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix><instance_geometry url=`"#$($_.Name)-mesh`" name=`"$($_.Name)`"/></node>"
}) -join "`n"

$document = @"
<?xml version="1.0" encoding="utf-8"?>
<!-- Project-created clean-room low-poly ATV. Coordinates are baked Y_UP; every visible part owns an independent mesh. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
  <asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-AtvAssets.ps1</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
  <library_geometries>
$geometries
  </library_geometries>
  <library_visual_scenes><visual_scene id="Scene" name="Scene">
$nodes
  </visual_scene></library_visual_scenes>
  <scene><instance_visual_scene url="#Scene"/></scene>
</COLLADA>
"@
[IO.File]::WriteAllText($modelPath, $document, [Text.UTF8Encoding]::new($false))

Add-Type -AssemblyName System.Drawing
$bitmap = New-Object Drawing.Bitmap 32, 8, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $palette = @(
        [Drawing.Color]::FromArgb(255, 84, 132, 49),
        [Drawing.Color]::FromArgb(255, 32, 35, 31),
        [Drawing.Color]::FromArgb(255, 62, 46, 34),
        [Drawing.Color]::FromArgb(255, 145, 150, 142)
    )
    for ($x = 0; $x -lt $bitmap.Width; $x++) {
        $color = $palette[[Math]::Min([int]($x / 8), 3)]
        for ($y = 0; $y -lt $bitmap.Height; $y++) { $bitmap.SetPixel($x, $y, $color) }
    }
    $bitmap.Save($texturePath, [Drawing.Imaging.ImageFormat]::Png)
}
finally { $bitmap.Dispose() }

Write-Host "Generated: $modelPath"
Write-Host "Generated: $texturePath"
