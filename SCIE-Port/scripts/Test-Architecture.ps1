param([Parameter(Mandatory=$true)][string]$GameDir,[string]$AssemblyPath,[string]$Package,[string]$MeshOutput)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$csv=Join-Path $root 'src/SCIEPort/Assets/Blocks/Architecture.csv'
$rows=@(Import-Csv -LiteralPath $csv -Delimiter ';' -Encoding UTF8)
if($rows.Count -ne 7){throw 'Architecture needs all seven families'}
$ids=@{}
foreach($type in [Game.Block].Assembly.GetTypes()){
    if(-not $type.IsSubclassOf([Game.Block])){continue}
    $field=$type.GetField('Index');if($field -and $field.IsLiteral){$ids[[int]$field.GetRawConstantValue()]=$type.FullName}
}
foreach($row in $rows){
    $id=[int]$row.DisplayOrder
    if($id -lt 978 -or $id -gt 984 -or $ids.ContainsKey($id)){throw "Architectural ID $id conflicts with target native blocks"}
    if($row.DefaultCategory -ne 'SCIEPort.Structures' -or $row.Behaviors -or $row.DefaultCreativeData -ne '0' -or $row.IsPlaceable -ne 'TRUE'){throw "Invalid architectural registration $id"}
}
if($AssemblyPath){
    $assembly=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($AssemblyPath));$flags=[Reflection.BindingFlags]'Static,NonPublic'
    $geometry=$assembly.GetType('SCIEPort.Features.Architecture.ArchitectureGeometry',$true)
    $build=$geometry.GetMethod('Build',$flags);$samples=@();$all=@{}
    foreach($type in $assembly.GetTypes()){
        if($type.IsAbstract -or -not $type.IsSubclassOf([Game.Block])){continue}
        $field=$type.GetField('Index');if($field -and $field.IsLiteral){$id=[int]$field.GetRawConstantValue();if($all.ContainsKey($id)){throw "Duplicate owned ID $id"};$all[$id]=$type.FullName}
    }
    foreach($row in $rows){
        $type=$assembly.GetType('SCIEPort.Features.Architecture.'+$row.'Class Name',$true);$block=[Activator]::CreateInstance($type)
        $id=[int]$type.GetField('Index').GetRawConstantValue();$block.BlockIndex=$id
        if($id -ne [int]$row.DisplayOrder){throw 'Architecture CSV/id mismatch'}
        foreach($value in $block.GetCreativeValues()){
            $data=[Game.Terrain]::ExtractData($value);$shape=$build.Invoke($null,@($id,$data))
            if($block.GetEmittedLightAmount($value) -ne $(if($id -eq 981){12}else{0})){throw 'Architecture native lighting changed'}
            if($MeshOutput){
                $parts=@()
                foreach($part in @('Opaque','Glass')){
                    $mesh=$shape.GetType().GetField($part,[Reflection.BindingFlags]'Instance,NonPublic').GetValue($shape)
                    $vertices=@();for($i=0;$i -lt $mesh.Vertices.Count;$i++){$v=$mesh.Vertices.Array[$i];$vertices+=,@($v.Position.X,$v.Position.Y,$v.Position.Z,[int]$v.Color.R,[int]$v.Color.G,[int]$v.Color.B,[int]$v.Color.A)}
                    $indices=@();for($i=0;$i -lt $mesh.Indices.Count;$i++){$indices+=[int]$mesh.Indices.Array[$i]}
                    $parts+=@{vertices=$vertices;indices=$indices}
                }
                $samples+=@{id=$id;data=$data;name=$block.GetDisplayName($null,$value);parts=$parts}
            }
        }
    }
    foreach($name in @('SCIEPort.Features.Home.BookshelfBlock','SCIEPort.Features.Home.BookcaseBlock','SCIEPort.Features.Fishing.FishingRodBlock','SCIEPort.Features.CelestialCodex.Mod.CelestialCodexBlock')){
        if(-not $assembly.GetType($name)){throw "Shared main feature lost: $name"}
    }
    if($MeshOutput){$samples | ConvertTo-Json -Depth 12 -Compress | Set-Content -LiteralPath $MeshOutput -Encoding UTF8}
}
if($Package){
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Package))
    try{
        $entry=$zip.GetEntry('Assets/Blocks/Architecture.csv');if(-not $entry){throw 'Missing packaged architectural suite'}
        $sha=[Security.Cryptography.SHA256]::Create();$stream=$entry.Open()
        try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
        if($hash -ne (Get-FileHash -LiteralPath $csv).Hash){throw 'Stale architecture CSV in package'}
    }finally{$zip.Dispose()}
}
Write-Host 'Architecture: seven IDs verified, dedicated category, exact native registration and package checked.'
