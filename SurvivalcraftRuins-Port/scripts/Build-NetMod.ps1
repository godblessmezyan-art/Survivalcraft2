[CmdletBinding()]
param(
    [string]$GameDir,
    [ValidateSet('Release')][string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Resolve-GameDir.ps1')
$GameDir = Resolve-SurvivalcraftGameDirectory -RequestedPath $GameDir
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifacts = Join-Path $root 'artifacts'
$stage = Join-Path $artifacts 'stage'
$package = Join-Path $artifacts 'SurvivalcraftRuins.netmod'
$hashFile = Join-Path $artifacts 'SurvivalcraftRuins.sha256'
$logFile = Join-Path $artifacts 'SurvivalcraftRuins-build.log'
$project = Join-Path $root 'src\SurvivalcraftRuins\SurvivalcraftRuins.csproj'
$tests = Join-Path $root 'tests\SurvivalcraftRuins.Tests\SurvivalcraftRuins.Tests.csproj'
$source = Join-Path $root 'src\SurvivalcraftRuins'

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
if (Test-Path -LiteralPath $logFile) { Remove-Item -LiteralPath $logFile -Force }
Start-Transcript -LiteralPath $logFile | Out-Null
try {
    & (Join-Path $PSScriptRoot 'Test-Environment.ps1') -GameDir $GameDir
    if ($LASTEXITCODE -ne 0) { throw 'Environment validation failed.' }

    & dotnet restore $tests "-p:SCNetGameDir=$GameDir" --ignore-failed-sources
    if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }
    & dotnet test $tests -c $Configuration "-p:SCNetGameDir=$GameDir" --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Compatibility tests failed.' }
    & dotnet build $project -c $Configuration "-p:SCNetGameDir=$GameDir" --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

    if ([IO.Path]::GetFullPath($stage) -ne (Join-Path $artifacts 'stage')) { throw 'Unsafe stage path.' }
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    New-Item -ItemType Directory -Path (Join-Path $stage 'Assets\Structures') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $stage 'Assets\Loot') -Force | Out-Null

    $dll = Join-Path $source "bin\$Configuration\net472\SurvivalcraftRuins.dll"
    $modAssembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($dll)
    $modFramework = $modAssembly.GetCustomAttributesData() |
        Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.Versioning.TargetFrameworkAttribute' } |
        ForEach-Object { $_.ConstructorArguments[0].Value }
    if ($modFramework -ne '.NETFramework,Version=v4.7.2') { throw "Mod DLL target framework is $modFramework" }
    $forbiddenReferences = @($modAssembly.GetReferencedAssemblies() | Where-Object { $_.Name -in @('System.Text.Json') })
    if ($forbiddenReferences.Count -ne 0) { throw 'Mod DLL has a forbidden System.Text.Json dependency.' }
    Copy-Item -LiteralPath $dll -Destination (Join-Path $stage 'SurvivalcraftRuins.dll')
    Copy-Item -LiteralPath (Join-Path $source 'modinfo.json') -Destination (Join-Path $stage 'modinfo.json')
    Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $stage 'LICENSE')
    Get-ChildItem -LiteralPath (Join-Path $source 'Assets\Structures') -Filter '*.json' -File |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $stage 'Assets\Structures') }
    Get-ChildItem -LiteralPath (Join-Path $source 'Assets\Loot') -Filter '*.json' -File |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $stage 'Assets\Loot') }

    $metadata = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $stage 'modinfo.json') | ConvertFrom-Json
    if ($metadata.ApiVersion -ne '1.42' -or $metadata.ScVersion -ne '2.4.10.6') { throw 'modinfo target versions are invalid.' }
    if ($metadata.Dependencies -isnot [Array]) { throw 'modinfo Dependencies must be an array.' }
    if ((Get-ChildItem -LiteralPath (Join-Path $stage 'Assets\Structures') -Filter '*.json').Count -ne 13) { throw 'Expected 13 packaged JSON templates.' }
    if ((Get-ChildItem -LiteralPath (Join-Path $stage 'Assets\Loot') -Filter '*.json').Count -ne 1) { throw 'Expected one packaged loot configuration.' }
    $forbidden = Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object {
        $_.Name -in @('Engine.dll', 'EntitySystem.dll', 'Survivalcraft.exe', 'Survivalcraft.dll', 'Content.scpak') -or $_.Extension -in @('.apk', '.pdb')
    }
    if ($forbidden) { throw "Forbidden proprietary files in package stage: $($forbidden.FullName -join ', ')" }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Force }
    $stream = [IO.File]::Open($package, [IO.FileMode]::CreateNew)
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $false)
        try {
            foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName) {
                $relative = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
                $entry = $zip.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
                $input = $file.OpenRead()
                $output = $entry.Open()
                try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
            }
        }
        finally { $zip.Dispose() }
    }
    finally { $stream.Dispose() }

    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $names = @($archive.Entries | ForEach-Object FullName)
        foreach ($required in @('SurvivalcraftRuins.dll', 'modinfo.json', 'LICENSE', 'Assets/Loot/pools.json')) {
            if ($names -notcontains $required) { throw "Package missing $required" }
        }
        if (($names | Where-Object { $_ -like 'Assets/Structures/*.json' }).Count -ne 13) { throw 'Package structure template count mismatch.' }
    }
    finally { $archive.Dispose() }

    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $package).Hash
    "$hash  SurvivalcraftRuins.netmod" | Set-Content -Encoding ASCII -LiteralPath $hashFile
    Write-Host "PACKAGE=$package"
    Write-Host "SIZE=$((Get-Item -LiteralPath $package).Length)"
    Write-Host "SHA256=$hash"
    Write-Host 'Build, tests, packaging, and content validation passed.'
}
finally {
    Stop-Transcript | Out-Null
}
