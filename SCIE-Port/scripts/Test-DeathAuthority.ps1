param([Parameter(Mandatory=$true)][string]$GameDir,[string]$AssemblyPath,[string]$Package)
$ErrorActionPreference='Stop';$root=Split-Path $PSScriptRoot
$sources=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'design/abyss-death/sources.json')|ConvertFrom-Json
foreach($a in $sources.assets){foreach($key in @('original','runtime')){if((Get-FileHash -LiteralPath (Join-Path $root $a.$key)).Hash -ne $a.($key+'_sha256')){throw 'Death imagegen provenance mismatch'}}}
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $f))}
foreach($a in $sources.assets){$s=[IO.File]::OpenRead((Join-Path $root $a.runtime));try{$image=[Engine.Media.Image]::Load($s)}finally{$s.Dispose()};if($image.Width -ne 256 -or $image.Height -ne 256 -or $image.GetPixel(0,0).A -gt 8){throw 'Death icon size/alpha invalid'}}
if($Package){Add-Type -AssemblyName System.IO.Compression.FileSystem;$z=[IO.Compression.ZipFile]::OpenRead($Package);try{foreach($a in $sources.assets){$entry=$z.GetEntry($a.runtime.Substring('src/SCIEPort/'.Length));if(!$entry){throw 'Death icon absent from package'};$s=$entry.Open();$hash=[Security.Cryptography.SHA256]::Create();try{$value=[BitConverter]::ToString($hash.ComputeHash($s)).Replace('-','')}finally{$s.Dispose();$hash.Dispose()};if($value -ne $a.runtime_sha256){throw 'Death packaged icon mismatch'}}}finally{$z.Dispose()}}
Write-Host 'Death authority: four built-in imagegen originals/runtime/native alpha/package hashes verified.'
if(!$AssemblyPath){return}
$mod=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($AssemblyPath));$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider;$options=New-Object System.CodeDom.Compiler.CompilerParameters;$options.GenerateInMemory=$true
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $f))};foreach($f in @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll')){[void]$options.ReferencedAssemblies.Add($f)}
$files=[string[]]@((Join-Path $root 'tests/SCIEPort.Abyss.Tests/AbyssHostNativeProbe.cs'),(Join-Path $root 'tests/SCIEPort.Abyss.Tests/VoidSpaceNativeProbe.cs'),(Join-Path $root 'tests/SCIEPort.Abyss.Tests/DeathAuthorityNativeProbe.cs'),(Join-Path $root 'tests/SCIEPort.DevouringEye.Tests/NativeApiProbe.cs'))
$result=$compiler.CompileAssemblyFromFile($options,$files);if($result.Errors.HasErrors){throw (($result.Errors|ForEach-Object ToString)-join "`n")}
$decoder=$result.CompiledAssembly.GetType('DevouringNativeApiProbe',$true);$decoded=$decoder.GetMethod('DecodeContent').Invoke($null,@(,[IO.File]::ReadAllBytes((Join-Path $GameDir 'Content.scpak'))));Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.Xml.Linq
$memory=New-Object IO.MemoryStream(,$decoded);$zip=New-Object IO.Compression.ZipArchive($memory)
try{$reader=[IO.StreamReader]::new($zip.GetEntry('Assets/Database.xml').Open());try{$xml=[System.Xml.Linq.XElement]::Parse($reader.ReadToEnd())}finally{$reader.Dispose()};$result.CompiledAssembly.GetType('DeathAuthorityNativeProbe',$true).GetMethod('Run').Invoke($null,@($mod,$xml))}catch{Write-Host $_.Exception.ToString();throw}finally{$zip.Dispose();$memory.Dispose()}
