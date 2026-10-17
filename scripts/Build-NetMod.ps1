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

# Canonical vanilla database folder/template GUIDs. A mod that reuses these must
# match the game's own GUID exactly; using a fresh GUID with a vanilla NAME (e.g.
# an "Entities" folder) produces a rogue duplicate folder and the x25.01.17
# merge throws "Another database object with name ... is already nested".
$vanillaFolderGuids = @{
    'Entities'       = '5fcdf35f-7001-442c-883e-e0502b15d291'
    'Gameplay'       = 'd3d4b692-acc9-4128-9b99-a5acf1de1fbb'
    'BlockBehaviors' = '00c97f0f-731e-481c-9909-eae9cc5ee940'
}
$vanillaProjectGuid = '85023bf8-1c90-4dd1-9442-e6c13691d078'

& (Join-Path $PSScriptRoot 'Test-Environment.ps1') -GameDir $GameDir
& (Join-Path $PSScriptRoot 'Test-Logic.ps1')

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
        'Assets/Textures/Speedboat.png',
        'Assets/Blocks/BuilderTools.csv',
        'Assets/Database/Building.xdb'
    )) {
        if ($entries -notcontains $requiredAsset) {
            throw "Package is missing required asset: $requiredAsset"
        }
    }
    if ($entries | Where-Object { $_ -match '^(Survivalcraft|Engine|EntitySystem)\.(dll|exe)$' }) {
        throw 'Package unexpectedly contains proprietary game assemblies.'
    }

    # Guard 1: block class names must be unique across every block CSV.
    $classNames = New-Object System.Collections.Generic.List[string]
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName -match '^Assets/Blocks/.*\.csv$') {
            $reader = New-Object System.IO.StreamReader($entry.Open())
            $csv = $reader.ReadToEnd()
            $reader.Dispose()
            $lines = @($csv -split "`r?`n" | Where-Object { $_.Trim().Length -gt 0 })
            for ($i = 1; $i -lt $lines.Count; $i++) {
                $first = ($lines[$i] -split ';')[0].Trim()
                if ($first.Length -gt 0) { $classNames.Add($first) }
            }
        }
    }
    $dupes = $classNames | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name }
    if ($dupes) {
        throw ("Duplicate block class names in package CSVs: " + ($dupes -join ', '))
    }

    # Guard 2: any vanilla-named folder / project template must carry the exact
    # vanilla GUID, so the mod never produces a rogue duplicate database folder.
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName -match '^Assets/Database/.*\.xdb$') {
            $reader = New-Object System.IO.StreamReader($entry.Open())
            $xdb = $reader.ReadToEnd()
            $reader.Dispose()

            foreach ($folderMatch in [regex]::Matches($xdb, '<Folder\s+Name="([^"]+)"\s+Guid="([^"]+)"')) {
                $folderName = $folderMatch.Groups[1].Value
                $folderGuid = $folderMatch.Groups[2].Value
                if ($vanillaFolderGuids.ContainsKey($folderName) -and
                    $folderGuid -ne $vanillaFolderGuids[$folderName]) {
                    throw ("$($entry.FullName): folder '$folderName' uses non-vanilla GUID $folderGuid " +
                        "(expected $($vanillaFolderGuids[$folderName])). This re-triggers the x25.01.17 database merge error.")
                }
            }
            foreach ($projMatch in [regex]::Matches($xdb, '<ProjectTemplate\s+Name="Project"\s+Guid="([^"]+)"')) {
                if ($projMatch.Groups[1].Value -ne $vanillaProjectGuid) {
                    throw ("$($entry.FullName): ProjectTemplate uses non-vanilla GUID $($projMatch.Groups[1].Value) " +
                        "(expected $vanillaProjectGuid).")
                }
            }
        }
    }

    Write-Host ("Block class names unique: " + (($classNames | Sort-Object -Unique) -join ', '))
}
finally {
    $archive.Dispose()
}

$packageHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $packagePath).Hash
Write-Host "Package: $packagePath"
Write-Host "Entries: $($entries -join ', ')"
Write-Host "SHA-256: $packageHash"
