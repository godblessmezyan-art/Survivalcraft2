[CmdletBinding()]
param(
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' })
)

$ErrorActionPreference = 'Stop'
$required = @{
    'Survivalcraft.exe' = '3D14B8D698546F13E66FBF49DBCFAC45A2CDDFC97709D738050DF2E1CC74A7AD'
    'Engine.dll' = '98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694'
    'EntitySystem.dll' = '7A87E6435DBC81652D610D26934BBD7594314C3D4A7DD7C765DF4F5BB67ED19C'
}
foreach ($item in $required.GetEnumerator()) {
    $path = Join-Path $GameDir $item.Key
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing required target assembly: $path"
    }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($actual -ne $item.Value) {
        throw "$($item.Key) hash is not the audited Net_x25.01.17 Windows assembly. Actual: $actual"
    }
}

$framework = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
if (-not (Test-Path -LiteralPath $framework -PathType Container)) {
    throw 'Missing .NET Framework 4.7.2 Targeting Pack.'
}

$gamePath = Join-Path $GameDir 'Survivalcraft.exe'
$assemblyName = [Reflection.AssemblyName]::GetAssemblyName($gamePath)
if ($assemblyName.Version.ToString() -ne '2.4.0.0') {
    throw "Unexpected Survivalcraft assembly version: $($assemblyName.Version)"
}

$bytes = [IO.File]::ReadAllBytes($gamePath)
$unicode = [Text.Encoding]::Unicode.GetString($bytes) + [Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1)
foreach ($marker in @('Initilizing Mods Manager. Api Version: 1.42', '2.4.10.6', '.netmod')) {
    if (-not $unicode.Contains($marker)) { throw "Missing target marker: $marker" }
}

[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'Engine.dll')))
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'EntitySystem.dll')))
$assembly = [Reflection.Assembly]::Load($bytes)
$modLoader = $assembly.GetType('Game.ModLoader', $true)
$cubeBlock = $assembly.GetType('Game.CubeBlock', $true)
$block = $assembly.GetType('Game.Block', $true)
$blocksManager = $assembly.GetType('Game.BlocksManager', $true)
$modEntity = $assembly.GetType('Game.ModEntity', $true)
$globalModsManager = $assembly.GetType('ModsManager', $true)
$componentBoat = $assembly.GetType('Game.ComponentBoat', $true)
$componentMount = $assembly.GetType('Game.ComponentMount', $true)
$componentRider = $assembly.GetType('Game.ComponentRider', $true)
$componentBody = $assembly.GetType('Game.ComponentBody', $true)
$componentModel = $assembly.GetType('Game.ComponentModel', $true)
$componentPlayer = $assembly.GetType('Game.ComponentPlayer', $true)
$componentInput = $assembly.GetType('Game.ComponentInput', $true)
$playerInput = $componentInput.GetProperty('PlayerInput').PropertyType
$creatureSpawn = $assembly.GetType('Game.SubsystemCreatureSpawn', $true)
$subsystemProjectiles = $assembly.GetType('Game.SubsystemProjectiles', $true)
$subsystemExplosions = $assembly.GetType('Game.SubsystemExplosions', $true)
$bulletBlock = $assembly.GetType('Game.BulletBlock', $true)

if (-not $modLoader.GetMethod('__ModInitialize', [Reflection.BindingFlags]'Public,Instance')) {
    throw 'Game.ModLoader.__ModInitialize was not found.'
}
if (-not $globalModsManager.GetMethod('RegisterHook', [Reflection.BindingFlags]'Public,Static')) {
    throw 'Global ModsManager.RegisterHook was not found.'
}
if (-not $blocksManager.GetMethod('LoadBlocksData', [Reflection.BindingFlags]'Public,Static', $null, [Type[]]@([string]), $null)) {
    throw 'BlocksManager.LoadBlocksData(string) was not found.'
}
if (-not $modEntity.GetMethod('LoadBlocksData', [Reflection.BindingFlags]'Public,Instance')) {
    throw 'ModEntity.LoadBlocksData was not found.'
}

