[CmdletBinding()]
param([string]$GameDir = 'E:\SCNET\SCNETx25.01.17')
$ErrorActionPreference = 'Stop'
$poseidonRoot = Split-Path -Parent $PSScriptRoot
$poseidonProject = Join-Path $poseidonRoot 'src\SCIEPort'
$poseidonEngine = [Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'EntitySystem.dll'))
$poseidonGame = [Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$poseidonStream = [IO.File]::OpenRead((Join-Path $poseidonProject 'Assets\Models\PoseidonTrident.dae'))
try { $poseidonModel = $poseidonEngine.GetType('Engine.Media.ModelData',$true).GetMethod('Load',[Type[]]@([IO.Stream])).Invoke($null,@($poseidonStream)) }
finally { $poseidonStream.Dispose() }
if ($poseidonModel.Bones.Count -ne 1 -or $poseidonModel.Meshes.Count -ne 83) { throw 'Poseidon target importer must produce one bone / 83 meshes.' }
$poseidonTriangles = 0
$poseidonMinY = [single]::PositiveInfinity; $poseidonMaxY = [single]::NegativeInfinity
foreach ($mesh in $poseidonModel.Meshes) { foreach ($part in $mesh.MeshParts) { $poseidonTriangles += $part.IndicesCount/3 } }
foreach ($mesh in $poseidonModel.Meshes) { $poseidonMinY=[Math]::Min($poseidonMinY,$mesh.BoundingBox.Min.Y);$poseidonMaxY=[Math]::Max($poseidonMaxY,$mesh.BoundingBox.Max.Y) }
if ([Math]::Abs($poseidonMinY+1.31) -gt 0.001 -or [Math]::Abs($poseidonMaxY-1.84) -gt 0.001) { throw 'Actual imported trident bounds changed.' }
if ($poseidonTriangles -ne 1504) { throw "Unexpected triangle count: $poseidonTriangles" }
[xml]$poseidonXml = Get-Content -Raw (Join-Path $poseidonProject 'Assets\Models\PoseidonTrident.dae')
if ($poseidonXml.COLLADA.asset.up_axis -ne 'Y_UP') { throw 'Poseidon requires Y_UP.' }
$poseidonNodes = @($poseidonXml.SelectNodes("//*[local-name()='visual_scene']//*[local-name()='node']"))
if ($poseidonNodes.Count -ne 1 -or $poseidonNodes[0].matrix.InnerText -ne '1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1') { throw 'Unbaked transforms.' }
foreach ($name in @('SovereignMainBlade','PortProng','StarboardProng','HeartOfTheOcean','MainDeepBlueInlay')) {
    if (-not $poseidonXml.SelectSingleNode("//*[local-name()='geometry'][@name='$name']")) { throw "Missing authored shape: $name" }
}
foreach ($array in $poseidonXml.SelectNodes("//*[local-name()='float_array']")) {
    foreach ($number in ($array.InnerText -split '\s+')) {
        $v = [double]::Parse($number,[Globalization.CultureInfo]::InvariantCulture)
        if ([double]::IsNaN($v) -or [double]::IsInfinity($v)) { throw 'Non-finite model number.' }
        if ($array.id -like '*-uv-array' -and ($v -lt 0 -or $v -gt 1)) { throw 'UV outside opaque palette.' }
    }
}
Add-Type -AssemblyName System.Drawing
$poseidonBitmap = [Drawing.Bitmap]::FromFile((Join-Path $poseidonProject 'Assets\Textures\PoseidonTrident.png'))
try {
    if ($poseidonBitmap.Width -ne 256 -or $poseidonBitmap.Height -ne 16) { throw 'Expected POT 256x16 palette.' }
    for($x=0;$x -lt 256;$x++) { for($y=0;$y -lt 16;$y++) { if($poseidonBitmap.GetPixel($x,$y).A -ne 255){throw 'Weapon texture must be opaque.'} } }
} finally { $poseidonBitmap.Dispose() }
$poseidonHealth = $poseidonGame.GetType('Game.ComponentHealth',$true)
$poseidonSwim = $poseidonGame.GetType('Game.ComponentLocomotion',$true).GetProperty('SwimSpeed')
if (-not $poseidonHealth.GetProperty('Air').CanWrite -or -not $poseidonSwim.CanWrite) { throw 'Actual breathing/swimming ABI mismatch.' }
if (-not $poseidonGame.GetType('Game.ComponentBody',$true).GetMethod('ApplyImpulse')) { throw 'Missing authoritative native impulse.' }
$poseidonBlock = $poseidonGame.GetType('Game.Block',$true)
foreach ($t in $poseidonGame.GetTypes()) {
    if ($t.IsSubclassOf($poseidonBlock)) {
        $field=$t.GetField('Index',[Reflection.BindingFlags]'Public,Static,DeclaredOnly')
        if ($field -and $field.IsLiteral -and $field.GetRawConstantValue() -eq 951) { throw "Native ID 951 occupied by $t" }
    }
}
# Constant native ID getter IL is read without invoking package constructors or opening networking.
$poseidonPackage = $poseidonGame.GetType('Game.NetWork.IPackage',$true)
$poseidonNativePackages=0
foreach ($t in $poseidonGame.GetTypes()) {
    if ($t.IsAbstract -or -not $poseidonPackage.IsAssignableFrom($t) -or $t.IsInterface) { continue }
    $il=$t.GetProperty('ID').GetGetMethod().GetMethodBody().GetILAsByteArray()
    $nativeId=-1
    if ($il.Length -eq 6 -and $il[0] -eq 0x20) { $nativeId=[BitConverter]::ToInt32($il,1) }
    elseif ($il.Length -eq 3 -and $il[0] -eq 0x1f) { $nativeId=[int]$il[1] }
    elseif ($il.Length -eq 2 -and $il[0] -ge 0x16 -and $il[0] -le 0x1e) { $nativeId=[int]$il[0]-0x16 }
    if ($nativeId -lt 0 -or $il[$il.Length-1] -ne 0x2a) { throw "Cannot audit native package getter without guessing: $t" }
    if ($nativeId -eq 246) { throw "Native network ID 246 occupied: $t" }
    $poseidonNativePackages++
}
$poseidonIds=@{}; $poseidonNetIds=@{}
foreach ($file in Get-ChildItem (Join-Path $poseidonProject 'Features') -Recurse -Filter *.cs) {
    $source=[IO.File]::ReadAllText($file.FullName)
    foreach ($m in [regex]::Matches($source,'public const int Index\s*=\s*(\d+)')) {
        $id=[int]$m.Groups[1].Value
        if ($poseidonIds.ContainsKey($id)){throw "Duplicate block ID $id"};$poseidonIds[$id]=$file.Name
    }
    foreach ($m in [regex]::Matches($source,'public byte ID\s*=>\s*(\d+)')) {
        $id=[int]$m.Groups[1].Value
        if ($poseidonNetIds.ContainsKey($id)){throw "Duplicate network ID $id"};$poseidonNetIds[$id]=$file.Name
    }
}
if ($poseidonIds[951] -ne 'PoseidonBlock.cs' -or $poseidonNetIds[246] -ne 'PoseidonPackage.cs') { throw 'Independent IDs missing.' }
$poseidonSource=[IO.File]::ReadAllText((Join-Path $poseidonProject 'Features\Poseidon\SubsystemPoseidon.cs'))
if ($poseidonSource -match '\b(ChangeCell|DestroyCell|SetCellValueFast|RemoveEntity|AddEntity|DamageActiveTool)\s*\(') { throw 'Poseidon must not alter terrain/entities/durability.' }
foreach ($guard in @('if (!Authority || !Equipped(p)', 'if (Authority) Query', 'c.Hit.Add', 'PoseidonRules.MaxCandidates', 'ApplyPassive(s,false)', 'm_hud?.Dispose()', 'Wall(sightOrigin', 'ApplyImpulse(desired -')) {
    if (-not $poseidonSource.Contains($guard)) { throw "Missing safety contract: $guard" }
}
$poseidonPacket=[IO.File]::ReadAllText((Join-Path $poseidonProject 'Features\Poseidon\PoseidonPackage.cs'))
foreach ($guard in @('player.Entity.EntityId == Caster','From == node.Server','Kind == 0')) { if(-not $poseidonPacket.Contains($guard)){throw "Missing packet authority: $guard"} }
$poseidonRow=Import-Csv -Delimiter ';' (Join-Path $poseidonProject 'Assets\Blocks\Poseidon.csv')
if($poseidonRow.IsPlaceable -ne 'FALSE' -or $poseidonRow.Durability -ne '-1' -or $poseidonRow.DefaultCreativeData -ne '0'){throw 'Item contract mismatch.'}
if (($poseidonMaxY-$poseidonMinY)*[single]$poseidonRow.DefaultIconViewScale -gt 1.65) { throw 'Icon exceeds conservative native 1.8-unit viewport envelope.' }
Write-Host "Poseidon target Engine import: 1 bone / 83 meshes / $poseidonTriangles triangles; native package getters audited: $poseidonNativePackages. Opaque palette, baked Y_UP, icon envelope, ABI, IDs and static safety contracts passed (not live gameplay)."
