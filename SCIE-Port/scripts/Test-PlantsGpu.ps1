param([Parameter(Mandatory=$true)][string]$GameDir,[Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$OutputDir)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$AssemblyPath=[IO.Path]::GetFullPath($AssemblyPath);$OutputDir=[IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force $OutputDir|Out-Null
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$options=New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateExecutable=$true;$options.OutputAssembly=Join-Path $OutputDir 'PlantGpuProbe.exe'
foreach($f in @('System.dll','System.Core.dll')){[void]$options.ReferencedAssemblies.Add($f)}
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $f))}
$result=$compiler.CompileAssemblyFromFile($options,(Join-Path $root 'tests/SCIEPort.Plants.NativeTests/PlantGpuProbe.cs'))
if($result.Errors.HasErrors){throw (($result.Errors|ForEach-Object ToString)-join "`n")}
$atlas=Join-Path $root 'src/SCIEPort/Assets/Textures/Apple.png'
$treeAtlas=Join-Path $root 'src/SCIEPort/Assets/Textures/SpecialTrees.png'
$bambooAtlas=Join-Path $root 'src/SCIEPort/Assets/Textures/Bamboo.png'
$arguments=@('"'+$GameDir+'"','"'+$AssemblyPath+'"','"'+$OutputDir+'"','"'+$atlas+'"','"'+$treeAtlas+'"','"'+$bambooAtlas+'"')
$process=Start-Process -FilePath $options.OutputAssembly -ArgumentList $arguments -WorkingDirectory $GameDir -WindowStyle Hidden -RedirectStandardOutput (Join-Path $OutputDir 'stdout.txt') -RedirectStandardError (Join-Path $OutputDir 'stderr.txt') -PassThru
$handle=$process.Handle
if(!$process.WaitForExit(55000)){$process.Kill();throw 'Plants GPU probe timed out'}
Get-Content (Join-Path $OutputDir 'stdout.txt')
if($process.ExitCode -ne 0){Get-Content (Join-Path $OutputDir 'stderr.txt');throw "Plants GPU fixture failed: $($process.ExitCode)"}
