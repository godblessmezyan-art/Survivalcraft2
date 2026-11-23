param([Parameter(Mandatory=$true)][string]$GameDir,[Parameter(Mandatory=$true)][string]$AssemblyPath)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$csvPath=Join-Path $root 'src/SCIEPort/Assets/Blocks/Scenery.csv'
$atlasPath=Join-Path $root 'src/SCIEPort/Assets/Textures/Scenery.png'
$rows=@(Import-Csv -LiteralPath $csvPath -Delimiter ';' -Encoding UTF8)
if($rows.Count -ne 6){throw "Scenery.csv must declare exactly six blocks, found $($rows.Count)."}
$expected=@(
    @{Name='SceneryCloudstoneBlock';Texture=5;Light=0},
    @{Name='SceneryHullBlock';Texture=0;Light=0},
    @{Name='SceneryEnvelopeBlock';Texture=2;Light=0},
    @{Name='SceneryCrystalBlock';Texture=6;Light=12},
    @{Name='SceneryBrassBlock';Texture=8;Light=0},
    @{Name='ScenerySailBlock';Texture=9;Light=0}
)
$byName=@{}
foreach($row in $rows){
    if($byName.ContainsKey($row.'Class Name')){throw "Duplicate scenery block class: $($row.'Class Name')"}
    $byName[$row.'Class Name']=$row
    if($row.DefaultCategory -ne 'SCIEPort.Structures'){throw "Scenery block must use the structures tab: $($row.'Class Name')"}
    if($row.IsPlaceable -ne 'TRUE' -or $row.IsCollidable -ne 'TRUE'){throw "Scenery block must be placeable and collidable: $($row.'Class Name')"}
    if($row.IsTransparent -ne 'FALSE' -or $row.GenerateFacesForSameNeighbors -ne 'FALSE'){throw "Scenery block must be an opaque cube: $($row.'Class Name')"}
    if($row.DefaultCreativeData -ne '0'){throw "Scenery block must default to DATA 0: $($row.'Class Name')"}
}
foreach($item in $expected){
    $row=$byName[$item.Name]
    if($null -eq $row){throw "Scenery.csv is missing $($item.Name)."}
    if([int]$row.DefaultTextureSlot -ne $item.Texture){throw "Atlas slot drift: $($item.Name)"}
    if([int]$row.DefaultEmittedLightAmount -ne $item.Light){throw "Emitted light drift: $($item.Name)"}
}
if(@($rows | ForEach-Object { $_.DisplayOrder } | Sort-Object -Unique).Count -ne 6){throw 'Scenery display orders must be unique.'}
Add-Type -AssemblyName System.Drawing
$image=$null
try{
    $image=[System.Drawing.Image]::FromFile($atlasPath)
    if($image.Width -ne 512 -or $image.Height -ne 512){throw "Scenery atlas must be 512x512, found $($image.Width)x$($image.Height)."}
}finally{
    if($null -ne $image){$image.Dispose()}
}
Write-Host "Scenery resource guard: 6 rows / 10 atlas tiles / 512x512 PNG."

$assembly=[IO.Path]::GetFullPath($AssemblyPath)
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$mod=[Reflection.Assembly]::LoadFrom($assembly)
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$options=New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateInMemory=$true
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $file))}
foreach($file in @('System.dll','System.Core.dll')){[void]$options.ReferencedAssemblies.Add($file)}
[void]$options.ReferencedAssemblies.Add($assembly)
$compiled=$compiler.CompileAssemblyFromFile($options,(Join-Path $root 'tests/SCIEPort.Scenery.NativeTests/SceneryNativeProbe.cs'))
if($compiled.Errors.HasErrors){throw (($compiled.Errors|ForEach-Object ToString)-join "`n")}
try{$compiled.CompiledAssembly.GetType('SceneryNativeProbe').GetMethod('Run').Invoke($null,@($mod))}catch{Write-Host $_.Exception.ToString();throw}
