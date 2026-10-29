using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using SCIEPort.Features.AbyssReaper;
internal static class PortalTests
{
    static int count;
    static void Check(bool value,string name) { count++;if(!value)throw new Exception("Portal: "+name); }
    static void Reject(string xml,string name)
    {
        VoidPortalRegistry result;
        Check(!VoidPortalRegistry.TryDeserialize(xml,out result) && result==null,name+" leaves no partial registry");
    }
    // Independent old-format producer: keep XmlWriter here, never in the shipped mod.
    static string LegacySave(VoidPortalRegistry registry)
    {
        var buffer=new StringBuilder();
        using(var text=new StringWriter(buffer,CultureInfo.InvariantCulture))
        using(var writer=XmlWriter.Create(text,new XmlWriterSettings { OmitXmlDeclaration=true }))
        {
            writer.WriteStartElement("voidPortals");
            writer.WriteAttributeString("version","1");
            writer.WriteAttributeString("world",registry.WorldId.ToString("D"));
            writer.WriteAttributeString("limit",registry.Limit.ToString(CultureInfo.InvariantCulture));
            foreach(var p in registry.Records)
            {
                writer.WriteStartElement("portal");
                writer.WriteAttributeString("id",p.Id.ToString("D"));
                writer.WriteAttributeString("owner",p.Owner.ToString("D"));
                writer.WriteAttributeString("name",p.Name);
                writer.WriteAttributeString("x",p.X.ToString("R",CultureInfo.InvariantCulture));
                writer.WriteAttributeString("y",p.Y.ToString("R",CultureInfo.InvariantCulture));
                writer.WriteAttributeString("z",p.Z.ToString("R",CultureInfo.InvariantCulture));
                writer.WriteAttributeString("facing",p.Facing.ToString("R",CultureInfo.InvariantCulture));
                writer.WriteAttributeString("target",p.Target.ToString("D"));
                writer.WriteAttributeString("active",p.Active?"1":"0");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        return buffer.ToString();
    }
    static void FormatRegression(Guid world,Guid owner)
    {
        Guid first=Guid.NewGuid(),second=Guid.NewGuid();
        string name="雪\t&\r<\n\"' > 😀";
        var source=new VoidPortalRegistry(world,12);
        Check(source.TryAdd(first,owner,name,-1.25f,65.5f,0.125f,-90.25f),"whitespace and XML punctuation name");
        Check(source.TryAdd(second,owner,"destination",24,64,0,0) && source.Link(first,second),"legacy linked fixture");
        string legacy=LegacySave(source),current=source.Serialize();
        foreach(string xml in new[]{legacy,current})
        {
            VoidPortalRegistry read;VoidPortalRegistry.Portal p;
            Check(VoidPortalRegistry.TryDeserialize(xml,out read) && read.Count==2 && read.WorldId==world,"old and new writer formats read");
            Check(read.TryGet(first,out p) && p.Name==name && p.Owner==owner && p.Target==second && p.Active
                && p.X==-1.25f && p.Y==65.5f && p.Z==0.125f && p.Facing==-90.25f,"old and new writer fields survive");
            var root=XElement.Parse(xml);
            Check(root.Name=="voidPortals" && (string)root.Element("portal").Attribute("name")==name,"independent parser sees exact attribute text");
        }
        VoidPortalRegistry empty;
        Check(VoidPortalRegistry.TryDeserialize(LegacySave(new VoidPortalRegistry(world,12)),out empty) && empty.Count==0,"legacy self-closing empty root");
        Check(VoidPortalRegistry.TryDeserialize(current.Replace("active=\"1\"","active=\"0\"").Replace(second.ToString("D")+"\" active",Guid.Empty.ToString("D")+"\" active"),out empty),"inactive unlinked records survive");
        Reject(legacy+legacy,"multiple roots");
        Reject(legacy.Replace("version=\"1\"","version=\"1\" version=\"1\""),"duplicate attributes");
        Reject(legacy.Replace("</voidPortals>","<extra /></voidPortals>"),"unexpected child");
        Reject(legacy.Replace("</voidPortals>","unexpected text</voidPortals>"),"unexpected text");
        Reject(legacy.Replace("</voidPortals>","<![CDATA[unexpected]]></voidPortals>"),"unexpected CDATA");
        Reject(legacy.Replace("<voidPortals ","<voidPortals xmlns=\"urn:other\" "),"unexpected namespace");
        Reject(legacy.Replace("id=\""+second,"id=\""+first),"duplicate record id after valid first record");
        Reject(legacy.Replace("target=\""+second,"target=\""+Guid.NewGuid()),"dangling target after complete parse");
        Reject(legacy.Replace("target=\""+second,"target=\""+first),"self target after complete parse");
        Reject(legacy.Replace("active=\"1\"","active=\"0\""),"inactive linked records");
        Reject(legacy.Replace("x=\"-1.25\"","x=\"NaN\""),"NaN coordinate");
        Reject(legacy.Replace("x=\"-1.25\"","x=\"Infinity\""),"infinite coordinate");
        Reject(legacy.Replace("owner=\""+owner,"owner=\""+Guid.Empty),"empty owner");
        Reject(legacy.Replace("name=\"","name=\"&#x1;"),"invalid numeric XML character");
        Reject(legacy.Replace("name=\"","name=\"&unknown;"),"undefined entity");
        Reject("<!DOCTYPE voidPortals [<!ENTITY e 'expanded'>]>"+legacy,"internal DTD rejected before parsing");
        Reject("<!DOCTYPE voidPortals SYSTEM 'file:///no'>"+legacy,"external DTD rejected before parsing");
        Reject("<!DOCTYPE voidPortals [<!ENTITY % e SYSTEM 'file:///no'>%e;]>"+legacy,"parameter entity DTD");
        var nested=new StringBuilder();for(int i=0;i<128;i++)nested.Append("<nested>");
        for(int i=0;i<128;i++)nested.Append("</nested>");
        Reject(legacy.Replace("</voidPortals>",nested+"</voidPortals>"),"nested unexpected content");
        Reject(new string(' ',VoidPortalRegistry.MaxDocument)+legacy,"size bound before parser");
        foreach(string invalid in new[]{"a\0b","a\u0001b","a\u000bb","a\ufffeb","a\uffffb","a\ud800b","a\udc00b","a\udc00\ud800b"})
            Check(!VoidPortalRegistry.ValidName(invalid),"invalid XML character or surrogate rejected");
        foreach(string valid in new[]{"a\tb","a\nb","a\rb","a\ud800\udc00b","a\udbff\udfffb","a\u007fb","a\ud7ffb","a\ue000b","a\ufffdb"})
        {
            Check(source.Rename(first,valid),"valid XML boundary character accepted");
            VoidPortalRegistry read;VoidPortalRegistry.Portal p;
            Check(VoidPortalRegistry.TryDeserialize(source.Serialize(),out read) && read.TryGet(first,out p) && p.Name==valid,"valid XML boundary character survives save");
        }
    }
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
        FormatRegression(world,owner);
        Console.WriteLine("Void portal registry and persistence: "+count+" checks passed (no game world exercised).");
    }
}
