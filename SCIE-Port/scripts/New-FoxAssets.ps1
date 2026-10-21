[CmdletBinding()]
param(
    [string]$ProjectRoot = $(Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$modelPath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Models\Fox.dae'
$texturePath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Textures\Fox.png'
$invariant = [Globalization.CultureInfo]::InvariantCulture

# Clean-room geometry authored for SCNET's procedural four-legged model. The
# six nodes are the exact bones required by ComponentFourLeggedModel; there is
# deliberately no imported skin, controller or glTF animation clip.
$parts = @(
    @{ Name = 'BodyCore';      Bone = 'Body'; Min = @(-0.28, 0.40, -0.52); Max = @( 0.28, 0.76,  0.50); U = 0.125 },
    @{ Name = 'Chest';         Bone = 'Body'; Min = @(-0.31, 0.38, -0.53); Max = @( 0.31, 0.74, -0.18); U = 0.125 },
    @{ Name = 'TailBase';      Bone = 'Body'; Min = @(-0.13, 0.54,  0.46); Max = @( 0.13, 0.76,  0.92); U = 0.125 },
    @{ Name = 'TailTip';       Bone = 'Body'; Min = @(-0.15, 0.47,  0.88); Max = @( 0.15, 0.70,  1.20); U = 0.375 },
    @{ Name = 'HeadCore';      Bone = 'Head'; Min = @(-0.23,-0.04, -0.30); Max = @( 0.23, 0.30,  0.14); U = 0.125 },
    @{ Name = 'Muzzle';        Bone = 'Head'; Min = @(-0.14,-0.02, -0.50); Max = @( 0.14, 0.17, -0.27); U = 0.375 },
    @{ Name = 'Nose';          Bone = 'Head'; Min = @(-0.11, 0.00, -0.54); Max = @( 0.11, 0.13, -0.47); U = 0.625 },
    @{ Name = 'EarLeft';       Bone = 'Head'; Min = @(-0.21, 0.27, -0.14); Max = @(-0.06, 0.55,  0.04); U = 0.875 },
    @{ Name = 'EarRight';      Bone = 'Head'; Min = @( 0.06, 0.27, -0.14); Max = @( 0.21, 0.55,  0.04); U = 0.875 },
    @{ Name = 'FrontLeftLeg';  Bone = 'Leg1'; Min = @(-0.07,-0.48, -0.08); Max = @( 0.07, 0.00,  0.08); U = 0.875 },
    @{ Name = 'FrontRightLeg'; Bone = 'Leg2'; Min = @(-0.07,-0.48, -0.08); Max = @( 0.07, 0.00,  0.08); U = 0.875 },
    @{ Name = 'HindLeftLeg';   Bone = 'Leg3'; Min = @(-0.08,-0.48, -0.09); Max = @( 0.08, 0.00,  0.09); U = 0.875 },
    @{ Name = 'HindRightLeg';  Bone = 'Leg4'; Min = @(-0.08,-0.48, -0.09); Max = @( 0.08, 0.00,  0.09); U = 0.875 }
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
    <geometry id="$name-mesh" name="$name"><mesh>
      <source id="$name-positions"><float_array id="$name-positions-array" count="24">$x0 $y0 $z0 $x1 $y0 $z0 $x1 $y1 $z0 $x0 $y1 $z0 $x0 $y0 $z1 $x1 $y0 $z1 $x1 $y1 $z1 $x0 $y1 $z1</float_array><technique_common><accessor source="#$name-positions-array" count="8" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
      <source id="$name-normals"><float_array id="$name-normals-array" count="18">0 0 -1 0 0 1 -1 0 0 1 0 0 0 -1 0 0 1 0</float_array><technique_common><accessor source="#$name-normals-array" count="6" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
      <source id="$name-uv"><float_array id="$name-uv-array" count="2">$u 0.5</float_array><technique_common><accessor source="#$name-uv-array" count="1" stride="2"><param name="S" type="float"/><param name="T" type="float"/></accessor></technique_common></source>
      <vertices id="$name-vertices"><input semantic="POSITION" source="#$name-positions"/></vertices>
      <triangles count="12"><input semantic="VERTEX" source="#$name-vertices" offset="0"/><input semantic="NORMAL" source="#$name-normals" offset="1"/><input semantic="TEXCOORD" source="#$name-uv" offset="2" set="0"/><p>0 0 0 1 0 0 2 0 0 0 0 0 2 0 0 3 0 0 4 1 0 6 1 0 5 1 0 4 1 0 7 1 0 6 1 0 0 2 0 3 2 0 7 2 0 0 2 0 7 2 0 4 2 0 1 3 0 5 3 0 6 3 0 1 3 0 6 3 0 2 3 0 0 4 0 4 4 0 5 4 0 0 4 0 5 4 0 1 4 0 3 5 0 2 5 0 6 5 0 3 5 0 6 5 0 7 5 0</p></triangles>
    </mesh></geometry>
"@
}

function New-Instances([string]$bone) {
    return (($parts | Where-Object Bone -eq $bone | ForEach-Object {
        "      <instance_geometry url=`"#$($_.Name)-mesh`" name=`"$($_.Name)`"/>"
    }) -join "`n")
}

$geometries = ($parts | ForEach-Object { New-CuboidGeometry $_ }) -join "`n"
$body = New-Instances 'Body'; $head = New-Instances 'Head'
$leg1 = New-Instances 'Leg1'; $leg2 = New-Instances 'Leg2'; $leg3 = New-Instances 'Leg3'; $leg4 = New-Instances 'Leg4'
$document = @"
<?xml version="1.0" encoding="utf-8"?>
<!-- Project-created clean-room low-poly fox. Y_UP; native procedural gait; six bones. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
  <asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-FoxAssets.ps1</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
  <library_geometries>$geometries</library_geometries>
  <library_visual_scenes><visual_scene id="Scene" name="Scene">
    <node id="Body" name="Body" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
$body
      <node id="Head" name="Head" type="NODE"><translate>0 0.62 -0.52</translate>
$head
      </node>
      <node id="Leg1" name="Leg1" type="NODE"><translate>-0.21 0.43 -0.34</translate>$leg1</node>
      <node id="Leg2" name="Leg2" type="NODE"><translate>0.21 0.43 -0.34</translate>$leg2</node>
      <node id="Leg3" name="Leg3" type="NODE"><translate>-0.21 0.43 0.34</translate>$leg3</node>
      <node id="Leg4" name="Leg4" type="NODE"><translate>0.21 0.43 0.34</translate>$leg4</node>
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
        [Drawing.Color]::FromArgb(255, 205, 88, 31),
        [Drawing.Color]::FromArgb(255, 232, 211, 172),
        [Drawing.Color]::FromArgb(255, 28, 25, 23),
        [Drawing.Color]::FromArgb(255, 79, 48, 32)
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
