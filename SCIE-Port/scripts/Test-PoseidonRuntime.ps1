[CmdletBinding()]
param(
    [string]$GameDir = 'E:\SCNET\SCNETx25.01.17',
    [string]$BuiltAssembly = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\build\SCIEPort.dll')
)
$ErrorActionPreference='Stop'
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Engine.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'EntitySystem.dll'))
$poseidonGame=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$poseidonAssembly=[Reflection.Assembly]::LoadFrom($BuiltAssembly)
$poseidonPacketType=$poseidonAssembly.GetType('SCIEPort.Features.Poseidon.PoseidonPackage',$true)
$poseidonFields=[Reflection.BindingFlags]'Instance,NonPublic'
$poseidonChecks=0
foreach ($kind in @(0,1,2)) {
    $poseidonPacket=[Activator]::CreateInstance($poseidonPacketType)
    $poseidonVectorType=$poseidonGame.GetType('Game.ComponentBody',$true).GetProperty('Position').PropertyType
    $poseidonVectorConstructor=$poseidonVectorType.GetConstructor([Type[]]@([single],[single],[single]))
    $poseidonValues=@{
        Kind=[byte]$kind;Skill=[byte]2;Caster=[uint16]65534;Sequence=[uint32]::MaxValue;
        Origin=$poseidonVectorConstructor.Invoke([object[]]@([single]12.5,[single]63.25,[single]-300.5));
        Direction=$poseidonVectorConstructor.Invoke([object[]]@([single]0,[single]0,[single]-1));
        DashRemaining=[single]7.125;WaveRemaining=[single]25.75;Water=$true
    }
    foreach($name in $poseidonValues.Keys){$poseidonPacketType.GetField($name,$poseidonFields).SetValue($poseidonPacket,$poseidonValues[$name])}
    $poseidonWriter=[Activator]::CreateInstance($poseidonGame.GetType('Game.NetWork.PackageStreamWriter',$true))
    $poseidonPacket.WriteData($poseidonWriter)
    if($poseidonWriter.BaseStream.Length -ne 41){throw 'Poseidon wire payload must be exactly 41 bytes before native compression.'}
    $poseidonBytes=$poseidonWriter.Data()
    $poseidonReader=$poseidonGame.GetType('Game.NetWork.PackageStreamReader',$true).GetConstructor([Type[]]@([byte[]])).Invoke([object[]]@(,$poseidonBytes))
    $poseidonDecoded=[Activator]::CreateInstance($poseidonPacketType)
    try {
        $poseidonDecoded.ReadData($poseidonReader)
        foreach($name in $poseidonValues.Keys){
            if(-not $poseidonPacketType.GetField($name,$poseidonFields).GetValue($poseidonDecoded).Equals($poseidonValues[$name])){throw "Native compressed packet round trip failed: $name"}
            $poseidonChecks++
        }
        if($poseidonReader.BaseStream.Position -ne $poseidonReader.BaseStream.Length){throw 'Unread packet data.'}
    } finally {$poseidonReader.Dispose();$poseidonWriter.Dispose()}
}
# Real compiled subsystem constructor and queue lifecycle, without pretending a game world was loaded.
$poseidonRuntimeType=$poseidonAssembly.GetType('SCIEPort.Features.Poseidon.SubsystemPoseidon',$true)
$poseidonRuntime=[Activator]::CreateInstance($poseidonRuntimeType)
$poseidonEnqueue=$poseidonRuntimeType.GetMethod('Enqueue',$poseidonFields)
for($i=0;$i -lt 100;$i++){[void]$poseidonEnqueue.Invoke($poseidonRuntime,[object[]]@($null,$poseidonPacket))}
$poseidonQueue=$poseidonRuntimeType.GetField('m_pending',$poseidonFields).GetValue($poseidonRuntime)
if($poseidonQueue.Count -ne 32){throw 'Queue flood escaped the 32-request cap.'}
$poseidonRuntime.Dispose()
if($poseidonQueue.Count -ne 0){throw 'Dispose did not clear queued references.'}
[void]$poseidonEnqueue.Invoke($poseidonRuntime,[object[]]@($null,$poseidonPacket))
if($poseidonQueue.Count -ne 0){throw 'Disposed world accepted a late callback.'}
Write-Host "Poseidon compiled-runtime checks: $poseidonChecks native compressed field round trips, 41-byte layout, queue flood/cap/Dispose/late-callback passed (no game world or device exercised)."
