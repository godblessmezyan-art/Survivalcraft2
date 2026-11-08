[CmdletBinding()]
param(
    [string]$GameDir = $env:SCNET_GAME_DIR
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $root 'SCIE-Port\src\SCIEPort'
$project = Join-Path $projectDir 'SCIEPort.csproj'
$info = Get-Content -LiteralPath (Join-Path $projectDir 'modinfo.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$version = [string]$info.Version
$artifacts = Join-Path $root 'SCIE-Port\artifacts'
$stage = Join-Path $artifacts 'fast-package'
$package = Join-Path $artifacts ('荒野探险家-' + $version + '.netmod')

# This is an explicitly untested fast-build path. Do not call the full
# Build-NetMod.ps1, Test-Environment.ps1, test suites, or package auditors.
if ([string]::IsNullOrWhiteSpace($GameDir)) {
    throw 'Set SCNET_GAME_DIR to the Windows runner local SCNET x25.01.17 game directory.'
}
& dotnet build $project -c Release --nologo "-p:SCNetGameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw 'C# compilation failed; no .netmod was published.' }

$dll = Join-Path $projectDir 'bin\Release\net472\SCIEPort.dll'
if (-not (Test-Path -LiteralPath $dll)) {
    throw 'C# compiler did not produce SCIEPort.dll.'
}

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $stage 'SCIEPort.dll')
Copy-Item -LiteralPath (Join-Path $projectDir 'modinfo.json') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'SCIE-Port\THIRD-PARTY-NOTICES.md') -Destination $stage -ErrorAction SilentlyContinue
if (-not (Test-Path -LiteralPath (Join-Path $stage 'THIRD-PARTY-NOTICES.md'))) {
    Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.md') -Destination $stage
}
Copy-Item -LiteralPath (Join-Path $projectDir 'Assets') -Destination (Join-Path $stage 'Assets') -Recurse
# Known x25.01.17 mobile unsupported format, not a test or archive audit.
$unsupportedWebp = Join-Path $stage 'Assets\Textures\KellyExpansion.webp'
if (Test-Path -LiteralPath $unsupportedWebp) {
    Remove-Item -LiteralPath $unsupportedWebp -Force
}

if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Force }
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($package, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($stage.Length).TrimStart('\', '/').Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $_.FullName, $relative, [IO.Compression.CompressionLevel]::Optimal
        ) | Out-Null
    }
}
finally { $archive.Dispose() }
Write-Host "Fast-built (not tested or audited): $package"
Write-Output $package