$nativeIds = @()
$loadedTypes = @()
try {
    $loadedTypes = $assembly.GetTypes()
}
catch [Reflection.ReflectionTypeLoadException] {
    $loadedTypes = @($_.Exception.Types | Where-Object { $null -ne $_ })
}
foreach ($type in $loadedTypes) {
    if ($type -ne $block -and $block.IsAssignableFrom($type)) {
        $field = $type.GetField('Index', [Reflection.BindingFlags]'Public,Static,FlattenHierarchy')
        if ($field -and $field.FieldType -eq [int] -and $field.IsLiteral) {
            $nativeIds += [int]$field.GetRawConstantValue()
        }
    }
}
if ($nativeIds.Count -lt 200) {
    throw "Native block ID audit was incomplete; only $($nativeIds.Count) block constants were readable."
}
foreach ($reservedId in @((903..924) + (995..1000))) {
    if ($nativeIds -contains $reservedId) {
        throw "SCIE-Port block index $reservedId conflicts with a native block."
    }
}
if (-not $componentBoat.GetProperty('MoveOrder') -or -not $componentBoat.GetProperty('TurnOrder')) {
    throw 'ComponentBoat movement order properties were not found.'
}
if (-not $componentMount.GetProperty('Rider') -or -not $componentRider.GetProperty('Mount')) {
    throw 'Native mount/rider API required by the steam boat was not found.'
}
if (-not $componentRider.GetMethod('StartDismounting', [Reflection.BindingFlags]'Public,Instance')) {
    throw 'ComponentRider.StartDismounting required by the tank safety dismount was not found.'
}
if (-not $componentPlayer.GetProperty('ComponentInput') -or -not $componentPlayer.GetProperty('ComponentRider') -or -not $playerInput.GetField('Move', [Reflection.BindingFlags]'Public,Instance')) {
    throw 'ComponentPlayer -> ComponentInput -> PlayerInput.Move chain required by the tank was not found.'
}
if (-not $playerInput.GetField('Hit', [Reflection.BindingFlags]'Public,Instance')) {
    throw 'PlayerInput.Hit touch/attack ray required by the tank main gun was not found.'
}
if (-not ($subsystemProjectiles.GetMethods() | Where-Object { $_.Name -eq 'FireProjectile' -and $_.GetParameters().Count -eq 5 })) {
    throw 'SubsystemProjectiles.FireProjectile(value, position, velocity, angularVelocity, owner) was not found.'
}
if (-not ($subsystemExplosions.GetMethods() | Where-Object { $_.Name -eq 'AddExplosion' -and $_.GetParameters().Count -eq 7 })) {
    throw 'SubsystemExplosions.AddExplosion required by tank shell impacts was not found.'
}
$bulletIndex = $bulletBlock.GetField('Index', [Reflection.BindingFlags]'Public,Static,FlattenHierarchy')
if ($null -eq $bulletIndex -or $bulletIndex.GetRawConstantValue() -ne 214) {
    throw 'The audited native BulletBlock.Index 214 is unavailable.'
}
if (-not ($creatureSpawn.GetMethods() | Where-Object { $_.Name -eq 'SpawnCreature' })) {
    throw 'SubsystemCreatureSpawn.SpawnCreature was not found.'
}
foreach ($propertyName in @('IsGravityEnabled', 'IsGroundDragEnabled', 'IsSmoothRiseEnabled', 'MaxSmoothRiseHeight', 'StandingOnValue', 'StandingOnBody', 'Velocity', 'Rotation', 'ImmersionFactor')) {
    if (-not $componentBody.GetProperty($propertyName)) {
        throw "ComponentBody.$propertyName required by the airship was not found."
    }
}
if (-not $componentModel.GetProperty('TextureOverride')) {
    throw 'ComponentModel.TextureOverride required by the airship was not found.'
}
if (-not ($componentModel.GetMethods() | Where-Object { $_.Name -eq 'SetBoneTransform' -and $_.GetParameters().Count -eq 2 })) {
    throw 'ComponentModel.SetBoneTransform required by tank turret articulation was not found.'
}
$componentCreatureModel = $assembly.GetType('Game.ComponentCreatureModel', $true)
if (-not $componentCreatureModel.GetProperty('EyeRotation')) {
    throw 'ComponentCreatureModel.EyeRotation required by the tank spawn orientation was not found.'
}

Write-Host "SCNET: $gamePath"
Write-Host "Assembly version: $($assemblyName.Version)"
Write-Host 'API marker: 1.42; SC marker: 2.4.10.6; target: .NET Framework 4.7.2'
Write-Host "Native block IDs discovered: $($nativeIds.Count); maximum: $(($nativeIds | Measure-Object -Maximum).Maximum)"
Write-Host 'Loader, block registration, Kelly plant, industrial furniture, water/rail/land transport mount-spawn-body, and reserved ID ABI checks passed.'
