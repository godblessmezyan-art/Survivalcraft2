[CmdletBinding()]
param([string]$GameDir, [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference='Stop'
$project=Join-Path $ProjectRoot 'src\SCIEPort'
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'EntitySystem.dll'))
$game=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$specs=@(
    @{Name='XuanwuArmorV3';Bones=1;Meshes=40;Triangles=1212;Features=@('CoiledSnake','SnakeHead','TurtleShoulderLeft','TurtleShoulderRight','BackShell','SkirtLeft0','SkirtRight0')},
    @{Name='BaihuLegArmorV3';Bones=3;Meshes=36;Triangles=804;Features=@('TigerKneeMaskLeft','TigerKneeMaskRight','TigerFangLeft-1','TigerFangRight1','SweptShinLeft','SweptShinRight')},
    @{Name='ZhuqueBootsV3';Bones=3;Meshes=30;Triangles=596;Features=@('PhoenixToeLeft','PhoenixToeRight','DarkSoleLeft','DarkSoleRight','AnkleFeatherLeft2','AnkleFeatherRight2','HeelTailLeft0','HeelTailRight0')}
)
$imported=@{}
$total=680
foreach($spec in $specs){
    $path=Join-Path $project ("Assets\Models\"+$spec.Name+'.dae')
    [xml]$xml=Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $ns=[Xml.XmlNamespaceManager]::new($xml.NameTable);$ns.AddNamespace('c','http://www.collada.org/2005/11/COLLADASchema')
    if($xml.COLLADA.asset.up_axis -ne 'Y_UP' -or $xml.COLLADA.asset.unit.meter -ne '1'){throw 'Armor axis/units changed'}
    $parts=@($xml.SelectNodes('//c:geometry',$ns))
    if($parts.Count -ne $spec.Meshes){throw "Unexpected authored structure count: $($spec.Name)"}
    foreach($feature in $spec.Features){if(-not $xml.SelectSingleNode("//c:geometry[@name='$feature']",$ns)){throw "Missing real geometry: $feature"}}
    $triangles=0
    foreach($part in $parts){
        $position=@($part.SelectSingleNode(".//c:source[contains(@id,'-positions')]/c:float_array",$ns).InnerText -split '\s+' | Where-Object {$_} | ForEach-Object {[double]::Parse($_,[Globalization.CultureInfo]::InvariantCulture)})
        $normal=@($part.SelectSingleNode(".//c:source[contains(@id,'-normals')]/c:float_array",$ns).InnerText -split '\s+' | Where-Object {$_} | ForEach-Object {[double]::Parse($_,[Globalization.CultureInfo]::InvariantCulture)})
        $uv=@($part.SelectSingleNode(".//c:source[contains(@id,'-uv')]/c:float_array",$ns).InnerText -split '\s+' | Where-Object {$_} | ForEach-Object {[double]::Parse($_,[Globalization.CultureInfo]::InvariantCulture)})
        $ts=$part.SelectSingleNode('.//c:triangles',$ns);$count=[int]$ts.count;$triangles+=$count
        $indices=@($ts.p -split '\s+' | Where-Object {$_} | ForEach-Object {[int]$_})
        if($indices.Count -ne $count*9 -or $position.Count -ne $count*9 -or $normal.Count -ne $position.Count -or $uv.Count -ne $count*6){throw "Malformed arrays: $($part.name)"}
        foreach($v in $uv){if([double]::IsNaN($v) -or $v -lt 0 -or $v -gt 1){throw 'UV outside atlas'}}
        for($i=0;$i -lt $indices.Count;$i+=9){
            $p=@();foreach($j in 0,3,6){
                foreach($offset in 0,1,2){if($indices[$i+$j+$offset] -lt 0 -or $indices[$i+$j+$offset] -ge $count*3){throw 'Index outside attribute array'}}
                $k=$indices[$i+$j]*3
                $p+=,[Engine.Vector3]::new($position[$k],$position[$k+1],$position[$k+2])
            }
            $cross=[Engine.Vector3]::Cross(($p[1]-$p[0]),($p[2]-$p[0]))
            if($cross.LengthSquared() -lt 1e-16){throw 'Degenerate armor triangle'}
            $k=$indices[$i+1]*3;$n=[Engine.Vector3]::new($normal[$k],$normal[$k+1],$normal[$k+2])
            if([Engine.Vector3]::Dot([Engine.Vector3]::Normalize($cross),$n) -lt .999){throw 'Normal/winding mismatch'}
        }
    }
    if($triangles -ne $spec.Triangles){throw "Mobile geometry budget changed: $($spec.Name)/$triangles"}
    $stream=[IO.File]::OpenRead($path)
    try{$model=[Engine.Media.ModelData]::Load($stream)}finally{$stream.Dispose()}
    if($model.Bones.Count -ne $spec.Bones -or $model.Meshes.Count -ne $spec.Meshes){throw 'Target Engine importer bone/mesh mismatch'}
    $boneCounts=@{}
    foreach($mesh in $model.Meshes){$name=$model.Bones[$mesh.ParentBoneIndex].Name;$boneCounts[$name]=1+[int]$boneCounts[$name]}
    if($spec.Bones -eq 1){if(-not $boneCounts['Body']){throw 'Torso is not bound to Body'}}
    else{if(-not $boneCounts['Leg1'] -or -not $boneCounts['Leg2'] -or $boneCounts.Count -ne 2){throw 'Paired armor must have distinct Leg1/Leg2 geometry'}}
    $imported[$spec.Name]=$model;$total+=$triangles
    Write-Host "$($spec.Name): target ModelData.Load $($spec.Bones) bones / $($spec.Meshes) meshes / $triangles triangles; normals, winding, UV and attachment groups passed."
}
if($total -ne 3292){throw "Whole suit triangle budget changed: $total"}
Add-Type -AssemblyName System.Drawing
$atlas=[Drawing.Bitmap]::new((Join-Path $project 'Assets\Textures\FourSaintsArmorV3.png'))
try{
    if($atlas.Width -ne 256 -or $atlas.Height -ne 16){throw 'Shared armor atlas dimensions changed'}
    for($y=0;$y -lt 16;$y++){for($x=0;$x -lt 256;$x++){if($atlas.GetPixel($x,$y).A -ne 255){throw 'Shared material must be fully opaque'}}}
    if($atlas.GetPixel(8,8).R -ne 24 -or $atlas.GetPixel(8,8).G -ne 38 -or $atlas.GetPixel(8,8).B -ne 43){throw 'Shared dark lining differs from crown'}
}finally{$atlas.Dispose()}

# Inspect only in-memory native assets; nothing from Content.scpak is shipped.
Add-Type -AssemblyName System.IO.Compression
$reader=$game.GetType('Game.SurvivalCraftModEntity').GetMethod('GetDecipherStream',[Reflection.BindingFlags]'NonPublic,Static')
$content=[IO.File]::OpenRead((Join-Path $GameDir 'Content.scpak'));$decoded=$reader.Invoke($null,@($content));$archive=[IO.Compression.ZipArchive]::new($decoded)
try{
    foreach($gender in 'HumanMale','HumanFemale','OuterClothingMale','OuterClothingFemale'){
        $stream=$archive.GetEntry("Assets/Models/$gender.dae").Open()
        try{$human=[Engine.Media.ModelData]::Load($stream)}finally{$stream.Dispose()}
        foreach($boneName in 'Body','Leg1','Leg2'){
            $bone=$human.Bones | Where-Object Name -eq $boneName
            $bind=$bone.Transform;$parent=$bone.ParentBoneIndex
            while($parent -ge 0){$bind=$bind*$human.Bones[$parent].Transform;$parent=$human.Bones[$parent].ParentBoneIndex}
            $origin=$bind.Translation;$bind.Translation=[Engine.Vector3]::Zero
            $scale=$bind.Right.Length()/0.02413
            $xscale=if($boneName -eq 'Body'){$scale}else{1.0}
            $fit=[Engine.Matrix]::CreateScale([float]$xscale,[float]$scale,[float]$scale)
            $correction=$fit*[Engine.Matrix]::Invert($bind)
            foreach($pitch in @(0,.75,-.50,1.20,-1.40)){
                $pose=[Engine.Matrix]::CreateRotationX([float]$pitch)*[Engine.Matrix]::CreateRotationY(.43)*[Engine.Matrix]::CreateTranslation(13,2,-9)
                $view=[Engine.Matrix]::CreateLookAt([Engine.Vector3]::new(14,3,-12),[Engine.Vector3]::new(13,2,-9),[Engine.Vector3]::UnitY)
                $recovered=$correction*($bind*$pose*$view)*[Engine.Matrix]::Invert($view)
                $point=[Engine.Vector3]::new(.065,-.6,-.2)
                $actual=[Engine.Vector3]::Transform($point,$recovered);$expected=[Engine.Vector3]::Transform($point,($fit*$pose))
                if([Engine.Vector3]::Distance($actual,$expected) -gt .0001){throw "Basis/camera composition failed: $gender/$boneName/$pitch"}
            }
            if($boneName -ne 'Body'){
                $sole=[Engine.Vector3]::Transform([Engine.Vector3]::new(0,-.716,0),($correction*$bind*[Engine.Matrix]::CreateTranslation($origin)))
                if([Math]::Abs($sole.Y) -gt .01){throw "Boot sole is not within 1cm of native ground: $gender/$boneName"}
                # Actual per-leg attachment extents plus native hip offset must not cross the midline at rest.
                foreach($asset in 'BaihuLegArmorV3','ZhuqueBootsV3'){
                    $model=$imported[$asset]
                    foreach($mesh in $model.Meshes){
                        if($model.Bones[$mesh.ParentBoneIndex].Name -ne $boneName){continue}
                        $bound=$mesh.BoundingBox
                        $inner=if($boneName -eq 'Leg1'){$bound.Max.X+$origin.X}else{$bound.Min.X+$origin.X}
                        if(($boneName -eq 'Leg1' -and $inner -gt -.005) -or ($boneName -eq 'Leg2' -and $inner -lt .005)){throw "Armor crosses resting inter-leg clearance: $gender/$asset/$boneName"}
                    }
                }
            }else{
                $skirtBottom=.744*$scale+$origin.Y
                $hip=$human.Bones | Where-Object Name -eq 'Leg1'
                $hipY=[Engine.Vector3]::Transform($hip.Transform.Translation,$bind).Y+$origin.Y
                if($skirtBottom-$hipY -lt .02){throw 'Torso skirt extends into the native hip swing region'}
            }
        }
        Write-Host "${gender}: Body/Leg1/Leg2 basis and 15 synthetic camera/pose compositions, sole height and resting leg/skirt clearances passed (not device animation acceptance)."
    }
}finally{$archive.Dispose();$decoded.Dispose();$content.Dispose()}
Write-Host "Four Saints V3 whole suit: $total triangles; actual native import/calibration checks passed. Android and multiplayer remain pending owner acceptance."
