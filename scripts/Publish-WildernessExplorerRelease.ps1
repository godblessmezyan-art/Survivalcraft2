[CmdletBinding()]
param([string]$GameDir=$env:SCNET_GAME_DIR)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$repo=if($env:GITHUB_REPOSITORY){$env:GITHUB_REPOSITORY}else{'godblessmezyan-art/Survivalcraft2'}
$version=[string](Get-Content -LiteralPath (Join-Path $root 'SCIE-Port/src/SCIEPort/modinfo.json') -Raw -Encoding UTF8 | ConvertFrom-Json).Version
if([string]::IsNullOrWhiteSpace($version)){throw 'Missing unified release version.'}
if(-not(Get-Command gh -ErrorAction SilentlyContinue)){throw 'GitHub CLI is required for release upload.'}
$target=if($env:GITHUB_SHA){$env:GITHUB_SHA}else{(& git -C $root rev-parse HEAD | Out-String).Trim()}
$resolved=(& gh api "repos/$repo/commits/$target" --jq '.sha' | Out-String).Trim()
if($LASTEXITCODE-ne 0 -or $resolved-ne $target){throw 'Built commit is not accessible on GitHub.'}
$tag='wilderness-explorer-v'+$version
$package=Join-Path $root ('SCIE-Port/artifacts/荒野探险家-'+$version+'.netmod')
$previousPreference=$ErrorActionPreference;$ErrorActionPreference='Continue'
$existing=& gh api "repos/$repo/releases/tags/$tag" 2>$null
$lookupExit=$LASTEXITCODE;$ErrorActionPreference=$previousPreference
if($lookupExit-eq 0){
    $release=$existing | ConvertFrom-Json
    $tagCommit=(& gh api "repos/$repo/commits/$tag" --jq '.sha' | Out-String).Trim()
    if($LASTEXITCODE-ne 0 -or $tagCommit-ne $target){throw 'Existing version points to a different commit.'}
    $asset=@($release.assets | Where-Object name -CEQ ([IO.Path]::GetFileName($package)))
    if($asset.Count){
        if($asset.Count-ne 1 -or $asset[0].digest-notlike 'sha256:*'){throw 'Existing asset cannot be verified; not overwriting.'}
        $download=Join-Path $root 'SCIE-Port/artifacts/release-existing-check'
        New-Item -ItemType Directory -Force -Path $download | Out-Null
        & gh release download $tag --repo $repo --pattern ([IO.Path]::GetFileName($package)) --dir $download --clobber
        if($LASTEXITCODE-ne 0){throw 'Cannot verify existing asset bytes.'}
        $downloadHash=(Get-FileHash -LiteralPath (Join-Path $download ([IO.Path]::GetFileName($package)))).Hash.ToLowerInvariant()
        if($asset[0].digest-ne ('sha256:'+$downloadHash)){throw 'Existing asset digest differs.'}
        Write-Host "Verified existing Release: $($release.html_url)";exit 0
    }
    & (Join-Path $root 'SCIE-Port/scripts/Build-NetMod.ps1') -GameDir $GameDir -Configuration Release
    $hash=(Get-FileHash -LiteralPath $package).Hash.ToLowerInvariant()
    & gh release upload $tag $package --repo $repo
}else{
    & (Join-Path $root 'SCIE-Port/scripts/Build-NetMod.ps1') -GameDir $GameDir -Configuration Release
    $hash=(Get-FileHash -LiteralPath $package).Hash.ToLowerInvariant()
    $notes="荒野探险家 $version：单一SCIEPort.dll，内置家居、天工绘卷、云天书典及现有优化。`n`nWindows环境、Release、自动回归和包校验通过；Android7723及真实联机待验证。`n`nSHA256: $hash`n构建提交: $target"
    $notesPath=Join-Path $root ('SCIE-Port/artifacts/release-'+$version+'.md')
    [IO.File]::WriteAllText($notesPath,$notes,(New-Object Text.UTF8Encoding($false)))
    & gh release create $tag $package --repo $repo --target $target --title "荒野探险家 $version（Android待验证）" --notes-file $notesPath --prerelease --latest=false
}
if($LASTEXITCODE-ne 0){throw 'Release upload failed.'}
$release=(& gh api "repos/$repo/releases/tags/$tag" | Out-String) | ConvertFrom-Json
$asset=@($release.assets | Where-Object name -CEQ ([IO.Path]::GetFileName($package)))
if($asset.Count-ne 1 -or $asset[0].digest-ne ('sha256:'+$hash)){throw 'Uploaded asset hash differs.'}
Write-Host "Verified Release: $($release.html_url)"
