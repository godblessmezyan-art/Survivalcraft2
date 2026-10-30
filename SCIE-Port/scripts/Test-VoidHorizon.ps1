param([string]$GameDir,[string]$AssemblyPath)
$ErrorActionPreference='Stop'
# Retains the existing native input/persistence checks and shares its IL decoder.
. (Join-Path $PSScriptRoot 'Test-AbyssRuntime.ps1') -GameDir $GameDir -AssemblyPath $AssemblyPath
$flags=[Reflection.BindingFlags]'Instance,NonPublic,Public'
$injure=$g.GetType('Game.ComponentHealth').GetMethod('Injure')
if([AbyssIL]::Calls($injure) -notcontains 'HookAction'){throw 'Native injury hook removed.'}
$healthPacket=$g.GetType('Game.NetWork.Packages.ComponentHealthPackage',$true)
# FindEntityById uses compiler-generated callbacks; follow their actual IL as well.
$pendingMethods=New-Object 'System.Collections.Generic.Queue[System.Reflection.MethodBase]'
$seenMethods=New-Object 'System.Collections.Generic.HashSet[int]'
$pendingMethods.Enqueue($healthPacket.GetMethod('Handle'))
$foundInjure=$false
while($pendingMethods.Count -gt 0) {
 $method=$pendingMethods.Dequeue()
 if(-not $seenMethods.Add($method.MetadataToken)){continue}
 foreach($call in [AbyssIL]::Methods($method)) {
  if($call.DeclaringType.FullName -eq 'Game.ComponentHealth' -and $call.Name -eq 'Injure'){$foundInjure=$true}
  if($call.DeclaringType.FullName.StartsWith($healthPacket.FullName)){$pendingMethods.Enqueue($call)}
 }
}
if(-not $foundInjure){throw 'Native damage requests must pass through Injure, including entity callbacks.'}
$domain=$mod.GetType('SCIEPort.Features.AbyssReaper.VoidHorizonRuntime',$true)
$protection=$loader.GetMethod('DomainProtected',[Reflection.BindingFlags]'NonPublic,Static')
if(!$protection -or [AbyssIL]::Calls($protection) -notcontains 'Protects'){throw 'Loader must use actual domain lifetime protection.'}
foreach($name in @('OnCreatureInjure','AttackBody','AttackPowerParameter')) {
 if([AbyssIL]::Calls($loader.GetMethod($name)) -notcontains 'DomainProtected'){throw "Missing native damage coverage: $name"}
}
if([AbyssIL]::Calls($domain.GetMethod('ProcessSettlements',$flags)) -notcontains 'Target'){throw 'Annihilation must enforce target protection.'}
$late=2147483631
if($instance.DrawOrders -notcontains $late){throw 'Missing complete-domain late viewport pass.'}
# Do not instantiate native drawables: some static initializers compile GPU shaders.
# The audited ScreenOverlays pass is 1101; inspect ABI without creating a graphics context.
foreach($name in @('Game.ComponentScreenOverlays','Game.SubsystemModelsRenderer','Game.SubsystemTerrain')) {
 if(-not $g.GetType($name,$true).GetProperty('DrawOrders')){throw "Native drawing API missing: $name"}
}
Write-Host 'Horizon ABI: native injury callbacks use Injure; all three damage hooks protect caster; complete-domain late pass registered (no native renderer instantiated).'
$root=Split-Path $PSScriptRoot
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$options=New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateInMemory=$true
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $file))}
foreach($file in @('System.dll','System.Core.dll')){[void]$options.ReferencedAssemblies.Add($file)}
$sources=[string[]]@([IO.File]::ReadAllText((Join-Path $root 'tests/SCIEPort.Abyss.Tests/VoidHorizonNativeProbe.cs')),[IO.File]::ReadAllText((Join-Path $root 'tests/SCIEPort.Abyss.Tests/VoidAnnihilationNativeProbe.cs')))
$result=$compiler.CompileAssemblyFromSource($options,$sources)
if($result.Errors.HasErrors){throw (($result.Errors|ForEach-Object ToString)-join "`n")}
try { $result.CompiledAssembly.GetType('VoidHorizonNativeProbe',$true).GetMethod('Run').Invoke($null,@($mod));$result.CompiledAssembly.GetType('VoidAnnihilationNativeProbe',$true).GetMethod('Run').Invoke($null,@($mod)) }
catch { Write-Host $_.Exception.ToString(); throw }
