param([Parameter(Mandatory=$true)][string]$GameDir,[Parameter(Mandatory=$true)][string]$ModDll,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$assembly=[Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($ModDll))
$build=$assembly.GetType('SCIEPort.Features.Home.BookshelfGeometry',$true).GetMethod('Build',[Reflection.BindingFlags]'NonPublic,Static')
$samples=@()
foreach($id in @(973,976)){
    $variants=if($id -eq 973){3}else{4}
    for($variant=0;$variant -lt $variants;$variant++){
        $data=$variant*8;$mesh=$build.Invoke($null,@($id,$data,0));$vertices=@();$indices=@()
        for($i=0;$i -lt $mesh.Vertices.Count;$i++){$v=$mesh.Vertices.Array[$i];$vertices+=,@($v.Position.X,$v.Position.Y,$v.Position.Z,[int]$v.Color.R,[int]$v.Color.G,[int]$v.Color.B,[int]$v.Color.A)}
        for($i=0;$i -lt $mesh.Indices.Count;$i++){$indices+=[int]$mesh.Indices.Array[$i]}
        $samples+=@{id=$id;data=$data;parts=@(@{vertices=$vertices;indices=$indices},@{vertices=@();indices=@()})}
    }
}
$utf8=New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText([IO.Path]::GetFullPath($Output),($samples|ConvertTo-Json -Depth 10 -Compress),$utf8)
Write-Host 'Exported seven actual C# single-cell book furniture meshes; no game textures exported.'
