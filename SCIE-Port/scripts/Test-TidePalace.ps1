[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$GameDir,
    [Parameter(Mandatory = $true)][string]$AssemblyPath,
    [string]$Package
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src/SCIEPort/Assets'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Xml.Linq

if ($Package) {
    $archive = [IO.Compression.ZipFile]::OpenRead($Package)
    try {
        foreach ($name in @('Blocks/TidePalace.csv', 'Database/TidePalace.xdb', 'Textures/TidePalace.png',
                'Blocks/Pyramid.csv', 'Database/Pyramid.xdb', 'Textures/PyramidMechanism.png',
                'Database/PyramidGuardians.xdb', 'Models/PyramidMummy.dae', 'Models/PyramidPharaoh.dae',
                'Textures/PyramidGuardians.png')) {
            $entry = $archive.GetEntry("Assets/$name")
            if (!$entry) { throw "Missing palace asset $name" }
            $stream = $entry.Open()
            try {
                $actual = (Get-FileHash -InputStream $stream).Hash
                $expected = (Get-FileHash -LiteralPath (Join-Path $assets $name)).Hash
                if ($actual -ne $expected) { throw "Palace asset differs in the package: $name" }
            } finally { $stream.Dispose() }
        }
    } finally { $archive.Dispose() }
    Write-Host 'Tide Palace and Pyramid package resources match the tested source assets.'
    return
}

foreach ($file in @('Engine.dll', 'EntitySystem.dll', 'LiteNetLib.dll', 'Survivalcraft.exe')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))
}
$mod = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$options = New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateInMemory = $true
foreach ($file in @('Engine.dll', 'EntitySystem.dll', 'Survivalcraft.exe', 'LiteNetLib.dll')) {
    [void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $file))
}
foreach ($file in @('System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Xml.Linq.dll')) {
    [void]$options.ReferencedAssemblies.Add($file)
}
$compiler = New-Object Microsoft.CSharp.CSharpCodeProvider
[string[]]$sources = @(
    (Join-Path $root 'tests/SCIEPort.TidePalace.NativeTests/TidePalaceProbe.cs'),
    (Join-Path $root 'tests/SCIEPort.Geology.NativeTests/VolcanoNativeProbe.cs'),
    (Join-Path $root 'tests/SCIEPort.Geology.NativeTests/SpecialTerrainNativeProbe.cs'),
    (Join-Path $root 'tests/SCIEPort.Geology.NativeTests/CloudfallNativeProbe.cs'),
    (Join-Path $root 'tests/SCIEPort.Pyramid.NativeTests/PyramidNativeProbe.cs'),
    (Join-Path $root 'tests/SCIEPort.Pyramid.NativeTests/PyramidGuardianProbe.cs')
)
$compiled = $compiler.CompileAssemblyFromFile($options, $sources)
if ($compiled.Errors.HasErrors) { throw (($compiled.Errors | ForEach-Object ToString) -join "`n") }

function Read-Text($entry) {
    if (!$entry) { throw 'Required native content entry is missing.' }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { $reader.ReadToEnd() } finally { $reader.Dispose() }
}

$game = [Game.BlocksManager].Assembly
$decode = $game.GetType('Game.SurvivalCraftModEntity').GetMethod('GetDecipherStream', [Reflection.BindingFlags]'NonPublic,Static')
$content = [IO.File]::OpenRead((Join-Path $GameDir 'Content.scpak'))
try { $decoded = $decode.Invoke($null, @($content)) } finally { $content.Dispose() }
$zip = [IO.Compression.ZipArchive]::new($decoded, [IO.Compression.ZipArchiveMode]::Read)
try {
    $xml = [System.Xml.Linq.XElement]::Parse((Read-Text $zip.GetEntry('Assets/Database.xml')))
    $nativeTable = $zip.Entries | Where-Object { $_.Name -match '^Blocks(Data)?\.(csv|txt)$' } | Select-Object -First 1
    if (!$nativeTable) { throw "Native block CSV not found. Candidate names: $($zip.Entries.Name -like '*Block*' -join ', ')." }
    $nativeBlocks = Read-Text $nativeTable
} finally { $zip.Dispose() }
foreach ($name in @('Geology', 'SuspendedItem', 'TidePalace', 'Pyramid', 'PyramidGuardians')) {
    $xdb = [IO.File]::OpenRead((Join-Path $assets "Database/$name.xdb"))
    try { [ModsManager]::CombineDataBase($xml, $xdb) } finally { $xdb.Dispose() }
}
$geology = [IO.File]::ReadAllText((Join-Path $assets 'Blocks/Geology.csv'))
$palace = [IO.File]::ReadAllText((Join-Path $assets 'Blocks/TidePalace.csv'))
$pyramid = [IO.File]::ReadAllText((Join-Path $assets 'Blocks/Pyramid.csv'))
$pyramidDecor = [IO.File]::ReadAllText((Join-Path $assets 'Blocks/PyramidDecor.csv'))
[string[]]$artifactTables = Get-ChildItem -LiteralPath (Join-Path $assets 'Blocks') -Filter '*.csv' |
    Where-Object { Select-String -LiteralPath $_.FullName -Pattern ';SCIEPort.Artifacts;' -Quiet } |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }
try { $compiled.CompiledAssembly.GetType('TidePalaceProbe').GetMethod('Run').Invoke($null, @($mod, $xml, $nativeBlocks, $geology, $palace, $pyramid, $pyramidDecor, $artifactTables, $assets.PSObject.BaseObject)) }
catch { Write-Host $_.Exception.ToString(); throw }
