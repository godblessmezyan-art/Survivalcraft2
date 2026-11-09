# Development-only validator: uses the SAME parser/planner/executor as the mod.
# Requires PowerShell 7.5+ (.NET 9) and a Release build of the offline test runner.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Path,
    [string]$TestAssembly = (Join-Path $PSScriptRoot '../tests/BuilderAssistant.Tests/bin/Release/net8.0/BuilderAssistant.Tests.dll')
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$file=(Resolve-Path -LiteralPath $Path).Path
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $TestAssembly).Path)
$importer=$assembly.GetType('BuilderAssistant.Core.Blueprints.BlueprintImportService',$true)
$parse=$importer.GetMethod('FromString')
$serializer=$assembly.GetType('BuilderAssistant.Core.Blueprints.BlueprintJson',$true).GetMethod('Serialize')
function Get-Property($obj,[string]$name) { return ,($obj.GetType().GetProperty($name).GetValue($obj)) }
function Get-Field($obj,[string]$name) { $obj.GetType().GetField($name).GetValue($obj) }
function Read-Blueprint([string]$text) {
    $r=$parse.Invoke($null,[object[]]@($text,$null))
    if (!(Get-Field $r 'Success')) { throw "Actual importer rejected: $(Get-Field $r 'Reason')" }
    Get-Field $r 'Blueprint'
}
$bp=Read-Blueprint ([IO.File]::ReadAllText($file))
$count=Get-Property $bp 'CellCount'
$capabilities=Get-Content -LiteralPath (Join-Path $root 'docs/blueprint-block-capabilities.json') -Raw -Encoding utf8 | ConvertFrom-Json
$types=@{}
foreach ($b in $capabilities.blocks) { $types[$b.type]=$b }
$positions=@{}
foreach ($c in (Get-Property $bp 'Cells')) {
    $p=Get-Field $c 'Relative'
    $positions["$($p.X),$($p.Y),$($p.Z)"]=$c
}
$supports=0
foreach ($c in (Get-Property $bp 'Cells')) {
    $key=Get-Field $c 'MaterialKey';$d=Get-Field $c 'Data';$cap=$types[$key]
    if (!$cap -or $d -notin $cap.allowedData) { throw "Unsupported block/data: $key/$d" }
    if ($cap.requiresSupport) {
        $p=Get-Field $c 'Relative'
        if ($cap.family -in @('ladder','torch')) { throw 'This offline artifact validator currently checks downward supports only.' }
        $support=$positions["$($p.X),$($p.Y-1),$($p.Z)"]
        if (!$support) { throw "Missing support for $key at $p" }
        $supportKey=Get-Field $support 'MaterialKey'
        if ($cap.family -eq 'plant' -and $supportKey -notin @('GrassBlock','DirtBlock','SoilBlock')) { throw "Invalid soil for $key" }
        $supports++
    }
}
$posType=$assembly.GetType('BuilderAssistant.Core.CellPos',$true)
$origin=[Activator]::CreateInstance($posType,[object[]]@(0,30,0))
$terrainType=$assembly.GetType('BuilderAssistant.Tests.MaterialTerrain',$true)
$gridType=$assembly.GetType('BuilderAssistant.Tests.FakeTerrain',$true)
$resolverType=$assembly.GetType('BuilderAssistant.Tests.MaterialResolver',$true)
$taskType=$assembly.GetType('BuilderAssistant.Core.BuildEngine.BuildTask',$true)
$planner=$assembly.GetType('BuilderAssistant.Core.BuildEngine.BuildPlanner',$true).GetMethods() |
    Where-Object { $_.Name -eq 'Plan' -and $_.GetParameters().Count -eq 5 } | Select-Object -First 1
