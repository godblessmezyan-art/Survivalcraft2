[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDir,
    [Parameter(Mandatory)][string]$AssemblyPath,
    [string]$Package,
    [switch]$PackageOnly
)
$ErrorActionPreference = 'Stop'
$portRoot = Split-Path -Parent $PSScriptRoot
if (-not $PackageOnly) {
    & dotnet build (Join-Path $portRoot 'tests/SCIEPort.Blueprints.Tests/SCIEPort.Blueprints.Tests.csproj') -c Release "-p:SCNetGameDir=$GameDir" --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tiangong tests did not build.' }
    & (Join-Path $portRoot 'tests/SCIEPort.Blueprints.Tests/bin/Release/net472/SCIEPort.Blueprints.Tests.exe') $GameDir ([IO.Path]::GetFullPath($AssemblyPath))
    if ($LASTEXITCODE -ne 0) { throw 'Tiangong native/network/terrain checks failed.' }
}
if ($Package) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Package))
    try {
        foreach ($name in @('SCIEPort.dll', 'Assets/Blocks/TiangongBlueprint.csv', 'Assets/Database/TiangongBlueprint.xdb', 'Assets/Textures/TiangongBlueprintSkill.png')) {
            $entry = $zip.GetEntry($name)
            if (-not $entry) { throw "Missing Tiangong resource: $name" }
            $source = if ($name -eq 'SCIEPort.dll') { $AssemblyPath } else { Join-Path $portRoot ('src/SCIEPort/' + $name) }
            $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') } finally { $sha.Dispose(); $stream.Dispose() }
            if ($hash -ne (Get-FileHash -LiteralPath $source).Hash) { throw "Tiangong package bytes differ: $name" }
        }
        if ($zip.GetEntry('BuilderAssistant.dll') -or $zip.GetEntry('CelestialCodex.dll')) { throw 'Unified package must contain only SCIEPort.dll.' }
    } finally { $zip.Dispose() }
    Write-Host 'Tiangong package matches independent sources and final SCIEPort.dll.'
}
