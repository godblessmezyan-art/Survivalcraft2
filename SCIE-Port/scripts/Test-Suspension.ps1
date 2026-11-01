param([Parameter(Mandatory=$true)][string]$GameDir,[Parameter(Mandatory=$true)][string]$AssemblyPath,[string]$Package,[switch]$PackageOnly)
$ErrorActionPreference='Stop';$root=Split-Path $PSScriptRoot
$rows=@(Import-Csv (Join-Path $root 'src/SCIEPort/Assets/Blocks/SuspendedItem.csv') -Delimiter ';' -Encoding UTF8)
if($rows.Count -ne 1 -or $rows[0].'Class Name' -ne 'SuspendedItemBlock' -or $rows[0].DefaultCategory -ne 'SCIEPort.Building' -or $rows[0].IsCollidable -ne 'FALSE' -or $rows[0].IsTransparent -ne 'TRUE' -or $rows[0].LightAttenuation -ne '0' -or $rows[0].DefaultShadowStrength -ne '0' -or $rows[0].DefaultDropContent -ne '954'){throw 'Suspension category/transparent/non-collidable/drop contract changed'}
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $f))}
$mod=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
if(!$PackageOnly){
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider;$options=New-Object System.CodeDom.Compiler.CompilerParameters;$options.GenerateInMemory=$true
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $f))}
foreach($f in @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll')){[void]$options.ReferencedAssemblies.Add($f)}
$sources=[string[]]@((Join-Path $root 'tests/SCIEPort.DevouringEye.Tests/NativeApiProbe.cs'),(Join-Path $root 'tests/SCIEPort.Suspension.Tests/SuspensionNativeProbe.cs'))
$compiled=$compiler.CompileAssemblyFromFile($options,$sources);if($compiled.Errors.HasErrors){throw (($compiled.Errors|ForEach-Object ToString)-join "`n")}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.Xml.Linq
$bytes=$compiled.CompiledAssembly.GetType('DevouringNativeApiProbe').GetMethod('DecodeContent').Invoke($null,@(,[IO.File]::ReadAllBytes((Join-Path $GameDir 'Content.scpak'))))
$stream=[IO.MemoryStream]::new($bytes);$zip=[IO.Compression.ZipArchive]::new($stream)
try{
 $reader=[IO.StreamReader]::new($zip.GetEntry('Assets/Database.xml').Open());try{$xml=[System.Xml.Linq.XElement]::Parse($reader.ReadToEnd())}finally{$reader.Dispose()}
 $uiReader=[IO.StreamReader]::new($zip.GetEntry('Assets/Widgets/ChestWidget.xml').Open());try{$nativeUi=[System.Xml.Linq.XElement]::Parse($uiReader.ReadToEnd())}finally{$uiReader.Dispose()}
 $nativeLabels=@($nativeUi.Descendants() | Where-Object {$_.Name.LocalName -eq 'LabelWidget'} | ForEach-Object {$_.Attribute('Text').Value})
 if($nativeLabels[0] -ne '[ChestWidget:1]'){throw 'Native chest title layout changed; review the owned display widget'}
 foreach($name in @('ChestGrid','InventoryGrid')){if(@($nativeUi.Descendants()|Where-Object {$_.Name.LocalName -eq 'GridPanelWidget' -and $_.Attribute('Name').Value -eq $name}).Count -ne 1){throw "Native UI grid missing: $name"}}
 Write-Host 'Native chest modal title/grid bindings verified; touch and screen-size behavior still require device testing.'
 foreach($xdb in Get-ChildItem (Join-Path $root 'src/SCIEPort/Assets/Database') -Filter *.xdb){$source=$xdb.OpenRead();try{[ModsManager]::CombineDataBase($xml,$source)}finally{$source.Dispose()}}
 $probe=$compiled.CompiledAssembly.GetType('SuspensionNativeProbe');$probe.GetMethod('Run').Invoke($null,@($mod,$xml))
}catch{Write-Host $_.Exception.ToString();throw}finally{$zip.Dispose();$stream.Dispose()}
}
$source=Get-ChildItem (Join-Path $root 'src/SCIEPort/Features/Suspension') -Filter *.cs|ForEach-Object{[IO.File]::ReadAllText($_.FullName)}
if(($source -join "`n") -match '\b(ChangeCell|DestroyCell|SetCellValueFast|AttackBody|Injure)\s*\('){throw 'Display must not modify terrain or damage entities'}
if($Package){
 Add-Type -AssemblyName System.IO.Compression.FileSystem;$archive=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Package))
 try{foreach($name in @('Assets/Blocks/SuspendedItem.csv','Assets/Database/SuspendedItem.xdb','SCIEPort.dll')){
   $entry=$archive.GetEntry($name);if(!$entry){throw "Missing $name"}
   $s=$entry.Open();$ms=[IO.MemoryStream]::new();try{$s.CopyTo($ms);$actual=$ms.ToArray()}finally{$s.Dispose();$ms.Dispose()}
   $sourcePath=if($name -eq 'SCIEPort.dll'){$AssemblyPath}else{Join-Path $root ('src/SCIEPort/'+$name)}
   if([Convert]::ToBase64String($actual) -ne [Convert]::ToBase64String([IO.File]::ReadAllBytes([IO.Path]::GetFullPath($sourcePath)))){throw "Packaged bytes differ: $name"}
 }}finally{$archive.Dispose()}
 Write-Host 'Suspension package source/DLL bytes verified.'
}
