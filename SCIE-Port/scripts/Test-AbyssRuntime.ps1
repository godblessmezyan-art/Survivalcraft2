param([string]$GameDir,[string]$AssemblyPath)
$ErrorActionPreference='Stop'
foreach($f in @('Engine.dll','EntitySystem.dll','Survivalcraft.exe')){[void][Reflection.Assembly]::LoadFrom((Join-Path $GameDir $f))}
$g=[Reflection.Assembly]::LoadFrom((Join-Path $GameDir 'Survivalcraft.exe'))
$mod=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($AssemblyPath))
$decoder=@'
using System;using System.Collections.Generic;using System.Reflection;using System.Reflection.Emit;
public static class AbyssIL {
 static Dictionary<ushort,OpCode> codes=new Dictionary<ushort,OpCode>();
 static AbyssIL(){foreach(var f in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static)){var op=(OpCode)f.GetValue(null);codes[(ushort)op.Value]=op;}}
 public static string[] Calls(MethodInfo method){var names=new List<string>();foreach(var target in Methods(method))names.Add(target.Name);return names.ToArray();}
 public static MethodBase[] Methods(MethodBase method){var methods=new List<MethodBase>();var body=method.GetMethodBody();if(body==null)return methods.ToArray();byte[] il=body.GetILAsByteArray();
 for(int i=0;i<il.Length;){ushort key=il[i++];if(key==0xfe)key=(ushort)(0xfe00|il[i++]);var op=codes[key];int n=0;
 switch(op.OperandType){case OperandType.InlineNone:break;case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:n=1;break;
 case OperandType.InlineVar:n=2;break;case OperandType.InlineI8:case OperandType.InlineR:n=8;break;
 case OperandType.InlineSwitch:n=4+4*BitConverter.ToInt32(il,i);break;
 default:n=4;break;}
 if(op.OperandType==OperandType.InlineMethod)methods.Add(method.Module.ResolveMethod(BitConverter.ToInt32(il,i)));i+=n;}
 return methods.ToArray();}
}
'@
Add-Type -TypeDefinition $decoder
$registry=$mod.GetType('SCIEPort.Features.AbyssReaper.VoidPortalRegistry',$true)
# Check all registry methods/constructors, including escaping, validation and read helpers.
# The target game's XmlUtils.LoadXmlFromTextReader uses this exact reader factory and
# these three settings. Convenience methods may be stripped on the Android runtime.
$registryFlags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
$registryMethods=@($registry.GetMethods($registryFlags))+@($registry.GetConstructors($registryFlags))
foreach($method in $registryMethods) {
    foreach($call in [AbyssIL]::Methods($method)) {
        $owner=$call.DeclaringType.FullName
        if($owner -in @('System.Xml.XmlWriter','System.Xml.XmlWriterSettings','System.Xml.XmlConvert')) {
            throw "Portal persistence must not depend on stripped XML APIs: $($method.Name) -> $owner.$($call.Name)."
        }
        if($owner -eq 'System.Xml.XmlReader') {
            $parameters=@($call.GetParameters() | ForEach-Object { $_.ParameterType.FullName })
            if($call.Name -ne 'Create' -or ($parameters -join ',') -ne 'System.IO.TextReader,System.Xml.XmlReaderSettings') {
                throw "Portal reader uses an unaudited XML overload: $($method.Name) -> $call."
            }
        }
        if($owner -eq 'System.Xml.XmlReaderSettings' -and $call.Name -notin @('.ctor','set_CheckCharacters','set_IgnoreComments','set_IgnoreProcessingInstructions')) {
            throw "Portal reader uses a setting not retained by the native loader: $($method.Name) -> $call."
        }
    }
}
Write-Host 'Portal persistence IL: no XmlWriter/XmlWriterSettings/XmlConvert; reader calls limited to native loader primitives (not Android runtime proof).'
$input=$g.GetType('Game.ComponentInput')
$calls=[AbyssIL]::Calls($input.GetMethod('Update'))
$mouse=[Array]::IndexOf($calls,'UpdateInputFromMouseAndKeyboard');$touch=[Array]::IndexOf($calls,'UpdateInputFromWidgets')
if($mouse -lt 0 -or $touch -le $mouse){throw 'Native input assembly order changed'}
if([AbyssIL]::Calls($input.GetMethod('UpdateInputFromMouseAndKeyboard')) -notcontains 'HookAction'){throw 'Native early input hook placement changed'}
$instance=[Activator]::CreateInstance($mod.GetType('SCIEPort.Features.AbyssReaper.SubsystemAbyssReaper'))
$order=[int]$instance.UpdateOrder;$enum=$g.GetType('Game.UpdateOrder')
$nativeInput=[int][Enum]::Parse($enum,'Input');$player=[int][Enum]::Parse($enum,'Default')
if($order -le $nativeInput -or $order -ge $player){throw 'Abyss must sample after Input and before player actions'}
$loader=$mod.GetType('SCIEPort.Mod.SCIEPortLoader')
if([AbyssIL]::Calls($loader.GetMethod('UpdateInput')) -contains 'Capture'){throw 'Abyss is still consuming input before touch is assembled'}
if([AbyssIL]::Calls($instance.GetType().GetMethod('Update')) -notcontains 'Capture'){throw 'Post-input update must consume complete touch/gamepad input'}
$captureCalls=[AbyssIL]::Calls($instance.GetType().GetMethod('Capture'))
$lockCheck=[Array]::IndexOf($captureCalls,'IsActionLocked')
$comboInput=[Array]::IndexOf($captureCalls,'UpdateMeleeInput')
if($lockCheck -lt 0 -or $comboInput -le $lockCheck){throw 'Merged main must check Four Saints action lock before accepting Abyss combo input'}
$meleeInput=$instance.GetType().GetMethod('UpdateMeleeInput',[Reflection.BindingFlags]'Instance,NonPublic')
if([AbyssIL]::Calls($meleeInput) -notcontains 'Input'){throw 'The guarded melee adapter must drive the original combo state machine'}
Write-Host "Abyss real IL: MouseHook precedes Widgets; compiled sample order $order between Input $nativeInput and Player $player; no early Capture. Passed."
Write-Host 'Abyss merged main: Four Saints action lock is checked before combo input. Passed.'

