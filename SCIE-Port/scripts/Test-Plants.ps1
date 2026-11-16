param([Parameter(Mandatory=$true)][string]$GameDir,[Parameter(Mandatory=$true)][string]$AssemblyPath)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$mod=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$options=New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateInMemory=$true
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $file))}
foreach($file in @('System.dll','System.Core.dll')){[void]$options.ReferencedAssemblies.Add($file)}
$compiled=$compiler.CompileAssemblyFromFile($options,(Join-Path $root 'tests/SCIEPort.Plants.NativeTests/PlantNativeProbe.cs'))
if($compiled.Errors.HasErrors){throw (($compiled.Errors|ForEach-Object ToString)-join "`n")}
try{$compiled.CompiledAssembly.GetType('PlantNativeProbe').GetMethod('Run').Invoke($null,@($mod))}catch{Write-Host $_.Exception.ToString();throw}
