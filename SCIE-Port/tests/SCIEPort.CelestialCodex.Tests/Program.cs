using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SCIEPort.Features.CelestialCodex.Core;
using SCIEPort.Features.CelestialCodex.Networking;

internal static class Program
{
    private static int checks;
    private const string Revision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Summary = "\"slug\":\"test\",\"title\":\"测试书\",\"author\":\"作者\",\"resourceId\":\"legacy\",\"sourceMode\":\"ai_reading\",\"revision\":\"" + Revision + "\",\"status\":\"ready\",\"reason\":null";
    private const string Catalog = "{\"schemaVersion\":1,\"page\":1,\"pageSize\":20,\"hasMore\":false,\"books\":[{" + Summary + "}]}";
    private const string Detail = "{\"schemaVersion\":1," + Summary + ",\"chapters\":[{\"id\":\"c1\",\"title\":\"第一章\"}]}";
    private const string Body = "{\"schemaVersion\":1," + Summary + ",\"chapter\":{\"id\":\"c1\",\"title\":\"第一章\",\"text\":\"你好\\n\\n世界😀\"}}";
    private static byte[] Bytes(string s) { return Encoding.UTF8.GetBytes(s); }
    private static void Check(bool valid, string message) { checks++; if (!valid) throw new Exception(message); }
    private static void Reject(Action action) { bool rejected = false; try { action(); } catch (Exception) { rejected = true; } Check(rejected, "Expected rejection"); }
    private static async Task RejectAsync(Func<Task> action) { bool rejected = false; try { await action(); } catch (Exception) { rejected = true; } Check(rejected, "Expected async rejection"); }
    private static void Main(string[] args)
    {
        try
        {
            ReaderPresentationChecks.Run(Check);
            Run().GetAwaiter().GetResult();
            if (args.Length > 0)
            {
                Document catalog = Protocol.Decode(File.ReadAllBytes(Path.Combine(args[0], "catalog.json")));
                Document book = Protocol.Decode(File.ReadAllBytes(Path.Combine(args[0], "book.json")), "public");
                Document body = Protocol.Decode(File.ReadAllBytes(Path.Combine(args[0], "chapter.json")), "public", "c1", book.Book.Revision);
                Check(catalog.Books[0].Slug == "public", "actual API catalog contract");
                Check(book.Book.SourceMode == "original", "actual API source mode");
                Check(body.Chapter.Text == "原著\n\n正文", "actual API converted body contract");
            }
            Console.WriteLine("CelestialCodex: " + checks + " checks passed (Windows .NET Framework; no game/device)");
        }
        catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
    }
    private static async Task Run()
    {
        await DeadlineChecks();
        HandlerPolicyChecks();
        await HandlerWireChecks();
        LocalBookChecks();
        LocalBookTests.Run();
        Check(NetworkFailure.Code(new MissingMethodException("missing-member")) == "BCL", "missing member diagnostic");
        Check(NetworkFailure.Code(new HttpRequestException("outer", new MissingMethodException("inner-member"))) == "BCL", "nested missing member diagnostic");
        Check(NetworkFailure.Code(new HttpRequestException("outer", new System.Security.Authentication.AuthenticationException("certificate"))) == "TLS", "nested TLS diagnostic");
        Check(NetworkFailure.Code(new DecoderFallbackException("UTF8")) == "DATA", "UTF8 failure is data, not a connection error");
        string report = null;
        NetworkFailure.Report(message => report = message, "decode", "DATA", new InvalidDataException(new string('x', 10000)));
        Check(report.Contains("stage=decode") && report.Contains("type=System.IO.InvalidDataException message=") && report.Length < 9400, "bounded diagnostics with type/message on first line");
        NetworkFailure.Report(message => report = message, "http/handler", "BCL", new MissingMethodException("missing\nsetter"));
        Check(report.Split('\n')[0].Contains("type=System.MissingMethodException message=missing setter"), "summary survives multiline stack truncation");
        Check(Protocol.Decode(Bytes(Catalog)).Books[0].Title == "测试书", "catalog");
        Check(Protocol.Decode(Bytes(Detail), "test").Book.Chapters.Count == 1, "TOC");
        Check(Protocol.Decode(Bytes(Body), "test", "c1", Revision).Chapter.Text == "你好\n\n世界😀", "UTF8 body");
        var chapters = new List<Chapter> { new Chapter { Id = "c1" }, new Chapter { Id = "c2" }, new Chapter { Id = "c3" } };
        Check(ChapterNavigation.IndexOf(chapters, "c1") == 0, "first chapter lookup without predicate BCL");
        Check(ChapterNavigation.IndexOf(chapters, "c2") == 1, "resume chapter lookup without Exists");
        Check(ChapterNavigation.IndexOf(chapters, "c3") == 2, "last chapter lookup");
        Check(ChapterNavigation.IndexOf(chapters, "c4") == -1, "removed chapter lookup");
        Check(ChapterNavigation.IndexOf(null, "c1") == -1 && ChapterNavigation.IndexOf(chapters, null) == -1, "empty chapter lookup");
        foreach (string invalid in new[] { "", "{", "[]x", "{\"a\":1,\"a\":2}", "[1,]", "{\"a\":01}", "{\"a\":1.5}", "{\"a\":999999999999}", "\"\\uD800\"", "\"\\uDC00\"", "\"\\q\"", "[truefalse]", new string('[', 20) + new string(']', 20), "{\"a\":\"" + new string('x', 16001) + "\"}" }) Reject(() => StrictJson.Parse(Bytes(invalid)));
        Reject(() => StrictJson.Parse(new byte[] { 255 })); Reject(() => StrictJson.Parse(new byte[Limits.MaxFileBytes + 1]));
        Reject(() => Protocol.Decode(Bytes(Catalog.Replace("schemaVersion\":1", "schemaVersion\":2"))));
        Reject(() => Protocol.Decode(Bytes(Detail), "another")); Reject(() => Protocol.Decode(Bytes(Body), "test", "c2", Revision));
        Reject(() => Protocol.Decode(Bytes(Body.Replace(Revision, new string('b', 64))), "test", "c1", Revision));
        Reject(() => Protocol.Decode(Bytes(Body.Replace("你好\\n\\n世界😀", new string('x', 12001))), "test", "c1", Revision));
        foreach (string invalid in new[] { "../x", "a/b", "a%2fb", "a--b", "A", "a?token=1", new string('a', 161) }) Reject(() => new Request(invalid));
        foreach (string invalid in new[] { "http://wxhappylife.top/api/game-library/v1", "https://evil.test/api/game-library/v1", "https://wxhappylife.top.evil.test/api/game-library/v1", "https://user@wxhappylife.top/api/game-library/v1", "https://wxhappylife.top:8443/api/game-library/v1", "https://wxhappylife.top/api/game-library/v1?x=1" }) Reject(() => SourcePolicy.Validate(invalid));
        Check(SourcePolicy.Validate(SourcePolicy.DefaultEndpoint) == SourcePolicy.DefaultEndpoint, "HTTPS source");
        Check(ReadingPosition.Decode(Bytes("invalid")).Slug == "", "corrupt progress");
        foreach (string id in new[] { "c0", "c513", "c999", "c-1", "../" }) Reject(() => new Request("test", id, Revision));
        var position = new ReadingPosition { Slug = "test", Revision = Revision, Chapter = "c1", Scroll = 530000, Font = 4 };
        Check(ReadingPosition.Decode(position.Encode()).Scroll == 530000, "progress restart");
        foreach (var size in new[] { new[] { 320f, 240f }, new[] { 480f, 320f }, new[] { 1280f, 720f }, new[] { 720f, 1280f }, new[] { 2340f, 1080f } })
        {
            var layout = new ReaderLayout(size[0], size[1]); Check(layout.Width <= size[0] - 32 && layout.Height <= size[1] - 32 && layout.ButtonWidth >= 44 && layout.ContentHeight >= 44, "touch layout bounds");
        }
        string root = Path.Combine(Path.GetTempPath(), "CelestialCodex-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var files = new Files(root); var cache = new BoundedCache(files); string key = BoundedCache.Key("key");
            Check(cache.Read(key) == null, "missing file"); Check(cache.Write(key, Bytes(Catalog)), "write"); Check(cache.Read(key).Length > 0, "read");
            Reject(() => cache.Read("../private.json")); Check(!cache.Write(key, new byte[Limits.MaxFileBytes + 1]), "bounded write");
            File.WriteAllBytes(Path.Combine(root, key), new byte[Limits.MaxFileBytes + 1]); Check(cache.Read(key) == null && !File.Exists(Path.Combine(root, key)), "oversized corruption");
            Check(cache.Write("position.json", position.Encode()), "position");
            for (int i = 0; i < Limits.MaxFiles + 5; i++) Check(cache.Write(BoundedCache.Key("item" + i), new byte[70000]), "cache fill");
            var info = new DirectoryInfo(root).GetFiles(); long total = 0; foreach (var file in info) total += file.Length;
            Check(info.Length <= Limits.MaxFiles && total <= Limits.MaxTotalBytes, "count and capacity"); Check(cache.Read("position.json") != null, "keep position");
            for (int i = 0; i < Limits.MaxFiles + 5; i++) Check(cache.Write(BoundedCache.Key("tiny" + i), Bytes("{}")), "small cache fill");
            Check(new DirectoryInfo(root).GetFiles().Length == Limits.MaxFiles, "file count limit independently");
            string oldest = null;
            foreach (string file in Directory.GetFiles(root))
            {
                File.SetLastWriteTimeUtc(file, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                if (Path.GetFileName(file) != "position.json" && oldest == null) oldest = file;
            }
            File.SetLastWriteTimeUtc(oldest, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            Check(cache.Write(BoundedCache.Key("new-after-aging"), Bytes("{}")), "cache eviction without Sort/FindIndex");
            Check(!File.Exists(oldest), "oldest snapshot evicted");
            Check(cache.Read("position.json") != null, "position survives indexed eviction");
            File.WriteAllText(Path.Combine(root, "orphan.tmp"), "partial"); cache.Write(key, Bytes(Catalog)); Check(!File.Exists(Path.Combine(root, "orphan.tmp")), "interrupted write cleanup");
            await ClientChecks(cache);
        }
        finally { Directory.Delete(root, true); }
        await GateChecks();
    }
    private static void LocalBookChecks()
    {
        string root = Path.Combine(Path.GetTempPath(), "CelestialCodex-local-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "book.txt"), "第一章 开始\n你好，雪山。\n第二章 云端\n明天继续。", new UTF8Encoding(false));
            var library = new LocalBookLibrary(new Files(root));
            ReaderResult catalog = library.Read(new Request(1), CancellationToken.None);
            Check(catalog.Document != null && catalog.Document.Books.Count == 1, "local TXT catalog");
            string slug = catalog.Document.Books[0].Slug;
            ReaderResult detail = library.Read(new Request(slug), CancellationToken.None);
            Check(detail.Document != null && detail.Document.Book.Chapters.Count == 2, "TXT headings");
            ReaderResult chapter = library.Read(new Request(slug, "c1", detail.Document.Book.Revision), CancellationToken.None);
            Check(chapter.Document.Chapter.Text.Contains("雪山"), "TXT chapter text");
            Check(library.Read(new Request(slug, "c1", new string('a', 64)), CancellationToken.None).HttpStatus == 409, "local revision conflict");
        }
        finally { Directory.Delete(root, true); }
    }
    private static async Task DeadlineChecks()
    {
        Check(!RequestDeadline.HasElapsed(100, 119, 20) && RequestDeadline.HasElapsed(100, 120, 20), "monotonic deadline boundary");
        Check(RequestDeadline.HasElapsed(long.MaxValue - 10, long.MinValue + 15, 20), "monotonic tick wrap");
        Reject(() => new RequestDeadline(CancellationToken.None, 0));
        using (var caller = new CancellationTokenSource())
        {
            var deadline = new RequestDeadline(caller.Token, 1000);
            try
            {
                caller.Cancel();
                for (int i = 0; i < 100 && !deadline.Token.IsCancellationRequested; i++) await Task.Delay(10);
                Check(deadline.Token.IsCancellationRequested, "caller cancellation reaches request token without linked source");
            }
            finally { await deadline.Stop(); }
        }
        var expired = new RequestDeadline(CancellationToken.None, 50);
        try
        {
            await RejectAsync(async () => await Task.Delay(1000, expired.Token));
            Check(expired.Token.IsCancellationRequested, "real timeout cancels pending operation");
        }
        finally { await expired.Stop(); }
        var finished = new RequestDeadline(CancellationToken.None, 50);
        await finished.Stop(); await Task.Delay(80);
        Check(!finished.Token.IsCancellationRequested, "early completion stops deadline before source disposal");
        using (var caller = new CancellationTokenSource())
        {
            caller.Cancel(); var deadline = new RequestDeadline(caller.Token, 1000);
            try { Check(deadline.Token.IsCancellationRequested, "pre-canceled caller"); }
            finally { await deadline.Stop(); }
        }
    }
    private static void HandlerPolicyChecks()
    {
        using (var handler = (HttpClientHandler)HttpHandlerPolicy.Create())
            Check(!handler.AllowAutoRedirect && !handler.UseCookies && handler.AutomaticDecompression == DecompressionMethods.None, "desktop public setter policy");
        var trimmed = new TrimmedHandler();
        Check(trimmed.AllowAutoRedirect && trimmed.UseCookies && trimmed.AutomaticDecompression == DecompressionMethods.GZip, "trimmed fixture starts with unsafe defaults");
        HttpHandlerPolicy.Configure(trimmed);
        Check(!trimmed.AllowAutoRedirect && !trimmed.UseCookies && trimmed.AutomaticDecompression == DecompressionMethods.None, "verified Android fields enforce policy without setters");
        Reject(() => HttpHandlerPolicy.Configure(new UnknownHandler()));
        Reject(() => HttpHandlerPolicy.Configure(new IgnoredSetterHandler()));
        foreach (string type in new[] { "application/json", "APPLICATION/JSON; charset=\"utf-8\"", " application/json ; charset=utf-8" }) Check(LibraryTransport.IsJson(type), "JSON header without MediaType getter");
        foreach (string type in new[] { null, "", "text/html", "application/jsonp", "application/json-x; charset=utf-8" }) Check(!LibraryTransport.IsJson(type), "strict media type boundary");
    }
    private static async Task HandlerWireChecks()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        string address = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        var requests = new List<string>();
        Task server = Task.Run(async () =>
        {
            for (int i = 0; i < 2; i++)
            using (var connection = await listener.AcceptTcpClientAsync())
            using (var stream = connection.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
            {
                string line; var headers = new StringBuilder();
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync())) headers.AppendLine(line);
                requests.Add(headers.ToString());
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: " + address + "/followed\r\nSet-Cookie: codex-test=1; Path=/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(response, 0, response.Length);
            }
        });
        try
        {
            using (var client = new HttpClient(HttpHandlerPolicy.Create()))
            {
                client.Timeout = TimeSpan.FromSeconds(3);
                for (int i = 0; i < 2; i++)
                using (var response = await client.GetAsync(address + "/books")) Check(response.StatusCode == HttpStatusCode.Found, "real wire redirect is returned without following");
            }
            await server;
            Check(requests.Count == 2 && requests[1].IndexOf("Cookie:", StringComparison.OrdinalIgnoreCase) < 0 && requests[1].StartsWith("GET /books ", StringComparison.Ordinal), "real wire does not replay cookies or follow Location");
        }
        finally { listener.Stop(); }
    }
    private static HttpResponseMessage Response(string json = Catalog, int status = 200, string type = "application/json; charset=utf-8")
    {
        var r = new HttpResponseMessage((HttpStatusCode)status) { Content = new ByteArrayContent(Bytes(json)) }; r.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(type); return r;
    }
    private static LibraryTransport Transport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    { return new LibraryTransport(SourcePolicy.DefaultEndpoint, () => new Handler(send)); }
    private static async Task ClientChecks(BoundedCache cache)
    {
        var request = new Request(1);
        var ok = Transport((r, t) => { Check(r.RequestUri.Scheme == "https" && r.RequestUri.Host == "wxhappylife.top", "request source"); Check(r.Headers.AcceptEncoding.ToString() == "identity", "explicit identity response request"); return Task.FromResult(Response()); });
        var client = new LibraryClient(cache, SourcePolicy.DefaultEndpoint, ok);
        ReaderResult result = await client.Read(request, CancellationToken.None); Check(result.Document.Books.Count == 1 && !result.Offline, "network success");
        var fail = Transport((r, t) => { throw new IOException("Offline"); });
        result = await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, fail).Read(request, CancellationToken.None); Check(result.Offline && result.Document != null, "offline cached catalog");
        bool touchedNetwork = false;
        var noNetwork = Transport((r, t) => { touchedNetwork = true; throw new Exception("Must not fetch in offline mode"); });
        result = await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, noNetwork).Read(request, CancellationToken.None, true); Check(result.Offline && result.Document != null, "immediate offline cache");
        result = await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, noNetwork).Read(new Request(999), CancellationToken.None, true); Check(result.Document == null && result.Error == "尚未缓存", "uncached offline page");
        Check(!touchedNetwork, "offline mode does not contact server");
        foreach (string json in new[] { Detail, Body })
        {
            Request req = json == Detail ? new Request("test") : new Request("test", "c1", Revision);
            await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, Transport((r, t) => Task.FromResult(Response(json)))).Read(req, CancellationToken.None);
            result = await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, fail).Read(req, CancellationToken.None);
            Check(result.Offline && result.Document != null, "offline TOC/body");
        }
        var timeout = Transport((r, t) => { throw new TaskCanceledException(); });
        Check((await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, timeout).Read(request, CancellationToken.None)).Offline, "timeout uses cache");
        var badJson = Transport((r, t) => Task.FromResult(Response("{bad")));
        Check((await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, badJson).Read(request, CancellationToken.None)).Offline, "invalid network JSON preserves cache");
        foreach (int status in new[] { 401, 403, 404, 409 })
        {
            await client.Read(request, CancellationToken.None);
            result = await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, Transport((r, t) => Task.FromResult(Response(status: status)))).Read(request, CancellationToken.None);
            Check(result.Document == null && result.HttpStatus == status, "no stale resurrection " + status);
        }
        string key = BoundedCache.Key(SourcePolicy.DefaultEndpoint + request.Path);
        cache.Write(key, Bytes("corrupt")); result = await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, fail).Read(request, CancellationToken.None); Check(result.Document == null && cache.Read(key) == null, "corrupt cached JSON");
        string diagnostic = null;
        var missing = Transport((r, t) => { throw new HttpRequestException("wrapper", new MissingMethodException("test-network-member")); });
        result = await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, missing, message => diagnostic = message).Read(request, CancellationToken.None);
        Check(result.Error.Contains("[BCL]") && diagnostic.Contains("test-network-member") && diagnostic.Contains("stage=http"), "network exception reaches diagnostics");
        result = await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, badJson, message => diagnostic = message).Read(request, CancellationToken.None);
        Check(result.Error.Contains("[DATA]") && diagnostic.Contains("stage=decode"), "malformed server JSON is distinguishable");
        result = await new LibraryClient(cache, SourcePolicy.DefaultEndpoint, missing, message => { throw new IOException("logger failed"); }).Read(request, CancellationToken.None);
        Check(result.Error.Contains("[BCL]"), "diagnostic failure preserves original error");
        foreach (var response in new[] { Response(type: "text/html"), Response(type: "application/jsonp"), Response(type: "application/json; charset=gbk"), Response(status: 302), Response(new string('x', Limits.MaxFileBytes + 1)) })
            await RejectAsync(async () => await Transport((r, t) => Task.FromResult(response)).Get(request.Path, CancellationToken.None));
        // Unknown-length streaming body must be bounded too; Content-Length is not trusted.
        var streamed = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new NonSeekStream(new byte[Limits.MaxFileBytes + 1])) };
        streamed.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        await RejectAsync(async () => await Transport((r, t) => Task.FromResult(streamed)).Get(request.Path, CancellationToken.None));
        var disposed = new TrackStream(Bytes(Catalog));
        var tracked = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(disposed) }; tracked.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        await Transport((r, t) => Task.FromResult(tracked)).Get(request.Path, CancellationToken.None); Check(disposed.Disposed, "response stream disposal");
        using (var canceled = new CancellationTokenSource()) { canceled.Cancel(); await RejectAsync(async () => await client.Read(request, canceled.Token)); }
        using (var caller = new CancellationTokenSource())
        {
            bool entered = false;
            var slow = Transport(async (r, t) => { entered = true; await Task.Delay(3000, t); return Response(); });
            Task<byte[]> pending = slow.Get(request.Path, caller.Token);
            for (int i = 0; i < 100 && !entered; i++) await Task.Delay(5);
            Check(entered, "in-flight transport entered");
            caller.Cancel();
            await RejectAsync(async () => await pending);
        }
    }
    private static async Task GateChecks()
    {
        string diagnostic = null;
        using (var failed = new RequestGate(message => diagnostic = message))
        {
            failed.Begin(t => { throw new MissingMethodException("worker-setup-member"); });
            ReaderResult failure = null;
            for (int i = 0; i < 100 && failure == null; i++) { await Task.Delay(10); failure = failed.Take(); }
            Check(failure != null && failure.Error.Contains("[BCL]") && diagnostic.Contains("worker-setup-member") && diagnostic.Contains("stage=worker"), "worker initialization failures are logged");
        }
        var late = new TaskCompletionSource<ReaderResult>(); var started = new TaskCompletionSource<bool>();
        using (var gate = new RequestGate())
        {
            gate.Begin(t => { started.SetResult(true); return late.Task; }); await started.Task;
            gate.Begin(t => Task.FromResult(new ReaderResult { Error = "new" }));
            ReaderResult result = null;
            for (int i = 0; i < 100 && result == null; i++) { await Task.Delay(10); result = gate.Take(); }
            Check(result != null && result.Error == "new", "new generation");
            late.SetResult(new ReaderResult { Error = "old" }); await Task.Delay(50); Check(gate.Take() == null, "late callback discarded");
            var closed = new TaskCompletionSource<ReaderResult>(); var began = new TaskCompletionSource<bool>();
            gate.Begin(t => { began.SetResult(true); return closed.Task; }); await began.Task; gate.Dispose(); closed.SetResult(new ReaderResult()); await Task.Delay(50); Check(gate.Take() == null, "closed UI cannot receive result");
        }
        byte[] last = null; var first = new ManualResetEventSlim(false); var release = new ManualResetEventSlim(false);
        var writer = new PositionWriter(bytes => { first.Set(); release.Wait(); last = bytes; });
        writer.Save(Bytes("old")); Check(first.Wait(3000), "writer started"); writer.Save(Bytes("middle")); writer.Save(Bytes("latest")); release.Set();
        for (int i = 0; i < 100 && (last == null || Encoding.UTF8.GetString(last) != "latest"); i++) await Task.Delay(10);
        Check(Encoding.UTF8.GetString(last) == "latest", "position write ordering"); first.Dispose(); release.Dispose();
    }
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;
        internal Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) { this.send = send; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { token.ThrowIfCancellationRequested(); return send(request, token); }
    }
    private class NonSeekStream : MemoryStream { internal NonSeekStream(byte[] data) : base(data) { } public override bool CanSeek { get { return false; } } }
    private sealed class TrackStream : NonSeekStream
    {
        internal bool Disposed; internal TrackStream(byte[] data) : base(data) { }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    private sealed class Files : IFileStore
    {
        private readonly string root; internal Files(string root) { this.root = root; }
        private string PathOf(string name) { return Path.Combine(root, name); }
        public void Create() { Directory.CreateDirectory(root); }
        public IEnumerable<string> Names() { foreach (string file in Directory.GetFiles(root)) yield return Path.GetFileName(file); }
        public long Size(string name) { return new FileInfo(PathOf(name)).Length; }
        public DateTime Modified(string name) { return File.GetLastWriteTimeUtc(PathOf(name)); }
        public Stream Read(string name) { return File.OpenRead(PathOf(name)); }
        public Stream Write(string name) { return File.Create(PathOf(name)); }
        public void Move(string source, string destination) { if (File.Exists(PathOf(destination))) File.Delete(PathOf(destination)); File.Move(PathOf(source), PathOf(destination)); }
        public void Delete(string name) { File.Delete(PathOf(name)); }
    }
}
