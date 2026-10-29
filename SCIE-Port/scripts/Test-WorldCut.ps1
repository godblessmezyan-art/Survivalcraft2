param([string]$GameDir='E:\SCNET\SCNETx25.01.17',[string]$AssemblyPath,[string]$Package)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$repo=Split-Path $root
$manifest=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'tests/SCIEPort.Abyss.Tests/protected-weapon.json')|ConvertFrom-Json
foreach($property in $manifest.files.PSObject.Properties){
 if((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $root $property.Name)).Hash -ne $property.Value){throw ('Existing weapon asset/combo/ID changed: '+$property.Name)}
}
$weaponRow=Import-Csv -Delimiter ';' -Encoding UTF8 (Join-Path $root 'src/SCIEPort/Assets/Blocks/AbyssReaper.csv')
foreach($property in $manifest.csvFields.PSObject.Properties){if($weaponRow.($property.Name) -cne $property.Value){throw ('Frozen item/hold/combo field changed: '+$property.Name)}}
if($Package){
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $archive=[IO.Compression.ZipFile]::OpenRead($Package)
 try{
  foreach($property in $manifest.files.PSObject.Properties){
   if(!$property.Name.StartsWith('src/SCIEPort/Assets/')){continue}
   $path=$property.Name.Substring('src/SCIEPort/'.Length)
   $entry=$archive.GetEntry($path);if(!$entry){throw ('Frozen package resource missing: '+$path)}
   $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
   try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
   if($hash -ne $property.Value){throw ('Packaged weapon model/texture changed: '+$path)}
  }
  $reader=[IO.StreamReader]::new($archive.GetEntry('Assets/Blocks/AbyssReaper.csv').Open())
  try{$packedRow=$reader.ReadToEnd()|ConvertFrom-Csv -Delimiter ';'}finally{$reader.Dispose()}
  foreach($property in $manifest.csvFields.PSObject.Properties){if($packedRow.($property.Name) -cne $property.Value){throw ('Packed hold/ID/combo field changed: '+$property.Name)}}
  $reader=[IO.StreamReader]::new($archive.GetEntry('modinfo.json').Open())
  try{$packedInfo=$reader.ReadToEnd()|ConvertFrom-Json}finally{$reader.Dispose()}
  $sourceInfo=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'src/SCIEPort/modinfo.json')|ConvertFrom-Json
  if($packedInfo.Version -ne $sourceInfo.Version -or $packedInfo.PackageName -ne 'SCIEPort' -or $packedInfo.Name -ne '荒野探险家' -or $packedInfo.Author -ne 'zyan'){throw 'WorldCut package metadata/brand drift'}
  Write-Host 'WorldCut packaged model/texture SHA-256, every frozen CSV parameter and metadata verified.'
 }finally{$archive.Dispose()}
}
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$engine=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
$game=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$batch=$engine.GetType('Engine.Graphics.FlatBatch3D',$true)
foreach($method in @('QueueTriangle','QueueQuad','QueueLine')){if(@($batch.GetMethods()|Where-Object Name -EQ $method).Count -eq 0){throw "Missing real flat ribbon API $method"}}
if(!$game.GetType('Game.WidgetInput').GetProperty('TouchLocations')){throw 'Per-finger touch API missing'}
$sceneSource=Get-Content -Raw (Join-Path $root 'src/SCIEPort/Features/AbyssReaper/WorldCutScene.cs')
if(($sceneSource -replace '(?s)/\*.*?\*/|//[^\r\n]*','') -match 'AttackBody|FindComponent<ComponentHealth>|DestroyCell|ChangeCell|Weather|ParticleSystem'){throw 'Visual scene must be independent of damage, weather and terrain mutation'}
$damageSource=Get-Content -Raw (Join-Path $root 'src/SCIEPort/Features/AbyssReaper/WorldCutDamage.cs')
if($damageSource -match 'WorldCutScene|ShowRifts|LowEffects|Draw\('){throw 'Damage must not depend on effects or visibility'}
foreach($file in Get-ChildItem (Join-Path $root 'src/SCIEPort/Features/AbyssReaper') -Filter 'WorldCut*.cs'){
 if([IO.File]::ReadAllText($file.FullName) -match '\b(ChangeCell|DestroyCell|SetCellValueFast|AddParticleSystem)\s*\('){throw 'WorldCut must not mutate terrain or build particle blobs'}
}
if(!$AssemblyPath){Write-Host 'WorldCut frozen weapon, actual ribbon/touch ABI and no terrain/particle/visual-damage coupling guards passed.';return}
$mod=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($AssemblyPath))
$references=@('Engine.dll','EntitySystem.dll','Survivalcraft.exe')|ForEach-Object{Join-Path $GameDir $_}
$references+=@('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll')
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$options=New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateInMemory=$true
foreach($reference in $references){[void]$options.ReferencedAssemblies.Add($reference)}
$result=$compiler.CompileAssemblyFromSource($options,[IO.File]::ReadAllText((Join-Path $root 'tests/SCIEPort.Abyss.Tests/WorldCutNativeProbe.cs')))
if($result.Errors.HasErrors){throw (($result.Errors|ForEach-Object ToString)-join "`n")}
$result.CompiledAssembly.GetType('WorldCutNativeProbe',$true).GetMethod('Run').Invoke($null,@($mod))
