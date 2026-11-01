param([Parameter(Mandatory=$true)][string]$GameDir,[string]$AssemblyPath,[string]$Package)
$ErrorActionPreference='Stop';$root=Split-Path $PSScriptRoot
foreach($file in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$modelManifest=Get-Content (Join-Path $root 'docs/assets/excalibur/model.json') -Raw -Encoding UTF8|ConvertFrom-Json
$icons=Get-Content (Join-Path $root 'docs/assets/excalibur/icons.json') -Raw -Encoding UTF8|ConvertFrom-Json
$avalon=Get-Content (Join-Path $root 'docs/assets/excalibur-v2/avalon.json') -Raw -Encoding UTF8|ConvertFrom-Json
$required=@{}
foreach($p in $modelManifest.files.PSObject.Properties){$required[$p.Name]=$p.Value}
foreach($p in $icons.icons.PSObject.Properties){$required[$p.Value.asset]=$p.Value.sha256}
$required[$avalon.asset]=$avalon.sha256
if((Get-FileHash -LiteralPath (Join-Path $root ('docs/assets/excalibur-v2/'+$avalon.source))).Hash -ne $avalon.sourceSha256){throw 'Avalon imagegen original changed'}
if((Get-FileHash -LiteralPath (Join-Path $root 'src/SCIEPort/Assets/Textures/ExcaliburLake.png')).Hash -ne $avalon.unchangedSkill1Sha256){throw 'Requested unchanged skill 1 icon drifted'}
$s=[IO.File]::OpenRead((Join-Path $root $avalon.asset));try{$im=[Engine.Media.Image]::Load($s)}finally{$s.Dispose()}
if($im.Width -ne 256 -or $im.Height -ne 256 -or $im.GetPixel(0,0).A -ne 0 -or $im.GetPixel(128,128).A -lt 230){throw 'Avalon native icon alpha/dimensions'}
foreach($path in $required.Keys){if((Get-FileHash -LiteralPath (Join-Path $root $path)).Hash -ne $required[$path]){throw "Excalibur asset hash drift: $path"}}
if((Get-FileHash -LiteralPath (Join-Path $root ('docs/assets/excalibur/'+$icons.source))).Hash -ne $icons.sourceSha256){throw 'Imagegen original changed'}
$stream=[IO.File]::OpenRead((Join-Path $root 'src/SCIEPort/Assets/Models/Excalibur.dae'))
try{$model=[Engine.Media.ModelData]::Load($stream)}finally{$stream.Dispose()}
$triangles=0;foreach($m in $model.Meshes){foreach($p in $m.MeshParts){$triangles+=$p.IndicesCount/3}}
if($model.Bones.Count -ne 1 -or $model.Meshes.Count -ne $modelManifest.meshes -or $triangles -ne $modelManifest.triangles){throw 'Excalibur actual native mesh import failed'}
foreach($icon in $icons.icons.PSObject.Properties){$s=[IO.File]::OpenRead((Join-Path $root $icon.Value.asset));try{$im=[Engine.Media.Image]::Load($s)}finally{$s.Dispose()};if($im.Width -ne 256 -or $im.Height -ne 256 -or $im.GetPixel(0,0).A -ne 0 -or $im.GetPixel(128,128).A -lt 230){throw 'Excalibur native icon alpha/dimensions'}}
foreach($cue in @(@('Charge',2.8),@('Release',1.2))){
 $s=[IO.File]::OpenRead((Join-Path $root ('src/SCIEPort/Assets/Audio/Excalibur/'+$cue[0]+'.wav')))
 try{$sound=[Engine.Media.SoundData]::Load($s)}finally{$s.Dispose()}
 if($sound.ChannelsCount -ne 1 -or $sound.SamplingFrequency -ne 44100 -or [Math]::Abs($sound.Data.Length-44100*$cue[1]) -gt 1){throw 'Excalibur native PCM decode/duration'}
}
$source=Get-ChildItem (Join-Path $root 'src/SCIEPort/Features/Excalibur') -Filter *.cs|ForEach-Object{[IO.File]::ReadAllText($_.FullName)}
if(($source -join "`n") -match '\b(ChangeCell|DestroyCell|SetCellValueFast)\s*\('){throw 'Excalibur may not modify terrain'}
if($Package){
 Add-Type -AssemblyName System.IO.Compression.FileSystem;$archive=[IO.Compression.ZipFile]::OpenRead($Package)
 try{
  foreach($path in $required.Keys){$entry=$archive.GetEntry($path.Replace('src/SCIEPort/',''));if(!$entry){throw "Missing $path"};$s=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash($s)).Replace('-','')}finally{$s.Dispose();$sha.Dispose()};if($hash -ne $required[$path]){throw "Package asset hash: $path"}}
  foreach($name in @('Assets/Blocks/Excalibur.csv','Assets/Database/Excalibur.xdb','SCIEPort.dll')){if(!$archive.GetEntry($name)){throw "Missing $name"}}
 }finally{$archive.Dispose()}
}
Write-Host "Excalibur: native import $($model.Bones.Count) bone / $($model.Meshes.Count) meshes / $triangles triangles; imagegen/PNG/audio/model hashes, no-terrain and package checks passed."
if(!$AssemblyPath){return}
$mod=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($AssemblyPath))
$compiler=New-Object Microsoft.CSharp.CSharpCodeProvider;$options=New-Object System.CodeDom.Compiler.CompilerParameters;$options.GenerateInMemory=$true
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void]$options.ReferencedAssemblies.Add((Join-Path $GameDir $f))};foreach($f in @('System.dll','System.Core.dll','System.Xml.dll','System.Xml.Linq.dll')){[void]$options.ReferencedAssemblies.Add($f)}
$files=[string[]]@((Join-Path $root 'tests/SCIEPort.Abyss.Tests/AbyssHostNativeProbe.cs'),(Join-Path $root 'tests/SCIEPort.Excalibur.Tests/ExcaliburNativeProbe.cs'))
$result=$compiler.CompileAssemblyFromFile($options,$files);if($result.Errors.HasErrors){throw (($result.Errors|ForEach-Object ToString)-join "`n")}
try{$result.CompiledAssembly.GetType('ExcaliburNativeProbe',$true).GetMethod('Run').Invoke($null,@($mod))}catch{Write-Host $_.Exception.ToString();throw}
