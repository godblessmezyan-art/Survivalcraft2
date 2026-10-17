[CmdletBinding()]
param(
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' })
)

$ErrorActionPreference = 'Stop'
$assemblyPaths = @(
    (Join-Path $GameDir 'Survivalcraft.exe'),
    (Join-Path $GameDir 'Engine.dll'),
    (Join-Path $GameDir 'EntitySystem.dll')
)

foreach ($path in $assemblyPaths) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing assembly required for speedboat ABI validation: $path"
    }
}

$resolveHandler = [System.ResolveEventHandler] {
    param($sender, $eventArgs)

    $simpleName = ([System.Reflection.AssemblyName]::new($eventArgs.Name)).Name
    $candidate = Join-Path $GameDir ($simpleName + '.dll')
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        return [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($candidate)
    }
    return $null
}

[System.AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolveHandler)
try {
    $assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))

    function Get-RequiredType([string]$typeName) {
        $type = $assembly.GetType($typeName, $false, $false)
        if ($null -eq $type) {
            throw "Missing speedboat ABI type: $typeName"
        }
        return $type
    }

    function Assert-Property([string]$typeName, [string]$propertyName, [string]$propertyTypeName) {
        $type = Get-RequiredType $typeName
        $property = $type.GetProperty($propertyName, [System.Reflection.BindingFlags]'Public,Instance,Static')
        if ($null -eq $property -or $property.PropertyType.FullName -ne $propertyTypeName) {
            throw "Unexpected speedboat ABI property: $typeName.$propertyName (expected $propertyTypeName)"
        }
    }

    function Assert-Field([string]$typeName, [string]$fieldName, [string]$fieldTypeName) {
        $type = Get-RequiredType $typeName
        $field = $type.GetField($fieldName, [System.Reflection.BindingFlags]'Public,Instance,Static')
        if ($null -eq $field -or $field.FieldType.FullName -ne $fieldTypeName) {
            throw "Unexpected speedboat ABI field: $typeName.$fieldName (expected $fieldTypeName)"
        }
    }

    function Assert-Method(
        [string]$typeName,
        [string]$methodName,
        [string]$returnTypeName,
        [string[]]$parameterTypeNames,
        [bool]$isStatic = $false) {
        $type = Get-RequiredType $typeName
        $methods = $type.GetMethods([System.Reflection.BindingFlags]'Public,Instance,Static') |
            Where-Object {
                $_.Name -eq $methodName -and
                $_.IsStatic -eq $isStatic -and
                $_.ReturnType.FullName -eq $returnTypeName
            }
        $match = $methods | Where-Object {
            $actual = @($_.GetParameters() | ForEach-Object { $_.ParameterType.FullName })
            if ($actual.Count -ne $parameterTypeNames.Count) {
                return $false
            }
            for ($index = 0; $index -lt $actual.Count; $index++) {
                if ($actual[$index] -ne $parameterTypeNames[$index]) {
                    return $false
                }
            }
            return $true
        } | Select-Object -First 1
        if ($null -eq $match) {
            throw "Missing speedboat ABI method: $typeName.$methodName($($parameterTypeNames -join ', '))"
        }
    }

    function Assert-Constructor([string]$typeName, [string[]]$parameterTypeNames) {
        $type = Get-RequiredType $typeName
        $match = $type.GetConstructors([System.Reflection.BindingFlags]'Public,Instance') | Where-Object {
            $actual = @($_.GetParameters() | ForEach-Object { $_.ParameterType.FullName })
            if ($actual.Count -ne $parameterTypeNames.Count) {
                return $false
            }
            for ($index = 0; $index -lt $actual.Count; $index++) {
                if ($actual[$index] -ne $parameterTypeNames[$index]) {
                    return $false
                }
            }
            return $true
        } | Select-Object -First 1
        if ($null -eq $match) {
            throw "Missing speedboat ABI constructor: $typeName($($parameterTypeNames -join ', '))"
        }
    }

    foreach ($typeName in @(
        'Game.ComponentBoat',
        'Game.ComponentMount',
        'Game.ComponentRider',
        'Game.ComponentBody',
        'Game.ComponentInput',
        'Game.ComponentPlayer',
        'Game.PlayerData',
        'Game.SubsystemCreatureSpawn',
        'Game.DatabaseManager',
        'Game.NetWork.CommonLib',
        'Game.NetWork.WorkType',
        'Game.NetWork.Packages.ComponentMountPackage',
        'Game.NetWork.Packages.ComponentPlayerPackage',
        'Game.NetWork.Packages.EntityPackage',
        'Game.NetWork.Packages.SubsystemBodyPackage'
    )) {
        Get-RequiredType $typeName | Out-Null
    }

    Assert-Field 'Game.ComponentBoat' 'm_componentBody' 'Game.ComponentBody'
    Assert-Field 'Game.ComponentBoat' 'm_componentMount' 'Game.ComponentMount'
    Assert-Field 'Game.ComponentBoat' 'm_subsystemTime' 'Game.SubsystemTime'
    Assert-Property 'Game.ComponentBoat' 'MoveOrder' 'System.Single'
    Assert-Property 'Game.ComponentBoat' 'TurnOrder' 'System.Single'

    Assert-Property 'Game.ComponentMount' 'ComponentBody' 'Game.ComponentBody'
    Assert-Property 'Game.ComponentMount' 'MountOffset' 'Engine.Vector3'
    Assert-Property 'Game.ComponentMount' 'DismountOffset' 'Engine.Vector3'
    Assert-Property 'Game.ComponentMount' 'Rider' 'Game.ComponentRider'

    Assert-Property 'Game.ComponentRider' 'Mount' 'Game.ComponentMount'
    Assert-Field 'Game.ComponentRider' 'm_riderOffset' 'Engine.Vector3'
    Assert-Method 'Game.ComponentRider' 'StartMounting' 'System.Void' @('Game.ComponentMount')
    Assert-Method 'Game.ComponentRider' 'StartNetMounting' 'System.Void' @('Game.ComponentMount')
    Assert-Method 'Game.ComponentRider' 'StartDismounting' 'System.Void' @()
    Assert-Method 'Game.ComponentRider' 'StartNetDismounting' 'System.Void' @()
    Assert-Method 'Game.ComponentRider' 'ScoreMount' 'System.Single' @('Game.ComponentMount', 'System.Single')

    Assert-Property 'Game.ComponentBody' 'ParentBody' 'Game.ComponentBody'
    Assert-Property 'Game.ComponentBody' 'ParentBodyPositionOffset' 'Engine.Vector3'
    Assert-Property 'Game.ComponentBody' 'ParentBodyRotationOffset' 'Engine.Quaternion'
    Assert-Property 'Game.ComponentBody' 'ChildBodies' 'Engine.ReadOnlyList`1[[Game.ComponentBody, Survivalcraft, Version=2.4.0.0, Culture=neutral, PublicKeyToken=null]]'
    Assert-Property 'Game.ComponentBody' 'Velocity' 'Engine.Vector3'

    Assert-Property 'Game.ComponentInput' 'PlayerInput' 'Game.PlayerInput'
    Assert-Property 'Game.ComponentPlayer' 'ComponentInput' 'Game.ComponentInput'
    Assert-Property 'Game.ComponentPlayer' 'ComponentRider' 'Game.ComponentRider'
    Assert-Property 'Game.ComponentPlayer' 'PlayerData' 'Game.PlayerData'
    Assert-Property 'Game.PlayerData' 'IsMainPlayer' 'System.Boolean'

    Assert-Method 'Game.SubsystemCreatureSpawn' 'SpawnCreature' 'GameEntitySystem.Entity' @('System.String', 'Engine.Vector3', 'System.Boolean')
    Assert-Method 'Game.DatabaseManager' 'CreateEntity' 'GameEntitySystem.Entity' @('GameEntitySystem.Project', 'System.String', 'System.Boolean') $true

    Assert-Constructor 'Game.NetWork.Packages.ComponentMountPackage' @('Game.ComponentRider', 'Game.ComponentMount', 'System.Boolean')
    Assert-Constructor 'Game.NetWork.Packages.ComponentMountPackage' @('Game.ComponentRider', 'System.Boolean')
    Assert-Constructor 'Game.NetWork.Packages.EntityPackage' @('GameEntitySystem.Entity')
    Assert-Constructor 'Game.NetWork.Packages.SubsystemBodyPackage' @('System.Collections.Generic.List`1[[Game.ComponentBody, Survivalcraft, Version=2.4.0.0, Culture=neutral, PublicKeyToken=null]]')

    Write-Host 'Speedboat ABI validation passed (x25.01.17 public Windows assembly surface).'
}
finally {
    [System.AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolveHandler)
}
