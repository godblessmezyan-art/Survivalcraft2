param([string]$GameDir,[string]$AssemblyPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$game=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
foreach($spec in @(
    @('Game.ModLoader','ChangeSkyColor'),@('Game.TerrainUpdater','SetUpdateLocation'),
    @('Game.TerrainUpdater','RemoveUpdateLocation'),@('Game.ComponentBody','FindTerrainCollisionBoxes'),
    @('Game.ComponentBody','FindBodiesCollisionBoxes'),@('Game.SubsystemMovingBlocks','Raycast'),
    @('Game.SubsystemMovingBlocks','FindMovingBlocks'),@('Game.ComponentClothing','Save')
)) {
    $type=$game.GetType($spec[0],$true)
    if(@($type.GetMethods()|Where-Object Name -EQ $spec[1]).Count -eq 0){throw "Missing target API $($spec[0]).$($spec[1])"}
}
$path=Join-Path $root 'src/SCIEPort/Features/AbyssReaper'
foreach($file in Get-ChildItem $path -Filter 'Void*.cs') {
    $source=[IO.File]::ReadAllText($file.FullName)
    if($source -match '\b(ChangeCell|DestroyCell|SetCellValueFast|AddParticleSystem)\s*\('){throw "Unexpected terrain mutation or particle blob in $($file.Name)"}
}
$mod=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($AssemblyPath))
if (-not $game.GetType('Game.TerrainUpdater').GetField('m_pendingLocations',[Reflection.BindingFlags]'Instance,NonPublic')) { throw 'Audited queued terrain locations field missing.' }
foreach($type in @('VoidChunkLease','VoidPortalSafety','VoidPortalOperations','VoidPortalLocalTravel','VoidPortalScene','VoidBlinkRuntime','VoidHorizonRuntime','VoidCombatSafety')) {
    if(!$mod.GetType("SCIEPort.Features.AbyssReaper.$type",$false)){throw "Missing compiled $type"}
}
Write-Host 'Void prototype: real target API, compiled runtime types and no terrain mutation passed (no gameplay or device exercised).'
