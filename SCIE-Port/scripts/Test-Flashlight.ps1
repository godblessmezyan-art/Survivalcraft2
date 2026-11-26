[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDir,
    [Parameter(Mandatory)][string]$AssemblyPath
)

$ErrorActionPreference = 'Stop'
$assets = Join-Path $PSScriptRoot '../src/SCIEPort/Assets'
foreach ($file in @('Engine.dll', 'EntitySystem.dll', 'LiteNetLib.dll', 'Survivalcraft.exe')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))
}
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
$prefix = 'SCIEPort.Features.Flashlight.'
$blockType = $assembly.GetType($prefix + 'FlashlightBlock', $true)
$subsystemType = $assembly.GetType($prefix + 'SubsystemFlashlight', $true)
$packageType = $assembly.GetType($prefix + 'FlashlightPackage', $true)
$lightingType = $assembly.GetType($prefix + 'FlashlightTerrainLighting', $true)
$nonPublicStatic = [Reflection.BindingFlags]'NonPublic,Static'
$nonPublicInstance = [Reflection.BindingFlags]'NonPublic,Instance'
$block = [Activator]::CreateInstance($blockType)
$block.BlockIndex = $blockType.GetField('Index').GetRawConstantValue()
$id = $block.BlockIndex
$blocks = [Game.BlocksManager]::Blocks
$air = [Game.AirBlock]::new()
for ($i = 0; $i -lt $blocks.Length; $i++) { $blocks[$i] = $air }
$blocks[$id] = $block
$nativeBlocks = @([Game.BatteryBlock]::new(), [Game.LightbulbBlock]::new(), [Game.IronIngotBlock]::new())
foreach ($native in $nativeBlocks) {
    $native.BlockIndex = $native.GetType().GetField('Index').GetRawConstantValue()
    $blocks[$native.BlockIndex] = $native
}

Add-Type -AssemblyName System.IO.Compression
$decode = [Game.Terrain].Assembly.GetType('Game.SurvivalCraftModEntity').GetMethod('GetDecipherStream', $nonPublicStatic)
$content = [IO.File]::OpenRead((Join-Path $GameDir 'Content.scpak'))
try { $decoded = $decode.Invoke($null, @($content)) } finally { $content.Dispose() }
$zip = [IO.Compression.ZipArchive]::new($decoded, [IO.Compression.ZipArchiveMode]::Read)
function Read-Entry($entry) {
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $reader.ReadToEnd() } finally { $reader.Dispose() }
}
try {
    $rows = (Read-Entry ($zip.Entries | Where-Object { $_.Name -match '^Blocks(Data)?\.(csv|txt)$' } |
        Select-Object -First 1)) -split '\r?\n'
    $nativeRows = @($rows[0]) + @($rows | Where-Object { $_ -match '^(BatteryBlock|LightbulbBlock|IronIngotBlock);' })
    [Game.BlocksManager]::LoadBlocksData(($nativeRows -join "`n"))
    foreach ($pass in @('Opaque', 'AlphaTested', 'Transparent')) {
        foreach ($stage in @(@('vsh', 'VertexSource'), @('psh', 'FragmentSource'))) {
            $source = Read-Entry ($zip.Entries | Where-Object { $_.FullName -like "*/$pass.$($stage[0])" })
            $patched = $lightingType.GetMethod($stage[1], $nonPublicStatic).Invoke($null, @($source))
            $glsl = $source.IndexOf('#ifdef GLSL')
            if (!$patched.StartsWith($source.Substring(0, $glsl)) -or
                $patched -notmatch 'v_scieFlashlightDelta' -or $patched -notmatch 'u_fogColor|calculateFog') {
                throw "Unexpected shader patch for $pass.$($stage[0])."
            }
        }
    }
} finally { $zip.Dispose() }

