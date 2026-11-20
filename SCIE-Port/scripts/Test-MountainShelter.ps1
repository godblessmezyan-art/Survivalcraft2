param(
    [Parameter(Mandatory=$true)][string]$GameDir,
    [Parameter(Mandatory=$true)][string]$AssemblyPath
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$csv = Import-Csv -LiteralPath (Join-Path $root 'src/SCIEPort/Assets/Blocks/MountainShelter.csv') -Delimiter ';' -Encoding UTF8
if ($csv.'Class Name' -ne 'MountainShelterBlock' -or $csv.DefaultCategory -ne 'SCIEPort.Structures' -or
    $csv.Behaviors -ne 'SCIEPortMountainShelter' -or $csv.DefaultCreativeData -ne '0' -or
    $csv.DefaultEmittedLightAmount -ne '12' -or $csv.SleepSuitability -ne '0') {
    throw 'Mountain shelter block registration or native lighting contract is invalid.'
}

foreach ($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe','LiteNetLib.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))
}
$assembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
[void][Reflection.Assembly]::LoadFrom($assembly)
$options = New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateInMemory = $true
foreach ($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe','LiteNetLib.dll')) {
    [void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $file))
}
foreach ($file in @('System.dll','System.Core.dll',$assembly)) { [void]$options.ReferencedAssemblies.Add($file) }
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
$compiled = $provider.CompileAssemblyFromFile($options, [string[]]@((Join-Path $root 'tests/SCIEPort.MountainShelter.NativeTests/MountainShelterProbe.cs')))
if ($compiled.Errors.HasErrors) { throw (($compiled.Errors | ForEach-Object ToString) -join "`n") }
try { $compiled.CompiledAssembly.GetType('MountainShelterProbe').GetMethod('Run').Invoke($null, @([int]$csv.DefaultEmittedLightAmount)) }
catch { Write-Host $_.Exception.ToString(); throw }
