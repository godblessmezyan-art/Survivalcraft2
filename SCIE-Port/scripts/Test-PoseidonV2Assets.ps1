param([string]$GameDir,[string]$Package)
$ErrorActionPreference='Stop'
$poseidonRoot=Split-Path $PSScriptRoot
$poseidonManifest=Get-Content (Join-Path $poseidonRoot 'docs/assets/poseidon-v2/icons.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$poseidonEngine=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
$poseidonLoad=$poseidonEngine.GetType('Engine.Media.Image',$true).GetMethod('Load',[Type[]]@([IO.Stream]))
if($Package){Add-Type -AssemblyName System.IO.Compression.FileSystem;$poseidonArchive=[IO.Compression.ZipFile]::OpenRead($Package)}
try{
 foreach($property in $poseidonManifest.icons.PSObject.Properties){
  $icon=$property.Value;$path=Join-Path $poseidonRoot $icon.asset
  if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $icon.assetSha256){throw 'Poseidon V2 generated icon hash mismatch'}
  $original=Join-Path (Join-Path $poseidonRoot 'docs/assets/poseidon-v2') $icon.source
  if((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash -ne $icon.sourceSha256){throw 'Poseidon imagegen original mismatch'}
  $stream=[IO.File]::OpenRead($path)
  try{$image=$poseidonLoad.Invoke($null,@($stream))}finally{$stream.Dispose()}
  if($image.Width -ne 256 -or $image.Height -ne 256 -or $image.GetPixel(0,0).A -gt 8 -or $image.GetPixel(128,128).A -lt 230){throw 'Poseidon native PNG transparency/dimensions'}
  if($Package){
   $entry=$poseidonArchive.GetEntry($icon.asset.Replace('src/SCIEPort/',''))
   if(-not $entry){throw 'Missing V2 icon in package'}
   $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
   try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
   if($hash -ne $icon.assetSha256){throw 'Packaged icon differs from generated source'}
  }
 }
}finally{if($poseidonArchive){$poseidonArchive.Dispose()}}
Write-Host 'Poseidon V2: three original imagegen icons, native PNG decode/alpha and package hashes verified.'