[Game.BlocksManager]::LoadBlocksData([IO.File]::ReadAllText((Join-Path $assets 'Blocks/Flashlight.csv')))
$creative = @($block.GetCreativeValues())
$full = [Game.Terrain]::MakeBlockValue($id)
$hit = [Game.TerrainRaycastResult]::new()
if ($id -ne 890 -or $block.GetCategory($full) -ne 'SCIEPort.Gear' -or $block.IsPlaceable -or
    $block.GetPlacementValue($null, $null, $full, $hit).Value -ne 0 -or $block.MaxStacking -ne 1 -or
    $block.Durability -ne 300 -or $creative.Count -ne 1 -or $creative[0] -ne $full) {
    throw 'Flashlight registration, placement, category or creative DATA mismatch.'
}
$inventoryRules = $assembly.GetType('SCIEPort.Features.Inventory.PortInventoryRules', $true)
if ($inventoryRules.GetMethod('Select', $nonPublicStatic).Invoke($null, @($id, -1, 'SCIEPort.Gear')).ToString() -ne 'None') {
    throw 'Flashlight must use the ordinary tool slot background.'
}
$runtime = [Activator]::CreateInstance($subsystemType)
$terrainOrders = [Game.SubsystemTerrain]::m_drawOrders
if ($runtime.HandledBlocks[0] -ne $id -or $runtime.DrawOrders[0] -ge $terrainOrders[0] -or
    $runtime.DrawOrders[1] -le $terrainOrders[1]) { throw 'Flashlight behavior or camera draw order is incorrect.' }
$packages = @($assembly.GetTypes() | Where-Object { !$_.IsAbstract -and [Game.NetWork.IPackage].IsAssignableFrom($_) } |
    ForEach-Object { [Activator]::CreateInstance($_) })
if (@($packages | Where-Object ID -eq 231).Count -ne 1) { throw 'Flashlight request ID must be unique.' }
$owners = @($assembly.GetTypes() | Where-Object {
    if (!$_.IsAbstract -and [Game.Block].IsAssignableFrom($_)) {
        $index = $_.GetField('Index', [Reflection.BindingFlags]'Public,Static,DeclaredOnly')
        $index -and $index.IsLiteral -and $index.GetRawConstantValue() -eq $id
    }
})
if ($owners.Count -ne 1) { throw 'Flashlight block ID must be unique.' }

$package = [Activator]::CreateInstance($packageType)
$slotField = $packageType.GetField('Slot', $nonPublicInstance)
$onField = $packageType.GetField('TurnOn', $nonPublicInstance)
$slotField.SetValue($package, [byte]9)
$onField.SetValue($package, $true)
$writer = [Game.NetWork.PackageStreamWriter]::new()
try {
    $package.WriteData($writer)
    if ($writer.BaseStream.Length -ne 2) { throw 'Flashlight request must contain two bytes.' }
    $reader = [Game.NetWork.PackageStreamReader]::new($writer.Data())
    try {
        $received = [Activator]::CreateInstance($packageType)
        $received.ReadData($reader)
        if ($slotField.GetValue($received) -ne 9 -or !$onField.GetValue($received)) { throw 'Flashlight request did not round-trip.' }
    } finally { $reader.Dispose() }
} finally { $writer.Dispose() }

$power = $blockType.GetMethod('SetPower', $nonPublicStatic)
$isOn = $blockType.GetMethod('IsOn', $nonPublicStatic)
$drain = $blockType.GetMethod('Drain', $nonPublicStatic)
$lit = $power.Invoke($null, @($full, $true))
$value = $lit
for ($second = 1; $second -le 300; $second++) {
    $value = $drain.Invoke($null, @($value, 1))
    if ($block.GetDamage($value) -ne $second -or $isOn.Invoke($null, @($value)) -ne ($second -lt 300)) {
        throw "Flashlight power or native inventory bar incorrect at $second seconds."
    }
}
if ($power.Invoke($null, @($value, $true)) -ne $value -or $drain.Invoke($null, @($full, 60)) -ne $full -or
    [Game.BlocksManager]::DamageItem($lit, 1) -ne $lit -or [Game.BlocksManager]::DamageItem($value, 1) -ne $value) {
    throw 'An empty flashlight reopened, an off flashlight drained, or normal gameplay damaged it.'
}

