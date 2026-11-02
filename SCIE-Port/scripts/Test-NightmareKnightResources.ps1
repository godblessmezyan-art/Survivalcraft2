# Read-only resource/ABI import. No entities, GPU, world or game session.
param([string]$GameDir=$(if($env:SCNET_GAME_DIR){$env:SCNET_GAME_DIR}else{'E:/SCNET/SCNETx25.01.17'}),[string]$ModAssembly,[string]$Package)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$game=$GameDir
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $game $f))}
Add-Type -AssemblyName System.Xml.Linq
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
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

    $knightAssets=@('Assets/Models/AbyssNightmareKnight.dae','Assets/Textures/AbyssNightmareKnight.png','Assets/Database/AbyssNightmareKnight.xdb')
    $manifest=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'design/nightmare-knight/manifest.json')|ConvertFrom-Json
    foreach($asset in $knightAssets[0..1]){if((Get-FileHash -LiteralPath (Join-Path $root ('src/SCIEPort/'+$asset))).Hash -ne $manifest.assets.('src/SCIEPort/'+$asset)){throw 'Knight original resource hash differs'}}
    $knightStream=[IO.File]::OpenRead((Join-Path $root ('src/SCIEPort/'+$knightAssets[2])))
    try{[ModsManager]::CombineDataBase($xml,$knightStream)}finally{$knightStream.Dispose()}
    $db=[TemplatesDatabase.XmlDatabaseSerializer]::LoadDatabase($xml);$gd=[GameEntitySystem.GameDatabase]::new($db)
    $v=[TemplatesDatabase.ValuesDictionary]::new();$v.PopulateFromDatabaseObject($db.FindDatabaseObject('SCIEPortNightmareKnight',$gd.EntityTemplateType,$true))
    if($v.ContainsKey('Mount') -or $v.ContainsKey('SteedBehavior') -or $v.ContainsKey('Rider')){throw 'Knight must remain one complete NPC unit, without a player mount/rider entity'}
    if($v['Health']['AttackResilience'] -ne 70 -or $v['Locomotion']['WalkSpeed'] -ne 8 -or $v['Miner']['AttackPower'] -ne 15 -or $v['Locomotion']['FlySpeed'] -ne 0 -or $v['Spawn']['AutoDespawn'] -ne $false -or $v['CreatureEggData']['EggTypeIndex'] -ne -1){throw 'Knight database profile differs'}
    if($v['FourLeggedModel']['Class'] -ne 'SCIEPort.Features.AbyssReaper.ComponentNightmareKnightModel' -or $v['FourLeggedModel']['ModelName'] -ne 'Models/AbyssNightmareKnight' -or $v['FourLeggedModel']['TextureOverride'] -ne 'Textures/AbyssNightmareKnight'){throw 'Knight model/texture bindings differ'}
    $knightStream=[IO.File]::OpenRead((Join-Path $root ('src/SCIEPort/'+$knightAssets[0])))
    try{$model=[Engine.Media.ModelData]::Load($knightStream)}finally{$knightStream.Dispose()}
    $names=@($model.Bones|ForEach-Object Name)
    if($model.Bones.Count -ne 7 -or $model.Meshes.Count -ne 7 -or ($names|Sort-Object)-join ',' -ne 'Body,Leg1,Leg2,Leg3,Leg4,Rider,SwordArm'){throw 'Knight must contain exactly seven named rigid bones and meshes'}
    $triangles=0;$boneRows=@();$min=[Engine.Vector3]::new(100);$max=[Engine.Vector3]::new(-100)
    for($i=0;$i -lt $model.Bones.Count;$i++){
        $bone=$model.Bones[$i];$position=$bone.Transform.Translation;$parent=$bone.ParentBoneIndex;$seen=0
        while($parent -ge 0){if(++$seen -gt 7){throw 'Knight bone cycle'};$position+=$model.Bones[$parent].Transform.Translation;$parent=$model.Bones[$parent].ParentBoneIndex}
        $boneRows+=@{name=$bone.Name;parent=$bone.ParentBoneIndex;translation=$position.ToString()}
    }
    foreach($mesh in $model.Meshes){
        if($mesh.ParentBoneIndex -lt 0 -or $mesh.ParentBoneIndex -ge 7){throw 'Knight mesh bone out of range'}
        $position=$model.Bones[$mesh.ParentBoneIndex].Transform.Translation;$parent=$model.Bones[$mesh.ParentBoneIndex].ParentBoneIndex
        while($parent -ge 0){$position+=$model.Bones[$parent].Transform.Translation;$parent=$model.Bones[$parent].ParentBoneIndex}
        $lo=$mesh.BoundingBox.Min+$position;$hi=$mesh.BoundingBox.Max+$position
        $min=[Engine.Vector3]::Min($min,$lo);$max=[Engine.Vector3]::Max($max,$hi)
        foreach($part in $mesh.MeshParts){$triangles+=$part.IndicesCount/3}
    }
    if($triangles -ne $manifest.triangles -or $triangles -gt 6000){throw 'Knight triangle budget/manifest mismatch'}
    foreach($buffer in $model.Buffers){if($buffer.VertexDeclaration.VertexStride -ne 32){throw 'Knight native vertex format mismatch'}}
    if($min.X -lt -.9 -or $max.X -gt .9 -or $min.Y -lt 0 -or $max.Y -gt 3.65 -or $min.Z -lt -1.9 -or $max.Z -gt 1.9){throw 'Knight native geometry exceeds its body'}
    $knightStream=[IO.File]::OpenRead((Join-Path $root ('src/SCIEPort/'+$knightAssets[1])))
    try{$image=[Engine.Media.Image]::Load($knightStream)}finally{$knightStream.Dispose()}
    if($image.Width -ne 256 -or $image.Height -ne 256){throw 'Knight UV atlas must be 256 square'}
    for($x=0;$x -lt 256;$x+=8){for($y=0;$y -lt 256;$y+=8){if($image.GetPixel($x,$y).A -ne 255){throw 'Knight atlas must be opaque'}}}
    if($ModAssembly){
        [void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($ModAssembly));$type=[SCIEPort.Features.AbyssReaper.ComponentNightmareKnightModel]
        if(!$type.IsSubclassOf([Game.ComponentCreatureModel]) -or $type.GetMethod('Update').DeclaringType -ne $type -or $type.GetMethod('Animate').DeclaringType -ne $type){throw 'Knight native model/animation override ABI differs'}
    }
    if($Package){
        $knightArchive=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Package))
        try{foreach($asset in $knightAssets){$entry=$knightArchive.GetEntry($asset);if(!$entry){throw 'Knight asset absent from package'};$knightStream=$entry.Open();$hash=[Security.Cryptography.SHA256]::Create();try{$actual=[BitConverter]::ToString($hash.ComputeHash($knightStream)).Replace('-','')}finally{$knightStream.Dispose();$hash.Dispose()};if($actual -ne (Get-FileHash -LiteralPath (Join-Path $root ('src/SCIEPort/'+$asset))).Hash){throw 'Knight packaged resource mismatch'}}}finally{$knightArchive.Dispose()}
    }
    $record=[ordered]@{kind='Read-only native resource import and metadata inspection; not game/Android/network proof';game_sha256=(Get-FileHash -LiteralPath (Join-Path $game 'Survivalcraft.exe')).Hash;engine_sha256=(Get-FileHash -LiteralPath (Join-Path $game 'Engine.dll')).Hash;template_guid=$v.DatabaseObject.Guid.ToString();health=70;walk_speed=8;damage=15;bone_count=7;mesh_count=7;triangles=$triangles;native_bounds=@{min=$min.ToString();max=$max.ToString()};body=$v['Body']['BoxSize'].ToString();player_rideable=$false;egg_index=-1;bones=$boneRows}
    $record|ConvertTo-Json -Depth 6|Set-Content -Encoding UTF8 (Join-Path $root 'design/nightmare-knight/native-import.json')
    Write-Host "Nightmare knight native model/database/UV/bounds verified: 7 bones, 7 meshes, $triangles triangles. Android pending."
}finally{$zip.Dispose();$stream.Dispose()}
