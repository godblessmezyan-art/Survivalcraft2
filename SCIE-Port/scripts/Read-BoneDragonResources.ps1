# Read-only resource/ABI import. No entities, GPU, world or game session.
param([string]$GameDir='D:/Games/SCNET-x25.01.17',[string]$ModAssembly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$game=$GameDir
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $game $f))}
Add-Type -AssemblyName System.Xml.Linq
Add-Type -AssemblyName System.IO.Compression
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$options=New-Object System.CodeDom.Compiler.CompilerParameters
$options.GenerateInMemory=$true
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $game $f))}
foreach($f in @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll')){[void]$options.ReferencedAssemblies.Add($f)}
$result=$compiler.CompileAssemblyFromFile($options,[string[]]@((Join-Path $root 'tests/SCIEPort.DevouringEye.Tests/NativeApiProbe.cs')))
if($result.Errors.HasErrors){throw (($result.Errors|ForEach-Object ToString)-join "`n")}
$bytes=$result.CompiledAssembly.GetType('DevouringNativeApiProbe').GetMethod('DecodeContent').Invoke($null,@(,[IO.File]::ReadAllBytes((Join-Path $game 'Content.scpak'))))
$stream=New-Object IO.MemoryStream(,$bytes);$zip=New-Object IO.Compression.ZipArchive($stream)
try {
    $reader=[IO.StreamReader]::new($zip.GetEntry('Assets/Database.xml').Open())
    try{$xml=[System.Xml.Linq.XElement]::Parse($reader.ReadToEnd())}finally{$reader.Dispose()}
    $xdb=[IO.File]::OpenRead((Join-Path $root 'src/SCIEPort/Assets/Database/AbyssBoneDragon.xdb'))
    try{[ModsManager]::CombineDataBase($xml,$xdb)}finally{$xdb.Dispose()}
    $db=[TemplatesDatabase.XmlDatabaseSerializer]::LoadDatabase($xml);$gd=[GameEntitySystem.GameDatabase]::new($db)
    $v=[TemplatesDatabase.ValuesDictionary]::new();$v.PopulateFromDatabaseObject($db.FindDatabaseObject('SCIEPortBoneDragon_Saddled',$gd.EntityTemplateType,$true))
    $stream=[IO.File]::OpenRead((Join-Path $root 'src/SCIEPort/Assets/Models/AbyssBoneDragon.dae'))
    try{$model=[Engine.Media.ModelData]::Load($stream)}finally{$stream.Dispose()}
    $boneRows=@($model.Bones|ForEach-Object{[ordered]@{name=$_.Name;parent=$_.ParentBoneIndex;translation=@($_.Transform.M41,$_.Transform.M42,$_.Transform.M43)}})
    if($model.Bones.Count -ne 7 -or $model.Meshes.Count -ne 7){throw 'Dragon must fit the native seven-matrix renderer.'}
    $expectedNames=@('Body','WingL','WingR','Leg1','Leg2','Leg3','Leg4')
    $expectedPivots=@(@(0,0,0),@(-.48,1.72,-.48),@(.48,1.72,-.48),@(-.65,1.16,-.82),@(.65,1.16,-.82),@(-.61,1.14,1.05),@(.61,1.14,1.05))
    for($i=0;$i -lt 7;$i++){
        if($boneRows[$i].name -ne $expectedNames[$i] -or $boneRows[$i].parent -ne $(if($i -eq 0){-1}else{0})){throw 'Dragon animation bone contract changed'}
        for($axis=0;$axis -lt 3;$axis++){if([Math]::Abs($boneRows[$i].translation[$axis]-$expectedPivots[$i][$axis]) -gt .00001){throw 'Dragon animation pivot changed'}}
    }
    $triangles=0
    foreach($mesh in $model.Meshes){if($mesh.ParentBoneIndex -lt 0 -or $mesh.ParentBoneIndex -ge 7){throw 'Invalid bone index'};foreach($part in $mesh.MeshParts){$triangles+=$part.IndicesCount/3}}
    if($triangles -lt 1 -or $triangles -gt 7674){throw 'Dragon exceeds its original Android triangle budget'}
    foreach($buffer in $model.Buffers){if($buffer.VertexDeclaration.VertexStride -ne 32){throw 'Native vertex format mismatch'}}
    if($v['Locomotion']['FlySpeed'] -ne 16 -or $v['Health']['AttackResilience'] -ne 170 -or $v['Mount']['Class'] -ne 'Game.ComponentMount'){throw 'Dragon database values differ'}
    if($v['Mount']['MountOffset'].ToString() -ne '0,2.1,0.16' -or $v['Body']['BoxSize'].ToString() -ne '2.1,2.7,5.8'){throw 'Dragon rider/collision contract changed'}
    if($ModAssembly){[void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($ModAssembly));$t=[SCIEPort.Features.AbyssReaper.ComponentBoneDragonLocomotion];if(!$t.IsSubclassOf([Game.ComponentLocomotion])){throw 'Invalid native locomotion base'}}
    $record=[ordered]@{game_sha256=(Get-FileHash -LiteralPath (Join-Path $game 'Survivalcraft.exe')).Hash;engine_sha256=(Get-FileHash -LiteralPath (Join-Path $game 'Engine.dll')).Hash;triangles=$triangles;locomotion_class=$v['Locomotion']['Class'];fly_speed=$v['Locomotion']['FlySpeed'];walk_speed=$v['Locomotion']['WalkSpeed'];health=$v['Health']['AttackResilience'];mount_offset=$v['Mount']['MountOffset'].ToString();native_shader_budget=7;kind='Read-only target Engine model import and database compilation; not game/device testing';bones=$boneRows;meshes=$model.Meshes.Count;model_class=$v['FourLeggedModel']['Class'];model_name=$v['FourLeggedModel']['ModelName'];texture=$v['FourLeggedModel']['TextureOverride'];box=$v['Body']['BoxSize'].ToString();egg_index=$v['CreatureEggData']['EggTypeIndex'];rideable=$v.ContainsKey('Mount');template_guid=$v.DatabaseObject.Guid.ToString()}
    $record|ConvertTo-Json -Depth 5|Set-Content -Encoding UTF8 (Join-Path $root 'design/bone-dragon/native-import.json')
    $record|ConvertTo-Json -Depth 5
}finally{$zip.Dispose();$stream.Dispose()}
