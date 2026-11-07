param(
    [Parameter(Mandatory=$true)][string]$GameDir,
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$OutputDir
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$OutputDir = [IO.Path]::GetFullPath($OutputDir)
$AssemblyPath = [IO.Path]::GetFullPath($AssemblyPath)
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$compiler = New-Object Microsoft.CSharp.CSharpCodeProvider
$options = New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateExecutable = $true
$options.OutputAssembly = Join-Path $OutputDir 'PixelFxGpuProbe.exe'
foreach ($file in @('System.dll','System.Core.dll')) { [void]$options.ReferencedAssemblies.Add($file) }
foreach ($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')) { [void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $file)) }
$result = $compiler.CompileAssemblyFromFile($options,(Join-Path $root 'tests/SCIEPort.PixelFx.Tests/PixelFxGpuProbe.cs'))
if ($result.Errors.HasErrors) { throw (($result.Errors | ForEach-Object ToString) -join "`n") }
$probeArguments = @('"'+$GameDir+'"','"'+$AssemblyPath+'"','"'+(Join-Path $root 'src/SCIEPort/Assets')+'"','"'+$OutputDir+'"')
$process = Start-Process -FilePath $options.OutputAssembly -ArgumentList $probeArguments -WorkingDirectory $GameDir -WindowStyle Hidden -RedirectStandardOutput (Join-Path $OutputDir 'stdout.txt') -RedirectStandardError (Join-Path $OutputDir 'stderr.txt') -PassThru
$handle = $process.Handle
if (!$process.WaitForExit(55000)) { $process.Kill(); throw 'PixelFX GPU fixture timed out.' }
Get-Content (Join-Path $OutputDir 'stdout.txt')
if ($process.ExitCode -ne 0) { Get-Content (Join-Path $OutputDir 'stderr.txt'); throw "PixelFX GPU fixture failed: $($process.ExitCode)" }
