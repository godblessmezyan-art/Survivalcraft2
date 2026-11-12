[CmdletBinding()]
param([string]$GameDir=$env:SCNET_GAME_DIR)
$ErrorActionPreference='Stop'
# Compatibility entry: every route now builds the same validated unified package.
& (Join-Path $PSScriptRoot '../SCIE-Port/scripts/Build-NetMod.ps1') -GameDir $GameDir -Configuration Release
