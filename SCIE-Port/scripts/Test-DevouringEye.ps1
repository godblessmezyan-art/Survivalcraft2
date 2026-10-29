[CmdletBinding()]
param([string]$GameDir = 'E:\SCNET\SCNETx25.01.17')
$ErrorActionPreference = 'Stop'
$recoveryRoot=Split-Path -Parent $PSScriptRoot
$recoveryProject=Join-Path $recoveryRoot 'src\SCIEPort'
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))
}
$engine=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
$game=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$load=$engine.GetType('Engine.Media.ModelData',$true).GetMethod('Load',[Type[]]@([IO.Stream]))
$stream=[IO.File]::OpenRead((Join-Path $recoveryProject 'Assets\Models\DevouringEye.dae'))
try {$model=$load.Invoke($null,@($stream))}finally{$stream.Dispose()}
$triangles=0;foreach($mesh in $model.Meshes){foreach($part in $mesh.MeshParts){$triangles+=$part.IndicesCount/3}}
if($model.Bones.Count -ne 1 -or $model.Meshes.Count -ne 27 -or $triangles -ne 816){throw 'Devouring model import regression.'}
[xml]$xml=Get-Content -Raw (Join-Path $recoveryProject 'Assets\Models\DevouringEye.dae')
$nodes=@($xml.SelectNodes("//*[local-name()='visual_scene']//*[local-name()='node']"))
if($xml.COLLADA.asset.up_axis -ne 'Y_UP' -or $nodes.Count -ne 1 -or $nodes[0].matrix.InnerText -ne '1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1'){throw 'Devouring transform contract.'}
foreach($array in $xml.SelectNodes("//*[local-name()='float_array'][contains(@id,'-uv-array')]")){
    foreach($value in $array.InnerText -split '\s+'){
        $uv=[double]::Parse($value,[Globalization.CultureInfo]::InvariantCulture)
        if($uv -lt 0 -or $uv -gt 1){throw 'Devouring invalid UV.'}
    }
}
Add-Type -AssemblyName System.Drawing
$png=[Drawing.Bitmap]::FromFile((Join-Path $recoveryProject 'Assets\Textures\DevouringEye.png'))
try {
    if($png.Width -ne 128 -or $png.Height -ne 16){throw 'Devouring palette dimensions.'}
    for($x=0;$x -lt $png.Width;$x++){for($y=0;$y -lt $png.Height;$y++){if($png.GetPixel($x,$y).A -ne 255){throw 'Devouring PNG must be opaque.'}}}
}finally{$png.Dispose()}
$painted=$game.GetType('Game.PaintedCubeBlock',$true)
foreach($name in @('GraniteBlock','CobblestoneBlock','PlanksBlock','SandstoneBlock','MarbleBlock','BasaltBlock','BricksBlock','ClayBlock')){
    if(-not $painted.IsAssignableFrom($game.GetType("Game.$name",$true))){throw "Unverified painted data schema: $name"}
}
$row=Import-Csv -Delimiter ';' (Join-Path $recoveryProject 'Assets\Blocks\DevouringEye.csv')
if($row.IsPlaceable -ne 'FALSE' -or $row.Durability -ne '-1' -or $row.DefaultMeleePower -ne '0'){throw 'Devouring item contract.'}
foreach($power in @('ShovelPower','QuarryPower','HackPower')){if($row.$power -ne '0'){throw "Devouring vanilla dig bypass: $power"}}
$loader=Get-Content -Raw (Join-Path $recoveryProject 'Mod\SCIEPortLoader.cs')
$loader=[regex]::Replace($loader,'\s+','').Replace('componentInput.m_playerInput','input.m_playerInput')
foreach($inputGuard in @('input.m_playerInput.Hit=null','input.m_playerInput.Dig=null','input.m_playerInput.Interact=input.m_playerInput.Interact??input.m_playerInput.Hit??input.m_playerInput.Dig')){
    if(-not $loader.Contains($inputGuard)){throw 'Devouring input must route vanilla hit/dig to the confirmation workflow.'}
}
$source=Get-Content -Raw (Join-Path $recoveryProject 'Features\DevouringEye\NativeRecovery.cs')
if($source -match '\b(DestroyCell|Despawn|DropAllItems|AddPickable)\s*\('){throw 'Devouring recovery must not enter death/drop/respawn APIs.'}
$refs=@('Engine.dll','EntitySystem.dll','Survivalcraft.exe')|ForEach-Object{Join-Path $GameDir $_}
$refs+=@('System.Xml.Linq.dll','System.Xml.dll','System.Core.dll')
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider
$parameters=New-Object System.CodeDom.Compiler.CompilerParameters
$parameters.GenerateInMemory=$true
foreach($reference in ($refs+@('System.dll'))){[void]$parameters.ReferencedAssemblies.Add($reference)}
$compiled=$compiler.CompileAssemblyFromSource($parameters,[IO.File]::ReadAllText((Join-Path $recoveryRoot 'tests\SCIEPort.DevouringEye.Tests\NativeApiProbe.cs')))
if($compiled.Errors.HasErrors){throw (($compiled.Errors|ForEach-Object ToString)-join "`n")}
$probe=$compiled.CompiledAssembly.GetType('DevouringNativeApiProbe',$true)
Add-Type -AssemblyName System.IO.Compression
$decoded=$probe.GetMethod('DecodeContent').Invoke($null,@(,[IO.File]::ReadAllBytes((Join-Path $GameDir 'Content.scpak'))))
$memory=New-Object IO.MemoryStream(,$decoded);$archive=New-Object IO.Compression.ZipArchive($memory)
try {
    $reader=New-Object IO.StreamReader($archive.GetEntry('Assets/Database.xml').Open())
    try{$databaseXml=[System.Xml.Linq.XElement]::Parse($reader.ReadToEnd())}finally{$reader.Dispose()}
    $probe.GetMethod('Run').Invoke($null,@($databaseXml))
    $probe.GetMethod('ResolveSerializerDependencies').Invoke($null,@($GameDir))
    # Windows PowerShell has no game binding redirects; serializer dependencies
    # are loaded only for the additional JSON probe, after native database setup.
    foreach($file in @('System.Buffers.dll','System.Memory.dll','System.Runtime.CompilerServices.Unsafe.dll','System.Threading.Tasks.Extensions.dll','Microsoft.Bcl.AsyncInterfaces.dll','MessagePack.Annotations.dll','MessagePack.dll')) {
        $dependency=Join-Path $GameDir $file
        if(Test-Path -LiteralPath $dependency){[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes($dependency))}
    }
    $probe.GetMethod('JsonProbe').Invoke($null,@())
}finally{$archive.Dispose();$memory.Dispose()}
Write-Host "Devouring Eye: target importer 1 bone / 27 meshes / $triangles triangles; opaque PNG, native mapping/transaction API fixtures passed."
