param([Parameter(Mandatory=$true)][string]$GameDir,[Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$OutputDir,[switch]$Animation)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$OutputDir=[IO.Path]::GetFullPath($OutputDir)
$AssemblyPath=[IO.Path]::GetFullPath($AssemblyPath)
New-Item -ItemType Directory -Force $OutputDir | Out-Null
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$options=New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateExecutable=$true
$options.OutputAssembly=Join-Path $OutputDir 'VoidHorizonGpuProbe.exe'
foreach($file in @('System.dll','System.Core.dll')){[void]$options.ReferencedAssemblies.Add($file)}
[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir 'Engine.dll'))
$result=$compiler.CompileAssemblyFromFile($options,(Join-Path $root 'tests/SCIEPort.Abyss.Tests/VoidHorizonGpuProbe.cs'))
if($result.Errors.HasErrors){throw (($result.Errors|ForEach-Object ToString)-join "`n")}
# Isolated local graphics fixture, no game installation/save modifications.
$arguments=@('"'+$GameDir+'"','"'+$AssemblyPath+'"','"'+$OutputDir+'"')
if($Animation){$arguments+='animation'}
$process=Start-Process -FilePath $options.OutputAssembly -ArgumentList $arguments -WorkingDirectory $GameDir -WindowStyle Hidden -RedirectStandardOutput (Join-Path $OutputDir 'stdout.txt') -RedirectStandardError (Join-Path $OutputDir 'stderr.txt') -PassThru
$handle=$process.Handle
if(-not $process.WaitForExit(55000)){$process.Kill();throw 'Native graphics fixture timed out (55 seconds).'}
Get-Content (Join-Path $OutputDir 'stdout.txt')
if($process.ExitCode -ne 0){Get-Content (Join-Path $OutputDir 'stderr.txt');throw "Native graphics fixture failed: $($process.ExitCode)"}
