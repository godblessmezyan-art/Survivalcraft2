[CmdletBinding()]
param([string]$ProjectRoot = $(Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
$modelPath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Models\OffroadCar.dae'
$texturePath = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Textures\OffroadCar.png'
$invariant = [Globalization.CultureInfo]::InvariantCulture

# Clean-room block-style open-top 4x4. Bounds: X +/-1.15, Y .08..1.48,
# Z -1.78..1.68. All parts share one render node for Android shader safety.
$parts = @(
    @{ Name='CarChassis';       Min=@(-0.92,0.38,-1.52); Max=@(0.92,0.67,1.42); U=0.10 },
    @{ Name='CarHood';          Min=@(-0.86,0.66,-1.52); Max=@(0.86,1.00,-0.58); U=0.10 },
    @{ Name='CarCabinFloor';    Min=@(-0.84,0.66,-0.52); Max=@(0.84,0.82,0.78); U=0.10 },
    @{ Name='CarDriverSeat';    Min=@(-0.72,0.80,-0.34); Max=@(-0.08,0.98,0.46); U=0.50 },
    @{ Name='CarPassengerSeat'; Min=@(0.08,0.80,-0.34); Max=@(0.72,0.98,0.46); U=0.50 },
    @{ Name='CarDriverBack';    Min=@(-0.72,0.86,0.40); Max=@(-0.08,1.30,0.58); U=0.50 },
    @{ Name='CarPassengerBack'; Min=@(0.08,0.86,0.40); Max=@(0.72,1.30,0.58); U=0.50 },
    @{ Name='CarRearDeck';      Min=@(-0.88,0.66,0.76); Max=@(0.88,1.02,1.42); U=0.10 },
    @{ Name='CarFrontBumper';   Min=@(-1.04,0.34,-1.78); Max=@(1.04,0.56,-1.52); U=0.90 },
    @{ Name='CarRearBumper';    Min=@(-1.04,0.34,1.42); Max=@(1.04,0.56,1.68); U=0.90 },
    @{ Name='CarWheelFL';       Min=@(-1.15,0.08,-1.48); Max=@(-0.88,0.76,-0.88); U=0.30 },
    @{ Name='CarWheelFR';       Min=@(0.88,0.08,-1.48); Max=@(1.15,0.76,-0.88); U=0.30 },
    @{ Name='CarWheelRL';       Min=@(-1.15,0.08,0.82); Max=@(-0.88,0.76,1.42); U=0.30 },
    @{ Name='CarWheelRR';       Min=@(0.88,0.08,0.82); Max=@(1.15,0.76,1.42); U=0.30 },
    @{ Name='CarCageLeftFront'; Min=@(-0.82,0.88,-0.52); Max=@(-0.70,1.48,-0.40); U=0.90 },
    @{ Name='CarCageRightFront';Min=@(0.70,0.88,-0.52); Max=@(0.82,1.48,-0.40); U=0.90 },
    @{ Name='CarCageLeftRear';  Min=@(-0.82,0.88,0.58); Max=@(-0.70,1.48,0.70); U=0.90 },
    @{ Name='CarCageRightRear'; Min=@(0.70,0.88,0.58); Max=@(0.82,1.48,0.70); U=0.90 },
    @{ Name='CarCageFrontTop';  Min=@(-0.82,1.38,-0.52); Max=@(0.82,1.48,-0.40); U=0.90 },
    @{ Name='CarCageRearTop';   Min=@(-0.82,1.38,0.58); Max=@(0.82,1.48,0.70); U=0.90 },
    @{ Name='CarCageLeftTop';   Min=@(-0.82,1.38,-0.40); Max=@(-0.70,1.48,0.58); U=0.90 },
    @{ Name='CarCageRightTop';  Min=@(0.70,1.38,-0.40); Max=@(0.82,1.48,0.58); U=0.90 },
    @{ Name='CarSteering';      Min=@(-0.62,1.06,-0.48); Max=@(-0.28,1.15,-0.36); U=0.90 },
    @{ Name='CarHeadlights';    Min=@(-0.62,0.76,-1.58); Max=@(0.62,0.92,-1.51); U=0.70 }
)

function F([double]$v) { $v.ToString('0.######', $invariant) }
function New-Cuboid($p) {
    $x0=F $p.Min[0]; $y0=F $p.Min[1]; $z0=F $p.Min[2]
    $x1=F $p.Max[0]; $y1=F $p.Max[1]; $z1=F $p.Max[2]
    $u=F $p.U; $n=$p.Name
@"
    <geometry id="$n-mesh" name="$n"><mesh>
      <source id="$n-positions"><float_array id="$n-positions-array" count="24">$x0 $y0 $z0 $x1 $y0 $z0 $x1 $y1 $z0 $x0 $y1 $z0 $x0 $y0 $z1 $x1 $y0 $z1 $x1 $y1 $z1 $x0 $y1 $z1</float_array><technique_common><accessor source="#$n-positions-array" count="8" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
      <source id="$n-normals"><float_array id="$n-normals-array" count="18">0 0 -1 0 0 1 -1 0 0 1 0 0 0 -1 0 0 1 0</float_array><technique_common><accessor source="#$n-normals-array" count="6" stride="3"><param name="X" type="float"/><param name="Y" type="float"/><param name="Z" type="float"/></accessor></technique_common></source>
      <source id="$n-uv"><float_array id="$n-uv-array" count="2">$u 0.5</float_array><technique_common><accessor source="#$n-uv-array" count="1" stride="2"><param name="S" type="float"/><param name="T" type="float"/></accessor></technique_common></source>
      <vertices id="$n-vertices"><input semantic="POSITION" source="#$n-positions"/></vertices>
      <triangles count="12"><input semantic="VERTEX" source="#$n-vertices" offset="0"/><input semantic="NORMAL" source="#$n-normals" offset="1"/><input semantic="TEXCOORD" source="#$n-uv" offset="2" set="0"/><p>0 0 0 1 0 0 2 0 0 0 0 0 2 0 0 3 0 0 4 1 0 6 1 0 5 1 0 4 1 0 7 1 0 6 1 0 0 2 0 3 2 0 7 2 0 0 2 0 7 2 0 4 2 0 1 3 0 5 3 0 6 3 0 1 3 0 6 3 0 2 3 0 0 4 0 4 4 0 5 4 0 0 4 0 5 4 0 1 4 0 3 5 0 2 5 0 6 5 0 3 5 0 6 5 0 7 5 0</p></triangles>
    </mesh></geometry>
"@
}

$geometries = ($parts | ForEach-Object { New-Cuboid $_ }) -join "`n"
$instances = ($parts | ForEach-Object { "        <instance_geometry url=`"#$($_.Name)-mesh`" name=`"$($_.Name)`"/>" }) -join "`n"
$dae = @"
<?xml version="1.0" encoding="utf-8"?>
<!-- Clean-room two-seat off-road car; baked Y_UP, one render bone. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
  <asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-OffroadCarAssets.ps1</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
  <library_geometries>
$geometries
  </library_geometries>
  <library_visual_scenes><visual_scene id="Scene" name="Scene"><node id="OffroadCar" name="OffroadCar" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
$instances
  </node></visual_scene></library_visual_scenes>
  <scene><instance_visual_scene url="#Scene"/></scene>
</COLLADA>
"@
[IO.File]::WriteAllText($modelPath, $dae, [Text.UTF8Encoding]::new($false))

Add-Type -AssemblyName System.Drawing
$bitmap = New-Object Drawing.Bitmap 40, 8, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $palette = @(
        [Drawing.Color]::FromArgb(255,44,104,122),
        [Drawing.Color]::FromArgb(255,25,27,29),
        [Drawing.Color]::FromArgb(255,66,48,37),
        [Drawing.Color]::FromArgb(255,214,204,128),
        [Drawing.Color]::FromArgb(255,155,163,166)
    )
    for ($x=0; $x -lt 40; $x++) {
        for ($y=0; $y -lt 8; $y++) { $bitmap.SetPixel($x,$y,$palette[[Math]::Min([int]($x/8),4)]) }
    }
    $bitmap.Save($texturePath, [Drawing.Imaging.ImageFormat]::Png)
}
finally { $bitmap.Dispose() }

Write-Host "Generated: $modelPath"
Write-Host "Generated: $texturePath"
