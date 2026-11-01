param([Parameter(Mandatory=$true)][string]$GameDir,[Parameter(Mandatory=$true)][string]$AssemblyPath,[string]$Package,[switch]$PackageOnly)
$ErrorActionPreference='Stop';$root=Split-Path $PSScriptRoot
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $f))}
$mod=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$registry=$mod.GetType('SCIEPort.Features.Codex.CodexRules',$true).GetField('Items',[Reflection.BindingFlags]'Static,NonPublic').GetValue($null)
foreach($csv in Get-ChildItem (Join-Path $root 'src/SCIEPort/Assets/Blocks') -Filter *.csv){foreach($row in Import-Csv -Delimiter ';' -Encoding UTF8 $csv.FullName){if($row.DefaultCategory -eq 'SCIEPort.Artifacts' -and $row.'Class Name' -ne 'ArtifactCodexBlock'){$type=@($mod.GetTypes()|Where-Object Name -eq $row.'Class Name')[0];$id=$type.GetField('Index').GetRawConstantValue();if($id -notin $registry){throw ('Artifact missing from true-name catalog: '+$row.'Class Name')}}}}

if(!$PackageOnly){
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider;$options=New-Object System.CodeDom.Compiler.CompilerParameters;$options.GenerateInMemory=$true
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $f))}
foreach($f in @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll')){[void]$options.ReferencedAssemblies.Add($f)}
$sources=[string[]]@((Join-Path $root 'tests/SCIEPort.Abyss.Tests/AbyssHostNativeProbe.cs'),(Join-Path $root 'tests/SCIEPort.DevouringEye.Tests/NativeApiProbe.cs'),(Join-Path $root 'tests/SCIEPort.Codex.Tests/CodexNativeProbe.cs'),(Join-Path $root 'tests/SCIEPort.Codex.Tests/GuidCompatibilityProbe.cs'),(Join-Path $root 'tests/SCIEPort.Codex.Tests/LegacyParsingProbe.cs'))
$compiled=$compiler.CompileAssemblyFromFile($options,$sources);if($compiled.Errors.HasErrors){throw (($compiled.Errors|ForEach-Object ToString)-join "`n")}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.Xml.Linq
$bytes=$compiled.CompiledAssembly.GetType('DevouringNativeApiProbe').GetMethod('DecodeContent').Invoke($null,@(,[IO.File]::ReadAllBytes((Join-Path $GameDir 'Content.scpak'))))
$stream=[IO.MemoryStream]::new($bytes);$zip=[IO.Compression.ZipArchive]::new($stream)
try{
 $reader=[IO.StreamReader]::new($zip.GetEntry('Assets/Database.xml').Open());try{$xml=[System.Xml.Linq.XElement]::Parse($reader.ReadToEnd())}finally{$reader.Dispose()}
 foreach($xdb in Get-ChildItem (Join-Path $root 'src/SCIEPort/Assets/Database') -Filter *.xdb){$source=$xdb.OpenRead();try{[ModsManager]::CombineDataBase($xml,$source)}finally{$source.Dispose()}}
 $compiled.CompiledAssembly.GetType('GuidCompatibilityProbe').GetMethod('Run').Invoke($null,@($mod))
 $compiled.CompiledAssembly.GetType('LegacyParsingProbe').GetMethod('Run').Invoke($null,@($mod))
 $probe=$compiled.CompiledAssembly.GetType('CodexNativeProbe');$probe.GetMethod('Run').Invoke($null,@($mod,$xml))
}catch{Write-Host $_.Exception.ToString();throw}finally{$zip.Dispose();$stream.Dispose()}
}
$source=Get-ChildItem (Join-Path $root 'src/SCIEPort/Features/Codex') -Filter *.cs|ForEach-Object{[IO.File]::ReadAllText($_.FullName)}
if(($source -join "`n") -match '\b(ChangeCell|DestroyCell|SetCellValueFast|AttackBody|Injure)\s*\('){throw 'Display must not modify terrain or damage entities'}
if($Package){
 Add-Type -AssemblyName System.IO.Compression.FileSystem;$archive=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Package))
 try{foreach($name in @('Assets/Blocks/ArtifactCodex.csv','Assets/Database/ArtifactCodex.xdb','SCIEPort.dll')){
   $entry=$archive.GetEntry($name);if(!$entry){throw "Missing $name"}
   $s=$entry.Open();$ms=[IO.MemoryStream]::new();try{$s.CopyTo($ms);$actual=$ms.ToArray()}finally{$s.Dispose();$ms.Dispose()}
   $sourcePath=if($name -eq 'SCIEPort.dll'){$AssemblyPath}else{Join-Path $root ('src/SCIEPort/'+$name)}
   if([Convert]::ToBase64String($actual) -ne [Convert]::ToBase64String([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($sourcePath)))){throw "Packaged bytes differ: $name"}
 }}finally{$archive.Dispose()}
 Write-Host 'Codex package source/DLL bytes verified.'
}
