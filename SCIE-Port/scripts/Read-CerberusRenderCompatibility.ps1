param(
    [string]$GameDir='D:/Games/SCNET-x25.01.17',
    [Parameter(Mandatory=$true)][string]$ModAssembly,
    [string]$BaselinePackage
)
# Read-only resource/ABI audit. Does not create a world, GPU device or game entity.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
foreach($name in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){
    [void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $name))
}
[void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($ModAssembly))
# A fresh loader has no initialization side effects; inherited native method only.
$loader=New-Object SCIEPort.Mod.SCIEPortLoader
$limit=$loader.GetMaxInstancesCount()
function Read-Model([IO.Stream]$stream){
    $model=[Engine.Media.ModelData]::Load($stream)
    $bones=@($model.Bones|ForEach-Object{[ordered]@{name=$_.Name;parent=$_.ParentBoneIndex;translation=@($_.Transform.M41,$_.Transform.M42,$_.Transform.M43)}})
    $indices=0
    $meshes=@(foreach($mesh in $model.Meshes){
        $count=0
        foreach($part in $mesh.MeshParts){$count+=$part.IndicesCount}
        if($count -le 0){throw ('Empty mesh: '+$mesh.Name)}
        $indices+=$count
        [ordered]@{name=$mesh.Name;bone_index=$mesh.ParentBoneIndex;indices=$count}
    })
    $buffers=@(foreach($buffer in $model.Buffers){
        [ordered]@{stride=$buffer.VertexDeclaration.VertexStride;vertex_bytes=$buffer.Vertices.Length;index_bytes=$buffer.Indices.Length;elements=@($buffer.VertexDeclaration.VertexElements|ForEach-Object{[ordered]@{offset=$_.Offset;semantic=$_.Semantic;format=$_.Format.ToString()}})}
    })
    [ordered]@{bone_count=$model.Bones.Count;mesh_count=$model.Meshes.Count;triangles=$indices/3;bones=$bones;meshes=$meshes;buffers=$buffers;fits_native_shader=($model.Bones.Count -le $limit)}
}
$asset=Join-Path $root 'src/SCIEPort/Assets/Models/AbyssCerberus.dae'
$stream=[IO.File]::OpenRead($asset)
try{$current=Read-Model $stream}finally{$stream.Dispose()}
if(!$current.fits_native_shader){throw "Model uses $($current.bone_count) bones, native shader supports $limit."}
if($current.bone_count -ne 7 -or $current.mesh_count -ne 7 -or $current.triangles -ne 6628){throw 'Unexpected Cerberus model structure.'}
foreach($mesh in $current.meshes){if($mesh.bone_index -lt 0 -or $mesh.bone_index -ge $limit){throw 'Mesh bone index exceeds shader capacity.'}}
foreach($buffer in $current.buffers){
    if($buffer.stride -ne 32 -or $buffer.elements.Count -ne 3 -or $buffer.elements[0].offset -ne 0 -or $buffer.elements[0].semantic -ne 'POSITION' -or $buffer.elements[1].offset -ne 12 -or $buffer.elements[1].semantic -ne 'NORMAL' -or $buffer.elements[2].offset -ne 24 -or $buffer.elements[2].semantic -ne 'TEXCOORD'){throw 'Native instanced renderer vertex format mismatch.'}
}
$baseline=$null
if($BaselinePackage){
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($BaselinePackage))
    try{
        $entry=$zip.GetEntry('Assets/Models/AbyssCerberus.dae')
        if($entry){$stream=$entry.Open();try{$baseline=Read-Model $stream}finally{$stream.Dispose()}}
    }finally{$zip.Dispose()}
}
$report=[ordered]@{
    kind='Read-only native ModelData import and renderer ABI audit; not gameplay, GPU or Android testing'
    native_shader_limit=$limit
    limit_method='Game.ModLoader.GetMaxInstancesCount (inherited by SCIEPortLoader)'
    game_sha256=(Get-FileHash -LiteralPath (Join-Path $GameDir 'Survivalcraft.exe')).Hash
    engine_sha256=(Get-FileHash -LiteralPath (Join-Path $GameDir 'Engine.dll')).Hash
    model_sha256=(Get-FileHash -LiteralPath $asset).Hash
    current=$current
    previous_0_65_0=$baseline
}
$output=Join-Path $root 'design/cerberus/render-compatibility.json'
$report|ConvertTo-Json -Depth 9|Set-Content -Encoding UTF8 -LiteralPath $output
Write-Host "Native model resource audit: $($current.bone_count) bones / $($current.mesh_count) meshes / $($current.triangles) triangles; shader capacity $limit."
if($baseline){Write-Host "Old package: $($baseline.bone_count) bones; fits native shader: $($baseline.fits_native_shader)."}
Write-Host "Report: $output"
