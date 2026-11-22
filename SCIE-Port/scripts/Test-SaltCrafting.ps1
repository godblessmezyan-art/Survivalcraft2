[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [Parameter(Mandatory = $true)][string]$AssemblyPath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src/SCIEPort/Assets'
foreach ($file in @('Engine.dll', 'EntitySystem.dll', 'LiteNetLib.dll', 'Survivalcraft.exe')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))
}
$mod = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$blocks = [Game.BlocksManager]::Blocks
$air = [Game.AirBlock]::new()
for ($i = 0; $i -lt $blocks.Length; $i++) { $blocks[$i] = $air }

$torch = [Game.TorchBlock]::new()
$torch.BlockIndex = [Game.TorchBlock]::Index
$blocks[$torch.BlockIndex] = $torch
Add-Type -AssemblyName System.IO.Compression
$game = [Game.BlocksManager].Assembly
$decode = $game.GetType('Game.SurvivalCraftModEntity').GetMethod('GetDecipherStream', [Reflection.BindingFlags]'NonPublic,Static')
$content = [IO.File]::OpenRead((Join-Path $GameDir 'Content.scpak'))
try { $decoded = $decode.Invoke($null, @($content)) } finally { $content.Dispose() }
$zip = [IO.Compression.ZipArchive]::new($decoded, [IO.Compression.ZipArchiveMode]::Read)
try {
    $entry = $zip.Entries | Where-Object { $_.Name -match '^Blocks(Data)?\.(csv|txt)$' } | Select-Object -First 1
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $rows = @($reader.ReadToEnd() -split '\r?\n') } finally { $reader.Dispose() }
    [Game.BlocksManager]::LoadBlocksData(($rows[0], ($rows | Where-Object { $_ -like 'TorchBlock;*' }) -join "`n"))
} finally { $zip.Dispose() }

foreach ($table in @('Geology', 'SaltCrafting')) {
    $csv = [IO.File]::ReadAllText((Join-Path $assets "Blocks/$table.csv"))
    foreach ($row in ($csv | ConvertFrom-Csv -Delimiter ';')) {
        $type = $mod.GetType('SCIEPort.Features.Geology.' + $row.'Class Name', $true)
        $block = [Activator]::CreateInstance($type)
        $block.BlockIndex = $type.GetField('Index', [Reflection.BindingFlags]'Public,Static,DeclaredOnly').GetRawConstantValue()
        $blocks[$block.BlockIndex] = $block
    }
    [Game.BlocksManager]::LoadBlocksData($csv)
}

foreach ($id in @(898, 899)) {
    $owners = @($mod.GetTypes() | Where-Object {
        if (!$_.IsAbstract -and [Game.Block].IsAssignableFrom($_)) {
            $index = $_.GetField('Index', [Reflection.BindingFlags]'Public,Static,DeclaredOnly')
            $index -and $index.IsLiteral -and $index.GetRawConstantValue() -eq $id
        }
    })
    if ($owners.Count -ne 1) { throw "Expected exactly one mod block registered at $id." }
}

$crystal = $blocks[898]
$lamp = $blocks[899]
$crust = $blocks[1012]
$basalt = $blocks[1006]
$hit = [Game.TerrainRaycastResult]::new()
if ($crystal.IsPlaceable -or $crystal.GetPlacementValue($null, $null, 898, $hit).Value -ne 0 -or
    !$lamp.IsPlaceable -or !$lamp.IsCollidable -or $lamp.GetPlacementValue($null, $null, 899, $hit).Value -ne 899 -or
    $lamp.GetEmittedLightAmount(899) -ne 12) { throw 'Salt crystal placement or lamp light/placement is incorrect.' }

function Assert-Recipe($block, [int]$count, [hashtable]$slots) {
    $ingredients = [string[]]::new(9)
    foreach ($slot in $slots.Keys) { $ingredients[$slot] = $slots[$slot].CraftingId + ':0' }
    $recipes = @($block.GetProceduralCraftingRecipes())
    if ($recipes.Count -ne 1 -or $recipes[0].ResultValue -ne $block.BlockIndex -or
        $recipes[0].ResultCount -ne $count -or $recipes[0].RequiredHeatLevel -ne 0 -or
        ![Game.CraftingRecipesManager]::MatchRecipe($recipes[0].Ingredients, $ingredients)) {
        throw "Native recipe mismatch for $($block.GetType().Name)."
    }
    $ingredients.SetValue($null, 0)
    if ([Game.CraftingRecipesManager]::MatchRecipe($recipes[0].Ingredients, $ingredients)) {
        throw "Native recipe matched with a missing ingredient for $($block.GetType().Name)."
    }
}
Assert-Recipe $crystal 2 @{ 0 = $crust }
Assert-Recipe $lamp 1 @{ 0 = $crystal; 1 = $torch; 2 = $crystal; 4 = $basalt }

$drops = [Collections.Generic.List[Game.BlockDropValue]]::new()
$debris = $false
$lamp.GetDropValues($null, 899, 0, 0, $drops, [ref]$debris)
if ($drops.Count -ne 1 -or $drops[0].Value -ne 899 -or $drops[0].Count -ne 1) { throw 'Lamp must drop itself once.' }
Write-Host 'Salt crafting: target block data, ID uniqueness, recipes and missing-ingredient rejection, placement, light and lamp drop passed.'
