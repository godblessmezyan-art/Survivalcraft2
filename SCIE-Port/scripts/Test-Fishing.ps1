param([Parameter(Mandatory=$true)][string]$GameDir,[string]$AssemblyPath,[string]$Package)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$game=[Game.Block].Assembly

# Native survival facts used by the fishing loop.
if([Game.RawFishBlock].GetField('Index') -eq $null -or [Game.CookedFishBlock].GetField('Index') -eq $null){throw 'Native fish items are missing'}
if([Game.WaterBlock].GetField('Index') -eq $null){throw 'Native water block is missing'}
if(-not ([Game.FluidBlock].GetMethod('GetLevelHeight') -and [Game.FluidBlock].GetMethod('GetLevel'))){throw 'Fluid level API missing'}
$recipe=[Game.Block].GetMethod('GetProceduralCraftingRecipes')
if($recipe -eq $null -or -not $recipe.IsVirtual){throw 'Procedural crafting hook missing'}
foreach($name in @('Ingredients','ResultValue','ResultCount','RequiredHeatLevel','RequiredPlayerLevel','Description','Message')){
    if([Game.CraftingRecipe].GetField($name) -eq $null){throw "CraftingRecipe field missing: $name"}
}
if([Game.BlocksManager].GetMethod('FindBlockByTypeName') -eq $null){throw 'Block type lookup missing'}
if([Game.ComponentInventoryBase].GetMethod('FindAcquireSlotForItem') -eq $null){throw 'Inventory acquire helper missing'}
if(-not ([Game.SubsystemPickables].GetMethod('AddPickable') -and [Game.SubsystemParticles].GetMethod('AddParticleSystem'))){throw 'Pickable/particle subsystems missing'}
$splash=[Game.WaterSplashParticleSystem].GetConstructors() | Where-Object { $_.GetParameters().Count -eq 3 }
if($splash -eq $null){throw 'Water splash constructor changed'}
$message=[Game.ComponentGui].GetMethods() | Where-Object { $_.Name -eq 'DisplaySmallMessage' -and $_.GetParameters().Count -eq 4 }
if($message -eq $null){throw 'Small message API changed'}
if([Game.SubsystemBlockBehavior].GetMethod('OnUse') -eq $null){throw 'Block behavior use hook missing'}

# Packet 236 must stay free in the target client.
$nativePackageIds=@();$unknown=@()
foreach($type in $game.GetTypes()){
    if($type.IsAbstract -or -not [Game.NetWork.IPackage].IsAssignableFrom($type)){continue}
    $getter=$type.GetProperty('ID').GetGetMethod();$il=@($getter.GetMethodBody().GetILAsByteArray() | Where-Object {$null -ne $_})
    while($il.Count -gt 0 -and $il[0] -eq 0){$il=$il[1..($il.Count-1)]}
    $id=$null
    if($il[0] -eq 0x20){$id=[BitConverter]::ToInt32([byte[]]$il,1) -band 255}
    elseif($il[0] -eq 0x1f){$id=[int]$il[1]}
    elseif($il[0] -ge 0x15 -and $il[0] -le 0x1e){$id=([int]$il[0]-0x16) -band 255}
    if($null -eq $id){$unknown+=$type.FullName}else{$nativePackageIds+=$id}
}
$tableField=[Game.NetWork.PackageManager].GetField('basePackages',[Reflection.BindingFlags]'NonPublic,Static')
$table=$tableField.GetValue($null)
if($table.Length -ne 255 -or $unknown.Count -ne 0 -or $nativePackageIds -contains 236){throw "Fishing packet 236 is not free; unknown=$($unknown -join ',')"}

