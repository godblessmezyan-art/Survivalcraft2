[CmdletBinding()]
param(
    [string]$ProjectRoot = $(Split-Path -Parent $PSScriptRoot)
)

# Clean-room chicken for SCNET's procedural four-legged model. The six nodes are the
# exact bones required by ComponentFourLeggedModel (Body + Head + Leg1..Leg4); there is
# deliberately no imported skin, controller or glTF animation clip. A chicken only has
# two real legs, so the two front bones carry the visible legs and the two rear bones
# carry short stubby legs tucked just behind them, which keeps the mandatory rig intact
# while reading as a bird stance.
$ErrorActionPreference = 'Stop'
$modelPath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Models\Chicken.dae'
$texturePath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Textures\Chicken.png'
$invariant = [Globalization.CultureInfo]::InvariantCulture

# U values select one of four texture palette columns: 0.125 white body, 0.375 red
# comb/wattle, 0.625 orange beak/legs, 0.875 dark tail.
$parts = @(
    @{ Name = 'BodyCore';  Bone = 'Body'; Min = @(-0.16, 0.34, -0.22); Max = @( 0.16, 0.60,  0.24); U = 0.125 },
    @{ Name = 'Tail';      Bone = 'Body'; Min = @(-0.06, 0.40,  0.22); Max = @( 0.06, 0.66,  0.44); U = 0.875 },
    @{ Name = 'HeadCore';  Bone = 'Head'; Min = @(-0.10,-0.10, -0.12); Max = @( 0.10, 0.14,  0.12); U = 0.125 },
    @{ Name = 'Beak';      Bone = 'Head'; Min = @(-0.05,-0.06, -0.26); Max = @( 0.05, 0.02, -0.12); U = 0.625 },
    @{ Name = 'Comb';      Bone = 'Head'; Min = @(-0.03, 0.14, -0.06); Max = @( 0.03, 0.26,  0.06); U = 0.375 },
    @{ Name = 'Wattle';    Bone = 'Head'; Min = @(-0.03,-0.14, -0.14); Max = @( 0.03,-0.04, -0.08); U = 0.375 },
    @{ Name = 'FrontLeftLeg';  Bone = 'Leg1'; Min = @(-0.03,-0.40, -0.03); Max = @( 0.03, 0.00,  0.03); U = 0.625 },
    @{ Name = 'FrontRightLeg'; Bone = 'Leg2'; Min = @(-0.03,-0.40, -0.03); Max = @( 0.03, 0.00,  0.03); U = 0.625 },
    @{ Name = 'HindLeftLeg';   Bone = 'Leg3'; Min = @(-0.02,-0.20, -0.02); Max = @( 0.02, 0.00,  0.02); U = 0.625 },
    @{ Name = 'HindRightLeg';  Bone = 'Leg4'; Min = @(-0.02,-0.20, -0.02); Max = @( 0.02, 0.00,  0.02); U = 0.625 }
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
<!-- Project-created clean-room low-poly chicken. Y_UP; native procedural gait; six bones. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
  <asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-ChickenAssets.ps1</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
  <library_geometries>$geometries</library_geometries>
  <library_visual_scenes><visual_scene id="Scene" name="Scene">
    <node id="Body" name="Body" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
$body
      <node id="Head" name="Head" type="NODE"><translate>0 0.55 -0.30</translate>
$head
      </node>
      <node id="Leg1" name="Leg1" type="NODE"><translate>-0.07 0.40 -0.02</translate>$leg1</node>
      <node id="Leg2" name="Leg2" type="NODE"><translate>0.07 0.40 -0.02</translate>$leg2</node>
      <node id="Leg3" name="Leg3" type="NODE"><translate>-0.07 0.40 0.08</translate>$leg3</node>
      <node id="Leg4" name="Leg4" type="NODE"><translate>0.07 0.40 0.08</translate>$leg4</node>
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
        [Drawing.Color]::FromArgb(255, 240, 240, 236),  # white body
        [Drawing.Color]::FromArgb(255, 200, 40, 40),    # red comb / wattle
        [Drawing.Color]::FromArgb(255, 226, 150, 44),   # orange beak / legs
        [Drawing.Color]::FromArgb(255, 46, 42, 40)      # dark tail feathers
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
