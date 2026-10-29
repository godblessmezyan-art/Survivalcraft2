param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference='Stop'
# Run isolated Windows PowerShell. This never starts the game or builds a Mod.
$enginePath=Join-Path $GameDir 'Engine.dll'
$engineHash=(Get-FileHash -LiteralPath $enginePath -Algorithm SHA256).Hash
if($engineHash -ne '98F2035B98129259567E39ECAC46638226B46A842D3062D83AA934716E770694'){
    throw "Unverified x25.01.17 Engine.dll: $engineHash"
}
$engine=[Reflection.Assembly]::LoadFrom($enginePath)
$candidate=Join-Path $PSScriptRoot 'assets/AbyssEclipseCandidate.dae'
$manifest=Get-Content (Join-Path $PSScriptRoot 'assets/manifest.json') -Raw | ConvertFrom-Json
$stream=[IO.File]::OpenRead($candidate)
try{$model=$engine.GetType('Engine.Media.ModelData').GetMethod('Load',[type[]]@([IO.Stream])).Invoke($null,@($stream))}
finally{$stream.Dispose()}
$triangles=0
foreach($mesh in $model.Meshes){foreach($part in $mesh.MeshParts){$triangles+=$part.IndicesCount/3}}
if($model.Bones.Count -ne 1 -or $model.Meshes.Count -ne $manifest.meshes -or $triangles -ne $manifest.triangles){
    throw "Candidate native import mismatch: $($model.Bones.Count) / $($model.Meshes.Count) / $triangles"
}
$result=[ordered]@{status='passed';target='SCNET Net_x25.01.17 Windows Engine.Media.ModelData.Load';
    engineSha256=$engineHash;candidateSha256=(Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash;
    bones=$model.Bones.Count;meshes=$model.Meshes.Count;triangles=$triangles;runtimeIntegrated=$true;
    android='pending';releaseBuild='see SCIE-Port/docs/TEST-RESULTS.md for main 0.47.0 build evidence'}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'native-import.json'), (($result | ConvertTo-Json) -replace "`r`n","`n") + "`n", [Text.UTF8Encoding]::new($false))
Write-Host "PASS native candidate import: $($model.Bones.Count) bone / $($model.Meshes.Count) meshes / $triangles triangles. Runtime copy checked by Test-AbyssGeometry.py; no game process started."
