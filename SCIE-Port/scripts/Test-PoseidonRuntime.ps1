[CmdletBinding()]
param([string]$GameDir='E:\SCNET\SCNETx25.01.17',[string]$BuiltAssembly=(Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\build\SCIEPort.dll'))
$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'EntitySystem.dll'))
$poseidonGame=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$poseidonAssembly=[Reflection.Assembly]::LoadFrom($BuiltAssembly)
$poseidonFlags=[Reflection.BindingFlags]'Instance,NonPublic'
$poseidonVector=$poseidonGame.GetType('Game.ComponentBody').GetProperty('Position').PropertyType.GetConstructor([Type[]]@([single],[single],[single]))
function Vec([single]$x,[single]$y,[single]$z){$poseidonVector.Invoke([object[]]@($x,$y,$z))}
$poseidonChecks=0
foreach($v2 in @($false,$true)){
    $packetType=$poseidonAssembly.GetType($(if($v2){'SCIEPort.Features.Poseidon.PoseidonV2Package'}else{'SCIEPort.Features.Poseidon.PoseidonPackage'}),$true)
    foreach($kind in @(0,1,2)){
        $values=@{Kind=[byte]$kind;Skill=[byte]$(if($v2){3}else{2});Caster=[uint16]65534;Sequence=[uint32]::MaxValue;Origin=(Vec 12.5 63.25 -300.5);Direction=(Vec 0 0 -1);Water=$true}
        if($v2){$values.Cooldowns=Vec 7 5 60;$values.Shield=[single]5;$values.DomainRemaining=[single]20;$values.DomainOrigin=Vec -10 60 30;$values.DomainDirection=Vec 1 0 0;$values.DomainWater=$true}
        else{$values.DashRemaining=[single]7.125;$values.WaveRemaining=[single]25.75}
        $packet=[Activator]::CreateInstance($packetType)
        foreach($name in $values.Keys){$packetType.GetField($name,$poseidonFlags).SetValue($packet,$values[$name])}
        $writer=[Activator]::CreateInstance($poseidonGame.GetType('Game.NetWork.PackageStreamWriter',$true))
        $packet.WriteData($writer)
        $length=if($v2){78}else{41}
        if($writer.BaseStream.Length -ne $length){throw "Poseidon wire layout changed: $($writer.BaseStream.Length), expected $length"}
        $bytes=$writer.Data();$reader=$poseidonGame.GetType('Game.NetWork.PackageStreamReader',$true).GetConstructor([Type[]]@([byte[]])).Invoke([object[]]@(,$bytes))
        try{
            $decoded=[Activator]::CreateInstance($packetType);$decoded.ReadData($reader)
            foreach($name in $values.Keys){if(-not $packetType.GetField($name,$poseidonFlags).GetValue($decoded).Equals($values[$name])){throw "Roundtrip: $name"};$poseidonChecks++}
            if($reader.BaseStream.Position -ne $reader.BaseStream.Length){throw 'Unread trailing fields'}
        }finally{$reader.Dispose();$writer.Dispose()}
    }
}
$type=$poseidonAssembly.GetType('SCIEPort.Features.Poseidon.SubsystemPoseidon',$true)
$runtime=[Activator]::CreateInstance($type);$enqueue=$type.GetMethod('Enqueue',$poseidonFlags)
for($i=0;$i -lt 100;$i++){[void]$enqueue.Invoke($runtime,[object[]]@($null,$packet))}
$queue=$type.GetField('m_pending',$poseidonFlags).GetValue($runtime)
if($queue.Count -ne 32){throw 'Network queue cap'}
$runtime.Dispose();if($queue.Count -ne 0){throw 'Dispose did not clear queue'}
[void]$enqueue.Invoke($runtime,[object[]]@($null,$packet));if($queue.Count -ne 0){throw 'Late callback accepted'}
$health=$poseidonGame.GetType('Game.ComponentHealth',$true)
if(-not $health.GetProperty('IsInvulnerable').CanRead){throw 'Native shield invulnerability guard missing'}
$overlay=$poseidonGame.GetType('Game.ComponentScreenOverlays',$true)
if(-not $overlay.GetMethod('DrawOverlay')){throw 'Native ocean overlay ABI missing'}
# Validate real compiled circular Widget transform, including nonuniform scale.
$hitType=$poseidonAssembly.GetType('SCIEPort.Features.Poseidon.PoseidonSkillHitTarget',$true)
$hit=[Activator]::CreateInstance($hitType,$true)
$hit.Measure([Engine.Vector2]::new(68,68))
$vector2=[Engine.Vector2]::new(68,68);$hit.Arrange([Engine.Vector2]::Zero,$vector2)
if(-not $hit.HitTest([Engine.Vector2]::new(34,34)) -or $hit.HitTest([Engine.Vector2]::new(0,0))){throw 'Native circular HitTest mismatch'}
Write-Host "Poseidon V2: $poseidonChecks real compressed field roundtrips; legacy 41 / V2 78 bytes; queue lifecycle, shield/overlay ABI and native circular HitTest passed. Android pending."
