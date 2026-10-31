param(
    [string]$GameDir='D:/Games/SCNET-x25.01.17',
    [Parameter(Mandatory=$true)][string]$BaselinePackage,
    [string[]]$AssetUpdates=@()
)
# Explicit manual-testing workflow. No Test-* script, test executable or game is run.
# Reuses frozen assets; any new/changed asset must be explicitly named.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$project=Join-Path $root 'src/SCIEPort'
$info=Get-Content -Raw -Encoding UTF8 (Join-Path $project 'modinfo.json')|ConvertFrom-Json
$artifacts=Join-Path $root 'artifacts'
$baseline=[IO.Path]::GetFullPath($BaselinePackage)
$updates=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach($name in $AssetUpdates){
    if(!$name.StartsWith('Assets/') -or $name.Contains('..') -or $name.Contains('\') -or [IO.Path]::GetExtension($name) -notin @('.dae','.png','.xdb')){throw ('Invalid asset override: '+$name)}
    if(!(Test-Path -LiteralPath (Join-Path $project $name))){throw ('Missing asset override: '+$name)}
    [void]$updates.Add($name)
}
$versioned=Join-Path $artifacts ('荒野探险家-'+$info.Version+'.netmod')
if($baseline -eq [IO.Path]::GetFullPath($versioned)){throw 'Baseline must be a retained older version.'}
$env:DOTNET_ROOT=Join-Path $env:LOCALAPPDATA 'Microsoft/dotnet'
$env:DOTNET_ROLL_FORWARD='Major'
if(!$env:FrameworkPathOverride){throw 'Set FrameworkPathOverride to the net472 reference assemblies.'}
$build=Join-Path $artifacts ('manual-'+$info.Version+'-build')
& (Join-Path $env:DOTNET_ROOT 'dotnet.exe') build (Join-Path $project 'SCIEPort.csproj') -c Release "-p:SCNetGameDir=$GameDir" -o $build --nologo
if($LASTEXITCODE -ne 0){throw 'Release compilation failed.'}
if($updates.Contains('Assets/Models/AbyssBoneDragon.dae')){
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Read-BoneDragonResources.ps1') -GameDir $GameDir -ModAssembly (Join-Path $build 'SCIEPort.dll')
    if($LASTEXITCODE -ne 0){throw 'Bone dragon resources differ from the verified native renderer/database.'}
}
if($updates.Contains('Assets/Models/AbyssCerberus.dae')){
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Read-CerberusRenderCompatibility.ps1') -GameDir $GameDir -ModAssembly (Join-Path $build 'SCIEPort.dll') -BaselinePackage $baseline
    if($LASTEXITCODE -ne 0){throw 'Cerberus resource exceeds native renderer capabilities.'}
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($baseline)
try{
    foreach($entry in $archive.Entries){
        if(!$entry.FullName.StartsWith('Assets/')){continue}
        if($updates.Contains($entry.FullName)){continue}
        $source=Join-Path $project $entry.FullName
        # Authorized upstream-only resources are already in the retained package.
        if(!(Test-Path -LiteralPath $source)){continue}
        $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
        if($hash -ne (Get-FileHash -LiteralPath $source).Hash){throw ('Asset changed; use the complete packager instead: '+$entry.FullName)}
    }
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $project 'Assets') -Recurse -File){
        $name='Assets/'+$file.FullName.Substring((Join-Path $project 'Assets').Length+1).Replace('\','/')
        if($name -eq 'Assets/Textures/KellyExpansion.webp'){continue} # Source-only, as in Build-NetMod.
        if(!$archive.GetEntry($name) -and !$updates.Contains($name)){throw ('New asset requires explicit packaging: '+$name)}
    }
}finally{$archive.Dispose()}
$temp=Join-Path $artifacts ('manual-'+[Guid]::NewGuid().ToString('N')+'.netmod')
Copy-Item -LiteralPath $baseline -Destination $temp
try{
    $archive=[IO.Compression.ZipFile]::Open($temp,[IO.Compression.ZipArchiveMode]::Update)
    try{
        $payload=@{'SCIEPort.dll'=(Join-Path $build 'SCIEPort.dll');'modinfo.json'=(Join-Path $project 'modinfo.json');'THIRD-PARTY-NOTICES.md'=(Join-Path $root 'THIRD-PARTY-NOTICES.md')}
        foreach($name in $updates){$payload[$name]=Join-Path $project $name}
        foreach($name in $payload.Keys){$old=$archive.GetEntry($name);if($old){$old.Delete()};[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$payload[$name],$name,[IO.Compression.CompressionLevel]::Optimal)}
    }finally{$archive.Dispose()}
    Move-Item -LiteralPath $temp -Destination $versioned -Force
    Copy-Item -LiteralPath $versioned -Destination (Join-Path $artifacts '荒野探险家.netmod') -Force
}finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp}}
Write-Host ('Manual-test candidate: '+$versioned)
Write-Host ('SHA256: '+(Get-FileHash -LiteralPath $versioned).Hash)
Write-Host 'Tests skipped at user request. Gameplay, network, save/load and device acceptance remain pending.'
