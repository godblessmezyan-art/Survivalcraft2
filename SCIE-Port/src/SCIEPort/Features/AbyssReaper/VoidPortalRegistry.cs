using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

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
            try { XmlConvert.VerifyXmlChars(name); return true; } catch (Exception e) when (e is XmlException || e is ArgumentException) { return false; }
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
            var settings=new XmlWriterSettings { OmitXmlDeclaration=true, ConformanceLevel=ConformanceLevel.Document };
            using(var writer=XmlWriter.Create(b,settings))
            {
                writer.WriteStartElement("voidPortals");writer.WriteAttributeString("version",Version.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("world",WorldId.ToString("D"));writer.WriteAttributeString("limit",Limit.ToString(CultureInfo.InvariantCulture));
                foreach (Portal p in records.Values)
                {
                    writer.WriteStartElement("portal");writer.WriteAttributeString("id",p.Id.ToString("D"));writer.WriteAttributeString("owner",p.Owner.ToString("D"));
                    writer.WriteAttributeString("name",p.Name);writer.WriteAttributeString("x",p.X.ToString("R",CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("y",p.Y.ToString("R",CultureInfo.InvariantCulture));writer.WriteAttributeString("z",p.Z.ToString("R",CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("facing",p.Facing.ToString("R",CultureInfo.InvariantCulture));writer.WriteAttributeString("target",p.Target.ToString("D"));
                    writer.WriteAttributeString("active",p.Active?"1":"0");writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            if(b.Length>MaxDocument) throw new InvalidOperationException("Portal document exceeds limit.");
            return b.ToString();
        }
        // A rejected/corrupt document is never partially installed and remains available to the caller.
        internal static bool TryDeserialize(string raw, out VoidPortalRegistry result)
        {
            result=null;if(string.IsNullOrEmpty(raw)||raw.Length>MaxDocument)return false;
            try
            {
                var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit, XmlResolver=null, MaxCharactersInDocument=MaxDocument, IgnoreWhitespace=true };
                using(var reader=XmlReader.Create(new StringReader(raw),settings))
                {
                    reader.MoveToContent();if(reader.Name!="voidPortals" || reader.GetAttribute("version")!="1")return false;
                    Guid world;int limit;
                    if(!Guid.TryParse(reader.GetAttribute("world"),out world)||world==Guid.Empty || !int.TryParse(reader.GetAttribute("limit"),NumberStyles.None,CultureInfo.InvariantCulture,out limit)||limit<1||limit>HardLimit)return false;
                    var temp=new VoidPortalRegistry(world,limit);var targets=new Dictionary<Guid,Guid>();var active=new Dictionary<Guid,bool>();
                    if(reader.IsEmptyElement){reader.Read();if(reader.MoveToContent()!=XmlNodeType.None)return false;result=temp;return true;}
                    reader.ReadStartElement("voidPortals");
                    while(reader.NodeType==XmlNodeType.Element)
                    {
                        if(reader.Name!="portal" || !reader.IsEmptyElement)return false;
                        Guid id,owner,target;float x,y,z,facing;string name=reader.GetAttribute("name"),enabled=reader.GetAttribute("active");
                        if(!Guid.TryParse(reader.GetAttribute("id"),out id)||!Guid.TryParse(reader.GetAttribute("owner"),out owner)||!Guid.TryParse(reader.GetAttribute("target"),out target)
                            || !float.TryParse(reader.GetAttribute("x"),NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!float.TryParse(reader.GetAttribute("y"),NumberStyles.Float,CultureInfo.InvariantCulture,out y)
                            || !float.TryParse(reader.GetAttribute("z"),NumberStyles.Float,CultureInfo.InvariantCulture,out z)||!float.TryParse(reader.GetAttribute("facing"),NumberStyles.Float,CultureInfo.InvariantCulture,out facing)
                            || (enabled!="0"&&enabled!="1") || !temp.TryAdd(id,owner,name,x,y,z,facing))return false;
                        targets.Add(id,target);active.Add(id,enabled=="1");reader.Read();
                    }
                    reader.ReadEndElement();if(reader.MoveToContent()!=XmlNodeType.None)return false;
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
