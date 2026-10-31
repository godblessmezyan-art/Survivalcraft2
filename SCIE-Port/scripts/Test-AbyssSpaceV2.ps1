param([Parameter(Mandatory=$true)][string]$GameDir,[string]$AssemblyPath,[string]$Package)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$protected=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'tests/SCIEPort.Abyss.Tests/space-v2-protected.json') | ConvertFrom-Json
foreach($file in $protected.files.PSObject.Properties){if((Get-FileHash -LiteralPath (Join-Path $root $file.Name)).Hash -ne $file.Value){throw ('V2 changed protected horizon/portal implementation: '+$file.Name)}}
$sources=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'design/abyss-space-v2/sources.json') | ConvertFrom-Json
foreach($entry in $sources.assets){foreach($key in @('original','runtime')){if((Get-FileHash -LiteralPath (Join-Path $root $entry.$key)).Hash -ne $entry.($key+'_sha256')){throw 'V2 generated art hash mismatch'}}}
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
foreach($entry in $sources.assets){$stream=[IO.File]::OpenRead((Join-Path $root $entry.runtime));try{$image=[Engine.Media.Image]::Load($stream)}finally{$stream.Dispose()};if($image.Width -ne 256 -or $image.Height -ne 256 -or $image.GetPixel(0,0).A -gt 8){throw 'V2 icon decode/size/transparency failed'}}
if($Package){
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $archive=[IO.Compression.ZipFile]::OpenRead($Package)
 try{foreach($entry in $sources.assets){$name=$entry.runtime.Substring('src/SCIEPort/'.Length);$file=$archive.GetEntry($name);if(!$file){throw 'V2 icon missing from package'};$stream=$file.Open();$sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()};if($hash -ne $entry.runtime_sha256){throw 'V2 packaged art hash mismatch'}}}finally{$archive.Dispose()}
}
Write-Host 'Space V2: protected 4.2s horizon/portal sources unchanged; three native PNG imagegen assets and provenance hashes verified.'
if(!$AssemblyPath){return}
$mod=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($AssemblyPath))
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$options=New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateInMemory=$true
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $file))}
foreach($file in @('System.dll','System.Core.dll')){[void]$options.ReferencedAssemblies.Add($file)}
$files=[string[]]@((Join-Path $root 'tests/SCIEPort.Abyss.Tests/AbyssHostNativeProbe.cs'),(Join-Path $root 'tests/SCIEPort.Abyss.Tests/VoidSpaceNativeProbe.cs'))
$result=$compiler.CompileAssemblyFromFile($options,$files)
if($result.Errors.HasErrors){throw (($result.Errors|ForEach-Object ToString)-join "`n")}
try{$result.CompiledAssembly.GetType('VoidSpaceNativeProbe',$true).GetMethod('Run').Invoke($null,@($mod))}catch{Write-Host $_.Exception.ToString();throw}
