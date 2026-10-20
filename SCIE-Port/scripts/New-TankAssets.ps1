[CmdletBinding()]
param(
    [string]$ProjectRoot = $(Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$modelPath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Models\T34Tank.dae'
$texturePath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Textures\T34Tank.png'
$invariant = [Globalization.CultureInfo]::InvariantCulture
$modelScale = 1.35

# Clean-room, project-created geometry. Child coordinates are local to their
# pivots so the turret and gun can later be animated without rebuilding assets.
$parts = @(
    @{ Bone = 'TankHull'; Name = 'LowerHull'; Min = @(-1.00, 0.22, -1.35); Max = @( 1.00, 0.62,  1.35); U = 0.0625 },
    @{ Bone = 'TankHull'; Name = 'UpperHull'; Min = @(-0.82, 0.62, -1.02); Max = @( 0.82, 0.90,  1.00); U = 0.0625 },
    @{ Bone = 'TankHull'; Name = 'Glacis';    Min = @(-0.90, 0.54, -1.52); Max = @( 0.90, 0.80, -1.05); U = 0.1875 },
    @{ Bone = 'TankHull'; Name = 'TrackLeft'; Min = @(-1.24, 0.08, -1.45); Max = @(-0.92, 0.60,  1.40); U = 0.5625 },
    @{ Bone = 'TankHull'; Name = 'TrackRight';Min = @( 0.92, 0.08, -1.45); Max = @( 1.24, 0.60,  1.40); U = 0.5625 },
    @{ Bone = 'TankHull'; Name = 'WheelL1'; Min = @(-1.27, 0.16, -1.25); Max = @(-0.89, 0.51, -0.88); U = 0.6875 },
    @{ Bone = 'TankHull'; Name = 'WheelL2'; Min = @(-1.27, 0.16, -0.72); Max = @(-0.89, 0.51, -0.35); U = 0.6875 },
    @{ Bone = 'TankHull'; Name = 'WheelL3'; Min = @(-1.27, 0.16, -0.19); Max = @(-0.89, 0.51,  0.18); U = 0.6875 },
    @{ Bone = 'TankHull'; Name = 'WheelL4'; Min = @(-1.27, 0.16,  0.34); Max = @(-0.89, 0.51,  0.71); U = 0.6875 },
    @{ Bone = 'TankHull'; Name = 'WheelL5'; Min = @(-1.27, 0.16,  0.87); Max = @(-0.89, 0.51,  1.24); U = 0.6875 },
    @{ Bone = 'TankHull'; Name = 'WheelR1'; Min = @( 0.89, 0.16, -1.25); Max = @( 1.27, 0.51, -0.88); U = 0.6875 },
    @{ Bone = 'TankHull'; Name = 'WheelR2'; Min = @( 0.89, 0.16, -0.72); Max = @( 1.27, 0.51, -0.35); U = 0.6875 },
    @{ Bone = 'TankHull'; Name = 'WheelR3'; Min = @( 0.89, 0.16, -0.19); Max = @( 1.27, 0.51,  0.18); U = 0.6875 },
    @{ Bone = 'TankHull'; Name = 'WheelR4'; Min = @( 0.89, 0.16,  0.34); Max = @( 1.27, 0.51,  0.71); U = 0.6875 },
    @{ Bone = 'TankHull'; Name = 'WheelR5'; Min = @( 0.89, 0.16,  0.87); Max = @( 1.27, 0.51,  1.24); U = 0.6875 },
    @{ Bone = 'TankTurret'; Name = 'TurretBase'; Min = @(-0.64, 0.00, -0.56); Max = @( 0.64, 0.35,  0.52); U = 0.1875 },
    @{ Bone = 'TankTurret'; Name = 'TurretRoof'; Min = @(-0.48, 0.35, -0.40); Max = @( 0.48, 0.51,  0.37); U = 0.0625 },
    @{ Bone = 'TankTurret'; Name = 'Hatch';      Min = @(-0.23, 0.51, -0.03); Max = @( 0.23, 0.61,  0.32); U = 0.3125 },
    @{ Bone = 'TankGun'; Name = 'GunBarrel'; Min = @(-0.09, -0.07, -1.45); Max = @( 0.09, 0.07,  0.10); U = 0.8125 },
    @{ Bone = 'TankGun'; Name = 'GunMuzzle'; Min = @(-0.14, -0.11, -1.58); Max = @( 0.14, 0.11, -1.40); U = 0.8125 }
)

function Format-Number([double]$value) { return $value.ToString('0.######', $invariant) }

function New-CuboidGeometry($part) {
    $x0 = Format-Number ($part.Min[0] * $modelScale); $y0 = Format-Number ($part.Min[1] * $modelScale); $z0 = Format-Number ($part.Min[2] * $modelScale)
    $x1 = Format-Number ($part.Max[0] * $modelScale); $y1 = Format-Number ($part.Max[1] * $modelScale); $z1 = Format-Number ($part.Max[2] * $modelScale)
    $u = Format-Number $part.U; $name = $part.Name
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
        "        <instance_geometry url=`"#$($_.Name)-mesh`" name=`"$($_.Name)`"/>"
    }) -join "`n")
}

$geometries = ($parts | ForEach-Object { New-CuboidGeometry $_ }) -join "`n"
$hull = New-Instances 'TankHull'; $turret = New-Instances 'TankTurret'; $gun = New-Instances 'TankGun'
$document = @"
<?xml version="1.0" encoding="utf-8"?>
<!-- Project-created clean-room T-34-inspired low-poly tank. Y_UP, three articulated render bones. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
  <asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-TankAssets.ps1</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
  <library_geometries>$geometries</library_geometries>
  <library_visual_scenes><visual_scene id="Scene" name="Scene">
    <node id="TankHull" name="TankHull" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
$hull
      <node id="TankTurret" name="TankTurret" type="NODE"><translate>0 1.242 0</translate>
$turret
        <node id="TankGun" name="TankGun" type="NODE"><translate>0 0.324 -0.7425</translate>
$gun
        </node>
      </node>
    </node>
  </visual_scene></library_visual_scenes>
  <scene><instance_visual_scene url="#Scene"/></scene>
</COLLADA>
"@
[IO.File]::WriteAllText($modelPath, $document, [Text.UTF8Encoding]::new($false))

Add-Type -AssemblyName System.Drawing
$bitmap = New-Object Drawing.Bitmap 64, 8, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $palette = @(
        [Drawing.Color]::FromArgb(255, 73, 101, 50),
        [Drawing.Color]::FromArgb(255, 91, 117, 61),
        [Drawing.Color]::FromArgb(255, 55, 70, 39),
        [Drawing.Color]::FromArgb(255, 104, 119, 82),
        [Drawing.Color]::FromArgb(255, 34, 38, 32),
        [Drawing.Color]::FromArgb(255, 48, 50, 45),
        [Drawing.Color]::FromArgb(255, 69, 72, 65),
        [Drawing.Color]::FromArgb(255, 118, 123, 111)
    )
    for ($x = 0; $x -lt $bitmap.Width; $x++) {
        $color = $palette[[Math]::Min([int]($x / 8), 7)]
        for ($y = 0; $y -lt $bitmap.Height; $y++) { $bitmap.SetPixel($x, $y, $color) }
    }
    $bitmap.Save($texturePath, [Drawing.Imaging.ImageFormat]::Png)
}
finally { $bitmap.Dispose() }

Write-Host "Generated: $modelPath"
Write-Host "Generated: $texturePath"
