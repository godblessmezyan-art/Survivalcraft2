param([Parameter(Mandatory=$true)][string]$GameDir,[string]$AssemblyPath,[string]$Package)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$csvPath=Join-Path $root 'src/SCIEPort/Assets/Blocks/Bookshelf.csv'
if(-not (Test-Path -LiteralPath $csvPath)){throw 'Missing Bookshelf.csv'}
$lines=@(Get-Content -LiteralPath $csvPath -Encoding UTF8 | Where-Object {$_.Trim().Length -gt 0})
if($lines.Count -ne 6){throw "Bookshelf.csv must contain one header and five rows, got $($lines.Count)"}
if($lines[0].Split(';').Count -ne 18){throw 'Bookshelf.csv header must keep the native eighteen columns'}
$classes=@{973='BookshelfBlock';974='BookshelfTallBlock';975='BookcaseBlock';976='BookPileBlock';977='BookcasePartBlock'}
$ids=@()
for($i=1;$i -lt $lines.Count;$i++){
    $f=$lines[$i].Split(';')
    if($f.Count -ne 18){throw "Bookshelf.csv row $($i+1) has $($f.Count) columns"}
    $id=[int]$f[4]
    if(-not $classes.ContainsKey($id)){throw "Unexpected book block id $id"}
    if($f[0] -ne $classes[$id]){throw "Book class mismatch for $id"}
    if($f[2] -ne 'SCIEPort.Structures'){throw "Book block $id escaped the Structures category"}
    if($f[3] -ne 'SCIEPortHomeFurniture'){throw "Book block $id lost the furniture behavior"}
    if($id -eq 977){
        if($f[5] -ne '-1' -or $f[8] -ne 'FALSE' -or $f[15] -ne '0'){throw 'Bookcase part must stay hidden, unplaceable and unstackable'}
    }else{
        if($f[5] -ne '0' -or $f[8] -ne 'TRUE' -or $f[15] -ne '40'){throw "Book block $id is not a normal creative item"}
    }
    $ids+=$id
}
if((($ids | Sort-Object) -join ',') -ne '973,974,975,976,977'){throw 'Book block id set is incomplete'}
if($AssemblyPath){
    $assembly=[Reflection.Assembly]::LoadFrom($AssemblyPath)
    $owned=@()
    foreach($type in $assembly.GetTypes()){
        if($type.IsAbstract -or -not $type.IsSubclassOf([Game.Block])){continue}
        $field=$type.GetField('Index');if($field){$owned+=[int]$field.GetRawConstantValue()}
    }
    if(@($owned | Group-Object | Where-Object Count -gt 1).Count){throw 'Duplicate owned Block ID'}
    foreach($id in 973..977){if($owned -notcontains $id){throw "Missing Book Block $id"}}
    $variants=@{973=3;974=2;975=1;976=4}
    $geometry=$assembly.GetType('SCIEPort.Features.Home.BookshelfGeometry',$true)
    $build=$geometry.GetMethod('Build',[Reflection.BindingFlags]'NonPublic,Static')
    $meshes=0
    foreach($id in 973..976){
        for($v=0;$v -lt $variants[$id];$v++){
            for($r=0;$r -lt 4;$r++){
                $limit=if($id -eq 975){@(-1,2,0,1.9,-1,2)}
                    elseif($id -eq 976){if($r -eq 1 -or $r -eq 3){@(0.12,0.88,0,0.34,0.10,0.90)}else{@(0.10,0.90,0,0.34,0.12,0.88)}}
                    else{
                        $x0=0.04;$x1=0.96;$z0=0.28;$z1=0.96;$y1=if($id -eq 974){1.9}else{1.0}
                        if($r -eq 1){$x0=0.28;$z0=0.04}
                        elseif($r -eq 2){$z0=0.04;$z1=0.72}
                        elseif($r -eq 3){$x1=0.72;$z0=0.04}
                        @($x0,$x1,0,$y1,$z0,$z1)
                    }
                $mesh=$build.Invoke($null,@($id,($v -shl 3),$r))
                $count=$mesh.Vertices.Count
                if($count -eq 0 -or $count % 4 -ne 0 -or $mesh.Indices.Count % 6 -ne 0){throw "Mesh layout invalid for $id/$v/$r"}
                if($mesh.Indices.Count / 3 -gt 6000){throw "Triangle budget exceeded for $id/$v/$r"}
                $minX=[double]::MaxValue;$minY=[double]::MaxValue;$minZ=[double]::MaxValue
                $maxX=[double]::MinValue;$maxY=[double]::MinValue;$maxZ=[double]::MinValue
                for($i=0;$i -lt $count;$i++){
                    $vertex=$mesh.Vertices.Array[$i]
                    $p=$vertex.Position
                    foreach($c in @([double]$p.X,[double]$p.Y,[double]$p.Z)){
                        if([double]::IsNaN($c) -or [double]::IsInfinity($c)){throw "Non-finite vertex for $id/$v/$r"}
                    }
                    if($vertex.Face -gt 5){throw "Invalid lighting face for $id/$v/$r"}
                    $uv=$vertex.TextureCoordinates
                    if($uv.X -lt 0 -or $uv.X -gt 1 -or $uv.Y -lt 0 -or $uv.Y -gt 1){throw "UV outside the white texel for $id/$v/$r"}
                    if($p.X -lt $minX){$minX=$p.X};if($p.X -gt $maxX){$maxX=$p.X}
                    if($p.Y -lt $minY){$minY=$p.Y};if($p.Y -gt $maxY){$maxY=$p.Y}
                    if($p.Z -lt $minZ){$minZ=$p.Z};if($p.Z -gt $maxZ){$maxZ=$p.Z}
                }
                if($minX -lt $limit[0] - .001 -or $maxX -gt $limit[1] + .001 -or $minY -lt $limit[2] - .001 -or $maxY -gt $limit[3] + .001 -or $minZ -lt $limit[4] - .001 -or $maxZ -gt $limit[5] + .001){
                    throw "Mesh escapes authored bounds for $id/$v/$r -> $minX,$minY,$minZ - $maxX,$maxY,$maxZ"
                }
                for($t=0;$t -lt $mesh.Indices.Count;$t+=3){
                    $a=[int]$mesh.Indices.Array[$t];$b=[int]$mesh.Indices.Array[$t+1];$c=[int]$mesh.Indices.Array[$t+2]
                    if($a -ge $count -or $b -ge $count -or $c -ge $count){throw "Index out of range for $id/$v/$r"}
                    $pa=$mesh.Vertices.Array[$a].Position;$pb=$mesh.Vertices.Array[$b].Position;$pc=$mesh.Vertices.Array[$c].Position
                    $u=[Engine.Vector3]::new($pb.X-$pa.X,$pb.Y-$pa.Y,$pb.Z-$pa.Z)
                    $w=[Engine.Vector3]::new($pc.X-$pa.X,$pc.Y-$pa.Y,$pc.Z-$pa.Z)
                    $cross=[Engine.Vector3]::Cross($u,$w)
                    if($cross.Length() * .5 -le 1e-6){throw "Degenerate triangle for $id/$v/$r"}
                }
                $meshes++
            }
        }
    }
    $blocks=@{}
    foreach($id in 973..977){
        $type=$assembly.GetType('SCIEPort.Features.Home.'+$classes[$id],$true)
        $block=[Activator]::CreateInstance($type)
        $block.BlockIndex=$id
        $blocks[$id]=$block
    }
    $homeType=$assembly.GetType('SCIEPort.Features.Home.HomeBlock',$true)
    $boxesMethod=$homeType.GetMethod('Boxes',[Reflection.BindingFlags]'NonPublic,Static')
    foreach($id in 973..976){
        if(@($blocks[$id].GetCreativeValues()).Count -ne $variants[$id]){throw "Creative variant count changed for $id"}
        for($v=0;$v -lt $variants[$id];$v++){
            for($r=0;$r -lt 4;$r++){
                $boxes=@($boxesMethod.Invoke($null,@($id,(($v -shl 3) -bor $r),$r,$false,$false)))
                if($boxes.Count -eq 0){throw "Missing collision for $id/$v/$r"}
                foreach($box in $boxes){
                    if($box.Max.X-$box.Min.X -le 0 -or $box.Max.Y-$box.Min.Y -le 0 -or $box.Max.Z-$box.Min.Z -le 0){throw "Empty collision box for $id/$v/$r"}
                    if($id -eq 975){
                        if($box.Min.X -lt -.001 -or $box.Max.X -gt 2.001 -or $box.Min.Z -lt -.001 -or $box.Max.Z -gt 2.001){throw "Bookcase collision escapes the two-by-two footprint"}
                    }else{
                        if($box.Min.X -lt -.001 -or $box.Max.X -gt 1.001 -or $box.Min.Z -lt -.001 -or $box.Max.Z -gt 1.001){throw "Single-cell collision escapes its cell for $id/$v/$r"}
                    }
                }
            }
        }
    }
    if(@($blocks[977].GetCreativeValues()).Count -ne 0){throw 'Bookcase part leaked into the creative inventory'}
    for($part=1;$part -le 3;$part++){
        for($r=0;$r -lt 4;$r++){
            $boxes=@($blocks[977].GetCustomCollisionBoxes($null,[Game.Terrain]::MakeBlockValue(977,0,($part -shl 2) -bor $r)))
            if($boxes.Count -eq 0){throw "Bookcase part $part/$r has no collision"}
            foreach($box in $boxes){
                if($box.Min.X -lt -.001 -or $box.Max.X -gt 2.001 -or $box.Min.Z -lt -.001 -or $box.Max.Z -gt 2.001){throw "Bookcase part collision escapes the footprint"}
                if($box.Max.X-$box.Min.X -gt 1.001 -or $box.Max.Z-$box.Min.Z -gt 1.001){throw "Bookcase part collision spans more than one cell"}
            }
        }
    }
    $upperType=$assembly.GetType('SCIEPort.Features.Home.HomeUpperBlock',$true)
    $upper=[Activator]::CreateInstance($upperType);$upper.BlockIndex=963
    if(@($upper.GetCreativeValues()).Count -ne 0){throw 'Tall book upper cell leaked into the creative inventory'}
    $upperBoxes=@($upper.GetCustomCollisionBoxes($null,[Game.Terrain]::MakeBlockValue(963,0,128)))
    if($upperBoxes.Count -eq 0){throw 'Tall book upper cell has no collision'}
    foreach($box in $upperBoxes){if($box.Max.Y -gt .921){throw 'Tall book upper collision exceeds the authored shelf height'}}
    $table=[Game.BlocksManager]::Blocks
    $game=[Game.Block].Assembly
    $planks=[Activator]::CreateInstance($game.GetType('Game.PlanksBlock',$true));$planks.BlockIndex=21;$planks.CraftingId='test:planks'
    $stick=[Activator]::CreateInstance($game.GetType('Game.StickBlock',$true));$stick.BlockIndex=23;$stick.CraftingId='test:stick'
    $cotton=[Activator]::CreateInstance($game.GetType('Game.CottonWadBlock',$true));$cotton.BlockIndex=205;$cotton.CraftingId='test:cotton'
    for($i=0;$i -lt $table.Length;$i++){if($null -eq $table[$i]){$table[$i]=$planks}}
    $table[21]=$planks;$table[23]=$stick;$table[205]=$cotton
    $filled=@{973=4;974=4;975=4;976=2}
    $results=@()
    foreach($id in 973..976){
        $recipes=@($blocks[$id].GetProceduralCraftingRecipes())
        if($recipes.Count -ne 1){throw "Book block $id must expose exactly one recipe"}
        $recipe=$recipes[0]
        if($recipe.Ingredients.Count -ne 4){throw "Book recipe $id is not a two-by-two recipe"}
        if(@($recipe.Ingredients | Where-Object {$_}).Count -ne $filled[$id]){throw "Book recipe $id material count changed"}
        if($recipe.ResultCount -ne 1 -or $recipe.RequiredHeatLevel -ne 0 -or $recipe.RequiredPlayerLevel -ne 0){throw "Book recipe $id requirements changed"}
        if([Game.Terrain]::ExtractContents($recipe.ResultValue) -ne $id){throw "Book recipe $id returns the wrong block"}
        $results+=$recipe.ResultValue
    }
    if(@($results | Group-Object).Count -ne 4){throw 'Book recipes must not share a result'}
    Write-Host "Bookshelf assembly: $meshes meshes, $($blocks.Count) blocks, collision and four two-by-two recipes verified."
}else{
    Write-Host 'Bookshelf CSV: five blocks in the Structures category with matching class names.'
}
if($Package){
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead($Package)
    try{
        $entry=$zip.GetEntry('Assets/Blocks/Bookshelf.csv')
        if(-not $entry){throw 'Missing packaged Bookshelf.csv'}
        $hash=[Security.Cryptography.SHA256]::Create();$stream=$entry.Open()
        try{$actual=[BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$hash.Dispose()}
        if($actual -ne (Get-FileHash -LiteralPath $csvPath).Hash){throw 'Stale packaged Bookshelf.csv'}
    }finally{$zip.Dispose()}
    Write-Host 'Bookshelf package: CSV bytes match the source.'
}
