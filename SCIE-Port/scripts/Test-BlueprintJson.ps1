[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Path,
    [Parameter(Mandatory=$true)][string]$GameDir,
    [Parameter(Mandatory=$true)][string]$ModDll,
    [Parameter(Mandatory=$true)][string]$ReportPath,
    [string]$PortableOutput
)
$ErrorActionPreference='Stop'
foreach($file in @('Engine.dll','EntitySystem.dll','LiteNetLib.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $file))}
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModDll).Path)
$importer=$assembly.GetType('SCIEPort.Features.Blueprints.Core.Blueprints.BlueprintImportService',$true)
$parse=$importer.GetMethod('FromString',[Reflection.BindingFlags]'Public,Static')
$catalog=$assembly.GetType('SCIEPort.Features.Blueprints.Core.Blueprints.VanillaBlockCapabilities',$true)
$resolve=$catalog.GetMethod('TryGetByMaterialKey',[Reflection.BindingFlags]'Public,Static')
$source=(Resolve-Path -LiteralPath $Path).Path
$raw=[IO.File]::ReadAllBytes($source)
$utf8=New-Object Text.UTF8Encoding($false,$true)
if($raw.Length -ge 3 -and $raw[0] -eq 239 -and $raw[1] -eq 187 -and $raw[2] -eq 191){throw 'Cloud blueprints must be UTF-8 without BOM.'}
$parsed=$parse.Invoke($null,[object[]]@($utf8.GetString($raw),$null))
if(-not $parsed.GetType().GetField('Success').GetValue($parsed)){throw $parsed.GetType().GetField('Detail').GetValue($parsed)}
$bp=$parsed.GetType().GetField('Blueprint').GetValue($parsed)
$cells=$bp.GetType().GetProperty('Cells').GetValue($bp)
$keys=@{}
foreach($cell in $cells){
    $ct=$cell.GetType();$key=[string]$ct.GetField('MaterialKey').GetValue($cell);$data=[int]$ct.GetField('Data').GetValue($cell);$family=$ct.GetField('Family').GetValue($cell)
    $signature=$key+'/'+$data+'/'+$family
    if($keys.ContainsKey($signature)){continue}
    $argsForResolve=[object[]]@($key,$null,$null)
    if(-not $resolve.Invoke($null,$argsForResolve)){throw "Unsupported material: $key"}
    $cap=$argsForResolve[1];$capType=$cap.GetType()
    if($capType.GetField('Family').GetValue($cap) -ne $family -or -not $capType.GetMethod('IsValidData').Invoke($cap,@($data))){throw "Unsupported family/DATA: $signature"}
    $keys[$signature]=$true
}
if($PortableOutput){
    # Expand only already validated structural templates. No guessed IDs or native furniture state.
    $cellType=$assembly.GetType('SCIEPort.Features.Blueprints.Core.Blueprints.BlueprintCell',$true)
    $listType=[Collections.Generic.List``1].MakeGenericType($cellType)
    $flat=[Activator]::CreateInstance($listType)
    foreach($cell in $cells){$flat.Add($cell)}
    $normalize=$bp.GetType().GetMethod('Normalize',[Reflection.BindingFlags]'NonPublic,Static')
    $bp=$normalize.Invoke($null,[object[]]@($bp.GetType().GetProperty('Name').GetValue($bp),3,$flat))
    $json=$assembly.GetType('SCIEPort.Features.Blueprints.Core.Blueprints.BlueprintJson',$true).GetMethod('Serialize').Invoke($null,@($bp))
    $source=[IO.Path]::GetFullPath($PortableOutput)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($source)) | Out-Null
    [IO.File]::WriteAllText($source,$json,$utf8)
    $raw=[IO.File]::ReadAllBytes($source)
    $reparsed=$parse.Invoke($null,[object[]]@($utf8.GetString($raw),$null))
    if(-not $reparsed.GetType().GetField('Success').GetValue($reparsed)){throw 'Portable blueprint failed the actual importer.'}
    $roundTrip=$reparsed.GetType().GetField('Blueprint').GetValue($reparsed)
    $expected=@{}
    foreach($cell in $cells){$t=$cell.GetType();$expected[[string]$t.GetField('Relative').GetValue($cell)]=[string]$t.GetField('MaterialKey').GetValue($cell)+'|'+$t.GetField('Family').GetValue($cell)+'|'+$t.GetField('Data').GetValue($cell)}
    $decodedCells=$roundTrip.GetType().GetProperty('Cells').GetValue($roundTrip)
    if($decodedCells.Count -ne $expected.Count){throw 'Portable export changed the cell count.'}
    foreach($cell in $decodedCells){
        $t=$cell.GetType();$p=[string]$t.GetField('Relative').GetValue($cell)
        $signature=[string]$t.GetField('MaterialKey').GetValue($cell)+'|'+$t.GetField('Family').GetValue($cell)+'|'+$t.GetField('Data').GetValue($cell)
        if(-not $expected.ContainsKey($p) -or $expected[$p] -ne $signature){throw 'Portable export changed geometry, material, family or DATA.'}
    }
}
$size=@{};foreach($axis in @('X','Y','Z')){$size[$axis.ToLowerInvariant()]=[int]$bp.GetType().GetProperty('Size'+$axis).GetValue($bp)}
$count=[int]$bp.GetType().GetProperty('CellCount').GetValue($bp)
$sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash($raw)).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
$report=[ordered]@{
    status='PASS';validator='SCIEPort.BlueprintImportService + material capability catalog';source=$source
    sha256=$hash;byteLength=$raw.Length;name=$bp.GetType().GetProperty('Name').GetValue($bp)
    schemaVersion=[int]$bp.GetType().GetProperty('SchemaVersion').GetValue($bp);kind='builderassistant.blueprint';size=$size;blockCount=$count
    modVersion=$assembly.GetName().Version.ToString();modDllSha256=(Get-FileHash -LiteralPath $ModDll -Algorithm SHA256).Hash.ToLowerInvariant()
    furnitureExpanded=[bool]$PortableOutput;android7723='待验证';liveMultiplayer='待验证'
}
$reportFile=[IO.Path]::GetFullPath($ReportPath);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($reportFile)) | Out-Null
[IO.File]::WriteAllText($reportFile,($report | ConvertTo-Json -Depth 6),$utf8)
Write-Host "Blueprint validated: $count cells; $($size.x)x$($size.y)x$($size.z); SHA256=$hash"