$recipes = @($block.GetProceduralCraftingRecipes())
$ingredients = [string[]]::new(9)
$ingredients[0] = $nativeBlocks[1].CraftingId + ':0'
$ingredients[1] = $nativeBlocks[2].CraftingId + ':0'
$ingredients[3] = $nativeBlocks[0].CraftingId + ':0'
$ingredients[4] = $ingredients[1]
if ($recipes.Count -ne 2 -or ![Game.CraftingRecipesManager]::MatchRecipe($recipes[0].Ingredients, $ingredients) -or
    $recipes[0].ResultValue -ne $full) { throw 'Native flashlight manufacturing recipe mismatch.' }
$ingredients = [string[]]::new(9)
foreach ($data in @(2, 299, 599, 600)) {
    $ingredients[3] = $nativeBlocks[0].CraftingId + ':0'
    $ingredients[4] = $block.CraftingId + ':' + $data
    $result = $block.GetAdHocCraftingRecipe($null, $ingredients, 0, $null)
    if ($null -eq $result -or $result.ResultValue -ne $full -or $result.ResultCount -ne 1) { throw "Refill failed for DATA $data." }
}
$ingredients[4] = $block.CraftingId + ':0'
if ($block.GetAdHocCraftingRecipe($null, $ingredients, 0, $null)) { throw 'A full flashlight accepted a replacement battery.' }
$ingredients[4] = $block.CraftingId + ':600'
$ingredients.SetValue($null, 3)
if ($block.GetAdHocCraftingRecipe($null, $ingredients, 0, $null)) { throw 'Refill accepted a missing battery.' }

$inventory = [Game.ComponentInventory]::new()
$inventory.m_slots.Add([Game.ComponentInventoryBase+Slot]@{Count = 1; Value = $lit})
$inventory.m_slots.Add([Game.ComponentInventoryBase+Slot]@{Count = 0; Value = 0})
$player = [Game.ComponentPlayer]::new()
$player.ComponentMiner = [Game.ComponentMiner]::new()
$player.ComponentMiner.Inventory = $inventory
$player.ComponentHealth = [Game.ComponentHealth]::new()
$player.ComponentHealth.Health = 1
$players = [Game.SubsystemPlayers]::new()
$players.m_componentPlayers.Add($player)
$subsystemType.GetField('m_players', $nonPublicInstance).SetValue($runtime, $players)
[Game.NetWork.CommonLib]::WorkType = [Game.NetWork.WorkType]::Local
$runtime.Update(0.75)
$inventory.ActiveSlotIndex = 1
$runtime.Update(2)
if ($inventory.GetSlotValue(0) -ne $lit) { throw 'Unselected flashlight consumed charge.' }
$inventory.ActiveSlotIndex = 0
[Game.NetWork.CommonLib]::WorkType = [Game.NetWork.WorkType]::Client
$runtime.Update(1)
if ($inventory.GetSlotValue(0) -ne $lit) { throw 'Client updated charge without host authority.' }
[Game.NetWork.CommonLib]::WorkType = [Game.NetWork.WorkType]::Server
$runtime.Update(0.25)
if ($block.GetDamage($inventory.GetSlotValue(0)) -ne 1 -or $inventory.GetSlotCount(0) -ne 1) {
    throw 'Host did not debit accumulated active time on the native slot.'
}
$saved = [TemplatesDatabase.ValuesDictionary]::new()
$inventory.Save($saved, $null)
if ($saved['Slots']['Slot0']['Contents'] -ne $inventory.GetSlotValue(0)) { throw 'Native save did not retain exact flashlight DATA.' }
$runtime.Dispose()
Write-Host 'Flashlight: native registration, three terrain shader pairs, request round-trip, 300 power states, crafting, replacement, slot authority and save passed.'
