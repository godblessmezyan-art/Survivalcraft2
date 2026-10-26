[CmdletBinding()]
param([string]$ProjectRoot = $(Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$models = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Models'
$textures = Join-Path $ProjectRoot 'src\SCIEPort\Assets\Textures'
$invariant = [Globalization.CultureInfo]::InvariantCulture
function F([double]$v) { return $v.ToString('0.######', $invariant) }

# ---------------------------------------------------------------------------
# Control core device: a squat base, four armour fins and a faceted prism.
# Envelope X/Z +/-0.42, Y 0..0.62 so it reads as a device standing on the ground.
# ---------------------------------------------------------------------------
$palette = @(
    [Drawing.Color]::FromArgb(255, 44, 48, 54),   # 0 armour
    [Drawing.Color]::FromArgb(255, 92, 98, 106),  # 1 trim
    [Drawing.Color]::FromArgb(255, 196, 138, 54), # 2 bronze
    [Drawing.Color]::FromArgb(255, 236, 176, 72), # 3 core glow
    [Drawing.Color]::FromArgb(255, 24, 26, 30),   # 4 shadow
    [Drawing.Color]::FromArgb(255, 120, 168, 196) # 5 cool lens
)

$parts = @(
    @{ Name='CoreBase';   Min=@(-0.34,0.00,-0.34); Max=@(0.34,0.10,0.34); U=0 },
    @{ Name='CoreBody';   Min=@(-0.24,0.10,-0.24); Max=@(0.24,0.40,0.24); U=1 },
    @{ Name='CoreCollar'; Min=@(-0.30,0.40,-0.30); Max=@(0.30,0.46,0.30); U=2 },
    @{ Name='CorePrism';  Min=@(-0.16,0.46,-0.16); Max=@(0.16,0.62,0.16); U=3 },
    @{ Name='CoreLensN';  Min=@(-0.10,0.20,-0.27); Max=@(0.10,0.34,-0.245); U=5 },
    @{ Name='CoreLensS';  Min=@(-0.10,0.20,0.245); Max=@(0.10,0.34,0.27); U=5 },
    @{ Name='CoreLensE';  Min=@(0.245,0.20,-0.10); Max=@(0.27,0.34,0.10); U=5 },
    @{ Name='CoreLensW';  Min=@(-0.27,0.20,-0.10); Max=@(-0.245,0.34,0.10); U=5 },
    @{ Name='CoreFinNE';  Min=@(0.20,0.10,0.20); Max=@(0.42,0.44,0.42); U=0 },
    @{ Name='CoreFinNW';  Min=@(-0.42,0.10,0.20); Max=@(-0.20,0.44,0.42); U=0 },
    @{ Name='CoreFinSE';  Min=@(0.20,0.10,-0.42); Max=@(0.42,0.44,-0.20); U=0 },
    @{ Name='CoreFinSW';  Min=@(-0.42,0.10,-0.42); Max=@(-0.20,0.44,-0.20); U=0 },
    @{ Name='CoreFoot';   Min=@(-0.40,0.00,-0.40); Max=@(-0.30,0.06,-0.30); U=4 },
    @{ Name='CoreFoot2';  Min=@(0.30,0.00,0.30); Max=@(0.40,0.06,0.40); U=4 }
)

function New-Cuboid($p) {
    $x0=F $p.Min[0]; $y0=F $p.Min[1]; $z0=F $p.Min[2]
    $x1=F $p.Max[0]; $y1=F $p.Max[1]; $z1=F $p.Max[2]
    $u=F ((($p.U + 0.5) / $palette.Count))
    $n=$p.Name
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
<!-- SCIE-Port portable fortress control core; baked Y_UP, one render bone. -->
<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema" version="1.4.1">
  <asset><contributor><author>SCIE-Port contributors</author><authoring_tool>New-PortableFortressAssets.ps1</authoring_tool></contributor><unit name="meter" meter="1"/><up_axis>Y_UP</up_axis></asset>
  <library_geometries>
$geometries
  </library_geometries>
  <library_visual_scenes><visual_scene id="Scene" name="Scene"><node id="FortressCore" name="FortressCore" type="NODE"><matrix sid="transform">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>
$instances
  </node></visual_scene></library_visual_scenes>
  <scene><instance_visual_scene url="#Scene"/></scene>
</COLLADA>
"@
New-Item -ItemType Directory -Force -Path $models, $textures | Out-Null
[IO.File]::WriteAllText((Join-Path $models 'FortressCore.dae'), $dae, [Text.UTF8Encoding]::new($false))

# Palette strip consumed by the core model (one flat colour per part).
$coreBitmap = New-Object Drawing.Bitmap 48, 8, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    for ($i = 0; $i -lt $palette.Count; $i++) {
        for ($x = $i * 8; $x -lt ($i + 1) * 8; $x++) {
            for ($y = 0; $y -lt 8; $y++) { $coreBitmap.SetPixel($x, $y, $palette[$i]) }
        }
    }
    $coreBitmap.Save((Join-Path $textures 'FortressCore.png'), [Drawing.Imaging.ImageFormat]::Png)
}
finally { $coreBitmap.Dispose() }

# Fortress body texture. The migrated DAE maps the whole 0..1 range onto every face,
# so this image is a single armour panel that repeats per face.
$armour = New-Object Drawing.Bitmap 64, 64, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $base = [Drawing.Color]::FromArgb(255, 46, 50, 56)
    $bevel = [Drawing.Color]::FromArgb(255, 74, 80, 88)
    $dark = [Drawing.Color]::FromArgb(255, 32, 35, 40)
    $bolt = [Drawing.Color]::FromArgb(255, 108, 114, 122)
    for ($x = 0; $x -lt 64; $x++) {
        for ($y = 0; $y -lt 64; $y++) { $armour.SetPixel($x, $y, $base) }
    }
    for ($x = 0; $x -lt 64; $x++) {
        $armour.SetPixel($x, 0, $bevel); $armour.SetPixel($x, 1, $bevel); $armour.SetPixel($x, 62, $dark); $armour.SetPixel($x, 63, $dark)
    }
    for ($y = 0; $y -lt 64; $y++) {
        $armour.SetPixel(0, $y, $bevel); $armour.SetPixel(1, $y, $bevel); $armour.SetPixel(62, $y, $dark); $armour.SetPixel(63, $y, $dark)
    }
    for ($x = 8; $x -lt 56; $x++) { $armour.SetPixel($x, 31, $dark); $armour.SetPixel($x, 32, $bevel) }
    foreach ($bx in @(5, 58)) {
        foreach ($by in @(5, 58)) {
            for ($dx = 0; $dx -lt 2; $dx++) {
                for ($dy = 0; $dy -lt 2; $dy++) { $armour.SetPixel($bx + $dx, $by + $dy, $bolt) }
            }
        }
    }
    $armour.Save((Join-Path $textures 'PortableFortress.png'), [Drawing.Imaging.ImageFormat]::Png)
}
finally { $armour.Dispose() }

Write-Host "Generated: $(Join-Path $models 'FortressCore.dae')"
Write-Host "Generated: $(Join-Path $textures 'FortressCore.png')"
Write-Host "Generated: $(Join-Path $textures 'PortableFortress.png')"
