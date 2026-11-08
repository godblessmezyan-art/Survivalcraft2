[CmdletBinding()]
param([string]$GameDir = $env:SCNET_GAME_DIR, [switch]$ValidateOnly)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$repo = if ($env:GITHUB_REPOSITORY) { $env:GITHUB_REPOSITORY } else { 'godblessmezyan-art/Survivalcraft2' }
$ready = Join-Path $root 'docs\UNIFIED-RELEASE-READY'
if (-not (Test-Path -LiteralPath $ready) -or (Get-Content -LiteralPath $ready -Raw -Encoding UTF8).Trim() -cne 'ready=1') {
    throw 'Unified package is not integration-verified. Refusing to release an old SCIE-only build.'
}
if ([string]::IsNullOrWhiteSpace($GameDir)) { throw 'Set SCNET_GAME_DIR to the local x25.01.17 game directory.' }
foreach ($name in @('Survivalcraft.exe', 'Engine.dll', 'EntitySystem.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $GameDir $name))) { throw "Missing local SCNET assembly: $name" }
}
$infoPath = Join-Path $root 'SCIE-Port\src\SCIEPort\modinfo.json'
$projectPath = Join-Path $root 'SCIE-Port\src\SCIEPort\SCIEPort.csproj'
$meta = Get-Content -LiteralPath $infoPath -Raw -Encoding UTF8 | ConvertFrom-Json
$version = [string]$meta.Version
if ($meta.Name -cne '荒野探险家' -or $meta.PackageName -cne 'SCIEPort' -or $version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw 'Unexpected unified mod identity or version.'
}
$project = [xml](Get-Content -LiteralPath $projectPath -Raw -Encoding UTF8)
$props = $project.Project.PropertyGroup | Select-Object -First 1
if ($props.Version -cne $version -or $props.AssemblyVersion -cne ($version + '.0') -or $props.FileVersion -cne ($version + '.0')) {
    throw 'modinfo and assembly versions differ.'
}
$head = (& git -C $root rev-parse HEAD | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $head -cnotmatch '^[0-9a-f]{40}$') { throw 'Git HEAD not available.' }
if ($env:GITHUB_ACTIONS -eq 'true' -and $head -cne $env:GITHUB_SHA) { throw 'HEAD != GitHub Actions build commit.' }
if ($env:GITHUB_ACTIONS -ne 'true') {
    $branch = (& git -C $root branch --show-current | Out-String).Trim()
    if ($branch -cne 'main') { throw 'Releases must be built on main.' }
}
$dirty = (& git -C $root status --porcelain --untracked-files=no | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $dirty.Length -ne 0) { throw 'Tracked worktree must be clean before release.' }

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'SCIE-Port\scripts\Build-NetMod.ps1') -GameDir $GameDir -Configuration Release
if ($LASTEXITCODE -ne 0) { throw 'SCNET Release build/tests failed. Release not created.' }

$artifacts = Join-Path $root 'SCIE-Port\artifacts'
$package = Join-Path $artifacts ('荒野探险家-' + $version + '.netmod')
$plain = Join-Path $artifacts '荒野探险家.netmod'
if (-not (Test-Path -LiteralPath $package) -or -not (Test-Path -LiteralPath $plain)) { throw 'Missing .netmod output.' }
$size = (Get-Item -LiteralPath $package).Length
if ($size -lt 1000 -or $size -gt 250MB) { throw 'Unexpected .netmod size.' }
$sha = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant()
if ((Get-FileHash -LiteralPath $plain -Algorithm SHA256).Hash.ToLowerInvariant() -cne $sha) { throw 'Package copies differ.' }

# Verify ONE integrated module, not a renamed SCIE-only build or nested dual mods.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $names = @($zip.Entries | ForEach-Object { $_.FullName })
    if (@($names | Where-Object { $_ -ceq 'modinfo.json' }).Count -ne 1 -or -not $zip.GetEntry('SCIEPort.dll') -or $zip.GetEntry('CelestialCodex.dll')) {
        throw 'Package is not a single SCIEPort mod.'
    }
    if (@($names | Where-Object { $_ -match '\.netmod$|(^|/)(Survivalcraft\.exe|Engine\.dll|EntitySystem\.dll)$' }).Count -gt 0) {
        throw 'Nested mod or proprietary game assembly detected.'
    }
    $reader = [IO.StreamReader]::new($zip.GetEntry('modinfo.json').Open(), [Text.Encoding]::UTF8)
    try { $packed = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
    if ($packed.PackageName -cne 'SCIEPort' -or $packed.Version -cne $version) { throw 'Packaged modinfo mismatch.' }
    $stream = $zip.GetEntry('SCIEPort.dll').Open()
    $buffer = [IO.MemoryStream]::new()
    try { $stream.CopyTo($buffer); $assemblyText = [Text.Encoding]::UTF8.GetString($buffer.ToArray()) }
    finally { $buffer.Dispose(); $stream.Dispose() }
    foreach ($marker in @('LocalBookLibrary', 'CodexDialog', 'SubsystemCelestialCodexBehavior')) {
        if (-not $assemblyText.Contains($marker)) { throw "Missing integrated reader type: $marker" }
    }
    if (@($names | Where-Object { $_ -like '*CelestialCodex*' -and $_ -notlike '*.dll' }).Count -lt 2) {
        throw 'Book assets missing from unified .netmod.'
    }
}
finally { $zip.Dispose() }

$checksum = Join-Path $artifacts ('荒野探险家-' + $version + '.sha256')
$checksumText = $sha + '  ' + [IO.Path]::GetFileName($package) + [Environment]::NewLine
[IO.File]::WriteAllText($checksum, $checksumText, (New-Object Text.UTF8Encoding($false)))
Write-Host "Validated .netmod: $package ($size bytes), SHA-256: $sha"
if ($ValidateOnly) { Write-Host 'ValidateOnly: no GitHub Release created.'; exit 0 }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI missing. Install gh and authenticate.' }
$tag = 'wilderness-explorer-v' + $version
& gh release view $tag --repo $repo 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { throw "Release $tag exists. Increment version instead of overwriting assets." }
$notes = @"
# 荒野探险家 $version（包含云天书典）

请在 Assets 下载 荒野探险家-$version.netmod，导入游戏 NetMods。
GitHub 自动提供的 Source code (zip/tar.gz) 只是源码，不能直接安装。

- 安装包 SHA-256：$sha
- 提交：$head
- 目标：Survivalcraft 2.4，SCNET x25.01.17，Android
- Windows Release 构建与离线包校验：通过
- Android 真机与多人联机：以真实专项测试记录为准；未验证部分仍待验证
"@
$notesPath = Join-Path $artifacts ('release-notes-' + $version + '.md')
[IO.File]::WriteAllText($notesPath, $notes, (New-Object Text.UTF8Encoding($false)))
& gh release create $tag $package $checksum --repo $repo --target $head --title "荒野探险家 $version（含云天书典）" --notes-file $notesPath --prerelease --latest=false
if ($LASTEXITCODE -ne 0) { throw 'Release upload failed; inspect GitHub state before retrying.' }
Write-Host "Published with .netmod: https://github.com/$repo/releases/tag/$tag"
