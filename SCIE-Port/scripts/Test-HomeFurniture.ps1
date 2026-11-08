param([Parameter(Mandatory=$true)][string]$GameDir,[string]$AssemblyPath,[string]$Package)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$manifest=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'docs/assets/home-furniture/manifest.json') | ConvertFrom-Json
$extension=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'docs/assets/home-extension/manifest.json') | ConvertFrom-Json
$models=@($manifest.models)+@($extension.models)
$triangles=0
foreach($entry in $models){
    $path=Join-Path $root ('src/SCIEPort/Assets/Models/'+$entry.name+'.dae')
    if((Get-FileHash -LiteralPath $path).Hash -ne $entry.sha256){throw "Home model hash drift: $($entry.name)"}
    $stream=[IO.File]::OpenRead($path)
    try{$model=[Engine.Media.ModelData]::Load($stream)}finally{$stream.Dispose()}
    $count=0;foreach($mesh in $model.Meshes){foreach($part in $mesh.MeshParts){$count+=$part.IndicesCount/3}}
    if($model.Bones.Count -ne 1 -or $model.Meshes.Count -ne $entry.meshes -or $count -ne $entry.triangles){throw "Actual SCNET import failed: $($entry.name)"}
    $triangles+=$count
}
$stream=[IO.File]::OpenRead((Join-Path $root 'src/SCIEPort/Assets/Textures/HomeFurniture.png'))
try{$image=[Engine.Media.Image]::Load($stream)}finally{$stream.Dispose()}
if($image.Width -ne 512 -or $image.Height -ne 512){throw 'Native Home PNG decoding failed'}
# All native package getters are audited from IL constants, without running constructors.
$game=[Game.Block].Assembly;$nativePackageIds=@();$unknown=@()
foreach($type in $game.GetTypes()){
    if($type.IsAbstract -or -not [Game.NetWork.IPackage].IsAssignableFrom($type)){continue}
    $getter=$type.GetProperty('ID').GetGetMethod();$il=@($getter.GetMethodBody().GetILAsByteArray() | Where-Object {$null -ne $_})
    while($il.Count -gt 0 -and $il[0] -eq 0){$il=$il[1..($il.Count-1)]}
    $id=$null
    if($il[0] -eq 0x20){$id=[BitConverter]::ToInt32([byte[]]$il,1) -band 255}
    elseif($il[0] -eq 0x1f){$id=[int]$il[1]}
    elseif($il[0] -ge 0x15 -and $il[0] -le 0x1e){$id=([int]$il[0]-0x16) -band 255}
    if($null -eq $id){$unknown+=$type.FullName}else{$nativePackageIds+=$id}
}
$tableField=[Game.NetWork.PackageManager].GetField('basePackages',[Reflection.BindingFlags]'NonPublic,Static')
$table=$tableField.GetValue($null)
if($table.Length -ne 255 -or $nativePackageIds.Count -lt 20 -or $unknown.Count -ne 0 -or $nativePackageIds -contains 239){throw "Cannot establish valid/free packet 239; capacity=$($table.Length), unknown=$($unknown -join ',')"}
if($AssemblyPath){
    $assembly=[Reflection.Assembly]::LoadFrom($AssemblyPath)
    $blockType=[Game.Block];$ids=@()
    foreach($type in $assembly.GetTypes()){
        if($type.IsAbstract -or -not $type.IsSubclassOf($blockType)){continue}
        $field=$type.GetField('Index');if($field){$ids+=[int]$field.GetRawConstantValue()}
    }
    if(@($ids | Group-Object | Where-Object Count -gt 1).Count){throw 'Duplicate owned Block ID'}
    foreach($id in 957..970){if($ids -notcontains $id){throw "Missing Home Block $id"}}
    $packet=[Activator]::CreateInstance($assembly.GetType('SCIEPort.Features.Home.HomeFurniturePackage',$true))
    if($packet.ID -ne 239){throw 'Furniture packet identity changed'}
    if($null -ne $table[239]){throw 'Furniture protocol 239 already registered in this audit process'}
    try{[Game.NetWork.PackageManager]::RegisterPackage($packet);if($tableField.GetValue($null)[239] -ne $packet){throw 'Actual native furniture package registration failed'}}
    finally{[Game.NetWork.PackageManager]::UnRegisterPackage($packet)}
    $flags=[Reflection.BindingFlags]'Instance,NonPublic';$packetType=$packet.GetType()
    foreach($action in 0..1){
        $packetType.GetField('Point',$flags).SetValue($packet,[Engine.Point3]::new(-123,253,456))
        $packetType.GetField('Expected',$flags).SetValue($packet,[Game.Terrain]::MakeBlockValue(957,0,7))
        $packetType.GetField('Action',$flags).SetValue($packet,[byte]$action)
        $writer=[Game.NetWork.PackageStreamWriter]::new()
        try{
            $packet.WriteData($writer)
            if($writer.BaseStream.Length -ne 17){throw 'Furniture uncompressed payload is not 17 bytes'}
            $bytes=$writer.Data() # The real target compresses the stream here.
        }finally{$writer.Dispose()}
        $copy=[Activator]::CreateInstance($packetType);$reader=[Game.NetWork.PackageStreamReader]::new([byte[]]$bytes)
        try{$copy.ReadData($reader)}finally{$reader.Dispose()}
        foreach($name in @('Point','Expected','Action')){
            $field=$packetType.GetField($name,$flags)
            if(-not $field.GetValue($packet).Equals($field.GetValue($copy))){throw "Native package stream roundtrip failed: $name"}
        }
    }
    $upper=[Activator]::CreateInstance($assembly.GetType('SCIEPort.Features.Home.HomeUpperBlock',$true));$upper.BlockIndex=963
    if(@($upper.GetCreativeValues()).Count -ne 0){throw 'Upper occupancy leaked into creative inventory'}
    foreach($name in @('HomeDiningPartBlock','HomeBedPartBlock')){
        $hidden=[Activator]::CreateInstance($assembly.GetType('SCIEPort.Features.Home.'+$name,$true))
        if(@($hidden.GetCreativeValues()).Count -ne 0){throw "Footprint leaked into creative inventory: $name"}
    }
}
if($Package){
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead($Package)
    try{
        $paths=@('Assets/Blocks/HomeFurniture.csv','Assets/Blocks/HomeExtension.csv','Assets/Database/HomeFurniture.xdb','Assets/Textures/HomeFurniture.png')
        $paths+=@($models | ForEach-Object {'Assets/Models/'+$_.name+'.dae'})
        foreach($path in $paths){
            $entry=$zip.GetEntry($path);if(-not $entry){throw "Missing packaged furniture: $path"}
            $source=Join-Path $root ('src/SCIEPort/'+$path);$hash=[Security.Cryptography.SHA256]::Create();$stream=$entry.Open()
            try{$actual=[BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$hash.Dispose()}
            if($actual -ne (Get-FileHash -LiteralPath $source).Hash){throw "Stale packaged Home asset: $path"}
        }
    }finally{$zip.Dispose()}
}
Write-Host "Home: native Engine $($models.Count) models / one bone each / $triangles triangles, PNG, $($nativePackageIds.Count) native protocol IDs and package integrity passed."