$roundTrips=0;$batches=0;$undoBatches=0;$maxBuildBatch=0;$maxRestoreBatch=0
foreach ($rotation in 0..3) {
    foreach ($mirror in @($false,$true)) {
        $rotated=$bp.GetType().GetMethod('Reorient').Invoke($bp,[object[]]@($rotation,$mirror))
        $decoded=Read-Blueprint ($serializer.Invoke($null,[object[]]@($rotated)))
        if ((Get-Property $decoded 'CellCount') -ne $count) { throw 'Orientation changed cell count' }
        $terrain=[Activator]::CreateInstance($terrainType,$true)
        $resolver=[Activator]::CreateInstance($resolverType,$true)
        $plan=$planner.Invoke($null,[object[]]@($decoded,$origin,$resolver,$terrain,$false))
        if (!(Get-Property $plan 'Success')) { throw "Plan failed: $(Get-Property $plan 'FailureReason')" }
        if ((Get-Field (Get-Field $terrain 'Grid') 'Writes') -ne 0) { throw 'Planning wrote terrain' }
        $changes=Get-Property $plan 'Changes'
        $task=[Activator]::CreateInstance($taskType,[object[]]@('artifact-check',$origin,$changes,$false))
        $guard=0
        while (!(Get-Property $task 'IsFinished')) {
            $before=Get-Field (Get-Field $terrain 'Grid') 'Writes'
            $applied=$taskType.GetMethod('Advance').Invoke($task,[object[]]@($terrain,32))
            $writes=(Get-Field (Get-Field $terrain 'Grid') 'Writes')-$before
            $maxBuildBatch=[Math]::Max($maxBuildBatch,$writes)
            if ($writes -gt 32 -or $applied -gt 32 -or ++$guard -gt 2000) { throw 'Batch budget / bounded progress failed' }
            $batches++
        }
        if ((Get-Property $task 'State').ToString() -ne 'Completed') { throw 'Execution did not complete' }
        $before=Get-Field (Get-Field $terrain 'Grid') 'Writes'
        $null=$taskType.GetMethod('Undo').Invoke($task,[object[]]@($terrain));$undoBatches++
        $writes=(Get-Field (Get-Field $terrain 'Grid') 'Writes')-$before
        $maxRestoreBatch=[Math]::Max($maxRestoreBatch,$writes)
        if ($writes -gt 32) { throw 'Undo batch exceeded 32' }
        while ((Get-Property $task 'PendingUndoCount') -gt 0) {
            $before=Get-Field (Get-Field $terrain 'Grid') 'Writes'
            $null=$taskType.GetMethod('RetryUndo').Invoke($task,[object[]]@($terrain));$undoBatches++
            $writes=(Get-Field (Get-Field $terrain 'Grid') 'Writes')-$before
            $maxRestoreBatch=[Math]::Max($maxRestoreBatch,$writes)
            if ($writes -gt 32) { throw 'Undo retry batch exceeded 32' }
            if ($undoBatches -gt 5000) { throw 'Unbounded undo' }
        }
        foreach ($change in $changes) {
            $p=Get-Field $change 'Absolute'
            $v=$gridType.GetMethod('ValueAt').Invoke((Get-Field $terrain 'Grid'),[object[]]@($p))
            if ($v -ne 0) { throw 'Undo left a modified cell' }
        }
        $roundTrips++
    }
}
# The exact artifact also exercises cancel/rollback and the default no-overwrite guard.
$terrain=[Activator]::CreateInstance($terrainType,$true)
$resolver=[Activator]::CreateInstance($resolverType,$true)
$plan=$planner.Invoke($null,[object[]]@($bp,$origin,$resolver,$terrain,$false))
$changes=Get-Property $plan 'Changes'
$task=[Activator]::CreateInstance($taskType,[object[]]@('cancel-check',$origin,$changes,$false))
for ($i=0;$i -lt 3;$i++) { $null=$taskType.GetMethod('Advance').Invoke($task,[object[]]@($terrain,32)) }
$null=$taskType.GetMethod('Cancel').Invoke($task,[object[]]@($terrain))
while ((Get-Property $task 'PendingRollbackCount') -gt 0) { $null=$taskType.GetMethod('RetryRollback').Invoke($task,[object[]]@($terrain)) }
foreach ($change in $changes) {
    if ($gridType.GetMethod('ValueAt').Invoke((Get-Field $terrain 'Grid'),[object[]]@((Get-Field $change 'Absolute'))) -ne 0) { throw 'Cancel left a modified cell' }
}
$terrain=[Activator]::CreateInstance($terrainType,$true)
$null=$gridType.GetMethod('Occupy').Invoke((Get-Field $terrain 'Grid'),[object[]]@((Get-Field $changes[0] 'Absolute'),123))
$blocked=$planner.Invoke($null,[object[]]@($bp,$origin,$resolver,$terrain,$false))
if ((Get-Property $blocked 'Success') -or (Get-Field (Get-Field $terrain 'Grid') 'Writes') -ne 0) { throw 'Non-air protection failed' }
[ordered]@{
    status='PASS';sha256=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    cells=$count;size=@((Get-Property $bp 'SizeX'),(Get-Property $bp 'SizeY'),(Get-Property $bp 'SizeZ'))
    orientationsRoundTripped=$roundTrips;buildBatches=$batches;undoBatches=$undoBatches;supportChecks=$supports
    maxBuildWritesPerBatch=$maxBuildBatch;maxUndoWritesPerBatch=$maxRestoreBatch
    cancellationRollback='PASS';nonAirProtection='PASS';parser='Actual BlueprintImportService / BlueprintJson'
    terrain='In-memory MaterialTerrain/FakeTerrain, no game process';android7723='待验证'
} | ConvertTo-Json -Depth 4
