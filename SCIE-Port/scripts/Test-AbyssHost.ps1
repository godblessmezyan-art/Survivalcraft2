param([string]$GameDir,[string]$AssemblyPath)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$mod=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($AssemblyPath))
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$options=New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateInMemory=$true
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $file))}
foreach($file in @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll')){[void]$options.ReferencedAssemblies.Add($file)}
$sources=[string[]]@([IO.File]::ReadAllText((Join-Path $root 'tests/SCIEPort.Abyss.Tests/AbyssHostNativeProbe.cs')),[IO.File]::ReadAllText((Join-Path $root 'tests/SCIEPort.Abyss.Tests/PortalNetworkNativeProbe.cs')))
$result=$compiler.CompileAssemblyFromSource($options,$sources)
if($result.Errors.HasErrors){throw (($result.Errors|ForEach-Object ToString)-join "`n")}
try{$result.CompiledAssembly.GetType('AbyssHostNativeProbe',$true).GetMethod('Run').Invoke($null,@($mod));$result.CompiledAssembly.GetType('PortalNetworkNativeProbe',$true).GetMethod('Run').Invoke($null,@($mod))}
catch{Write-Host $_.Exception.ToString();throw}
