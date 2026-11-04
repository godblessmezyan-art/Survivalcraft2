[CmdletBinding()]
param([string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' }))
$ErrorActionPreference = 'Stop'
Write-Host 'Celestial Codex is integrated into Wilderness Explorer 0.77.0. Building the integrated package.'
& (Join-Path $PSScriptRoot '../SCIE-Port/scripts/Build-WithPinnedSdk.ps1') -GameDir $GameDir -Configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Integrated Wilderness Explorer build failed.' }
