[CmdletBinding()]
param(
    [string]$ProjectRoot = $(Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$modelPath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Models\Helicopter.dae'
$texturePath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Textures\Helicopter.png'
$invariant = [Globalization.CultureInfo]::InvariantCulture

# Clean-room low-poly helicopter. Coordinates on articulated children are local
# to their audited pivots so ComponentModel.SetBoneTransform can spin each rotor.
$parts = @(
    @{ Bone='HelicopterRoot'; Name='Cabin';    Min=@(-0.82,0.30,-1.45); Max=@(0.82,1.48,0.55); U=0.083333 },
    @{ Bone='HelicopterRoot'; Name='Nose';     Min=@(-0.64,0.45,-1.95); Max=@(0.64,1.25,-1.45); U=0.250000 },
    @{ Bone='HelicopterRoot'; Name='TailBoom'; Min=@(-0.22,0.74,0.55); Max=@(0.22,1.08,2.38); U=0.083333 },
    @{ Bone='HelicopterRoot'; Name='TailFin';  Min=@(-0.08,0.88,2.20); Max=@(0.08,1.72,2.52); U=0.416667 },
    @{ Bone='HelicopterRoot'; Name='SkidLeft'; Min=@(-1.02,0.05,-1.35); Max=@(-0.86,0.16,1.00); U=0.583333 },
    @{ Bone='HelicopterRoot'; Name='SkidRight';Min=@(0.86,0.05,-1.35); Max=@(1.02,0.16,1.00); U=0.583333 },
    @{ Bone='HelicopterRoot'; Name='StrutLeft';Min=@(-0.90,0.14,-0.65); Max=@(-0.68,0.48,-0.45); U=0.750000 },
    @{ Bone='HelicopterRoot'; Name='StrutRight';Min=@(0.68,0.14,-0.65); Max=@(0.90,0.48,-0.45); U=0.750000 },
    @{ Bone='HelicopterRoot'; Name='Mast';     Min=@(-0.08,1.45,-0.38); Max=@(0.08,1.76,-0.22); U=0.750000 },
    @{ Bone='MainRotor'; Name='MainBladeLeft';  Min=@(-2.35,-0.025,-0.07); Max=@(-0.12,0.025,0.07); U=0.916667 },
    @{ Bone='MainRotor'; Name='MainBladeRight'; Min=@(0.12,-0.025,-0.07); Max=@(2.35,0.025,0.07); U=0.916667 },
    @{ Bone='MainRotor'; Name='MainBladeFront'; Min=@(-0.07,-0.025,-2.35); Max=@(0.07,0.025,-0.12); U=0.916667 },
    @{ Bone='MainRotor'; Name='MainBladeRear';  Min=@(-0.07,-0.025,0.12); Max=@(0.07,0.025,2.35); U=0.916667 },
    @{ Bone='TailRotor'; Name='TailBladeUp';    Min=@(-0.025,0.08,-0.05); Max=@(0.025,0.68,0.05); U=0.916667 },
    @{ Bone='TailRotor'; Name='TailBladeDown';  Min=@(-0.025,-0.68,-0.05); Max=@(0.025,-0.08,0.05); U=0.916667 },
    @{ Bone='TailRotor'; Name='TailBladeFront'; Min=@(-0.025,-0.05,-0.68); Max=@(0.025,0.05,-0.08); U=0.916667 },
    @{ Bone='TailRotor'; Name='TailBladeRear';  Min=@(-0.025,-0.05,0.08); Max=@(0.025,0.05,0.68); U=0.916667 }
)

function Format-Number([double]$value) { $value.ToString('0.######', $invariant) }

function New-CuboidGeometry($part) {
    $x0=Format-Number $part.Min[0]; $y0=Format-Number $part.Min[1]; $z0=Format-Number $part.Min[2]
    $x1=Format-Number $part.Max[0]; $y1=Format-Number $part.Max[1]; $z1=Format-Number $part.Max[2]
    $u=Format-Number $part.U; $name=$part.Name
    @"
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
    (($parts | Where-Object Bone -eq $bone | ForEach-Object {
        "        <instance_geometry url=`"#$($_.Name)-mesh`" name=`"$($_.Name)`"/>"
    }) -join "`n")
}

$geometries=($parts | ForEach-Object { New-CuboidGeometry $_ }) -join "`n"
$root=New-Instances 'HelicopterRoot'; $main=New-Instances 'MainRotor'; $tail=New-Instances 'TailRotor'
$document=@"
<?xml version="1.0" encoding="utf-8"?>
<!-- Project-created clean-room two-seat helicopter. Y_UP, three articulated render bones. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
  <asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-HelicopterAssets.ps1</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
  <library_geometries>$geometries</library_geometries>
  <library_visual_scenes><visual_scene id="Scene" name="Scene">
    <node id="HelicopterRoot" name="HelicopterRoot" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
$root
      <node id="MainRotor" name="MainRotor" type="NODE"><translate>0 1.78 -0.3</translate>
$main
      </node>
      <node id="TailRotor" name="TailRotor" type="NODE"><translate>0.3 1.18 2.34</translate>
$tail
      </node>
    </node>
  </visual_scene></library_visual_scenes>
  <scene><instance_visual_scene url="#Scene"/></scene>
</COLLADA>
"@
[IO.File]::WriteAllText($modelPath,$document,[Text.UTF8Encoding]::new($false))

Add-Type -AssemblyName System.Drawing
$bitmap=New-Object Drawing.Bitmap 48,8,([Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $palette=@(
        [Drawing.Color]::FromArgb(255,215,112,32),
        [Drawing.Color]::FromArgb(255,48,58,64),
        [Drawing.Color]::FromArgb(255,48,105,125),
        [Drawing.Color]::FromArgb(255,190,198,201),
        [Drawing.Color]::FromArgb(255,37,40,42),
        [Drawing.Color]::FromArgb(255,222,205,76)
    )
    for($x=0;$x -lt $bitmap.Width;$x++){
        $color=$palette[[Math]::Min([int]($x/8),5)]
        for($y=0;$y -lt $bitmap.Height;$y++){ $bitmap.SetPixel($x,$y,$color) }
    }
    $bitmap.Save($texturePath,[Drawing.Imaging.ImageFormat]::Png)
}
finally { $bitmap.Dispose() }

Write-Host "Generated: $modelPath"
Write-Host "Generated: $texturePath"
