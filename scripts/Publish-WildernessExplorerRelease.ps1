[CmdletBinding()]
param([string]$GameDir = $env:SCNET_GAME_DIR)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$repo = if ($env:GITHUB_REPOSITORY) { $env:GITHUB_REPOSITORY } else { 'godblessmezyan-art/Survivalcraft2' }
$manifest = Join-Path $root 'SCIE-Port\src\SCIEPort\modinfo.json'
$version = [string](Get-Content -LiteralPath $manifest -Raw -Encoding UTF8 | ConvertFrom-Json).Version
if ([string]::IsNullOrWhiteSpace($version)) { throw 'Missing release version.' }

# Intentionally skip all tests, integration gates and package-content audits.
# A real C# DLL still must compile; a .netmod cannot be built from source-only ZIP.
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Build-WildernessExplorerFast.ps1') -GameDir $GameDir
if ($LASTEXITCODE -ne 0) { throw 'Compilation or .netmod creation failed.' }

$package = Join-Path $root ('SCIE-Port\artifacts\荒野探险家-' + $version + '.netmod')
if (-not (Test-Path -LiteralPath $package)) { throw 'The .netmod file was not created.' }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required. Authenticate or provide GH_TOKEN.'
}

$tag = 'wilderness-explorer-v' + $version
# gh release create fails on an existing release; never use --clobber or overwrite assets.
$notes = @"
# 荒野探险家 $version

在 Assets 中下载 **荒野探险家-$version.netmod**，导入游戏 NetMods。
Source code (zip/tar.gz) 是 GitHub 自带的源码，不是游戏安装包。

- 发布方式：直接编译并打包 .netmod，**跳过自动测试与安装包校验**。
- 状态：未验证预发布；无法保证 Android 或 SCNET 联机运行。
- 注意：当前云天书典与荒野探险家仍处于单包整合阶段。本脚本只打包 SCIEPort 工程，不能据此认定已包含云天书典。
"@
$notesPath = Join-Path $root ('SCIE-Port\artifacts\fast-release-' + $version + '.md')
[IO.File]::WriteAllText($notesPath, $notes, (New-Object Text.UTF8Encoding($false)))
$targetCommit = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { (& git -C $root rev-parse HEAD | Out-String).Trim() }
& gh release create $tag $package --repo $repo --target $targetCommit --title "荒野探险家 $version（未经测试）" --notes-file $notesPath --prerelease --latest=false
if ($LASTEXITCODE -ne 0) { throw 'GitHub Release upload failed.' }
Write-Host "Published .netmod: https://github.com/$repo/releases/tag/$tag"
