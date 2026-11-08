using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using SCIEPort.Features.CelestialCodex.Networking;

namespace SCIEPort.Features.CelestialCodex.Core
{
    // Files belong to the client device. Never expose local content to the game server.
    // A host-specific import path is passed in by StorageAdapter; no Android-specific APIs here.
    internal sealed class LocalBookLibrary
    {
        internal const int MaxSourceBytes = 8 * 1024 * 1024;
        internal const int MaxUnpackedBytes = 16 * 1024 * 1024;
        internal const int MaxEntries = 256;
        internal const int MaxBooks = 64;
        private readonly IFileStore files;

        internal LocalBookLibrary(IFileStore files) { this.files = files; }

        internal ReaderResult Read(Request request, CancellationToken cancellation)
        {
            var result = new ReaderResult { Request = request };
            try
            {
                if (request.Slug == null) { result.Document = List(request.Page, cancellation); return result; }
                string name = FindName(request.Slug, cancellation);
                if (name == null) { result.HttpStatus = 404; result.Error = "本地书籍不存在"; return result; }
                ImportedBook imported = Parse(name, cancellation);
                if (request.Revision != null && imported.Revision != request.Revision)
                { result.HttpStatus = 409; result.Error = "内容已更新"; return result; }
                var document = new Document { Book = imported.Book };
                if (request.Chapter != null)
                {
                    int index = ChapterNumber(request.Chapter) - 1;
                    if (index < 0 || index >= imported.Parts.Count)
                    { result.HttpStatus = 404; result.Error = "章节不存在"; return result; }
                    document.Chapter = imported.Parts[index];
                }
                result.Document = document;
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                result.Error = "本地文件不可读: " + e.GetType().Name;
                return result;
            }
        }

        private Document List(int page, CancellationToken cancellation)
        {
            var names = Enumerate(cancellation);
            var document = new Document { Page = page };
            int start = (page - 1) * 20;
            document.HasMore = names.Count > start + 20;
            for (int i = start; i < names.Count && i < start + 20; i++)
            {
                string name = names[i];
                document.Books.Add(new Book
                {
                    Slug = BookId(name), Title = DisplayName(name), Author = "",
                    SourceMode = "original", ResourceId = "local", Status = "available",
                    Revision = "", Reason = ""
                });
            }
            return document;
        }

        private List<string> Enumerate(CancellationToken cancellation)
        {
            var names = new List<string>();
            try { files.Create(); }
            catch (Exception) { return names; }
            int scanned = 0;
            foreach (string name in files.Names())
            {
                cancellation.ThrowIfCancellationRequested();
                if (++scanned > 512) throw new InvalidDataException("Too many import directory entries");
                if (!SafeName(name) || !Supported(name)) continue;
                if (files.Size(name) > MaxSourceBytes || files.Size(name) == 0) continue;
                names.Add(name);
                if (names.Count > MaxBooks) throw new InvalidDataException("Too many imported books");
            }
            // Some audited Android runtimes lack List<T>.Sort. The list is capped at 64.
            for (int i = 1; i < names.Count; i++)
            {
                string item = names[i]; int j = i - 1;
                while (j >= 0 && StringComparer.OrdinalIgnoreCase.Compare(names[j], item) > 0)
                { names[j + 1] = names[j]; j--; }
                names[j + 1] = item;
            }
            return names;
        }

        private string FindName(string slug, CancellationToken cancellation)
        {
            if (!slug.StartsWith("local-", StringComparison.Ordinal)) return null;
            foreach (string name in Enumerate(cancellation))
                if (string.Equals(BookId(name), slug, StringComparison.Ordinal)) return name;
            return null;
        }

