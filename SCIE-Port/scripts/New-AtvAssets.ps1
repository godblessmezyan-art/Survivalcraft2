[CmdletBinding()]
param(
    [string]$ProjectRoot = $(Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$modelPath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Models\ATV.dae'
$texturePath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Textures\ATV.png'
$invariant = [Globalization.CultureInfo]::InvariantCulture

$parts = @(
    @{ Name = 'BikeFrame';          Min = @(-0.18, 0.38, -0.82); Max = @( 0.18, 0.61,  0.82); U = 0.125 },
    @{ Name = 'BikeEngine';         Min = @(-0.34, 0.34, -0.20); Max = @( 0.34, 0.76,  0.35); U = 0.875 },
    @{ Name = 'BikeFuelTank';       Min = @(-0.33, 0.68, -0.66); Max = @( 0.33, 0.98, -0.12); U = 0.125 },
    @{ Name = 'BikeDriverSeat';     Min = @(-0.29, 0.81, -0.14); Max = @( 0.29, 0.96,  0.34); U = 0.625 },
    @{ Name = 'BikePassengerSeat';  Min = @(-0.30, 0.81,  0.34); Max = @( 0.30, 0.96,  0.88); U = 0.625 },
    @{ Name = 'BikeFrontFork';      Min = @(-0.11, 0.30, -1.06); Max = @( 0.11, 0.96, -0.94); U = 0.875 },
    @{ Name = 'BikeHandlebarStem';  Min = @(-0.07, 0.88, -0.98); Max = @( 0.07, 1.14, -0.88); U = 0.875 },
    @{ Name = 'BikeHandlebar';      Min = @(-0.50, 1.08, -0.99); Max = @( 0.50, 1.16, -0.88); U = 0.875 },
    @{ Name = 'BikeFrontWheel';     Min = @(-0.15, 0.06, -1.24); Max = @( 0.15, 0.64, -0.72); U = 0.375 },
    @{ Name = 'BikeRearWheel';      Min = @(-0.17, 0.06,  0.70); Max = @( 0.17, 0.66,  1.24); U = 0.375 },
    @{ Name = 'BikeFrontFender';    Min = @(-0.25, 0.62, -1.20); Max = @( 0.25, 0.72, -0.73); U = 0.125 },
    @{ Name = 'BikeRearFender';     Min = @(-0.28, 0.68,  0.70); Max = @( 0.28, 0.78,  1.17); U = 0.125 }
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
$geometryInstances = ($parts | ForEach-Object {
    "        <instance_geometry url=`"#$($_.Name)-mesh`" name=`"$($_.Name)`"/>"
}) -join "`n"

$document = @"
<?xml version="1.0" encoding="utf-8"?>
<!-- Clean-room tandem dirt bike; baked Y_UP, one render bone. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
  <asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-AtvAssets.ps1</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
  <library_geometries>
$geometries
  </library_geometries>
  <library_visual_scenes><visual_scene id="Scene" name="Scene">
      <node id="ATV" name="ATV" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
$geometryInstances
      </node>
  </visual_scene></library_visual_scenes>
  <scene><instance_visual_scene url="#Scene"/></scene>
</COLLADA>
"@
[IO.File]::WriteAllText($modelPath, $document, [Text.UTF8Encoding]::new($false))

Add-Type -AssemblyName System.Drawing
$bitmap = New-Object Drawing.Bitmap 32, 8, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $palette = @(
        [Drawing.Color]::FromArgb(255, 202, 74, 28),
        [Drawing.Color]::FromArgb(255, 27, 29, 28),
        [Drawing.Color]::FromArgb(255, 50, 42, 35),
        [Drawing.Color]::FromArgb(255, 154, 159, 156)
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
