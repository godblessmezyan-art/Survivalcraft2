using System;
using SCIEPort.Features.AbyssReaper;
internal static class PortalTests
{
    static int count;
    static void Check(bool value,string name) { count++;if(!value)throw new Exception("Portal: "+name); }
    internal static void Run()
    {
        Guid world=Guid.NewGuid(), owner=Guid.NewGuid(), a=Guid.NewGuid(), b=Guid.NewGuid();
        VoidPortalRegistry empty;
        Check(VoidPortalRegistry.TryDeserialize(new VoidPortalRegistry(world,12).Serialize(),out empty)
            && empty.Count==0 && empty.WorldId==world && empty.Limit==12,"empty new world persistence");
        var registry=new VoidPortalRegistry(world,2);
        Check(registry.TryAdd(a,owner,"家园 & 山",0,64,0,90),"create first");
        Check(!registry.TryAdd(a,owner,"duplicate",10,64,0,0),"duplicate id");
        Check(!registry.TryAdd(Guid.NewGuid(),owner,"overlap",1,64,1,0),"overlap");
        Check(!registry.TryAdd(Guid.NewGuid(),owner,"bad",float.NaN,64,0,0),"finite coordinates");
        Check(registry.TryAdd(b,owner,"远方",20,65,0,0),"create second");
        Check(!registry.TryAdd(Guid.NewGuid(),owner,"capacity",40,65,0,0),"limit");
        Check(!registry.SetLimit(1)&&registry.Count==2,"lower limit does not discard existing records");
        Check(registry.SetLimit(3)&&registry.Limit==3,"configured limit can be raised");
        Check(!registry.Link(a,a) && !registry.Link(a,Guid.NewGuid()),"invalid target");
        Check(registry.Link(a,b) && registry.Rename(b,"海底神殿"),"link and rename");
        string saved=registry.Serialize();VoidPortalRegistry loaded;
        Check(VoidPortalRegistry.TryDeserialize(saved,out loaded) && loaded.WorldId==world && loaded.Count==2,"roundtrip");
        VoidPortalRegistry.Portal portal;Check(loaded.TryGet(a,out portal) && portal.Target==b && portal.Name=="家园 & 山", "identity target unicode and escaping");
        Check(loaded.Remove(b) && loaded.TryGet(a,out portal) && portal.Target==Guid.Empty,"delete clears inbound links");
        Check(!VoidPortalRegistry.TryDeserialize(saved.Replace("version=\"1\"","version=\"2\""),out loaded),"reject future version");
        Check(!VoidPortalRegistry.TryDeserialize(saved.Replace("active=\"1\"","active=\"x\""),out loaded),"reject malformed flags");
        Check(!VoidPortalRegistry.TryDeserialize(saved.Substring(0,saved.Length-5),out loaded),"reject truncated save");
        Check(!VoidPortalRegistry.TryDeserialize("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///no'>]><voidPortals/>",out loaded),"reject external entity");
        Check(!VoidPortalRegistry.TryDeserialize(new string('x',VoidPortalRegistry.MaxDocument+1),out loaded),"bounded input");
        Check(!registry.Rename(a,"")&&!registry.Rename(a,new string('a',33)),"name bounds");
        Check(!registry.Rename(a,"bad\u0001name"),"reject invalid XML characters");
        var culture=System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            var fractional=new VoidPortalRegistry(world,64);
            for(int i=0;i<64;i++)
                Check(fractional.TryAdd(Guid.NewGuid(),owner,"星门 & <雪> \"北\" '南' 😀 "+i,i*8+.25f,64.5f,-.125f,90.25f),"max capacity escaped name "+i);
            string xml=fractional.Serialize();
            Check(!xml.StartsWith("<?xml") && xml.Contains("x=\"0.25\""),"declaration-free invariant XML");
            Check(VoidPortalRegistry.TryDeserialize(xml,out loaded) && loaded.Count==64 && loaded.WorldId==world,"full registry roundtrip under non-invariant culture");
            foreach(var entry in fractional.Records)
                Check(loaded.TryGet(entry.Id,out portal) && portal.Name==entry.Name && portal.Owner==entry.Owner
                    && portal.X==entry.X && portal.Y==entry.Y && portal.Z==entry.Z && portal.Facing==entry.Facing,"full registry fields preserved");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture=culture; }
        Check(registry.Link(a,Guid.Empty),"cancel target");
        Console.WriteLine("Void portal registry and persistence: "+count+" checks passed (no game world exercised).");
    }
}
