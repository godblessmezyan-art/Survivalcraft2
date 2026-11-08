param([Parameter(Mandatory=$true)][string]$GameDir,[Parameter(Mandatory=$true)][string]$AssemblyPath)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$mod=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$options=New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateInMemory=$true
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $file))}
foreach($file in @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll')){[void]$options.ReferencedAssemblies.Add($file)}
$sources=[string[]]@((Join-Path $root 'tests/SCIEPort.DevouringEye.Tests/NativeApiProbe.cs'),(Join-Path $root 'tests/SCIEPort.Home.NativeTests/HomeNativeProbe.cs'))
$compiled=$compiler.CompileAssemblyFromFile($options,$sources)
if($compiled.Errors.HasErrors){throw (($compiled.Errors | ForEach-Object ToString) -join "`n")}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.Xml.Linq
$bytes=$compiled.CompiledAssembly.GetType('DevouringNativeApiProbe').GetMethod('DecodeContent').Invoke($null,@(,[IO.File]::ReadAllBytes((Join-Path $GameDir 'Content.scpak'))))
$stream=[IO.MemoryStream]::new($bytes)
$zip=[IO.Compression.ZipArchive]::new($stream)
try{
    $reader=[IO.StreamReader]::new($zip.GetEntry('Assets/Database.xml').Open())
    try{$xml=[System.Xml.Linq.XElement]::Parse($reader.ReadToEnd())}finally{$reader.Dispose()}
    foreach($xdb in Get-ChildItem (Join-Path $root 'src/SCIEPort/Assets/Database') -Filter *.xdb){
        $source=$xdb.OpenRead();try{[ModsManager]::CombineDataBase($xml,$source)}finally{$source.Dispose()}
    }
    $compiled.CompiledAssembly.GetType('HomeNativeProbe').GetMethod('Run').Invoke($null,@($mod,$xml))
}catch{Write-Host $_.Exception.ToString();throw}finally{$zip.Dispose();$stream.Dispose()}
