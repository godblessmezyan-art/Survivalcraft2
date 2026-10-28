[CmdletBinding()]
param([string]$GameDir, [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$project = Join-Path $ProjectRoot 'src\SCIEPort'
$modelPath = Join-Path $project 'Assets\Models\QinglongCrown.dae'
[xml]$xml = Get-Content -LiteralPath $modelPath -Raw -Encoding UTF8
$ns = [Xml.XmlNamespaceManager]::new($xml.NameTable)
$ns.AddNamespace('c', 'http://www.collada.org/2005/11/COLLADASchema')
if ($xml.COLLADA.asset.up_axis -ne 'Y_UP') { throw 'Crown must be Y_UP meters.' }
$parts = @($xml.SelectNodes('//c:geometry', $ns))
if ($parts.Count -ne 31 -or $xml.SelectNodes('//c:visual_scene//c:node', $ns).Count -ne 1) { throw 'Crown topology changed: expected 31 structures on one render bone.' }
foreach ($name in @('HornLeft','HornRight','WhiskerLeft','WhiskerRight','JadeHeart','ForeheadScale','NapeScale0','NapeScale1','NapeScale2')) {
    if (-not $xml.SelectSingleNode("//c:geometry[@name='$name']", $ns)) { throw "Missing actual geometry $name" }
}
$triangleCount = 0
foreach ($part in $parts) {
    $positions = @($part.SelectSingleNode(".//c:source[contains(@id,'-positions')]/c:float_array",$ns).InnerText -split '\s+' | Where-Object { $_ } | ForEach-Object { [double]::Parse($_,[Globalization.CultureInfo]::InvariantCulture) })
    $triangles = $part.SelectSingleNode('.//c:triangles',$ns)
    $indices = @($triangles.p -split '\s+' | Where-Object { $_ } | ForEach-Object { [int]$_ })
    $count = [int]$triangles.count
    $triangleCount += $count
    if ($indices.Count -ne $count*9) { throw "Bad index stride: $($part.name)" }
    for ($i=0;$i -lt $indices.Count;$i+=9) {
        $p = @(); foreach ($j in 0,3,6) {
            $index = $indices[$i+$j]
            if ($index -lt 0 -or $index -ge $positions.Count/3) { throw 'Out of range vertex' }
            $p += ,@($positions[$index*3],$positions[$index*3+1],$positions[$index*3+2])
        }
        $a=@();$b=@();foreach($k in 0,1,2){$a+=($p[1][$k]-$p[0][$k]);$b+=($p[2][$k]-$p[0][$k])}
        $cross=@(($a[1]*$b[2]-$a[2]*$b[1]),($a[2]*$b[0]-$a[0]*$b[2]),($a[0]*$b[1]-$a[1]*$b[0]))
        if (($cross[0]*$cross[0]+$cross[1]*$cross[1]+$cross[2]*$cross[2]) -lt 1e-16) { throw 'Degenerate crown triangle' }
    }
}
if ($triangleCount -ne 680) { throw "Unexpected mobile triangle budget: $triangleCount" }
Add-Type -AssemblyName System.Drawing
$texture = [Drawing.Bitmap]::new((Join-Path $project 'Assets\Textures\QinglongCrown.png'))
try {
    if ($texture.Width -ne 128 -or $texture.Height -ne 16) { throw 'Crown atlas must be 128x16.' }
    for($x=0;$x -lt 128;$x++){if($texture.GetPixel($x,8).A -ne 255){throw 'Crown material must be opaque.'}}
} finally { $texture.Dispose() }

[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'EntitySystem.dll'))
$game = [Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$stream = [IO.File]::OpenRead($modelPath)
try { $model = [Engine.Media.ModelData]::Load($stream) } finally { $stream.Dispose() }
if ($model.Bones.Count -ne 1 -or $model.Meshes.Count -ne 31) { throw 'Target crown importer contract failed.' }

# Inspect native heads in memory via the target's own package reader. No game asset is copied into the repository.
Add-Type -AssemblyName System.IO.Compression
$reader = $game.GetType('Game.SurvivalCraftModEntity').GetMethod('GetDecipherStream',[Reflection.BindingFlags]'NonPublic,Static')
$content = [IO.File]::OpenRead((Join-Path $GameDir 'Content.scpak'))
$decoded = $reader.Invoke($null,@($content))
$archive = [IO.Compression.ZipArchive]::new($decoded)
try {
    foreach ($gender in 'Male','Female') {
        $entry = $archive.GetEntry("Assets/Models/Human$gender.dae")
        $stream = $entry.Open()
        try { $human = [Engine.Media.ModelData]::Load($stream) } finally { $stream.Dispose() }
        $headIndex = -1
        for($i=0;$i -lt $human.Bones.Count;$i++){if($human.Bones[$i].Name -eq 'Head'){$headIndex=$i}}
        if($headIndex -lt 0){throw 'Native head missing'}
        $head = $human.Bones[$headIndex]
        $bind = $head.Transform
        $parent = $head.ParentBoneIndex
        while($parent -ge 0){$bind=$bind*$human.Bones[$parent].Transform;$parent=$human.Bones[$parent].ParentBoneIndex}
        $bind.Translation = [Engine.Vector3]::Zero
        $correction = [Engine.Matrix]::Invert($bind)
        # Synthetic poses test basis/scale and row-matrix composition, NOT actual game animation.
        foreach($pitch in @(0.0,0.7,-0.7,1.570796)) {
            $pose = [Engine.Matrix]::CreateRotationX([float]$pitch)*[Engine.Matrix]::CreateRotationY(0.43)*[Engine.Matrix]::CreateTranslation(13,2,-9)
            $view = [Engine.Matrix]::CreateLookAt([Engine.Vector3]::new(14,3,-12),[Engine.Vector3]::new(13,2,-9),[Engine.Vector3]::UnitY)
            $inverseView = [Engine.Matrix]::Invert($view)
            $recovered = $correction*($bind*$pose*$view)*$inverseView
            $point = [Engine.Vector3]::new(0.2,0.55,-0.18)
            $actual = [Engine.Vector3]::Transform($point,$recovered)
            $expected = [Engine.Vector3]::Transform($point,$pose)
            if([Engine.Vector3]::Distance($actual,$expected) -gt 0.0001){throw "Head calibration roundtrip failed: $gender/$pitch"}
        }
        Write-Host "Native Human$gender Head bind correction + four synthetic camera/pose roundtrips passed (not in-game pose validation)."
    }
} finally { $archive.Dispose();$decoded.Dispose();$content.Dispose() }
Write-Host "Qinglong target ModelData.Load: 1 bone / 31 meshes / $triangleCount triangles; geometry, atlas and native Head basis checks passed."
