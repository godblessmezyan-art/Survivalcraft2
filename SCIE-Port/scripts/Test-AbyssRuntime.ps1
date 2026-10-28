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
 public static string[] Calls(MethodInfo method){var names=new List<string>();byte[] il=method.GetMethodBody().GetILAsByteArray();
 for(int i=0;i<il.Length;){ushort key=il[i++];if(key==0xfe)key=(ushort)(0xfe00|il[i++]);var op=codes[key];int n=0;
 switch(op.OperandType){case OperandType.InlineNone:break;case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:n=1;break;
 case OperandType.InlineVar:n=2;break;case OperandType.InlineI8:case OperandType.InlineR:n=8;break;
 case OperandType.InlineSwitch:n=4+4*BitConverter.ToInt32(il,i);break;
 default:n=4;break;}
 if(op.OperandType==OperandType.InlineMethod)names.Add(method.Module.ResolveMethod(BitConverter.ToInt32(il,i)).Name);i+=n;}
 return names.ToArray();}
}
'@
Add-Type -TypeDefinition $decoder
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
Write-Host "Abyss real IL: MouseHook precedes Widgets; compiled sample order $order between Input $nativeInput and Player $player; no early Capture. Passed."
