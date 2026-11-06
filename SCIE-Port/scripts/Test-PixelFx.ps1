param(
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' }),
    [string]$AssemblyPath,
    [string]$Package
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
& python (Join-Path $PSScriptRoot 'Test-PixelFxAssets.py')
if ($LASTEXITCODE -ne 0) { throw 'PixelFX resource/reproducibility/preservation checks failed.' }
$engine = [Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'EntitySystem.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$vector3 = $engine.GetType('Engine.Vector3',$true)
$vector2 = $engine.GetType('Engine.Vector2',$true)
$color = $engine.GetType('Engine.Color',$true)
$batch = $engine.GetType('Engine.Graphics.TexturedBatch3D',$true)
$signature = [Type[]]@($vector3,$vector3,$vector3,$vector3,$vector2,$vector2,$vector2,$vector2,$color)
if (!$batch.GetMethod('QueueQuad',$signature)) { throw 'Target TexturedBatch3D.QueueQuad ABI differs.' }
foreach ($field in @('TriangleVertices','TriangleIndices')) { if (!$batch.GetField($field)) { throw "Missing native batch buffer: $field" } }
$stream = [IO.File]::OpenRead((Join-Path $root 'src/SCIEPort/Assets/Textures/PixelFxStormhammer.png'))
try { $image = [Engine.Media.Image]::Load($stream) } finally { $stream.Dispose() }
if ($image.Width -ne 1024 -or $image.Height -ne 2048 -or $image.GetPixel(0,0).A -ne 0) { throw 'Native PNG decode/alpha failed.' }
if ($AssemblyPath) {
    $mod = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($AssemblyPath)))
    $table = $mod.GetType('SCIEPort.Features.PixelFx.ArtifactSpriteClips',$true)
    $flags = [Reflection.BindingFlags]'Static,Instance,NonPublic,Public'
    $manifest = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'src/SCIEPort/Assets/Effects/PixelFxStormhammer.json') | ConvertFrom-Json
    foreach ($clip in $manifest.clips) {
        $actual = $table.GetField($clip.field,$flags).GetValue($null)
        foreach ($pair in @(@('Name','name'),@('FirstFrame','firstFrame'),@('FrameCount','frameCount'),@('Fps','fps'),@('Priority','priority'))) {
            if ($actual.GetType().GetField($pair[0],$flags).GetValue($actual) -ne $clip.($pair[1])) { throw "Compiled frame table differs: $($clip.name)/$($pair[0])" }
        }
    }
}
if ($Package) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Package))
    try {
        foreach ($name in @('Assets/Textures/PixelFxStormhammer.png','Assets/Effects/PixelFxStormhammer.json','Assets/Effects/PixelFX-LICENSE.txt','THIRD-PARTY-NOTICES.md')) {
            $entry = $zip.GetEntry($name)
            if (!$entry) { throw "Missing PixelFX package entry: $name" }
            $stream = $entry.Open(); $memory = New-Object IO.MemoryStream
            try { $stream.CopyTo($memory); $actual = [Convert]::ToBase64String($memory.ToArray()) } finally { $stream.Dispose(); $memory.Dispose() }
            $path = if ($name -eq 'THIRD-PARTY-NOTICES.md') { Join-Path $root $name } else { Join-Path $root ('src/SCIEPort/'+$name) }
            if ($actual -ne [Convert]::ToBase64String([IO.File]::ReadAllBytes($path))) { throw "Package PixelFX bytes differ: $name" }
        }
        if ($zip.Entries | Where-Object { $_.FullName -match '\.(js|mjs)$|tools/pixelfx|gameplay-baseline' }) { throw 'Offline tool/source leaked into runtime package.' }
    } finally { $zip.Dispose() }
}
Write-Host 'PixelFX target Quad ABI, native RGBA decode, compiled frame table and requested package checks passed.'
