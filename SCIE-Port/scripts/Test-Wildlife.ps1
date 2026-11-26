param([Parameter(Mandatory=$true)][string]$GameDir,[string]$AssemblyPath,[string]$Package)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe','LiteNetLib.dll')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$assets=Join-Path $root 'src/SCIEPort/Assets'
$all=@{}
foreach($t in [Game.Block].Assembly.GetTypes()) {if($t.IsSubclassOf([Game.Block])){$f=$t.GetField('Index');if($f -and $f.IsLiteral){$all[[int]$f.GetRawConstantValue()]=$t.FullName}}}
if($all.ContainsKey(1021)){throw 'Cat ID 1021 conflicts with native target'}
$models=@{Fox=6;Rabbit=6;Chicken=7;Cat=6}
foreach($name in $models.Keys){
    $path=Join-Path $assets ('Models/'+$name+'.dae')
    $stream=[IO.File]::OpenRead($path)
    try{$model=[Engine.Media.ModelData]::Load($stream)}finally{$stream.Dispose()}
    if($model.Bones.Count -ne $models[$name] -or $model.Meshes.Count -ne $models[$name]){throw "Native model shape failed: $name"}
    $required=if($name -eq 'Chicken'){@('Body','Neck','Head','Leg1','Leg2','Wing1','Wing2')}else{@('Body','Head','Leg1','Leg2','Leg3','Leg4')}
    foreach($bone in $required){if($model.Bones.Name -notcontains $bone){throw "Missing native bone $name/$bone"}}
    [xml]$dae=Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $triangles=0;foreach($tri in $dae.SelectNodes("//*[local-name()='triangles']")){$triangles += [int]$tri.count}
    if($triangles -lt 400 -or $triangles -gt 1000){throw "Wildlife triangle budget failed: $name/$triangles"}
    Write-Host "Wildlife native import: $name $($model.Bones.Count) bones/$($model.Meshes.Count) meshes/$triangles triangles"
}
$arthropods=@{SandScorpion=14;FiddlerCrab=11}
foreach($name in $arthropods.Keys){
    $path=Join-Path $assets ('Models/'+$name+'.dae')
    $stream=[IO.File]::OpenRead($path)
    try{$model=[Engine.Media.ModelData]::Load($stream)}finally{$stream.Dispose()}
    if($model.Bones.Count -ne $arthropods[$name] -or $model.Meshes.Count -ne $arthropods[$name]){
        throw "Arthropod bones/native meshes failed: $name"
    }
    foreach($side in @('L','R')){
        if($model.Bones.Name -notcontains "Claw$side"){throw "Missing $name claw $side"}
        foreach($i in 1..4){if($model.Bones.Name -notcontains "Leg$side$i"){throw "Missing $name leg $side$i"}}
    }
    if($name -eq 'SandScorpion'){
        foreach($i in 1..3){if($model.Bones.Name -notcontains "Tail$i"){throw "Missing scorpion tail bone $i"}}
    }
    [xml]$dae=Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $triangles=($dae.SelectNodes("//*[local-name()='triangles']") | ForEach-Object {[int]$_.count} | Measure-Object -Sum).Sum
    if($triangles -gt 1450 -or $triangles -lt 500){throw "Arthropod triangle budget failed: $name/$triangles"}
    Write-Host "Arthropod native import: $name $($model.Bones.Count) bones/$($model.Meshes.Count) meshes/$triangles triangles"
}
foreach($id in 887..889){if($all.ContainsKey($id)){throw "Arthropod block ID $id conflicts with native target"}}
$rows=@(Import-Csv -LiteralPath (Join-Path $assets 'Blocks/Creatures.csv') -Delimiter ';' -Encoding UTF8)
foreach($name in $models.Keys){$row=@($rows|Where-Object {$_.'Class Name' -eq ($name+'SpawnBlock')});if($row.Count -ne 1 -or $row[0].DefaultCategory -ne 'SCIEPort.Nature' -or $row[0].Behaviors -ne ('SCIEPort'+$name+'SpawnBehavior')){throw "Creative item mapping failed $name"}}
Add-Type -AssemblyName System.Drawing
$image=[Drawing.Bitmap]::FromFile((Join-Path $assets 'Textures/WildlifePalette.png'))
try{if($image.Width -ne 128 -or $image.Height -ne 8){throw 'Wildlife atlas dimensions changed'};for($y=0;$y -lt 8;$y++){for($x=0;$x -lt 128;$x++){if($image.GetPixel($x,$y).A -ne 255){throw 'Wildlife atlas must be opaque'}}}}finally{$image.Dispose()}
if($AssemblyPath){
    $mod=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath))
    $compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
    $options=New-Object System.CodeDom.Compiler.CompilerParameters;$options.GenerateInMemory=$true
    foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe','LiteNetLib.dll')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $file))}
    foreach($file in @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll')){[void]$options.ReferencedAssemblies.Add($file)}
    $sources=[string[]]@((Join-Path $root 'tests/SCIEPort.DevouringEye.Tests/NativeApiProbe.cs'),(Join-Path $root 'tests/SCIEPort.Ecology.Tests/WildlifeNativeProbe.cs'))
    $result=$compiler.CompileAssemblyFromFile($options,$sources)
    if($result.Errors.HasErrors){throw (($result.Errors|ForEach-Object ToString)-join "`n")}
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.Xml.Linq
    $bytes=$result.CompiledAssembly.GetType('DevouringNativeApiProbe').GetMethod('DecodeContent').Invoke($null,@(,[IO.File]::ReadAllBytes((Join-Path $GameDir 'Content.scpak'))))
    $stream=[IO.MemoryStream]::new($bytes);$zip=[IO.Compression.ZipArchive]::new($stream)
    try{
        $reader=[IO.StreamReader]::new($zip.GetEntry('Assets/Database.xml').Open());try{$xml=[System.Xml.Linq.XElement]::Parse($reader.ReadToEnd())}finally{$reader.Dispose()}
        foreach($xdb in Get-ChildItem (Join-Path $assets 'Database') -Filter *.xdb){$s=$xdb.OpenRead();try{[ModsManager]::CombineDataBase($xml,$s)}finally{$s.Dispose()}}
        $checks=$result.CompiledAssembly.GetType('WildlifeNativeProbe').GetMethod('Run').Invoke($null,@($xml,$mod));Write-Host "Wildlife effective native database/food/egg/authority checks: $checks"
    }finally{$zip.Dispose();$stream.Dispose()}
}
if($Package){
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Package))
    try{
        foreach($name in @($models.Keys) + @($arthropods.Keys)){
            if(-not $zip.GetEntry('Assets/Models/'+$name+'.dae')){throw "Packaged wildlife missing $name"}
        }
        foreach($entry in @('Assets/Textures/WildlifePalette.png','Assets/Textures/WildArthropods.png','Assets/Database/WildArthropods.xdb')){
            if(-not $zip.GetEntry($entry)){throw "Packaged wildlife missing $entry"}
        }
    }finally{$zip.Dispose()}
}
