[CmdletBinding()]
param(
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' })
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Fetch-Upstream.ps1')
& dotnet build (Join-Path $projectRoot 'audit\SCIE.Legacy.TargetProbe.csproj') --configuration Release "-p:SCNetGameDir=$GameDir"
if ($LASTEXITCODE -eq 0) {
    throw 'Unexpected result: the unmodified legacy source compiled against x25.01.17. Re-audit the probe.'
}
Write-Host 'Expected incompatibility confirmed: legacy SCIE does not compile unmodified against x25.01.17.'

