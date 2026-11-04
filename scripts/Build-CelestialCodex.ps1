[CmdletBinding()]
param([string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' }))
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\CelestialCodex'
$output = Join-Path $root 'artifacts\CelestialCodex'
$displayName = -join ([char[]](0x4e91, 0x5929, 0x4e66, 0x5178))
$package = Join-Path $output ($displayName + '.netmod')

# Environment.ps1 uses ReflectionOnlyLoad, so invoke with Windows PowerShell 5.1.
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-Environment.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'SCNET environment/ABI validation failed.' }
$hashes = @{
    'Survivalcraft.exe' = '3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD'
    'Engine.dll' = '98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694'
    'EntitySystem.dll' = '7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C'
}
foreach ($file in $hashes.Keys) { if ((Get-FileHash -LiteralPath (Join-Path $GameDir $file) -Algorithm SHA256).Hash -ne $hashes[$file]) { throw "Re-audit this target before building: $file" } }

& dotnet build (Join-Path $root 'tests\CelestialCodex.Tests\CelestialCodex.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Reader tests did not build.' }
& (Join-Path $root 'tests\CelestialCodex.Tests\bin\Release\net472\CelestialCodex.Tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Reader tests failed.' }
& dotnet build (Join-Path $root 'tests\CelestialCodex.TargetTests\CelestialCodex.TargetTests.csproj') -c Release "-p:SCNetGameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'Release/target tests did not build.' }
& (Join-Path $root 'tests\CelestialCodex.TargetTests\bin\Release\net472\CelestialCodex.TargetTests.exe') $GameDir $root
if ($LASTEXITCODE -ne 0) { throw 'Target CSV/XDB/member checks failed.' }

$info = Get-Content -LiteralPath (Join-Path $project 'modinfo.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($info.PackageName -ne 'CelestialCodex' -or $info.Version -ne '0.1.1' -or $info.ApiVersion -ne '1.42' -or $info.ScVersion -ne '2.4.10.6' -or $info.Dependencies.Count -ne 0) { throw 'Unexpected modinfo.' }
foreach ($other in @('src\BuilderAssistant', 'SCIE-Port\src', 'SurvivalcraftRuins-Port\src')) {
    Get-ChildItem -LiteralPath (Join-Path $root $other) -Filter modinfo.json -Recurse -File | ForEach-Object {
        $otherInfo = Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($otherInfo.PackageName -eq $info.PackageName) { throw "PackageName collision: $($_.FullName)" }
    }
}
New-Item -ItemType Directory -Force -Path $output | Out-Null
# No staging directory or recursive cleanup: only our named output is replaced.
if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($package, [IO.Compression.ZipArchiveMode]::Create)
try {
    $entries = [ordered]@{
        'CelestialCodex.dll' = (Join-Path $project 'bin\Release\net472\CelestialCodex.dll')
        'modinfo.json' = (Join-Path $project 'modinfo.json')
        'Assets/Blocks/CelestialCodex.csv' = (Join-Path $project 'Assets\Blocks\CelestialCodex.csv')
        'Assets/Database/CelestialCodex.xdb' = (Join-Path $project 'Assets\Database\CelestialCodex.xdb')
        'Assets/Textures/CelestialCodex/Book.png' = (Join-Path $project 'Assets\Textures\CelestialCodex\Book.png')
    }
    foreach ($name in $entries.Keys) { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entries[$name], $name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null }
} finally { $archive.Dispose() }
$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    if ($archive.Entries.Count -ne $entries.Count) { throw 'Unexpected package entry count.' }
    foreach ($entry in $archive.Entries) {
        if (-not $entries.Contains($entry.FullName)) { throw "Unexpected package entry: $($entry.FullName)" }
        $reader = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actual = [BitConverter]::ToString($sha.ComputeHash($reader)).Replace('-', '') } finally { $sha.Dispose(); $reader.Dispose() }
        if ($actual -ne (Get-FileHash -LiteralPath $entries[$entry.FullName] -Algorithm SHA256).Hash) { throw "Package bytes differ: $($entry.FullName)" }
    }
} finally { $archive.Dispose() }
$copy = Join-Path $output ($displayName + '-' + $info.Version + '.netmod')
Copy-Item -LiteralPath $package -Destination $copy -Force
Write-Host "Package: $package"
Write-Host "Bytes: $((Get-Item -LiteralPath $package).Length)"
Write-Host "SHA256: $((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash)"
Write-Host 'Release and package verified. Android / Local / Server / Client gameplay remains pending.'
