using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SCIEPort.Features.AbyssReaper
{
    // World-owned records only. Terrain checks, authorization, UI and movement belong to the runtime.
    internal sealed class VoidPortalRegistry
    {
        internal sealed class Portal
        {
            internal Guid Id, Owner, Target;
            internal string Name;
            internal float X, Y, Z, Facing;
            internal bool Active;
        }
        internal const int Version = 1, DefaultLimit = 12, HardLimit = 64, MaxDocument = 32768;
        private readonly Dictionary<Guid, Portal> records = new Dictionary<Guid, Portal>();
        internal Guid WorldId { get; private set; }
        internal int Count => records.Count;
        internal int Limit { get; private set; }
        internal IEnumerable<Portal> Records => records.Values;
        internal VoidPortalRegistry(Guid worldId, int limit)
        {
            if (worldId == Guid.Empty) throw new ArgumentException("worldId");
            WorldId = worldId; Limit = Math.Max(1, Math.Min(HardLimit, limit));
        }
        internal bool SetLimit(int limit)
        {
            int clamped=Math.Max(1,Math.Min(HardLimit,limit));
            if(clamped<records.Count)return false;
            Limit=clamped;return true;
        }
        internal static bool ValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 32 || name.Trim() != name) return false;
            // XML 1.0 characters, without XmlConvert helpers stripped from some Android builds.
            for(int i=0;i<name.Length;i++)
            {
                char c=name[i];
                if(c>=0xd800 && c<=0xdbff)
                {
                    if(++i>=name.Length || name[i]<0xdc00 || name[i]>0xdfff)return false;
                }
                else if((c<0x20 && c!='\t' && c!='\n' && c!='\r') || (c>=0xdc00 && c<=0xdfff) || c>=0xfffe)return false;
            }
            return true;
        }
        internal static bool ValidPosition(float x, float y, float z, float facing)
            => WorldCutRules.Coordinates(x, y, z) && WorldCutRules.Finite(facing) && Math.Abs(facing) <= 360f;
        internal bool TryAdd(Guid id, Guid owner, string name, float x, float y, float z, float facing)
        {
            if (id == Guid.Empty || owner == Guid.Empty || records.ContainsKey(id) || records.Count >= Limit || !ValidName(name) || !ValidPosition(x,y,z,facing)) return false;
            foreach (Portal p in records.Values)
                if (Math.Abs(p.X-x)<3f && Math.Abs(p.Y-y)<4f && Math.Abs(p.Z-z)<3f) return false;
            records.Add(id, new Portal { Id=id, Owner=owner, Name=name, X=x, Y=y, Z=z, Facing=facing, Active=true });
            return true;
        }
        internal bool Rename(Guid id, string name)
        {
            Portal p; if (!ValidName(name) || !records.TryGetValue(id,out p)) return false;
            p.Name=name; return true;
        }
        internal bool Link(Guid id, Guid target)
        {
            Portal p, destination;
            if (!records.TryGetValue(id,out p) || !p.Active) return false;
            if (target != Guid.Empty && (id == target || !records.TryGetValue(target,out destination) || !destination.Active)) return false;
            p.Target=target; return true;
        }
        internal bool Remove(Guid id)
        {
            if (!records.Remove(id)) return false;
            foreach (Portal p in records.Values) if (p.Target==id) p.Target=Guid.Empty;
            return true;
        }
        internal bool TryGet(Guid id, out Portal p) => records.TryGetValue(id,out p);
        internal string Serialize()
        {
            var b=new StringBuilder();
            // Fixed v1 schema: do not call XmlWriter, including its convenience overloads.
            // Android logs confirmed both Create(StringBuilder, ...) and WriteStartElement(string) missing.
            b.Append("<voidPortals");Attribute(b,"version",Version.ToString(CultureInfo.InvariantCulture));
            Attribute(b,"world",WorldId.ToString("D"));Attribute(b,"limit",Limit.ToString(CultureInfo.InvariantCulture));
            if(records.Count==0)b.Append(" />");
            else
            {
                b.Append('>');
                foreach (Portal p in records.Values)
                {
                    b.Append("<portal");Attribute(b,"id",p.Id.ToString("D"));Attribute(b,"owner",p.Owner.ToString("D"));
                    Attribute(b,"name",p.Name);Attribute(b,"x",p.X.ToString("R",CultureInfo.InvariantCulture));
                    Attribute(b,"y",p.Y.ToString("R",CultureInfo.InvariantCulture));Attribute(b,"z",p.Z.ToString("R",CultureInfo.InvariantCulture));
                    Attribute(b,"facing",p.Facing.ToString("R",CultureInfo.InvariantCulture));Attribute(b,"target",p.Target.ToString("D"));
                    Attribute(b,"active",p.Active?"1":"0");b.Append(" />");
                }
                b.Append("</voidPortals>");
            }
            if(b.Length>MaxDocument) throw new InvalidOperationException("Portal document exceeds limit.");
            return b.ToString();
        }
        private static void Attribute(StringBuilder output,string key,string value)
        {
            output.Append(' ').Append(key).Append("=\"");
            foreach(char c in value)
            {
                switch(c)
                {
                    case '&': output.Append("&amp;");break;
                    case '<': output.Append("&lt;");break;
                    case '>': output.Append("&gt;");break;
                    case '"': output.Append("&quot;");break;
                    // References preserve whitespace through XML attribute normalization.
                    case '\t': output.Append("&#x9;");break;
                    case '\n': output.Append("&#xA;");break;
                    case '\r': output.Append("&#xD;");break;
                    default: output.Append(c);break;
                }
            }
            output.Append('"');
        }
        private static string Attribute(XElement element,string name) => element.Attribute(name)?.Value;
        // A rejected/corrupt document is never partially installed and remains available to the caller.
        internal static bool TryDeserialize(string raw, out VoidPortalRegistry result)
        {
            result=null;
            // Keep method-resolution failures inside a separate call boundary. On an unknown
            // Android runtime the subsystem retains the original text and disables only portals.
            try { return ReadDocument(raw,out result); }
            catch(MissingMemberException) { return false; }
            catch(TypeLoadException) { return false; }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static bool ReadDocument(string raw,out VoidPortalRegistry result)
        {
            result=null;if(string.IsNullOrEmpty(raw)||raw.Length>MaxDocument)return false;
            // Reject DTDs before calling the native-backed parser, without optional settings setters.
            if(raw.IndexOf("<!DOCTYPE",StringComparison.OrdinalIgnoreCase)>=0)return false;
            try
            {
                // These exact calls are used by x25 EntitySystem's XmlUtils.LoadXmlFromTextReader.
                // Avoid XmlReader convenience methods and settings that the Android linker may strip.
                var settings=new XmlReaderSettings { CheckCharacters=true, IgnoreComments=true, IgnoreProcessingInstructions=true };
                using(var reader=XmlReader.Create(new StringReader(raw),settings))
                {
                    XElement root=XElement.Load(reader,LoadOptions.None);
                    if(root.Name.ToString()!="voidPortals" || Attribute(root,"version")!="1")return false;
                    Guid world;int limit;
                    if(!Guid.TryParse(Attribute(root,"world"),out world)||world==Guid.Empty || !int.TryParse(Attribute(root,"limit"),NumberStyles.None,CultureInfo.InvariantCulture,out limit)||limit<1||limit>HardLimit)return false;
                    var temp=new VoidPortalRegistry(world,limit);var targets=new Dictionary<Guid,Guid>();var active=new Dictionary<Guid,bool>();
                    foreach(XNode node in root.Nodes())
                    {
                        var element=node as XElement;
                        if(element==null)
                        {
                            var whitespace=node as XText;
                            if(whitespace!=null && !(node is XCData) && string.IsNullOrWhiteSpace(whitespace.Value))continue;
                            return false;
                        }
                        if(element.Name.ToString()!="portal" || !element.IsEmpty)return false;
                        Guid id,owner,target;float x,y,z,facing;string name=Attribute(element,"name"),enabled=Attribute(element,"active");
                        if(!Guid.TryParse(Attribute(element,"id"),out id)||!Guid.TryParse(Attribute(element,"owner"),out owner)||!Guid.TryParse(Attribute(element,"target"),out target)
                            || !float.TryParse(Attribute(element,"x"),NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!float.TryParse(Attribute(element,"y"),NumberStyles.Float,CultureInfo.InvariantCulture,out y)
                            || !float.TryParse(Attribute(element,"z"),NumberStyles.Float,CultureInfo.InvariantCulture,out z)||!float.TryParse(Attribute(element,"facing"),NumberStyles.Float,CultureInfo.InvariantCulture,out facing)
                            || (enabled!="0"&&enabled!="1") || !temp.TryAdd(id,owner,name,x,y,z,facing))return false;
                        targets.Add(id,target);active.Add(id,enabled=="1");
                    }
                    foreach(var pair in active)temp.records[pair.Key].Active=pair.Value;
                    foreach(var pair in targets)
                    {
                        if(pair.Value==Guid.Empty)continue;
                        Portal p,destination;
                        if(!temp.records.TryGetValue(pair.Key,out p)||!p.Active||pair.Key==pair.Value||!temp.records.TryGetValue(pair.Value,out destination)||!destination.Active)return false;
                        p.Target=pair.Value;
                    }
                    result=temp;return true;
                }
            }
            catch(Exception e) when(e is XmlException || e is ArgumentException || e is InvalidOperationException || e is FormatException) { return false; }
        }
    }
}
