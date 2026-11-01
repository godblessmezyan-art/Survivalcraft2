using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

// Desktop regression + compiled-call audit. Does not claim to emulate Android's BCL.
public static class GuidCompatibilityProbe
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception("GUID compatibility: "+label);}
    public static void Run(Assembly mod)
    {
        var type=mod.GetType("SCIEPort.Compatibility.LegacyGuid",true);
        var parse=type.GetMethod("TryParse",BindingFlags.Static|BindingFlags.NonPublic);
        var expected=new Guid("01234567-89ab-cdef-0123-456789abcdef");
        foreach(string format in new[]{"N","D","B","P","X"})foreach(string text in new[]{expected.ToString(format),expected.ToString(format).ToUpperInvariant()," \t"+expected.ToString(format)+"\r\n"})
        {
            object[] args={text,Guid.Empty};Check((bool)parse.Invoke(null,args)&&(Guid)args[1]==expected,"legacy constructor preserves valid "+format+" save identifiers");
        }
        object[] zero={Guid.Empty.ToString("N"),expected};Check((bool)parse.Invoke(null,zero)&&(Guid)zero[1]==Guid.Empty,"empty GUID remains parseable for unlinked portal targets");
        foreach(string text in new[]{null,""," ","not-guid","01234567-89ab-cdef-0123-456789abcde","01234567-89ab-cdef-0123-456789abcdeg","{0x100000000,0x89ab,0xcdef,{0x01,0x23,0x45,0x67,0x89,0xab,0xcd,0xef}}",new string('f',1024)})
        {
            object[] args={text,expected};Check(!(bool)parse.Invoke(null,args)&&(Guid)args[1]==Guid.Empty,"invalid identifier fails closed with an empty out value");
        }
        var opcodes=new Dictionary<ushort,OpCode>();foreach(var f in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static)){var op=(OpCode)f.GetValue(null);opcodes[(ushort)op.Value]=op;}
        int methods=0,calls=0,shimCalls=0;bool constructor=false;
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
        foreach(var owner in mod.GetTypes())
        {
            var members=new List<MethodBase>();members.AddRange(owner.GetMethods(flags));members.AddRange(owner.GetConstructors(flags));
            foreach(var method in members)
            {
                var body=method.GetMethodBody();if(body==null)continue;methods++;var bytes=body.GetILAsByteArray();
                for(int i=0;i<bytes.Length;)
                {
                    ushort key=bytes[i++];if(key==0xfe)key=(ushort)(0xfe00|bytes[i++]);var op=opcodes[key];int size;
                    switch(op.OperandType)
                    {
                        case OperandType.InlineNone:size=0;break;
                        case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:size=1;break;
                        case OperandType.InlineVar:size=2;break;
                        case OperandType.InlineI8:case OperandType.InlineR:size=8;break;
                        case OperandType.InlineSwitch:size=4+4*BitConverter.ToInt32(bytes,i);break;
                        default:size=4;break;
                    }
                    if(op.OperandType==OperandType.InlineMethod)
                    {
                        var info=method as MethodInfo;
                        var target=method.Module.ResolveMethod(BitConverter.ToInt32(bytes,i),owner.GetGenericArguments(),info!=null&&info.IsGenericMethod?info.GetGenericArguments():null);calls++;
                        if(target.DeclaringType==typeof(Guid)&&(target.Name=="TryParse"||target.Name=="TryParseExact"||target.Name=="Parse"||target.Name=="ParseExact"))throw new Exception("Unsupported Guid parsing reference in "+owner.FullName+"."+method.Name);
                        if(target.DeclaringType==type&&target.Name=="TryParse")shimCalls++;
                        if(owner==type&&target.DeclaringType==typeof(Guid)&&target.IsConstructor&&target.GetParameters().Length==1&&target.GetParameters()[0].ParameterType==typeof(string))constructor=true;
                    }
                    i+=size;
                }
            }
        }
        Check(methods>0&&calls>0,"entire compiled mod inspected");Check(shimCalls==5,"codex and all four portal identifier reads use the shim");Check(constructor,"shim binds to Guid(string), not the missing API");
        Console.WriteLine("GUID runtime compatibility: "+checks+" checks passed; "+methods+" compiled methods / "+calls+" call sites scanned, no Guid Parse/TryParse references. Android retest pending.");
    }
}
