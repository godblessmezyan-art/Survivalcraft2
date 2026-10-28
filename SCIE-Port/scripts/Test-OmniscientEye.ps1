[CmdletBinding()]
param([string]$GameDir = 'E:\SCNET\SCNETx25.01.17')
$ErrorActionPreference = 'Stop'
$eyeRoot = Split-Path -Parent $PSScriptRoot
$eyeProject = Join-Path $eyeRoot 'src\SCIEPort'
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'Engine.dll')))
$eyeEngine = [Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
$eyeLoad = $eyeEngine.GetType('Engine.Media.ModelData', $true).GetMethod('Load', [Type[]]@([IO.Stream]))
$eyeStream = [IO.File]::OpenRead((Join-Path $eyeProject 'Assets\Models\OmniscientEye.dae'))
try { $eyeModel = $eyeLoad.Invoke($null, @($eyeStream)) } finally { $eyeStream.Dispose() }
if ($eyeModel.Bones.Count -ne 1 -or $eyeModel.Meshes.Count -ne 25) { throw 'Eye model must import as 1 bone / 25 meshes.' }
$eyeTriangles = 0
foreach ($mesh in $eyeModel.Meshes) { foreach ($part in $mesh.MeshParts) { $eyeTriangles += $part.IndicesCount / 3 } }
if ($eyeTriangles -ne 784) { throw "Unexpected Eye triangles: $eyeTriangles" }
[xml]$eyeXml = Get-Content -Raw (Join-Path $eyeProject 'Assets\Models\OmniscientEye.dae')
if ($eyeXml.COLLADA.asset.up_axis -ne 'Y_UP') { throw 'Eye model must use Y_UP.' }
$eyeNodes = @($eyeXml.SelectNodes("//*[local-name()='visual_scene']//*[local-name()='node']"))
if ($eyeNodes.Count -ne 1 -or $eyeNodes[0].matrix.InnerText -ne '1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1') { throw 'Eye transforms must be baked.' }
foreach ($array in $eyeXml.SelectNodes("//*[local-name()='float_array'][contains(@id,'-uv-array')]")) {
    foreach ($number in ($array.InnerText -split '\s+')) {
        $v = [double]::Parse($number, [Globalization.CultureInfo]::InvariantCulture)
        if ($v -lt 0 -or $v -gt 1) { throw 'Eye UV outside texture.' }
    }
}
Add-Type -AssemblyName System.Drawing
$eyeBitmap = [Drawing.Bitmap]::FromFile((Join-Path $eyeProject 'Assets\Textures\OmniscientEye.png'))
try {
    if ($eyeBitmap.Width -ne 128 -or $eyeBitmap.Height -ne 16) { throw 'Eye palette must be power-of-two 128x16.' }
    for ($x=0; $x -lt $eyeBitmap.Width; $x++) { for ($y=0; $y -lt $eyeBitmap.Height; $y++) {
        if ($eyeBitmap.GetPixel($x,$y).A -ne 255) { throw 'Eye palette must be completely opaque.' }
    } }
} finally { $eyeBitmap.Dispose() }
$eyeIds = @{}
foreach ($file in Get-ChildItem (Join-Path $eyeProject 'Features') -Recurse -Filter *.cs) {
    foreach ($match in [regex]::Matches([IO.File]::ReadAllText($file.FullName), 'const int Index\s*=\s*(\d+)')) {
        $id = [int]$match.Groups[1].Value
        if ($eyeIds.ContainsKey($id)) { throw "Duplicate Block ID $id in $file and $($eyeIds[$id])" }
        $eyeIds[$id] = $file.FullName
    }
}
if (-not $eyeIds.ContainsKey(949)) { throw 'Eye ID 949 missing.' }
foreach ($file in Get-ChildItem (Join-Path $eyeProject 'Features\OmniscientEye') -Filter *.cs) {
    if ([IO.File]::ReadAllText($file.FullName) -match '\b(ChangeCell|DestroyCell|SetCellValueFast|AddEntity|RemoveEntity|RemoveActiveTool|DamageActiveTool)\s*\(') { throw "Read-only contract violated: $file" }
}
$eyeRow = Import-Csv -Delimiter ';' (Join-Path $eyeProject 'Assets\Blocks\OmniscientEye.csv')
if ($eyeRow.IsPlaceable -ne 'FALSE' -or $eyeRow.Durability -ne '-1' -or $eyeRow.DefaultCreativeData -ne '0') { throw 'Eye item contract violated.' }
Write-Host "Omniscient Eye target import: 1 bone / 25 meshes / $eyeTriangles triangles; opaque PNG, UV, IDs and read-only checks passed."
