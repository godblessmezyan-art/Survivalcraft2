[CmdletBinding()]
param(
    [string]$ModelPath,
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' })
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ModelPath) {
    $ModelPath = Join-Path $repoRoot 'src\SCIEPort\Assets\Models\PortableFortress.dae'
}
if (-not (Test-Path -LiteralPath $ModelPath)) { throw "Portable Fortress model missing: $ModelPath" }

[xml]$document = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $ModelPath))
$ns = New-Object Xml.XmlNamespaceManager($document.NameTable)
$ns.AddNamespace('c', 'http://www.collada.org/2005/11/COLLADASchema')

$rootNodes = @($document.SelectNodes('//c:library_visual_scenes/c:visual_scene/c:node', $ns))
$geometries = @($document.SelectNodes('//c:library_geometries/c:geometry', $ns))
$triangles = @($document.SelectNodes('//c:triangles', $ns))
if ($document.COLLADA.asset.up_axis -ne 'Y_UP') { throw 'Portable Fortress must be Y_UP.' }
if ($rootNodes.Count -ne 1) { throw "Portable Fortress must import as one static root; got $($rootNodes.Count) scene nodes." }
if ($rootNodes[0].matrix.InnerText.Trim() -ne '1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1') { throw 'Portable Fortress root transform is not identity.' }
if ($geometries.Count -ne 11) { throw "Portable Fortress mesh contract changed: expected 11 geometries, got $($geometries.Count)." }

$triangleCount = 0
foreach ($primitive in $triangles) {
    $triangleCount += [int]$primitive.count
    $inputs = @($primitive.SelectNodes('c:input', $ns) | Sort-Object { [int]$_.offset })
    $offsets = @($inputs | ForEach-Object { [int]$_.offset })
    if (($offsets -join ',') -ne '0,1,2') { throw "Triangle inputs are not interleaved offsets 0,1,2 in $($primitive.ParentNode.ParentNode.id)." }
    $indexText = $primitive.SelectSingleNode('c:p', $ns).InnerText
    $indices = @($indexText -split '\s+' | Where-Object { $_.Length -gt 0 })
    if ($indices.Count -ne ([int]$primitive.count * 3 * 3)) { throw "Triangle index stream length mismatch in $($primitive.ParentNode.ParentNode.id)." }
}
if ($triangleCount -ne 1248) { throw "Fortress triangle contract changed: expected 1248, got $triangleCount." }

$minX=[double]::PositiveInfinity; $minY=[double]::PositiveInfinity; $minZ=[double]::PositiveInfinity
$maxX=[double]::NegativeInfinity; $maxY=[double]::NegativeInfinity; $maxZ=[double]::NegativeInfinity
foreach ($array in $document.SelectNodes('//c:source[contains(@id,"-positions")]/c:float_array', $ns)) {
    $values = @($array.InnerText -split '\s+' | Where-Object { $_.Length -gt 0 } | ForEach-Object { [double]::Parse($_, [Globalization.CultureInfo]::InvariantCulture) })
    for ($i=0; $i -lt $values.Count; $i+=3) {
        $minX=[Math]::Min($minX,$values[$i]); $maxX=[Math]::Max($maxX,$values[$i])
        $minY=[Math]::Min($minY,$values[$i+1]); $maxY=[Math]::Max($maxY,$values[$i+1])
        $minZ=[Math]::Min($minZ,$values[$i+2]); $maxZ=[Math]::Max($maxZ,$values[$i+2])
    }
}
if ($minX -ne -5.5 -or $maxX -ne 5.5 -or $minY -ne 0 -or $maxY -ne 9 -or $minZ -ne -6.5 -or $maxZ -ne 6.5) {
    throw "Portable Fortress bounds must be X[-5.5,5.5] Y[0,9] Z[-6.5,6.5], got X[$minX,$maxX] Y[$minY,$maxY] Z[$minZ,$maxZ]."
}

$enginePath = Join-Path $GameDir 'Engine.dll'
if (-not (Test-Path -LiteralPath $enginePath)) { throw "Target Engine.dll missing: $enginePath" }
$engine = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($enginePath))
$modelDataType = $engine.GetType('Engine.Media.ModelData', $true)
$load = $modelDataType.GetMethod('Load', [Type[]]@([IO.Stream]))
if ($null -eq $load) { throw 'Target Engine.Media.ModelData.Load(Stream) was not found.' }
$stream = [IO.File]::OpenRead((Resolve-Path -LiteralPath $ModelPath))
try { $model = $load.Invoke($null, @($stream)) } finally { $stream.Dispose() }
if ($model.Bones.Count -ne 1) { throw "Target importer returned $($model.Bones.Count) bones; expected one static bone." }
if ($model.Meshes.Count -ne 11) { throw "Target importer returned $($model.Meshes.Count) meshes; expected 11." }

Write-Host 'Portable Fortress whitebox: PASS'
Write-Host "  Target importer: Engine.Media.ModelData.Load(Stream)"
Write-Host "  Bones/Meshes/Triangles: $($model.Bones.Count)/$($model.Meshes.Count)/$triangleCount"
Write-Host "  Bounds: 11 x 13 x 9 (X $minX..$maxX, Z $minZ..$maxZ, Y $minY..$maxY)"
