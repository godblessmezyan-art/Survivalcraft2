[CmdletBinding()]
param(
    [string]$Repository = 'https://github.com/Lixue9jiu/SCIE.git',
    [string]$Commit = '4864d956793b6e670413019f1f6b1cc5c8bb0970'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$upstreamRoot = Join-Path $projectRoot 'upstream'
$target = Join-Path $upstreamRoot 'SCIE'

New-Item -ItemType Directory -Force -Path $upstreamRoot | Out-Null
if (-not (Test-Path -LiteralPath (Join-Path $target '.git'))) {
    if (Test-Path -LiteralPath $target) {
        throw "Refusing to overwrite non-Git directory: $target"
    }
    & git clone --no-checkout $Repository $target
    if ($LASTEXITCODE -ne 0) { throw 'git clone failed.' }
}

$actualRemote = (& git -C $target remote get-url origin).Trim()
if ($actualRemote -ne $Repository) {
    throw "Unexpected upstream remote: $actualRemote"
}
& git -C $target fetch --no-tags origin $Commit
if ($LASTEXITCODE -ne 0) { throw 'git fetch of pinned commit failed.' }
& git -C $target checkout --detach $Commit
if ($LASTEXITCODE -ne 0) { throw 'git checkout of pinned commit failed.' }

$actualCommit = (& git -C $target rev-parse HEAD).Trim()
if ($actualCommit -ne $Commit) { throw "Pinned commit mismatch: $actualCommit" }
if (& git -C $target status --porcelain) {
    throw 'Pinned upstream working tree is not clean.'
}

Write-Host "SCIE research baseline ready: $actualCommit"