$csv=Import-Csv -LiteralPath (Join-Path $root 'src/SCIEPort/Assets/Blocks/Fishing.csv') -Delimiter ';' -Encoding UTF8
$row=$csv | Where-Object { $_.'Class Name' -eq 'FishingRodBlock' }
if($null -eq $row){throw 'Fishing CSV row missing'}
if($row.DefaultDisplayName -ne '鱼竿'){throw 'Fishing rod display name changed'}
if($row.DefaultCategory -ne 'SCIEPort.Tools'){throw 'Fishing rod category must stay SCIEPort.Tools'}
if($row.IsPlaceable -ne 'FALSE' -or $row.MaxStacking -ne '1'){throw 'Fishing rod placement/stack rules changed'}
if([int]$row.DefaultMeleePower -ne 0){throw 'Fishing rod must not deal melee damage'}
$xdb=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'src/SCIEPort/Assets/Database/Fishing.xdb')
if($xdb -notmatch 'SCIEPort\.Features\.Fishing\.SubsystemFishing' -or $xdb -notmatch 'MemberSubsystemTemplate'){throw 'Fishing database template incomplete'}

if($AssemblyPath){
    $assembly=[Reflection.Assembly]::LoadFrom($AssemblyPath)
    $rod=[Activator]::CreateInstance($assembly.GetType('SCIEPort.Features.Fishing.FishingRodBlock',$true))
    if([int]$rod.GetType().GetField('Index').GetRawConstantValue() -ne 972){throw 'Fishing rod block ID changed'}
    if($rod.GetCategory(0) -ne 'SCIEPort.Tools'){throw 'Fishing rod runtime category changed'}
    $subsystemType=$assembly.GetType('SCIEPort.Features.Fishing.SubsystemFishing',$true)
    if(-not $subsystemType.IsSubclassOf([Game.SubsystemBlockBehavior])){throw 'Fishing subsystem type changed'}
    $subsystem=[Activator]::CreateInstance($subsystemType)
    if(@($subsystem.HandledBlocks) -notcontains 972){throw 'Fishing subsystem does not handle the rod'}
    $packet=[Activator]::CreateInstance($assembly.GetType('SCIEPort.Features.Fishing.FishingCatchPackage',$true))
    if($packet.ID -ne 236){throw 'Fishing packet identity changed'}
    if($null -ne $table[236]){throw 'Fishing protocol 236 already registered in this audit process'}
    try{[Game.NetWork.PackageManager]::RegisterPackage($packet);if($tableField.GetValue($null)[236] -ne $packet){throw 'Native fishing package registration failed'}}
    finally{[Game.NetWork.PackageManager]::UnRegisterPackage($packet)}
    $flags=[Reflection.BindingFlags]'Instance,NonPublic';$packetType=$packet.GetType()
    $packetType.GetField('Kind',$flags).SetValue($packet,[byte]1)
    $packetType.GetField('Caster',$flags).SetValue($packet,[uint16]42)
    $packetType.GetField('Cell',$flags).SetValue($packet,[Engine.Point3]::new(-12,80,345))
    $packetType.GetField('Sequence',$flags).SetValue($packet,[uint32]7)
    $packetType.GetField('Count',$flags).SetValue($packet,[byte]3)
    $writer=[Game.NetWork.PackageStreamWriter]::new()
    try{
        $packet.WriteData($writer)
        if($writer.BaseStream.Length -ne 20){throw 'Fishing payload is not 20 bytes'}
        $bytes=$writer.Data()
    }finally{$writer.Dispose()}
    $copy=[Activator]::CreateInstance($packetType);$reader=[Game.NetWork.PackageStreamReader]::new([byte[]]$bytes)
    try{$copy.ReadData($reader)}finally{$reader.Dispose()}
    foreach($name in @('Kind','Caster','Cell','Sequence','Count')){
        $field=$packetType.GetField($name,$flags)
        if(-not $field.GetValue($packet).Equals($field.GetValue($copy))){throw "Fishing packet roundtrip failed: $name"}
    }
}

if($Package){
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead($Package)
    try{
        foreach($path in @('Assets/Blocks/Fishing.csv','Assets/Database/Fishing.xdb')){
            if(-not ($zip.Entries | Where-Object { $_.FullName -eq $path })){throw "Missing package entry: $path"}
        }
    }finally{$zip.Dispose()}
}
Write-Host 'Fishing native API/asset/package checks passed.'
