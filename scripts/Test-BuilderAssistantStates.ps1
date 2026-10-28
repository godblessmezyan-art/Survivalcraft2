[CmdletBinding()]
param([string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' }))
$ErrorActionPreference = 'Stop'
$GameDir = (Resolve-Path -LiteralPath $GameDir).Path
$expected = '3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD'
if ((Get-FileHash -LiteralPath (Join-Path $GameDir 'Survivalcraft.exe') -Algorithm SHA256).Hash -ne $expected) {
    throw 'Target state tests require the audited public Windows x25.01.17 assembly.'
}
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tests\BuilderAssistant.TargetTests\BuilderAssistant.TargetTests.csproj'
& dotnet build $project -c Release "-p:SCNetGameDir=$GameDir" --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Target state probe build failed.' }
& (Join-Path $repoRoot 'tests\BuilderAssistant.TargetTests\bin\Release\net472\BuilderAssistant.TargetTests.exe') $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Target state probe failed.' }
