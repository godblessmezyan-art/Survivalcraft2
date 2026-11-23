param([Parameter(Mandatory=$true)][string]$GameDir,[string]$AssemblyPath)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$manifest=Get-Content -Raw -Encoding UTF8 (Join-Path $root 'docs/assets/pyramid-decor/manifest.json') | ConvertFrom-Json
$triangles=0
foreach($entry in $manifest.models){
    $path=Join-Path $root ('src/SCIEPort/Assets/Models/'+$entry.name+'.dae')
    if((Get-FileHash -LiteralPath $path).Hash -ne $entry.sha256){throw "Pyramid decor model hash drift: $($entry.name)"}
    $stream=[IO.File]::OpenRead($path)
    try{$model=[Engine.Media.ModelData]::Load($stream)}finally{$stream.Dispose()}
    $count=0
    foreach($mesh in $model.Meshes){foreach($part in $mesh.MeshParts){$count+=$part.IndicesCount/3}}
    if($model.Bones.Count -ne 1 -or $model.Meshes.Count -ne $entry.meshes -or $count -ne $entry.triangles){throw "Actual SCNET import failed: $($entry.name) bones=$($model.Bones.Count) meshes=$($model.Meshes.Count) tris=$count expected=$($entry.triangles)/$($entry.meshes)"}
    $triangles+=$count
}
$stream=[IO.File]::OpenRead((Join-Path $root 'src/SCIEPort/Assets/Textures/PyramidDecor.png'))
try{$image=[Engine.Media.Image]::Load($stream)}finally{$stream.Dispose()}
if($image.Width -ne 512 -or $image.Height -ne 512){throw 'Native PyramidDecor PNG decoding failed'}
if($AssemblyPath){
    $assembly=[Reflection.Assembly]::LoadFrom($AssemblyPath)
    $blockType=[Game.Block];$ids=@()
    foreach($type in $assembly.GetTypes()){
        if($type.IsAbstract -or -not $type.IsSubclassOf($blockType)){continue}
        $field=$type.GetField('Index');if($field){$ids+=[int]$field.GetRawConstantValue()}
    }
    if(@($ids | Group-Object | Where-Object Count -gt 1).Count){throw 'Duplicate owned Block ID'}
    if ($assembly.GetType('SCIEPort.Features.Pyramid.PyramidDecorBlock', $true).GetField('Index').GetRawConstantValue() -ne 886 -or
        $assembly.GetType('SCIEPort.Features.MountainShelter.MountainShelterBlock', $true).GetField('Index').GetRawConstantValue() -ne 947) {
        throw 'Pyramid decor 886 and mountain shelter 947 must have distinct registrations.'
    }
    Write-Output 'Pyramid decor block 886 and mountain shelter block 947 registered; no duplicates.'
}
Write-Output "Pyramid decor: $($manifest.models.Count) models / $triangles triangles imported by target Engine, atlas decoded."
