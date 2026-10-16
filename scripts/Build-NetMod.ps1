[CmdletBinding()]
param(
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' }),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\BuilderAssistant\BuilderAssistant.csproj'
$projectDir = Split-Path -Parent $projectPath
$artifactsDir = Join-Path $repoRoot 'artifacts'
$buildDir = Join-Path $artifactsDir 'build'
$stageDir = Join-Path $artifactsDir 'package'
$zipPath = Join-Path $artifactsDir 'BuilderAssistant.zip'
$packagePath = Join-Path $artifactsDir 'BuilderAssistant.netmod'

& (Join-Path $PSScriptRoot 'Test-Environment.ps1') -GameDir $GameDir

New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null
foreach ($directory in @($buildDir, $stageDir)) {
    if (Test-Path -LiteralPath $directory) {
        $resolved = (Resolve-Path -LiteralPath $directory).Path
        if (-not $resolved.StartsWith((Resolve-Path -LiteralPath $artifactsDir).Path, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean path outside artifacts: $resolved"
        }
        Remove-Item -Recurse -Force -LiteralPath $resolved
    }
    New-Item -ItemType Directory -Path $directory | Out-Null
}

$buildArguments = @(
    'build', $projectPath,
    '--configuration', $Configuration,
    '--output', $buildDir,
    "-p:SCNetGameDir=$GameDir"
)
& dotnet @buildArguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

$modDll = Join-Path $buildDir 'BuilderAssistant.dll'
$modInfo = Join-Path $projectDir 'modinfo.json'
if (-not (Test-Path -LiteralPath $modDll)) {
    throw "Build output not found: $modDll"
}

Copy-Item -LiteralPath $modDll -Destination $stageDir
Copy-Item -LiteralPath $modInfo -Destination $stageDir

$assetsSource = Join-Path $projectDir 'Assets'
if (Test-Path -LiteralPath $assetsSource) {
    $assetsDestination = Join-Path $stageDir 'Assets'
    Copy-Item -Recurse -LiteralPath $assetsSource -Destination $assetsDestination

    Get-ChildItem -Path $assetsDestination -Recurse -File -Filter '*.base64' | ForEach-Object {
        $encoded = [System.IO.File]::ReadAllText($_.FullName).Trim()
        $decodedPath = $_.FullName.Substring(0, $_.FullName.Length - '.base64'.Length)
        [System.IO.File]::WriteAllBytes($decodedPath, [Convert]::FromBase64String($encoded))
        Remove-Item -LiteralPath $_.FullName
    }
}

foreach ($oldPackage in @($zipPath, $packagePath)) {
    if (Test-Path -LiteralPath $oldPackage) {
        Remove-Item -Force -LiteralPath $oldPackage
    }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$outputArchive = [System.IO.Compression.ZipFile]::Open(
    $zipPath,
    [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $stageDir -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Substring($stageDir.Length).TrimStart('\', '/')
        $entryName = $relativePath.Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $outputArchive,
            $_.FullName,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $outputArchive.Dispose()
}
Move-Item -LiteralPath $zipPath -Destination $packagePath

$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $entries = @($archive.Entries | ForEach-Object FullName | Sort-Object)
    foreach ($requiredEntry in @('BuilderAssistant.dll', 'modinfo.json')) {
        if ($entries -notcontains $requiredEntry) {
            throw "Package is missing required entry: $requiredEntry"
        }
    }
    foreach ($requiredAsset in @(
        'Assets/Blocks/Speedboat.csv',
        'Assets/Database/Speedboat.xdb',
        'Assets/Models/Speedboat.dae',
        'Assets/Textures/Speedboat.png'
    )) {
        if ($entries -notcontains $requiredAsset) {
            throw "Package is missing required asset: $requiredAsset"
        }
    }
    if ($entries | Where-Object { $_ -match '^(Survivalcraft|Engine|EntitySystem)\.(dll|exe)$' }) {
        throw 'Package unexpectedly contains proprietary game assemblies.'
    }
}
finally {
    $archive.Dispose()
}

$packageHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $packagePath).Hash
Write-Host "Package: $packagePath"
Write-Host "Entries: $($entries -join ', ')"
Write-Host "SHA-256: $packageHash"