        private ImportedBook Parse(string name, CancellationToken cancellation)
        {
            byte[] bytes;
            using (Stream stream = files.Read(name)) bytes = ReadBounded(stream, MaxSourceBytes, cancellation);
            string revision = Hash(bytes);
            var imported = new ImportedBook
            {
                Revision = revision,
                Book = new Book { Slug = BookId(name), Title = DisplayName(name), Author = "",
                    ResourceId = "local", SourceMode = "original", Revision = revision,
                    Status = "ready", Reason = "" }
            };
            if (name.EndsWith(".epub", StringComparison.OrdinalIgnoreCase)) ReadEpub(bytes, imported, cancellation);
            else ReadTxt(bytes, imported, cancellation);
            if (imported.Parts.Count == 0) imported.Book.Status = "empty";
            for (int i = 0; i < imported.Parts.Count; i++)
            {
                var part = imported.Parts[i]; part.Id = "c" + (i + 1);
                imported.Book.Chapters.Add(new Chapter { Id = part.Id, Title = part.Title });
            }
            return imported;
        }

        private static void ReadTxt(byte[] bytes, ImportedBook imported, CancellationToken cancellation)
        {
            int offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
            string text = new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
            // UTF-8 is intentionally the only guaranteed encoding on the target Android BCL.
            // Do not silently decode arbitrary GBK files into garbage.
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var current = new StringBuilder(); string heading = "正文";
            var re = new Regex(@"^\s*(?:第[零一二三四五六七八九十百千万两0-9]+[章回节卷篇]|Chapter\s+\d+|CHAPTER\s+\d+).{0,60}$", RegexOptions.IgnoreCase);
            for (int i = 0; i < lines.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                string line = lines[i].TrimEnd();
                if (re.IsMatch(line.Trim()) && current.Length > 0)
                {
                    AppendParts(imported, heading, current.ToString());
                    current.Length = 0; heading = line.Trim();
                }
                else if (re.IsMatch(line.Trim()) && current.Length == 0) heading = line.Trim();
                current.Append(line).Append('\n');
                if (current.Length > 1024 * 1024)
                { AppendParts(imported, heading, current.ToString()); current.Length = 0; }
            }
            AppendParts(imported, heading, current.ToString());
        }