$worldCut=$mod.GetType('SCIEPort.Features.AbyssReaper.WorldCutRuntime', $true)
$ownerSafe=$worldCut.GetMethod('OwnerSafe',[Reflection.BindingFlags]'Instance,NonPublic')
$ownerFailure=$worldCut.GetMethod('OwnerFailure',[Reflection.BindingFlags]'Instance,NonPublic')
if ([AbyssIL]::Calls($ownerSafe) -notcontains 'OwnerFailure') { throw 'WorldCut server authority must route ownership checks through OwnerFailure.' }
if ([AbyssIL]::Calls($ownerFailure) -notcontains 'IsActionLocked') { throw 'WorldCut server authority must enforce Four Saints action lock.' }
$skyCalls=[AbyssIL]::Calls($loader.GetMethod('ChangeSkyColor'))
foreach($name in @('get_SkyBlend','GetStormBlend','GetSkyBlend')) { if($skyCalls -notcontains $name) { throw "Merged sky composer lacks $name." } }
Write-Host 'WorldCut authority honors Four Saints lock; Abyss and existing artifact skies remain composed.'

$portalTravel=$mod.GetType('SCIEPort.Features.AbyssReaper.VoidPortalLocalTravel', $true)
$playerCheck=$portalTravel.GetMethod('Player',[Reflection.BindingFlags]'Instance,NonPublic')
if ([AbyssIL]::Calls($playerCheck) -notcontains 'IsActionLocked') { throw 'Portal travel must respect Four Saints action lock.' }
Write-Host 'Portal travel honors Four Saints action lock.'
