[CmdletBinding()]
param([string]$GameDir)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Resolve-GameDir.ps1')
$GameDir = Resolve-SurvivalcraftGameDirectory -RequestedPath $GameDir

$framework = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
if (-not (Test-Path -LiteralPath $framework -PathType Container)) {
    throw 'Missing .NET Framework 4.7.2 Targeting Pack.'
}

$expectedHashes = @{
    'Survivalcraft.exe' = '3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD'
    'Engine.dll' = '98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694'
    'EntitySystem.dll' = '7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C'
}
foreach ($entry in $expectedHashes.GetEnumerator()) {
    $path = Join-Path $GameDir $entry.Key
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash
    if ($actual -ne $entry.Value) {
        throw "Unexpected x25.01.17 hash for $($entry.Key): $actual"
    }
    Write-Host "HASH OK $($entry.Key) $actual"
}

$survivalcraft = Join-Path $GameDir 'Survivalcraft.exe'
$assemblyName = [Reflection.AssemblyName]::GetAssemblyName($survivalcraft)
if ($assemblyName.Version.ToString() -ne '2.4.0.0') {
    throw "Unexpected Survivalcraft assembly version: $($assemblyName.Version)"
}
$assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($survivalcraft)
$targetFramework = $assembly.GetCustomAttributesData() |
    Where-Object { $_.AttributeType.FullName -eq 'System.Runtime.Versioning.TargetFrameworkAttribute' } |
    ForEach-Object { $_.ConstructorArguments[0].Value }
if ($targetFramework -ne '.NETFramework,Version=v4.7.2') {
    throw "Unexpected target framework: $targetFramework"
}

$bytes = [IO.File]::ReadAllBytes($survivalcraft)
$unicode = [Text.Encoding]::Unicode.GetString($bytes) + [Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1)
foreach ($marker in @('Initilizing Mods Manager. Api Version: 1.42', '2.4.10.6', 'x25.01.17', '.netmod')) {
    if (-not $unicode.Contains($marker)) { throw "Missing target marker: $marker" }
}

$ilspy = Get-Command ilspycmd -ErrorAction SilentlyContinue
if (-not $ilspy) { throw 'ilspycmd is required for target ABI verification.' }
function Assert-TypeText([string]$TypeName, [string[]]$Patterns, [string]$AssemblyPath = $survivalcraft) {
    $text = (& $ilspy.Source --disable-updatecheck -t $TypeName $AssemblyPath | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "Failed to inspect $TypeName" }
    foreach ($pattern in $Patterns) {
        if ($text -notmatch $pattern) { throw "ABI pattern '$pattern' not found in $TypeName" }
    }
    Write-Host "ABI OK $TypeName"
}

Assert-TypeText 'Game.ModLoader' @(
    'void OnTerrainContentsGenerated\(TerrainChunk chunk\)',
    'void SubsystemUpdate\(float dt\)',
    'void SaveSettings\(XElement xElement\)',
    'void LoadSettings\(XElement xElement\)'
)
Assert-TypeText 'ModsManager' @('void RegisterHook\(string HookName, ModLoader modLoader\)', 'void HookAction')
Assert-TypeText 'Game.ModInfo' @('List<string> Dependencies')
Assert-TypeText 'Game.ModEntity' @('bool GetFile\(string filename, Action<Stream> stream\)', 'Dictionary<string, ZipArchiveEntry> ModFiles')
Assert-TypeText 'Game.ITerrainContentsGenerator' @('CalculateHeight', 'CalculateTemperature', 'CalculateHumidity', 'GenerateChunkContentsPass4')
Assert-TypeText 'Game.TerrainChunk' @('void SetCellValueFast\(int x, int y, int z, int value\)')
Assert-TypeText 'Game.SubsystemTerrain' @('ITerrainContentsGenerator TerrainContentsGenerator')
Assert-TypeText 'Game.SubsystemBlockEntities' @('GetBlockEntity', 'CreateBlockEntityLogic')
Assert-TypeText 'Game.SubsystemCreatureSpawn' @('WorkType.Client', 'void CreateEntity')
Assert-TypeText 'Game.NetWork.CommonLib' @('public static WorkType WorkType')
Assert-TypeText 'Game.NetWork.WorkType' @('Local', 'Server', 'Client')
Assert-TypeText 'Game.SubsystemUpdate' @('HookAction\("SubsystemUpdate"', 'loader.SubsystemUpdate\(dt\)')
Assert-TypeText 'Engine.Storage' @('CombinePaths', 'OpenFile', 'ReadAllText', 'WriteAllText', 'MoveFile') -AssemblyPath (Join-Path $GameDir 'Engine.dll')

$terrainUpdater = (& $ilspy.Source --disable-updatecheck -t 'Game.TerrainUpdater' $survivalcraft | Out-String)
$hook = $terrainUpdater.IndexOf('HookAction("OnTerrainContentsGenerated"', [StringComparison]::Ordinal)
$pass4 = $terrainUpdater.IndexOf('GenerateChunkContentsPass4(chunk)', [StringComparison]::Ordinal)
if ($hook -lt 0 -or $pass4 -lt 0 -or $hook -gt $pass4) {
    throw 'OnTerrainContentsGenerated is not called before terrain pass 4.'
}

Write-Host "SCNET_GAME_DIR=$GameDir"
Write-Host "AssemblyVersion=$($assemblyName.Version) TargetFramework=$targetFramework ApiVersion=1.42 ScVersion=2.4.10.6"
Write-Host 'Environment and ABI validation passed.'
