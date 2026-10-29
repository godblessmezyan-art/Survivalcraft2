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
 public static MethodBase[] Methods(MethodInfo method){var methods=new List<MethodBase>();byte[] il=method.GetMethodBody().GetILAsByteArray();
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
$serialize=$registry.GetMethod('Serialize',[Reflection.BindingFlags]'Instance,NonPublic')
$xmlCalls=@([AbyssIL]::Methods($serialize) | Where-Object { $_.DeclaringType.FullName -eq 'System.Xml.XmlWriter' -and $_.Name -eq 'Create' })
if($xmlCalls.Count -ne 1 -or $xmlCalls[0].GetParameters()[0].ParameterType.FullName -ne 'System.IO.TextWriter') {
    throw 'Portal save must call XmlWriter.Create(TextWriter, settings); StringBuilder overload is missing on Android/Mono.'
}
Write-Host 'Portal save compiled IL uses XmlWriter.Create(TextWriter, settings), avoiding the reported Android missing method.'
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
$comboInput=[Array]::IndexOf($captureCalls,'Input')
if($lockCheck -lt 0 -or $comboInput -le $lockCheck){throw 'Merged main must check Four Saints action lock before accepting Abyss combo input'}
Write-Host "Abyss real IL: MouseHook precedes Widgets; compiled sample order $order between Input $nativeInput and Player $player; no early Capture. Passed."
Write-Host 'Abyss merged main: Four Saints action lock is checked before combo input. Passed.'

$worldCut=$mod.GetType('SCIEPort.Features.AbyssReaper.WorldCutRuntime', $true)
$ownerSafe=$worldCut.GetMethod('OwnerSafe',[Reflection.BindingFlags]'Instance,NonPublic')
if ([AbyssIL]::Calls($ownerSafe) -notcontains 'IsActionLocked') { throw 'WorldCut server authority must enforce Four Saints action lock.' }
$skyCalls=[AbyssIL]::Calls($loader.GetMethod('ChangeSkyColor'))
foreach($name in @('get_SkyBlend','GetStormBlend','GetSkyBlend')) { if($skyCalls -notcontains $name) { throw "Merged sky composer lacks $name." } }
Write-Host 'WorldCut authority honors Four Saints lock; Abyss and existing artifact skies remain composed.'

$portalTravel=$mod.GetType('SCIEPort.Features.AbyssReaper.VoidPortalLocalTravel', $true)
$playerCheck=$portalTravel.GetMethod('Player',[Reflection.BindingFlags]'Instance,NonPublic')
if ([AbyssIL]::Calls($playerCheck) -notcontains 'IsActionLocked') { throw 'Portal travel must respect Four Saints action lock.' }
Write-Host 'Portal travel honors Four Saints action lock.'
