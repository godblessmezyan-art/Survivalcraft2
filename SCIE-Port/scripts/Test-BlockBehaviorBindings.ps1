[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [Parameter(Mandatory = $true)][string]$Package
)

$ErrorActionPreference = 'Stop'
foreach ($file in @('Engine.dll', 'EntitySystem.dll', 'Survivalcraft.exe')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Xml.Linq

function Read-EntryText($entry) {
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $reader.ReadToEnd() } finally { $reader.Dispose() }
}

$game = [Game.BlocksManager].Assembly
$decode = $game.GetType('Game.SurvivalCraftModEntity').GetMethod('GetDecipherStream', [Reflection.BindingFlags]'NonPublic,Static')
$content = [IO.File]::OpenRead((Join-Path $GameDir 'Content.scpak'))
try { $decoded = $decode.Invoke($null, @($content)) } finally { $content.Dispose() }
$nativeZip = [IO.Compression.ZipArchive]::new($decoded, [IO.Compression.ZipArchiveMode]::Read)
try {
    $databaseXml = [System.Xml.Linq.XElement]::Parse((Read-EntryText $nativeZip.GetEntry('Assets/Database.xml')))
} finally { $nativeZip.Dispose() }

$modZip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package).Path)
try {
    foreach ($entry in $modZip.Entries | Where-Object { $_.FullName -like 'Assets/Database/*.xdb' }) {
        $source = $entry.Open()
        try { [ModsManager]::CombineDataBase($databaseXml, $source) } finally { $source.Dispose() }
    }
    $tables = @{}
    foreach ($entry in $modZip.Entries | Where-Object { $_.FullName -like 'Assets/Blocks/*.csv' }) {
        $tables[$entry.Name] = Read-EntryText $entry
    }
    $assemblyStream = $modZip.GetEntry('SCIEPort.dll').Open()
    $assemblyBytes = [IO.MemoryStream]::new()
    try {
        $assemblyStream.CopyTo($assemblyBytes)
        $mod = [Reflection.Assembly]::Load($assemblyBytes.ToArray())
    } finally { $assemblyStream.Dispose(); $assemblyBytes.Dispose() }
} finally { $modZip.Dispose() }

$database = [TemplatesDatabase.XmlDatabaseSerializer]::LoadDatabase($databaseXml)
$gameDatabase = [GameEntitySystem.GameDatabase]::new($database)
$blocks = [Game.BlocksManager]::Blocks
$air = [Game.AirBlock]::new()
for ($i = 0; $i -lt $blocks.Length; $i++) { $blocks[$i] = $air }
$water = [Game.WaterBlock]::new()
$water.BlockIndex = [Game.WaterBlock]::Index
$blocks[$water.BlockIndex] = $water
[Game.BlocksManager]::FluidBlocks[$water.BlockIndex] = $water

$geology = $tables['Geology.csv']
foreach ($row in ($geology | ConvertFrom-Csv -Delimiter ';')) {
    $type = $mod.GetType('SCIEPort.Features.Geology.' + $row.'Class Name', $true)
    $block = [Activator]::CreateInstance($type)
    $block.BlockIndex = $type.GetField('Index', [Reflection.BindingFlags]'Public,Static,DeclaredOnly').GetRawConstantValue()
    $blocks[$block.BlockIndex] = $block
}
[Game.BlocksManager]::LoadBlocksData($geology)
$mineral = $blocks[1013]
[Game.BlocksManager]::FluidBlocks[1013] = $mineral
$water.Initialize()
$mineral.Initialize()
if (!$water.IsTheSameFluid(1013) -or !$mineral.IsTheSameFluid(18)) {
    throw 'Native fluid family must include both vanilla and mineral water.'
}

$bindings = @(
    foreach ($csv in $tables.Values) {
        foreach ($row in ($csv | ConvertFrom-Csv -Delimiter ';')) {
            foreach ($name in ([string]$row.Behaviors -split ',')) {
                if ($name.Trim()) { $name.Trim() }
            }
        }
    }
) | Sort-Object -Unique

foreach ($projectName in @('Project', 'GameProject')) {
    $template = $database.FindDatabaseObject($projectName, $gameDatabase.ProjectTemplateType, $true)
    $values = [TemplatesDatabase.ValuesDictionary]::new()
    $values.PopulateFromDatabaseObject($template)
    $project = [GameEntitySystem.Project]::new()
    $project.m_gameDatabase = $gameDatabase
    $waterClass = $game.GetType($values['WaterBehavior']['Class'], $true)
    $waterBehavior = [Activator]::CreateInstance($waterClass)
    $waterBehavior.Initialize($project, $values['WaterBehavior'])
    $project.m_subsystems.Add($waterBehavior)

    # Exercise the same loader and named lookup as world startup using the packaged CSV.
    $registry = [Game.SubsystemBlockBehaviors]::new()
    $registry.Initialize($project, $values['BlockBehaviors'])
    $registry.Load($values['BlockBehaviors'])
    foreach ($id in @(18, 1013)) {
        $resolved = $registry.GetBlockBehaviors($id, $null, $null)
        if ($resolved.Count -ne 1 -or ![object]::ReferenceEquals($resolved[0], $waterBehavior)) {
            throw "$projectName block $id must bind exactly once to the native water subsystem."
        }
    }

    $configured = $mineral.Behaviors
    $mineral.Behaviors = 'WaterBlockBehavior'
    $reproduced = $false
    try { $registry.Load($values['BlockBehaviors']) }
    catch {
        $reproduced = $_.Exception.GetBaseException().Message -eq 'Required subsystem Game.SubsystemBlockBehavior with name "WaterBlockBehavior" does not exist in project.'
    } finally { $mineral.Behaviors = $configured }
    if (!$reproduced) { throw "$projectName did not reproduce the original world load failure." }

    foreach ($name in $bindings) {
        $member = $values[$name]
        if ($null -eq $member -or $member.DatabaseObject.Type -ne $gameDatabase.MemberSubsystemTemplateType) {
            throw "$projectName has no subsystem member named '$name' referenced by packaged CSV data."
        }
        $type = $game.GetType($member['Class'])
        if ($null -eq $type) { $type = $mod.GetType($member['Class']) }
        if ($null -eq $type -or ![Game.SubsystemBlockBehavior].IsAssignableFrom($type)) {
            throw "$projectName member '$name' is not a block behavior."
        }
    }
}

Write-Host "Project and GameProject: $($bindings.Count) packaged behavior names resolved; water 18/1013 share the native behavior; old name reproduced the startup error."
