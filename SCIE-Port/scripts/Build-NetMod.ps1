[CmdletBinding()]
param(
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' }),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $projectRoot 'src\SCIEPort'
$projectFile = Join-Path $projectDir 'SCIEPort.csproj'
$upstream = Join-Path $projectRoot 'upstream\SCIE'
$artifacts = Join-Path $projectRoot 'artifacts'
$build = Join-Path $artifacts 'build'
$stage = Join-Path $artifacts 'package'
$zip = Join-Path $artifacts 'SCIEPort.zip'
$package = Join-Path $artifacts 'SCIEPort.netmod'

& (Join-Path $PSScriptRoot 'Test-Environment.ps1') -GameDir $GameDir

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$resolvedArtifacts = (Resolve-Path -LiteralPath $artifacts).Path
foreach ($directory in @($build, $stage)) {
    if (Test-Path -LiteralPath $directory) {
        $resolved = (Resolve-Path -LiteralPath $directory).Path
        if (-not $resolved.StartsWith($resolvedArtifacts, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean path outside artifacts: $resolved"
        }
        Remove-Item -Recurse -Force -LiteralPath $resolved
    }
    New-Item -ItemType Directory -Path $directory | Out-Null
}

& dotnet build $projectFile --configuration $Configuration --output $build "-p:SCNetGameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed: $LASTEXITCODE" }

Copy-Item -LiteralPath (Join-Path $build 'SCIEPort.dll') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectDir 'modinfo.json') -Destination $stage
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD-PARTY-NOTICES.md') -Destination $stage
Copy-Item -Recurse -LiteralPath (Join-Path $projectDir 'Assets') -Destination (Join-Path $stage 'Assets')
$authorizedAssets = @(
    @{ Source = (Join-Path $upstream 'IndustrialMod\Models\SteamBoat.dae'); Destination = (Join-Path $stage 'Assets\Models\SteamBoat.dae') },
    @{ Source = (Join-Path $upstream 'IndustrialMod\Textures\SteamBoat.png'); Destination = (Join-Path $stage 'Assets\Textures\SteamBoat.png') }
)
foreach ($asset in $authorizedAssets) {
    if (-not (Test-Path -LiteralPath $asset.Source)) {
        throw "Missing authorized upstream asset: $($asset.Source)"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $asset.Destination) | Out-Null
    Copy-Item -LiteralPath $asset.Source -Destination $asset.Destination
}

foreach ($old in @($zip, $package)) {
    if (Test-Path -LiteralPath $old) { Remove-Item -Force -LiteralPath $old }
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length).TrimStart('\', '/').Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally { $archive.Dispose() }
Move-Item -LiteralPath $zip -Destination $package

$archive = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entries = @($archive.Entries | ForEach-Object FullName | Sort-Object)
    foreach ($requiredEntry in @(
        'SCIEPort.dll',
        'modinfo.json',
        'THIRD-PARTY-NOTICES.md',
        'Assets/Blocks/SCIEPort.csv',
        'Assets/Blocks/Watercraft.csv',
        'Assets/Database/Watercraft.xdb',
        'Assets/Models/SteamBoat.dae',
        'Assets/Textures/SteamBoat.png'
    )) {
        if ($entries -notcontains $requiredEntry) { throw "Package missing: $requiredEntry" }
    }
    if ($entries | Where-Object { $_ -match '(^|/)(Survivalcraft\.exe|Engine\.dll|EntitySystem\.dll|Jint\.dll|System\.Reflection\.TypeExtensions\.dll)$' }) {
        throw 'Package contains a prohibited game or upstream dependency binary.'
    }
    if ($entries | Where-Object { $_ -match '(^|/)(Train|Airship)' }) {
        throw 'Package contains an out-of-scope upstream vehicle resource.'
    }

    [xml]$watercraftDatabase = Get-Content -Raw -Encoding UTF8 (Join-Path $projectDir 'Assets\Database\Watercraft.xdb')
    if ($watercraftDatabase.DocumentElement.Name -ne 'SCIEPortWatercraft') {
        throw 'Watercraft.xdb must use a Mod container root, not DatabaseObjects.'
    }
    if ($watercraftDatabase.DocumentElement.ChildNodes | Where-Object { $_.Name -eq 'DatabaseObjects' }) {
        throw 'Watercraft.xdb would create an invalid nested DatabaseObjects node.'
    }

    [xml]$steamBoatModel = Get-Content -Raw -Encoding UTF8 (Join-Path $upstream 'IndustrialMod\Models\SteamBoat.dae')
    $meshNode = $steamBoatModel.SelectSingleNode("//*[local-name()='node' and @name='Cylinder']")
    if ($null -eq $meshNode) {
        throw 'Authorized SteamBoat.dae is missing the required Cylinder mesh node.'
    }

    $pngBytes = [IO.File]::ReadAllBytes((Join-Path $upstream 'IndustrialMod\Textures\SteamBoat.png'))
    if ($pngBytes.Length -lt 24 -or $pngBytes[0] -ne 137 -or $pngBytes[1] -ne 80 -or $pngBytes[2] -ne 78 -or $pngBytes[3] -ne 71) {
        throw 'Authorized SteamBoat.png does not have a valid PNG signature.'
    }
}
finally { $archive.Dispose() }

Write-Host "Package: $package"
Write-Host "Entries: $($entries -join ', ')"
Write-Host "SHA-256: $((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash)"
