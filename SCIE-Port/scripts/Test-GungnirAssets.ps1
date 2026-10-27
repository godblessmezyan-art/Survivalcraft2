[CmdletBinding()]
param([string]$GameDir)
$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/SCIEPort'
$modelPath = Join-Path $project 'Assets/Models/Gungnir.dae'
[xml]$xml = [IO.File]::ReadAllText($modelPath)
$ns = New-Object Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace('c', 'http://www.collada.org/2005/11/COLLADASchema')
if ($xml.COLLADA.asset.up_axis -ne 'Y_UP') { throw 'Gungnir axis mismatch' }
$meshes = @($xml.SelectNodes('//c:geometry', $ns))
if ($meshes.Count -lt 30) { throw 'Gungnir authored structures missing' }
$triangles = 0
foreach ($t in $xml.SelectNodes('//c:triangles', $ns)) {
    $triangles += [int]$t.count
    if (@($t.p -split '\s+' | Where-Object { $_ }).Count -ne [int]$t.count * 9) { throw 'Gungnir index count mismatch' }
    if ($null -eq $t.SelectSingleNode('c:input[@semantic="TEXCOORD" and @set="0"]', $ns)) { throw 'Gungnir UV set missing' }
}
if ($triangles -lt 1000 -or $triangles -gt 4000) { throw 'Gungnir geometry budget violated' }
$engine = [Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
$load = $engine.GetType('Engine.Media.ModelData', $true).GetMethod('Load', [Type[]]@([IO.Stream]))
$stream = [IO.File]::OpenRead($modelPath)
try { $model = $load.Invoke($null, @($stream)) } finally { $stream.Dispose() }
if ($model.Bones.Count -ne 1 -or $model.Meshes.Count -ne $meshes.Count) { throw 'Gungnir target importer contract failed' }
foreach ($asset in @('Assets/Textures/Gungnir.png','Assets/Textures/GungnirSkills.png','Assets/Textures/GungnirRunes.png','Assets/Audio/Gungnir/Invoke.wav','Assets/Audio/Gungnir/Impact.wav','Assets/Blocks/Gungnir.csv','Assets/Database/Gungnir.xdb')) {
    if (-not (Test-Path -LiteralPath (Join-Path $project $asset))) { throw "Missing Gungnir asset: $asset" }
}
Add-Type -AssemblyName System.Drawing
$skills = New-Object Drawing.Bitmap((Join-Path $project 'Assets/Textures/GungnirSkills.png'))
$runes = New-Object Drawing.Bitmap((Join-Path $project 'Assets/Textures/GungnirRunes.png'))
try {
    if ($skills.Width -ne 512 -or $skills.Height -ne 256) { throw 'Gungnir skill atlas must remain 512x256.' }
    if ($runes.Width -ne 1024 -or $runes.Height -ne 256) { throw 'Gungnir rune atlas must be 8 cells in a 1024x256 strip.' }
    if ($skills.GetPixel(0, 0).A -ne 0 -or $skills.GetPixel(255, 0).A -ne 0 -or $skills.GetPixel(511, 255).A -ne 0) { throw 'Gungnir skill atlas lost its transparent background.' }
    $signatures = New-Object 'System.Collections.Generic.HashSet[string]'
    for ($slot = 0; $slot -lt 8; $slot++) {
        $bits = New-Object Text.StringBuilder
        foreach ($y in 40, 72, 104, 136, 168, 200) {
            foreach ($x in 32, 48, 64, 80, 96) {
                [void]$bits.Append($(if ($runes.GetPixel($slot * 128 + $x, $y).A -gt 80) { '1' } else { '0' }))
            }
        }
        [void]$signatures.Add($bits.ToString())
    }
    if ($signatures.Count -lt 7) { throw "Gungnir rune glyphs are not visually independent enough ($($signatures.Count)/8 signatures)." }
}
finally { $skills.Dispose(); $runes.Dispose() }
$csv = Import-Csv -LiteralPath (Join-Path $project 'Assets/Blocks/Gungnir.csv') -Delimiter ';'
if (@($csv).Count -ne 1 -or $csv.'Class Name' -ne 'GungnirBlock' -or $csv.Behaviors -ne 'SCIEPortGungnir' -or $csv.MaxStacking -ne '1' -or $csv.IsPlaceable -ne 'FALSE') { throw 'Gungnir item registration changed' }
Write-Host "Gungnir target ModelData.Load: $($model.Bones.Count) bone, $($model.Meshes.Count) meshes, $triangles triangles; transparent skills + 8-glyph rune atlas + UV/asset/CSV checks passed."
