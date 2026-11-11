using System;
using System.IO;
using System.Reflection;

internal static class Program
{
    private static int Main(string[] args)
    {
        string game=Path.GetFullPath(args[0]),mod=Path.GetFullPath(args[1]);
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>
        {
            string name=new AssemblyName(e.Name).Name;
            string path=name=="SCIEPort"?mod:Path.Combine(game,name=="Survivalcraft"?"Survivalcraft.exe":name+".dll");
            return File.Exists(path)?Assembly.LoadFrom(path):null;
        };
        try { BlueprintTests.Run();return 0; } catch(Exception ex) { Console.Error.WriteLine(ex);return 1; }
    }
}
