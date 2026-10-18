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

# Database merge safety (vanilla GUID tables + rules) lives in one shared place so
# Build-NetMod.ps1 and scripts/Test-XdbGuards.ps1 use the exact same validator.
. (Join-Path $PSScriptRoot 'XdbMergeSafety.ps1')

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

$dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
$dotnetSdkRoot = Join-Path (Split-Path -Parent $dotnetPath) 'sdk'
$sdkMsBuild = Get-ChildItem -LiteralPath $dotnetSdkRoot -Directory |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' -and (Test-Path -LiteralPath (Join-Path $_.FullName 'MSBuild.dll')) } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Get-Item -LiteralPath (Join-Path $_.FullName 'MSBuild.dll') } |
    Select-Object -First 1
if ($null -eq $sdkMsBuild) {
    throw "No installed .NET SDK MSBuild.dll found under $dotnetSdkRoot"
}

& $dotnetPath $sdkMsBuild.FullName $projectPath /restore /t:Build "/p:Configuration=$Configuration" "/p:OutputPath=$buildDir\" "/p:SCNetGameDir=$GameDir" /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE"
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

    # Guard 2 / 3 / 4: database merge safety, validated against the ACTUAL packaged
    # .xdb contents using the SAME shared validator as scripts/Test-XdbGuards.ps1.
    $xdbTexts = [ordered]@{}
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName -match '^Assets/Database/.*\.xdb$') {
            $reader = New-Object System.IO.StreamReader($entry.Open())
            $xdbTexts[$entry.FullName] = $reader.ReadToEnd()
            $reader.Dispose()
        }
    }
    $dbFailures = Test-XdbMergeSafety -XdbTexts $xdbTexts
    if ($dbFailures.Count -gt 0) {
        $detail = ($dbFailures | ForEach-Object { "  - $_" }) -join "`n"
        throw ("Database merge-safety validation FAILED ($($dbFailures.Count) problem(s)):`n$detail")
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
