using System;
using SCIEPort.Features.AbyssReaper;
internal static class PortalTests
{
    static int count;
    static void Check(bool value,string name) { count++;if(!value)throw new Exception("Portal: "+name); }
    internal static void Run()
    {
        Guid world=Guid.NewGuid(), owner=Guid.NewGuid(), a=Guid.NewGuid(), b=Guid.NewGuid();
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
        Check(registry.Link(a,Guid.Empty),"cancel target");
        Console.WriteLine("Void portal registry and persistence: "+count+" checks passed (no game world exercised).");
    }
}
