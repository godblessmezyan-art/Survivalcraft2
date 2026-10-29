param([string]$GameDir)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Resolve-AbyssId.ps1') -GameDir $GameDir
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $f))}
$e=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
$g=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$root=Join-Path (Split-Path $PSScriptRoot) 'src/SCIEPort'
$stream=[IO.File]::OpenRead((Join-Path $root 'Assets/Models/AbyssReaper.dae'))
try{$m=$e.GetType('Engine.Media.ModelData').GetMethod('Load',[type[]]@([IO.Stream])).Invoke($null,@($stream))}finally{$stream.Dispose()}
$tris=0;foreach($mesh in $m.Meshes){foreach($p in $mesh.MeshParts){$tris+=$p.IndicesCount/3}}
if($m.Bones.Count -ne 1 -or $m.Meshes.Count -ne 32 -or $tris -ne 9260){throw 'Abyss native model import mismatch'}
[xml]$xml=Get-Content -Raw (Join-Path $root 'Assets/Models/AbyssReaper.dae')
if($xml.COLLADA.asset.up_axis -ne 'Y_UP' -or @($xml.SelectNodes("//*[local-name()='node']")).Count -ne 1){throw 'Abyss axis/root contract'}
foreach($type in @('ComponentFirstPersonModel','ComponentCreatureModel')){ $prop=if($type -eq 'ComponentFirstPersonModel'){'ItemRotationOrder'}else{'InHandItemRotationOrder'};if(-not $g.GetType('Game.'+$type).GetProperty($prop).CanWrite){throw 'Animation order ABI missing'}}
if(-not $g.GetType('Game.ComponentMiner').GetMethod('AttackBody').IsStatic){throw 'AttackBody ABI missing'}
Write-Host "Abyss: 1 bone / 32 meshes / $tris triangles; real animation and combat API verified. Device pending."

Add-Type -AssemblyName System.Drawing
$png=[Drawing.Bitmap]::FromFile((Join-Path $root 'Assets/Textures/AbyssReaper.png'))
try {if($png.Width -ne 256 -or $png.Height -ne 16){throw 'Abyss palette dimensions'};for($x=0;$x -lt 256;$x++){for($y=0;$y -lt 16;$y++){if($png.GetPixel($x,$y).A -ne 255){throw 'Abyss palette alpha'}}}} finally {$png.Dispose()}
