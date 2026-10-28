[CmdletBinding()]
param(
    [string]$GameDir = $(if ($env:SCNET_GAME_DIR) { $env:SCNET_GAME_DIR } else { 'E:\SCNET\SCNETx25.01.17' })
)
$ErrorActionPreference = 'Stop'
$resolveHandler = [System.ResolveEventHandler] {
    param($sender, $eventArgs)
    $name = ([System.Reflection.AssemblyName]::new($eventArgs.Name)).Name
    $candidate = Join-Path $GameDir ($name + '.dll')
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        return [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($candidate)
    }
    return $null
}
[System.AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolveHandler)
try {
    $assembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
    $block = $assembly.GetType('Game.FurnitureBlock', $true)
    if ($block.GetField('Index').GetRawConstantValue() -ne 227) { throw 'Unexpected furniture block Index.' }
    $signatures = @(
        @('Game.FurnitureBlock', 'GetDesignIndex', 'System.Int32', 'System.Int32'),
        @('Game.FurnitureBlock', 'SetDesignIndex', 'System.Int32', 'System.Int32,System.Int32,System.Int32,System.Boolean'),
        @('Game.FurnitureBlock', 'GetRotation', 'System.Int32', 'System.Int32'),
        @('Game.FurnitureDesign', 'SetValues', 'System.Void', 'System.Int32,System.Int32[]'),
        @('Game.FurnitureDesign', 'Save', 'TemplatesDatabase.ValuesDictionary', ''),
        @('Game.SubsystemFurnitureBlockBehavior', 'GetDesign', 'Game.FurnitureDesign', 'System.Int32'),
        @('Game.SubsystemFurnitureBlockBehavior', 'TryAddDesign', 'Game.FurnitureDesign', 'Game.FurnitureDesign'),
        @('Game.SubsystemFurnitureBlockBehavior', 'GarbageCollectDesigns', 'System.Void', '')
    )
    foreach ($signature in $signatures) {
        $type = $assembly.GetType($signature[0], $true)
        $matches = @($type.GetMethods() | Where-Object {
            $_.Name -eq $signature[1] -and $_.ReturnType.FullName -eq $signature[2] -and
            (($_.GetParameters() | ForEach-Object { $_.ParameterType.FullName }) -join ',') -eq $signature[3]
        })
        if ($matches.Count -ne 1) { throw "Furniture ABI mismatch: $($signature -join ' / ')" }
    }
    $designs = $assembly.GetType('Game.SubsystemFurnitureBlockBehavior', $true).GetField('m_furnitureDesigns')
    if ($designs.FieldType.FullName -ne 'Game.FurnitureDesign[]') { throw 'Furniture world design table missing.' }
    foreach ($name in @('Resolution', 'Index', 'LinkedDesign', 'InteractionMode')) {
        if ($null -eq $assembly.GetType('Game.FurnitureDesign', $true).GetProperty($name)) { throw "Missing FurnitureDesign.$name" }
    }
    Write-Host "Furniture ABI confirmed against SHA-256 $((Get-FileHash -LiteralPath (Join-Path $GameDir 'Survivalcraft.exe') -Algorithm SHA256).Hash)"
    Write-Host 'Read-only metadata check; native placement, persistence and multiplayer remain unverified.'
}
finally {
    [System.AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolveHandler)
}