        private static void ReadEpub(byte[] bytes, ImportedBook imported, CancellationToken cancellation)
        {
            using (var archive = new ZipArchive(new MemoryStream(bytes, false), ZipArchiveMode.Read, false))
            {
                if (archive.Entries.Count > MaxEntries) throw new InvalidDataException("EPUB entry count");
                long unpacked = 0;
                int actualReadBudget = MaxUnpackedBytes;
                var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
                foreach (ZipArchiveEntry e in archive.Entries)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (e.FullName.EndsWith("/", StringComparison.Ordinal)) continue;
                    string path = ValidPath(e.FullName);
                    if (e.Length < 0 || e.Length > MaxUnpackedBytes || unpacked + e.Length > MaxUnpackedBytes)
                        throw new InvalidDataException("EPUB decompression limit");
                    unpacked += e.Length;
                    if (!entries.ContainsKey(path)) entries.Add(path, e);
                    else throw new InvalidDataException("EPUB duplicate entry");
                }
                ZipArchiveEntry container;
                if (!entries.TryGetValue("META-INF/container.xml", out container)) throw new InvalidDataException("EPUB container missing");
                XmlDocument rootDoc = SafeXml(ReadEntry(container, 256 * 1024, ref actualReadBudget, cancellation));
                XmlNode rootfile = rootDoc.SelectSingleNode("//*[local-name()='rootfile']");
                if (rootfile == null || rootfile.Attributes == null || rootfile.Attributes["full-path"] == null)
                    throw new InvalidDataException("EPUB package missing");
                string packagePath = ValidPath(rootfile.Attributes["full-path"].Value);
                ZipArchiveEntry package;
                if (!entries.TryGetValue(packagePath, out package)) throw new InvalidDataException("EPUB package not found");
                XmlDocument opf = SafeXml(ReadEntry(package, 512 * 1024, ref actualReadBudget, cancellation));
                XmlNode title = opf.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='title']");
                XmlNode creator = opf.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='creator']");
                if (title != null && !string.IsNullOrWhiteSpace(title.InnerText)) imported.Book.Title = Truncate(title.InnerText.Trim(), 120);
                if (creator != null) imported.Book.Author = Truncate(creator.InnerText.Trim(), 120);
                string parent = packagePath.LastIndexOf('/') >= 0 ? packagePath.Substring(0, packagePath.LastIndexOf('/') + 1) : "";
                var resources = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (XmlNode item in opf.SelectNodes("//*[local-name()='manifest']/*[local-name()='item']"))
                {
                    if (item.Attributes == null) continue;
                    XmlAttribute id = item.Attributes["id"], href = item.Attributes["href"], type = item.Attributes["media-type"];
                    if (id == null || href == null || type == null) continue;
                    if (type.Value != "application/xhtml+xml") continue;
                    string relative = Uri.UnescapeDataString(href.Value.Split('#')[0]);
                    string path = ResolvePath(parent, relative);
                    if (!resources.ContainsKey(id.Value)) resources[id.Value] = path;
                }
                int chapterIndex = 1;
                foreach (XmlNode itemref in opf.SelectNodes("//*[local-name()='spine']/*[local-name()='itemref']"))
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (itemref.Attributes == null || itemref.Attributes["idref"] == null) continue;
                    string href;
                    if (!resources.TryGetValue(itemref.Attributes["idref"].Value, out href)) continue;
                    ZipArchiveEntry entry;
                    if (!entries.TryGetValue(href, out entry)) throw new InvalidDataException("EPUB chapter missing");
                    string chapterText = ExtractXhtml(ReadEntry(entry, 2 * 1024 * 1024, ref actualReadBudget, cancellation));
                    if (chapterText.Trim().Length > 0)
                    {
                        AppendParts(imported, "第" + chapterIndex + "节", chapterText);
                        chapterIndex++;
                    }
                    if (imported.Parts.Count > 512) throw new InvalidDataException("Too many book sections");
                }
            }
        }

        private static XmlDocument SafeXml(byte[] data)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 2 * 1024 * 1024,
                MaxCharactersFromEntities = 0, IgnoreComments = true
            };
            var doc = new XmlDocument { XmlResolver = null };
            using (var stream = new MemoryStream(data, false))
            using (XmlReader reader = XmlReader.Create(stream, settings)) doc.Load(reader);
            return doc;
        }

        private static string ExtractXhtml(byte[] data)
        {
            XmlDocument doc = SafeXml(data);
            var result = new StringBuilder();
            var body = doc.SelectSingleNode("//*[local-name()='body']");
            if (body == null) return "";
            Walk(body, result, 0);
            return result.ToString().Trim();
        }
        private static void Walk(XmlNode node, StringBuilder output, int depth)
        {
            if (depth > 64 || output.Length > MaxUnpackedBytes) throw new InvalidDataException("EPUB XHTML depth or size");
            if (node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.CDATA)
            { output.Append(node.Value); return; }
            string name = node.LocalName == null ? "" : node.LocalName.ToLowerInvariant();
            if (name == "script" || name == "style" || name == "svg" || name == "iframe") return;
            bool newline = name == "p" || name == "div" || name == "section" || name == "li" || name == "h1" || name == "h2" || name == "h3" || name == "br";
            if (newline && output.Length > 0) output.Append('\n');
            foreach (XmlNode child in node.ChildNodes) Walk(child, output, depth + 1);
            if (newline) output.Append('\n');
        }

        private static void AppendParts(ImportedBook imported, string title, string text)
        {
            int pos = 0, count = 0;
            while (pos < text.Length)
            {
                int max = Math.Min(12000, text.Length - pos);
                // Don't split UTF-16 surrogate pairs.
                if (pos + max < text.Length && max > 0 && char.IsHighSurrogate(text[pos + max - 1])) max--;
                string fragment = text.Substring(pos, max).Trim();
                if (fragment.Length > 0)
                {
                    string suffix = count++ == 0 ? "" : " (续" + count + ")";
                    imported.Parts.Add(new Chapter { Title = Truncate(title, 70) + suffix, Text = fragment });
                    if (imported.Parts.Count > 512) throw new InvalidDataException("Too many book parts");
                }
                pos += max;
            }
        }

        private static byte[] ReadEntry(ZipArchiveEntry entry, int limit, ref int remaining, CancellationToken cancellation)
        {
            if (remaining <= 0) throw new InvalidDataException("EPUB actual expansion budget");
            byte[] bytes;
            using (Stream input = entry.Open()) bytes = ReadBounded(input, Math.Min(limit, remaining), cancellation);
            remaining -= bytes.Length;
            return bytes;
        }
        private static byte[] ReadBounded(Stream input, int limit, CancellationToken cancellation)
        {
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[8192]; int n;
                while ((n = input.Read(buffer, 0, Math.Min(buffer.Length, limit + 1 - (int)output.Length))) > 0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, n);
                    if (output.Length > limit) throw new InvalidDataException("Import file too large");
                }
                cancellation.ThrowIfCancellationRequested();
                return output.ToArray();
            }
        }
        private static string ValidPath(string name)
        {
            if (string.IsNullOrEmpty(name) || name.StartsWith("/", StringComparison.Ordinal) || name.IndexOf('\\') >= 0 || name.IndexOf(':') >= 0 || name.IndexOf('\0') >= 0)
                throw new InvalidDataException("EPUB path");
            string[] parts = name.Split('/');
            foreach (string part in parts) if (part == ".." || part == "." || part == "") throw new InvalidDataException("EPUB traversal");
            return name;
        }
        private static string ResolvePath(string parent, string relative)
        {
            string combined = parent + relative;
            var normalized = new List<string>();
            foreach (string part in combined.Split('/'))
            {
                if (part == "." || part == "") continue;
                if (part == "..") { if (normalized.Count == 0) throw new InvalidDataException("EPUB escape"); normalized.RemoveAt(normalized.Count - 1); }
                else { if (part.IndexOf(':') >= 0 || part.IndexOf('\\') >= 0) throw new InvalidDataException("EPUB href"); normalized.Add(part); }
            }
            return ValidPath(string.Join("/", normalized.ToArray()));
        }
        private static bool SafeName(string name)
        {
            return !string.IsNullOrEmpty(name) && name.Length <= 160 && name != "." && name != ".." &&
                name.IndexOf('/') < 0 && name.IndexOf('\\') < 0 && name.IndexOf(':') < 0 && name.IndexOf('\0') < 0;
        }
        private static bool Supported(string name) { return name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".epub", StringComparison.OrdinalIgnoreCase); }
        private static string DisplayName(string name)
        {
            int i = name.LastIndexOf('.'); return Truncate((i > 0 ? name.Substring(0, i) : name).Trim(), 120);
        }
        private static string Truncate(string value, int max) { return value.Length > max ? value.Substring(0, max) : value; }
        private static string BookId(string name)
        {
            // Stable across runs and independent of file content; changes to content change Revision.
            byte[] bytes = Encoding.UTF8.GetBytes(name);
            string hash = Hash(bytes);
            return "local-" + hash.Substring(0, 32);
        }
        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes); var sb = new StringBuilder(64);
                foreach (byte b in digest) sb.Append(b.ToString("x2")); return sb.ToString();
            }
        }
        private static int ChapterNumber(string id)
        {
            if (!Protocol.ChapterId(id)) return -1;
            int number = 0; for (int i = 1; i < id.Length; i++) number = number * 10 + id[i] - '0';
            return number;
        }
        private sealed class ImportedBook
        {
            internal Book Book;
            internal readonly List<Chapter> Parts = new List<Chapter>();
            internal string Revision;
        }
    }
}
