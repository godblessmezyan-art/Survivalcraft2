using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CelestialCodex.Core
{
    internal interface IFileStore
    {
        void Create();
        IEnumerable<string> Names();
        long Size(string name);
        DateTime Modified(string name);
        Stream Read(string name);
        Stream Write(string name);
        void Move(string source, string destination);
        void Delete(string name);
    }
    internal sealed class BoundedCache
    {
        private sealed class Entry { internal string Name; internal long Size; internal DateTime Modified; }
        private static readonly object Gate = new object();
        private readonly IFileStore files;
        internal BoundedCache(IFileStore files) { this.files = files; }
        internal static string Key(string input)
        {
            using (var hash = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input); byte[] digest = hash.ComputeHash(bytes);
                var text = new StringBuilder(); foreach (byte b in digest) text.Append(b.ToString("x2")); return text.ToString() + ".json";
            }
        }
        private static bool Managed(string name)
        {
            if (name == "position.json") return true;
            if (name == null || name.Length != 69 || !name.EndsWith(".json", StringComparison.Ordinal)) return false;
            for (int i = 0; i < 64; i++) if (!(name[i] >= 'a' && name[i] <= 'f') && !(name[i] >= '0' && name[i] <= '9')) return false;
            return true;
        }
        internal static byte[] ReadLimited(Stream input, int limit)
        {
            using (var output = new MemoryStream())
            {
                var buffer = new byte[4096]; int read;
                while ((read = input.Read(buffer, 0, Math.Min(buffer.Length, limit + 1 - (int)output.Length))) > 0)
                { output.Write(buffer, 0, read); if (output.Length > limit) throw new InvalidDataException("File size"); }
                return output.ToArray();
            }
        }
        internal byte[] Read(string key)
        {
            if (!Managed(key)) throw new InvalidDataException("Cache key");
            lock (Gate)
            {
                try { if (files.Size(key) > Limits.MaxFileBytes) { files.Delete(key); return null; } using (Stream stream = files.Read(key)) return ReadLimited(stream, Limits.MaxFileBytes); }
                catch (Exception) { return null; }
            }
        }
        internal void Delete(string key) { if (!Managed(key)) return; lock (Gate) { try { files.Delete(key); } catch (Exception) { } } }
        internal bool Write(string key, byte[] bytes)
        {
            if (!Managed(key) || bytes == null || bytes.Length > Limits.MaxFileBytes) return false;
            lock (Gate)
            {
                try
                {
                    files.Create(); var entries = new List<Entry>(); long total = 0; int scanned = 0;
                    foreach (string name in files.Names())
                    {
                        if (++scanned > 1024) throw new InvalidDataException("Cache directory limit");
                        if (name.EndsWith(".tmp", StringComparison.Ordinal)) { files.Delete(name); continue; }
                        if (!Managed(name)) continue;
                        if (name == key) continue;
                        long size = files.Size(name); if (size > Limits.MaxFileBytes) { files.Delete(name); continue; }
                        entries.Add(new Entry { Name = name, Size = size, Modified = files.Modified(name) }); total += size;
                    }
                    while (entries.Count >= Limits.MaxFiles || total + bytes.Length > Limits.MaxTotalBytes)
                    {
                        int victim = -1;
                        for (int i = 0; i < entries.Count; i++)
                            if (entries[i].Name != "position.json" && (victim < 0 || entries[i].Modified.CompareTo(entries[victim].Modified) < 0)) victim = i;
                        if (victim < 0) throw new InvalidDataException("Cache capacity");
                        Entry entry = entries[victim]; total -= entry.Size; files.Delete(entry.Name); entries.RemoveAt(victim);
                    }
                    using (Stream output = files.Write(key + ".tmp")) output.Write(bytes, 0, bytes.Length);
                    files.Move(key + ".tmp", key); return true;
                }
                catch (Exception) { try { files.Delete(key + ".tmp"); } catch (Exception) { } return false; }
            }
        }
    }
    internal sealed class ReadingPosition
    {
        internal string Slug = "", Revision = "", Chapter = "";
        internal int Scroll, Font = 2, Theme;
        internal byte[] Encode()
        {
            return Encoding.UTF8.GetBytes("{\"slug\":" + StrictJson.Quote(Slug) + ",\"revision\":" + StrictJson.Quote(Revision) + ",\"chapter\":" + StrictJson.Quote(Chapter) + ",\"scroll\":" + Scroll + ",\"font\":" + Font + ",\"theme\":" + Theme + "}");
        }
        internal static ReadingPosition Decode(byte[] bytes)
        {
            if (bytes == null) return new ReadingPosition();
            try
            {
                var o = Protocol.Object(StrictJson.Parse(bytes));
                var p = new ReadingPosition { Slug = Protocol.String(o, "slug", 160), Revision = Protocol.String(o, "revision", 64), Chapter = Protocol.String(o, "chapter", 4), Scroll = Protocol.Integer(o, "scroll", 0, 1000000), Font = Protocol.Integer(o, "font", 0, 4) };
                if (o.ContainsKey("theme")) p.Theme = Protocol.Integer(o, "theme", 0, 1);
                if (!Protocol.SafeSlug(p.Slug) || !Protocol.Revision(p.Revision) || !Protocol.ChapterId(p.Chapter)) throw new InvalidDataException("Position");
                return p;
            }
            catch (Exception) { return new ReadingPosition(); }
        }
    }
}
