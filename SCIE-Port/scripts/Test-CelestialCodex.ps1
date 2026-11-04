[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDir,
    [Parameter(Mandatory)][string]$AssemblyPath,
    [string]$Package,
    [switch]$PackageOnly,
    [switch]$HttpsProbe
)
$ErrorActionPreference = 'Stop'
$portRoot = Split-Path -Parent $PSScriptRoot
$repoRoot = Split-Path -Parent $portRoot
if (-not $PackageOnly) {
    $tests = Join-Path $portRoot 'tests/SCIEPort.CelestialCodex.Tests/SCIEPort.CelestialCodex.Tests.csproj'
    & dotnet build $tests -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Reader tests did not build.' }
    & (Join-Path $portRoot 'tests/SCIEPort.CelestialCodex.Tests/bin/Release/net472/SCIEPort.CelestialCodex.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Reader tests failed.' }
    $target = Join-Path $portRoot 'tests/SCIEPort.CelestialCodex.TargetTests/SCIEPort.CelestialCodex.TargetTests.csproj'
    & dotnet build $target -c Release --nologo "-p:SCNetGameDir=$GameDir"
    if ($LASTEXITCODE -ne 0) { throw 'Integrated reader target tests did not build.' }
    $arguments = @($GameDir, $repoRoot, [IO.Path]::GetFullPath($AssemblyPath))
    if ($HttpsProbe) { $arguments += '--https-probe' }
    & (Join-Path $portRoot 'tests/SCIEPort.CelestialCodex.TargetTests/bin/Release/net472/SCIEPort.CelestialCodex.TargetTests.exe') @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Integrated reader target tests failed.' }
}
if ($Package) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Package))
    try {
        if ($archive.GetEntry('CelestialCodex.dll')) { throw 'Standalone reader DLL must not be packaged.' }
        foreach ($name in @('SCIEPort.dll', 'Assets/Blocks/CelestialCodex.csv', 'Assets/Database/CelestialCodex.xdb', 'Assets/Textures/CelestialCodex/Book.png')) {
            $entry = $archive.GetEntry($name)
            if (-not $entry) { throw "Missing reader entry: $name" }
            $source = if ($name -eq 'SCIEPort.dll') { $AssemblyPath } else { Join-Path $portRoot ('src/SCIEPort/' + $name) }
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') } finally { $sha.Dispose(); $stream.Dispose() }
            if ($actual -ne (Get-FileHash -LiteralPath $source).Hash) { throw "Reader package bytes differ: $name" }
        }
    } finally { $archive.Dispose() }
    Write-Host 'Integrated reader package: source/DLL bytes verified; no standalone DLL.'
}
