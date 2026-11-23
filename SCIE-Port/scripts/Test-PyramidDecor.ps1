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
    $lowest=[single]::PositiveInfinity
    $extent=if($entry.footprint){$entry.footprint}else{@(1,1,1)}
    foreach($mesh in $model.Meshes){
        $lower=[Engine.Vector3]::new(-.5,0,-.5)
        $upper=[Engine.Vector3]::new(.5,1,.5)
        if ($entry.footprint) {
            if ($mesh.Name -notmatch '^Part([0-9]+)$') { throw "Invalid imported monument part: $($entry.name) $($mesh.Name)" }
            $partIndex=[int]$Matches[1]
            $width=[int]$extent[0]; $depth=[int]$extent[2]
            $x=$partIndex % $width
            $z=[int][Math]::Floor($partIndex / $width) % $depth
            $y=[int][Math]::Floor($partIndex / ($width * $depth))
            if ($y -ge $extent[1]) { throw "Invalid monument part height: $($entry.name)" }
            $lower=[Engine.Vector3]::new($x-.5,$y,$z-.5)
            $upper=$lower+[Engine.Vector3]::One
        }
        foreach($part in $mesh.MeshParts){
            $count+=$part.IndicesCount/3
            $box=$part.BoundingBox
            $lowest=[Math]::Min($lowest,$box.Min.Y)
            if ($box.Min.X -lt $lower.X-0.00001 -or $box.Max.X -gt $upper.X+0.00001 -or
                $box.Min.Z -lt $lower.Z-0.00001 -or $box.Max.Z -gt $upper.Z+0.00001 -or
                $box.Min.Y -lt $lower.Y-0.00001 -or $box.Max.Y -gt $upper.Y+0.00001) {
                throw "Pyramid decor leaves its block after native import: $($entry.name) $box"
            }
        }
    }
    if ([Math]::Abs($lowest) -gt 0.00001) { throw "Pyramid decor has no floor contact: $($entry.name)" }
    if($model.Bones.Count -ne $entry.bones -or $model.Meshes.Count -ne $entry.meshes -or $count -ne $entry.triangles){throw "Actual SCNET import failed: $($entry.name) bones=$($model.Bones.Count) meshes=$($model.Meshes.Count) tris=$count expected=$($entry.triangles)/$($entry.meshes)"}
    $triangles+=$count
}
$atlas=Join-Path $root 'src/SCIEPort/Assets/Textures/PyramidDecor.png'
if ((Get-FileHash -LiteralPath $atlas).Hash -ne $manifest.textureSha256) { throw 'Pyramid decor atlas hash drift.' }
$stream=[IO.File]::OpenRead($atlas)
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
Write-Output "Pyramid decor: $($manifest.models.Count) centered and grounded models / $triangles triangles imported by target Engine, atlas decoded."
