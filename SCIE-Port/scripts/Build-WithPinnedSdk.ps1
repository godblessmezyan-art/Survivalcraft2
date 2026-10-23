[CmdletBinding()]
param(
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'D:\SC' }),
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

# The repository global.json pins .NET SDK 9.0.300, which is installed per-user
# at %LOCALAPPDATA%\Microsoft\dotnet and is therefore absent from a default
# PATH. Export DOTNET_ROOT/roll-forward and prepend it before delegating so
# MSBuild resolution succeeds without mutating machine state.
$ErrorActionPreference = 'Stop'
$localDotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (-not (Test-Path -LiteralPath (Join-Path $localDotnet 'dotnet.exe'))) {
    throw "Pinned .NET SDK not found at $localDotnet; run dotnet-install.ps1 for the global.json version first."
}
$env:DOTNET_ROOT = $localDotnet
$env:DOTNET_ROLL_FORWARD = 'Major'
$env:PATH = "$localDotnet;" + $env:PATH
& (Join-Path $PSScriptRoot 'Build-NetMod.ps1') -GameDir $GameDir -Configuration $Configuration
exit $LASTEXITCODE
