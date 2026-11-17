using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

internal static class PickerChecks
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    internal static void Run(Assembly mod,string repo,Action<bool,string> check)
    {
        PickerJniChecks.Run(mod,check);
        byte[] expected=File.ReadAllBytes(Path.Combine(repo,"SCIE-Port/src/SCIEPort/Features/CelestialCodex/Android/DocumentPicker.dex"));
        using(Stream input=mod.GetManifestResourceStream("SCIEPort.CelestialCodex.DocumentPicker.dex"))
        using(var actual=new MemoryStream())
        {
            check(input!=null,"original Android picker DEX embedded in the one SCIEPort.dll");
            input.CopyTo(actual);
            using(SHA256 sha=SHA256.Create()) check(BitConverter.ToString(sha.ComputeHash(actual.ToArray()))==BitConverter.ToString(sha.ComputeHash(expected)),"embedded picker bytes match the pinned runtime asset");
        }
        foreach(AssemblyName name in mod.GetReferencedAssemblies())
            check(name.Name!="Mono.Android"&&name.Name!="Java.Interop"&&!name.Name.StartsWith("AndroidX",StringComparison.Ordinal),"Android bridge has no static platform DLL dependency");
        Type picker=mod.GetType("SCIEPort.Features.CelestialCodex.Mod.BookFilePicker",true);
        MethodInfo open=picker.GetMethod("OpenSelected",F),release=picker.GetMethod("Release",F);
        string file=Path.Combine(Path.GetTempPath(),"codex-picker-"+Guid.NewGuid().ToString("N")+".txt");
        try
        {
            byte[] text=System.Text.Encoding.UTF8.GetBytes("本地图书\n正文");File.WriteAllBytes(file,text);
            using(Stream selected=(Stream)open.Invoke(null,new object[]{file}))
            using(var memory=new MemoryStream())
            {
                selected.CopyTo(memory);check(System.Text.Encoding.UTF8.GetString(memory.ToArray())=="本地图书\n正文","selected system path opens directly without Storage virtual-path rejection");
            }
            release.Invoke(null,new object[]{file});check(File.Exists(file),"temporary cleanup never deletes a user-selected original file");
            bool opaqueRejected=false;
            try{open.Invoke(null,new object[]{"content://provider/opaque-id"});}
            catch(TargetInvocationException){opaqueRejected=true;}
            check(opaqueRejected,"opaque content URI is never treated as a filesystem path; native provider must copy it first");
        }
        finally{File.Delete(file);}
    }
}
