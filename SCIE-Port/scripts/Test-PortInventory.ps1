[CmdletBinding()]
param([string]$GameDir='E:\SCNET\SCNETx25.01.17',[string]$BuiltAssembly,[string]$Package)
$ErrorActionPreference='Stop'
$inventoryRoot=Split-Path -Parent $PSScriptRoot
$inventoryProject=Join-Path $inventoryRoot 'src\SCIEPort'
$inventoryAllowed=@('Artifacts','Vehicles','Saints','Plants','Food','Materials','Industry','Building','Creatures','Misc') | ForEach-Object { 'SCIEPort.'+$_ }
$inventoryRows=@(Get-ChildItem (Join-Path $inventoryProject 'Assets\Blocks') -Filter *.csv | ForEach-Object { Import-Csv -Delimiter ';' -Encoding UTF8 $_.FullName })
foreach($row in $inventoryRows){
    if($inventoryAllowed -notcontains $row.DefaultCategory -or $row.DefaultCategory -eq 'SCIEPort.Misc'){throw "Release item must have a deliberate dedicated tab: $($row.'Class Name')"}
}
foreach($name in @('StormhammerBlock','StormhammerAwakenedBlock','GungnirBlock','PoseidonBlock','OmniscientEyeBlock')){
    if(@($inventoryRows | Where-Object {$_.'Class Name' -eq $name -and $_.DefaultCategory -eq 'SCIEPort.Artifacts'}).Count -ne 1){throw "Artifact tab missing: $name"}
}
foreach($name in @('TankBlock','TankShellBlock','HelicopterBlock','AtvBlock','OffroadCarBlock','SteamBoatBlock','AirshipBlock','TrainBlock','CarriageBlock')){
    if(@($inventoryRows | Where-Object {$_.'Class Name' -eq $name -and $_.DefaultCategory -eq 'SCIEPort.Vehicles'}).Count -ne 1){throw "Vehicle tab missing: $name"}
}
$inventoryBuild=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Build-NetMod.ps1'))
if(-not $inventoryBuild.Contains("Join-Path `$artifacts '荒野探险家.netmod'") -or -not $inventoryBuild.Contains('荒野探险家-')){throw 'Release filename contract missing.'}
if($Package){
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $inventoryArchive=[IO.Compression.ZipFile]::OpenRead($Package)
    try{
        $inventoryCsvFiles=@(Get-ChildItem (Join-Path $inventoryProject 'Assets\Blocks') -Filter *.csv)
        if(@($inventoryArchive.Entries | Where-Object {$_.FullName -like 'Assets/Blocks/*.csv'}).Count -ne $inventoryCsvFiles.Count){throw 'Unexpected/missing packaged CSV.'}
        foreach($csv in $inventoryCsvFiles){
            $entry=$inventoryArchive.GetEntry('Assets/Blocks/'+$csv.Name)
            if(-not $entry){throw "Missing categorized package entry: $($csv.Name)"}
            $reader=[IO.StreamReader]::new($entry.Open())
            try{$packedRows=@($reader.ReadToEnd() | ConvertFrom-Csv -Delimiter ';')}finally{$reader.Dispose()}
            $sourceRows=@(Import-Csv -Delimiter ';' -Encoding UTF8 $csv.FullName)
            if($packedRows.Count -ne $sourceRows.Count){throw 'Packaged category row count changed.'}
            for($i=0;$i -lt $sourceRows.Count;$i++){
                if($packedRows[$i].'Class Name' -ne $sourceRows[$i].'Class Name' -or $packedRows[$i].DefaultCategory -ne $sourceRows[$i].DefaultCategory){throw "Packaged category drift: $($csv.Name)"}
            }
        }
        Write-Host "Packaged dedicated tabs match all $($inventoryCsvFiles.Count) CSV files / $($inventoryRows.Count) source rows."
    }finally{$inventoryArchive.Dispose()}
}
if([string]::IsNullOrEmpty($BuiltAssembly)){Write-Host "Dedicated inventory CSV/naming guard passed: $($inventoryRows.Count) rows.";return}
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'EntitySystem.dll'))
$inventoryGame=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$inventoryAssembly=[Reflection.Assembly]::LoadFrom($BuiltAssembly)
$inventoryBlockType=$inventoryGame.GetType('Game.Block',$true)
$inventoryNativeBlocks=$inventoryGame.GetType('Game.BlocksManager',$true)
$inventoryBlocks=$inventoryNativeBlocks.GetField('m_blocks').GetValue($null)
$inventoryCategories=$inventoryNativeBlocks.GetField('m_categories').GetValue($null)
$inventoryTypes=$inventoryAssembly.GetTypes()
$inventoryFlags=[Reflection.BindingFlags]'Static,NonPublic'
$inventoryRules=$inventoryAssembly.GetType('SCIEPort.Features.Inventory.PortInventoryRules',$true)
$inventorySelect=$inventoryRules.GetMethod('Select',$inventoryFlags)
$inventoryChecks=0
foreach($row in $inventoryRows){
    $t=@($inventoryTypes | Where-Object {$_.Name -eq $row.'Class Name' -and $_.IsSubclassOf($inventoryBlockType)})
    if($t.Count -ne 1){throw "CSV must identify exactly one owned Block type: $($row.'Class Name')"}
    $block=[Activator]::CreateInstance($t[0])
    $id=[int]$t[0].GetField('Index').GetRawConstantValue()
    $block.BlockIndex=$id;$block.DefaultCategory=$row.DefaultCategory;$block.DefaultCreativeData=[int]$row.DefaultCreativeData;$inventoryBlocks[$id]=$block
    # Target block GetCategory itself, without invoking graphics/content Initialize.
    $value=[Game.Terrain]::MakeBlockValue($id,0,0)
    if($block.GetCategory($value) -ne $row.DefaultCategory){throw "Actual category getter mismatch: $($row.'Class Name')"}
    $variantCount=0
    foreach($variant in $block.GetCreativeValues()){
        if(++$variantCount -gt 512){throw 'Creative variant enumeration exceeded offline budget.'}
        if($block.GetCategory($variant) -ne $row.DefaultCategory){throw "Creative DATA variant escaped the dedicated tab: $($row.'Class Name')"}
        $inventoryChecks++
    }
    if($row.DefaultCategory -eq 'SCIEPort.Artifacts'){
        $theme=$inventorySelect.Invoke($null,[object[]]@($id,-1,$row.DefaultCategory)).ToString()
        if($theme -eq 'None' -or $theme -eq 'Artifact'){throw "Release artifact requires its own authored background: $($row.'Class Name')"}
    }
    $inventoryChecks++
}
foreach($t in $inventoryTypes){
    if($t.IsAbstract -or -not $t.IsSubclassOf($inventoryBlockType)){continue}
    $field=$t.GetField('Index',[Reflection.BindingFlags]'Public,Static,DeclaredOnly')
    if($field -and $field.IsLiteral -and $field.GetRawConstantValue() -ne 203 -and $t.Name -notin $inventoryRows.'Class Name'){throw "Owned Block lacks a categorized CSV row: $t"}
}
$inventoryClothes=[Activator]::CreateInstance($inventoryAssembly.GetType('SCIEPort.Features.FourSaints.ClothingBlock',$true))
$inventoryClothes.DefaultCategory='Clothes';$inventoryClothes.BlockIndex=203;$inventoryBlocks[203]=$inventoryClothes
foreach($idx in @(0,200,201,202,203)) { foreach($color in @(0,5,15)){foreach($damage in @(0,8,15)){
    $data=[Game.ClothingBlock]::SetClothingColor([Game.ClothingBlock]::SetClothingIndex(0,$idx),$color)
    $value=$inventoryClothes.SetDamage([Game.Terrain]::MakeBlockValue(203,0,$data),$damage)
    $expected=if($idx -ge 200){'SCIEPort.Saints'}elseif($color -eq 0){'Clothes'}else{'Dyed'}
    if($inventoryClothes.GetCategory($value) -ne $expected){throw 'Saint DATA/damage/dye category isolation failed.'}
    $inventoryChecks++
}}}
# Exercise the actual target localization merge and idempotent category registration in this headless process.
$inventoryCategories.Clear()
foreach($key in @('Terrain','Weapons','Clothes','External.Mod')){$inventoryCategories.Add($key)}
$inventoryLanguageType=$inventoryGame.GetType('Game.LanguageControl',$true)
$inventorySeed=[IO.MemoryStream]::new([Text.Encoding]::UTF8.GetBytes('{"BlocksManager":{"Weapons":"native_weapons","External.Mod":"external_tab"}}'))
try{[void]$inventoryLanguageType.GetMethod('loadJson').Invoke($null,@($inventorySeed))}finally{$inventorySeed.Dispose()}
$inventoryCatalog=$inventoryAssembly.GetType('SCIEPort.Features.Inventory.PortInventoryCatalog',$true).GetMethod('Initialize',$inventoryFlags)
[void]$inventoryCatalog.Invoke($null,@())
$inventoryFirst=@($inventoryCategories)
[void]$inventoryCatalog.Invoke($null,@())
if(($inventoryFirst -join '|') -ne (@($inventoryCategories) -join '|')){throw 'Category registration duplicated/reordered native or external tabs.'}
if(($inventoryCategories | Select-Object -First 4) -join '|' -ne 'Terrain|Weapons|Clothes|External.Mod'){throw 'Native/external category order changed.'}
$inventoryKeys=$inventoryRules.GetField('Categories',$inventoryFlags).GetValue($null)
$inventoryLabels=$inventoryRules.GetField('Labels',$inventoryFlags).GetValue($null)
$inventoryLanguage=$inventoryGame.GetType('Game.LanguageControl',$true).GetMethod('Get',[Type[]]@([string[]]))
foreach($entry in @(@('Weapons','native_weapons'),@('External.Mod','external_tab'))){
    if($inventoryLanguage.Invoke($null,[object[]]@(,[string[]]@('BlocksManager',$entry[0]))) -ne $entry[1]){throw 'Existing language entry overwritten.'}
    $inventoryChecks++
}
for($i=0;$i -lt $inventoryKeys.Length;$i++){
    $actual=$inventoryLanguage.Invoke($null,[object[]]@(,[string[]]@('BlocksManager',$inventoryKeys[$i])))
    if($actual -ne $inventoryLabels[$i]){throw "Target localization mismatch: $($inventoryKeys[$i])"}
    $inventoryChecks++
}
foreach($property in @('CenterColor','BevelColor')){
    $p=$inventoryGame.GetType('Game.InventorySlotWidget',$true).GetProperty($property)
    if(-not $p.CanRead -or -not $p.CanWrite){throw "Native slot background ABI missing: $property"}
}
Write-Host "Inventory target API: $inventoryChecks checks passed; $($inventoryRows.Count) actual block categories, damaged/dyed Saints isolation, native labels, idempotent tabs, slot color ABI. No live widgets/world/device rendered."
