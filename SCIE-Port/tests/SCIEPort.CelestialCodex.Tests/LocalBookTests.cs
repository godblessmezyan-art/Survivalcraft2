using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using SCIEPort.Features.CelestialCodex.Core;
using SCIEPort.Features.CelestialCodex.Networking;

internal static class LocalBookTests
{
    private static void Assert(bool condition, string message)
    { if (!condition) throw new Exception("LocalBookTests: " + message); }

    internal static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "cc-local-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var files = new DiskFiles(root);
            var reader = new LocalBookLibrary(files);
            File.WriteAllText(Path.Combine(root, "普通小说.txt"), "第一章 雪山\n雪夜里有一盏灯。\n第二章 云海\n我看到了日出。", new UTF8Encoding(false));
            ReaderResult list = reader.Read(new Request(1), CancellationToken.None);
            Assert(list.Document != null && list.Document.Books.Count == 1, "TXT catalog");
            string txtId = list.Document.Books[0].Slug;
            ReaderResult detail = reader.Read(new Request(txtId), CancellationToken.None);
            Assert(detail.Document.Book.Chapters.Count == 2, "TXT headings");
            Assert(reader.Read(new Request(txtId, "c1", detail.Document.Book.Revision), CancellationToken.None).Document.Chapter.Text.Contains("雪夜"), "TXT text");
            Assert(reader.Read(new Request(txtId, "c1", new string('0', 64)), CancellationToken.None).HttpStatus == 409, "revision mismatch");

            using (Stream output = File.Create(Path.Combine(root, "demo.epub")))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, false))
            {
                Add(archive, "META-INF/container.xml", "<?xml version=\"1.0\"?><container><rootfiles><rootfile full-path=\"OPS/book.opf\"/></rootfiles></container>");
                Add(archive, "OPS/book.opf", "<?xml version=\"1.0\"?><package><metadata><title>远行之书</title><creator>测试作者</creator></metadata><manifest><item id=\"chapter\" href=\"text/1.xhtml\" media-type=\"application/xhtml+xml\"/></manifest><spine><itemref idref=\"chapter\"/></spine></package>");
                Add(archive, "OPS/text/1.xhtml", "<?xml version=\"1.0\"?><html><body><h1>荒原</h1><p>故事从这里开始。</p><script>do_not_show</script></body></html>");
            }
            list = reader.Read(new Request(1), CancellationToken.None);
            Assert(list.Document != null && list.Document.Books.Count == 2, "EPUB listed");
            string epubId = null;
            foreach (Book book in list.Document.Books) if (book.Title == "demo") epubId = book.Slug;
            Assert(epubId != null, "EPUB identity");
            detail = reader.Read(new Request(epubId), CancellationToken.None);
            Assert(detail.Document != null && detail.Document.Book.Title == "远行之书" && detail.Document.Book.Author == "测试作者", "EPUB OPF metadata");
            ReaderResult body = reader.Read(new Request(epubId, "c1", detail.Document.Book.Revision), CancellationToken.None);
            Assert(body.Document != null && body.Document.Chapter.Text.Contains("故事从这里开始") && !body.Document.Chapter.Text.Contains("do_not_show"), "EPUB XHTML text without script");

            // ZIP path traversal must fail closed, without opening/creating outside the import folder.
            using (Stream output = File.Create(Path.Combine(root, "unsafe.epub")))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, false))
                Add(archive, "../escape.txt", "bad");
            list = reader.Read(new Request(1), CancellationToken.None);
            string unsafeId = null;
            foreach (Book book in list.Document.Books) if (book.Title == "unsafe") unsafeId = book.Slug;
            Assert(unsafeId != null && reader.Read(new Request(unsafeId), CancellationToken.None).Document == null, "path traversal rejected");
            Assert(!File.Exists(Path.Combine(root, "..", "escape.txt")), "no traversal file written");
            File.WriteAllText(Path.Combine(root, "Zeta.txt"), "Z");
            File.WriteAllText(Path.Combine(root, "alpha.txt"), "A");
            list = reader.Read(new Request(1), CancellationToken.None);
            Assert(list.Document.Books[0].Title == "alpha" && list.Document.Books[1].Title == "demo", "case-insensitive catalog ordering without List.Sort");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void Add(ZipArchive zip, string name, string text)
    {
        using (Stream stream = zip.CreateEntry(name).Open())
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(text);
    }

    private sealed class DiskFiles : IFileStore
    {
        private readonly string dir;
        internal DiskFiles(string dir) { this.dir = dir; }
        private string At(string name) { return Path.Combine(dir, name); }
        public void Create() { Directory.CreateDirectory(dir); }
        public IEnumerable<string> Names()
        { foreach (string path in Directory.GetFiles(dir)) yield return Path.GetFileName(path); }
        public long Size(string name) { return new FileInfo(At(name)).Length; }
        public DateTime Modified(string name) { return File.GetLastWriteTimeUtc(At(name)); }
        public Stream Read(string name) { return File.OpenRead(At(name)); }
        public Stream Write(string name) { return File.Create(At(name)); }
        public void Move(string from, string to) { File.Move(At(from), At(to)); }
        public void Delete(string name) { File.Delete(At(name)); }
    }
}
