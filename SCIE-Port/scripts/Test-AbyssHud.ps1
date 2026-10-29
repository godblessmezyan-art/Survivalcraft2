param([string]$GameDir,[string]$Package)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$asset=Join-Path $root 'src/SCIEPort/Assets/Textures/AbyssSkillsV1.png'
$expected='F7834AE5DB890FC245BF954F474BF497B22362DB5797FF37629D1C3897981DB0'
if((Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash -ne $expected){throw 'Abyss skill art differs from the reviewed imagegen atlas.'}
$engine=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
$load=$engine.GetType('Engine.Media.Image',$true).GetMethod('Load',[Type[]]@([IO.Stream]))
$stream=[IO.File]::OpenRead($asset)
try{$image=$load.Invoke($null,@($stream))}finally{$stream.Dispose()}
if($image.Width -ne 1254 -or $image.Height -ne 1254){throw 'Abyss skill atlas must remain four equal 627-square cells.'}
foreach($corner in @(@(0,0),@(1253,0),@(0,1253),@(1253,1253))){if($image.GetPixel($corner[0],$corner[1]).A -gt 8){throw 'Skill atlas exterior lost alpha transparency.'}}
foreach($center in @(@(313,313),@(940,313),@(313,940),@(940,940))){if($image.GetPixel($center[0],$center[1]).A -lt 230){throw 'Skill emblem center should be visible.'}}
if($Package){
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $zip=[IO.Compression.ZipFile]::OpenRead($Package)
 try{
  $entry=$zip.GetEntry('Assets/Textures/AbyssSkillsV1.png');if(!$entry){throw 'Missing packaged Abyss skills atlas.'}
  $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
  try{$packed=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
  if($packed -ne $expected){throw 'Packaged Abyss icon art differs from reviewed original.'}
 }finally{$zip.Dispose()}
}
$game=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$hit=$game.GetType('Game.Widget',$true).GetMethod('HitTest',[Type[]]@($engine.GetType('Engine.Vector2',$true)))
if(!$hit.IsVirtual){throw 'Circular widget requires verified virtual native Widget.HitTest.'}
foreach($spec in @(@('Game.ComponentFirstPersonModel','ItemOffsetOrder'),@('Game.ComponentCreatureModel','InHandItemOffsetOrder'))){if(!$game.GetType($spec[0],$true).GetProperty($spec[1]).CanWrite){throw 'Required native pose offset API missing.'}}
Write-Host 'Abyss skills: target PNG importer, four alpha cells, original hash, circular widget and pose-offset ABI passed (no display or device).'
