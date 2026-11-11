// Adapted from Builder Assistant 0.25.1 at 3b6b08e; executes the independent SCIEPort implementation.
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SCIEPort.Features.Blueprints.Core.Blueprints;
using SCIEPort.Features.Blueprints.Core.Json;
using SCIEPort.Features.Blueprints.Networking;

    internal sealed class CloudTestHandler : HttpMessageHandler
    {
        internal Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) { return Respond(request, token); }
    }
    internal sealed class CloudTestBody : HttpContent
    {
        private readonly byte[] bytes;
        private readonly bool slow;
        internal CloudTestBody(byte[] bytes, bool slow) { this.bytes=bytes;this.slow=slow; }
        protected override bool TryComputeLength(out long length) { length=0;return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context) { return stream.WriteAsync(bytes,0,bytes.Length); }
        protected override Task<Stream> CreateContentReadStreamAsync() { return Task.FromResult<Stream>(new CloudTestStream(bytes,slow)); }
    }
    internal sealed class CloudTestStream : MemoryStream
    {
        private readonly bool slow;
        internal CloudTestStream(byte[] bytes,bool slow):base(bytes){this.slow=slow;}
        public override async Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken token)
        { if(slow)await Task.Delay(30000,token);return await base.ReadAsync(buffer,offset,count,token); }
    }
    internal static partial class BlueprintTests
    {
        private static async Task TestCloudBlueprints()
        {
            Console.WriteLine("[Cloud blueprint protocol, transport, persistence and failure contracts]");
            string root = Path.Combine(Path.GetTempPath(), "blueprint-cloud-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string json = "{\"schemaVersion\":3,\"kind\":\"builderassistant.blueprint\",\"name\":\"云端木屋\",\"size\":{\"x\":1,\"y\":1,\"z\":1},\"cells\":[{\"x\":0,\"y\":0,\"z\":0,\"key\":\"PlanksBlock\"}]}\n";
                byte[] raw = Encoding.UTF8.GetBytes(json);
                string meta = "{\"apiVersion\":1,\"id\":\"11111111-1111-4111-8111-111111111111\",\"revision\":1,\"sha256\":\"" + BlueprintCloudProtocol.Hash(raw) + "\",\"name\":\"云端木屋\",\"schemaVersion\":3,\"kind\":\"builderassistant.blueprint\",\"size\":{\"x\":1,\"y\":1,\"z\":1},\"blockCount\":1,\"byteLength\":" + raw.Length + ",\"status\":\"published\"}";
                byte[] catalog = Encoding.UTF8.GetBytes("{\"apiVersion\":1,\"page\":1,\"pageSize\":20,\"total\":1,\"items\":[" + meta + "]}");
                var entry = BlueprintCloudProtocol.Entry(JsonValue.Parse(meta));
                Check(BlueprintCloudProtocol.Download(raw, entry).Blueprint.CellCount == 1, "cloud bytes use the existing BlueprintImportService");
                byte[] escaped = Encoding.UTF8.GetBytes(json.Replace("云端木屋", "\\u4e91\\u7aef\\u6728\\u5C4B"));
                var escapedEntry = BlueprintCloudProtocol.Entry(JsonValue.Parse(meta));escapedEntry.Sha256=BlueprintCloudProtocol.Hash(escaped);escapedEntry.ByteLength=escaped.Length;
                Check(BlueprintCloudProtocol.Download(escaped,escapedEntry).Blueprint.Name==entry.Name,"Unicode escaped cloud JSON reuses importer without System.TryParse");
                bool invalidEscape=false;try{JsonValue.Parse("{\"name\":\"\\uZZZZ\"}");}catch(JsonParseException){invalidEscape=true;}
                Check(invalidEscape,"invalid hexadecimal escape remains rejected");
                foreach (int schema in new[] { 1, 2, 3 })
                {
                    byte[] legacy = Encoding.UTF8.GetBytes(json.Replace("\"schemaVersion\":3", "\"schemaVersion\":" + schema));
                    var e = BlueprintCloudProtocol.Entry(JsonValue.Parse(meta)); e.SchemaVersion = schema; e.Sha256 = BlueprintCloudProtocol.Hash(legacy); e.ByteLength = legacy.Length;
                    Check(BlueprintCloudProtocol.Download(legacy, e).Blueprint.SchemaVersion == schema, "cloud preserves legacy schema " + schema);
                }
                Func<string, bool> rejected = value => { try { BlueprintCloudProtocol.Parse(Encoding.UTF8.GetBytes(value)); return false; } catch (Exception) { return true; } };
                Check(rejected("{\"a\":1,\"a\":2}") && rejected("{\"a\":" + new string('[', 33) + "0" + new string(']', 33) + "}"), "duplicate keys and deep JSON rejected before canonical import");
                foreach (string endpoint in new[] { "http://wxhappylife.top/api/game-blueprints/v1/blueprints", "https://evil.test/api/game-blueprints/v1/blueprints", BlueprintCloudTransport.DefaultEndpoint + "/../books", BlueprintCloudTransport.DefaultEndpoint + "?x=1" })
                { bool failed = false; try { new BlueprintCloudTransport(endpoint); } catch (InvalidDataException) { failed = true; } Check(failed, "endpoint outside exact HTTPS allowlist rejected"); }
                bool offline = false, block = false, streamed = false, slowBody = false; int status = 200, calls = 0; byte[] overrideBody = null; string type = "application/json";Exception injected=null;
                var transport = new BlueprintCloudTransport(BlueprintCloudTransport.DefaultEndpoint, () => new CloudTestHandler { Respond = async (request, token) =>
                {
                    calls++;
                    Check(request.RequestUri.Host == "wxhappylife.top" && request.RequestUri.Scheme == "https", "request never leaves approved HTTPS source");
                    Check(request.Headers.AcceptEncoding.ToString() == "identity" && request.Headers.Authorization == null, "public mod request has no credentials and disables compression");
                    if(injected!=null)throw injected;
                    if (offline) throw new HttpRequestException("offline");
                    if (block) await Task.Delay(30000, token);
                    byte[] bytes=overrideBody ?? (request.RequestUri.AbsolutePath.EndsWith("/json", StringComparison.Ordinal) ? raw : catalog);
                    var response = new HttpResponseMessage((HttpStatusCode)status) { Content = streamed ? (HttpContent)new CloudTestBody(bytes,slowBody) : new ByteArrayContent(bytes) };
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type); return response;
                } });
                var logs=new System.Collections.Generic.List<string>();
                var cache = new BlueprintCloudCache(root); var client = new BlueprintCloudClient(cache, transport, logs.Add);
                bool badPath = false; try { await transport.Get("/../books", CancellationToken.None); } catch (InvalidDataException) { badPath = true; }
                Check(badPath && calls == 0, "path traversal rejected before HTTP");
                foreach(string path in new[]{"?page=01&pageSize=20","?page=0&pageSize=20","?page=10001&pageSize=20","?page=2147483648&pageSize=20","?page=+1&pageSize=20","?page=１&pageSize=20","/"+entry.Id+"/json?revision=01","/"+entry.Id+"/json?revision=2147483647","/"+entry.Id+"/json?revision=2147483648"})
                {bool rejectedPath=false;try{await transport.Get(path,CancellationToken.None);}catch(InvalidDataException){rejectedPath=true;}Check(rejectedPath&&calls==0,"strict canonical numeric path rejected before HTTP: "+path);}
                foreach (string path in new[] { "?page=10000&pageSize=20", "/" + entry.Id + "/json?revision=2147483646" })
                    Check((await transport.Get(path, CancellationToken.None)).Length > 0, "canonical maximum numeric path remains supported");
                var page = await client.List(1, CancellationToken.None); Check(page.Value != null && !page.Offline, "published paginated catalog decoded and cached");
                injected = new IOException("connection reset during response body");
                Check((await client.List(1, CancellationToken.None)).Offline, "interrupted response stream falls back to verified cache");
                injected = null;
                var downloaded = await client.Download(entry, CancellationToken.None); Check(downloaded.Value != null && downloaded.Value.Json == json && !downloaded.CacheFailed, "download checks SHA, schema, count and dimensions before success");
                var library = new BlueprintFileLibrary(Path.Combine(root, "library")); string filename, filename2, error;
                Check(library.SaveExact("cloud-" + entry.Id + "-r1", downloaded.Value.Json, out filename) == null, "download saved in original local file library");
                Check(library.SaveExact("cloud-" + entry.Id + "-r1", downloaded.Value.Json, out filename2) == null && filename2 == filename && library.ListNames(out error).Count == 1, "repeat download of same revision is idempotent");
                Check(library.SaveExact("cloud-" + entry.Id + "-r1", "changed", out filename2) != null, "local modification is never overwritten by cloud save");
                var restarted = new BlueprintCloudClient(new BlueprintCloudCache(root), transport); offline = true;
                Check((await restarted.List(1, CancellationToken.None)).Offline && (await restarted.Download(entry, CancellationToken.None)).Offline, "fresh client recovers catalog and exact version from disk when disconnected");
                var restored = BlueprintImportService.FromString(library.Load(Path.GetFileNameWithoutExtension(filename), out error), null);
                Check(restored.Success && restored.Blueprint.Name == "云端木屋", "restart offline local load reuses canonical parser");
                var updated = BlueprintCloudProtocol.Entry(JsonValue.Parse(meta)); updated.Revision = 2;
                Check((await restarted.Download(updated, CancellationToken.None)).Value == null, "revision 2 never uses revision 1 cache");
                offline = false; status = 409;
                Check((await client.Download(entry, CancellationToken.None)).Value == null, "authoritative version conflict never loads stale cache");
                status = 200; await client.Download(entry, CancellationToken.None); status = 404;
                Check((await client.Download(entry, CancellationToken.None)).Value == null, "unpublished content never falls back after 404");
                status = 200; await client.Download(entry, CancellationToken.None); overrideBody = Encoding.UTF8.GetBytes("invalid json");
                Check((await client.Download(entry, CancellationToken.None)).Value == null, "bad JSON/hash cannot resurrect previously cached content");
                overrideBody = new byte[BlueprintCloudProtocol.MaxBytes + 1];
                Check((await client.List(1, CancellationToken.None)).Value == null, "oversized response rejected");
                streamed=true;
                Check((await client.List(1, CancellationToken.None)).Value == null, "streaming response without Content-Length is bounded");
                overrideBody=null;slowBody=true;var watch=System.Diagnostics.Stopwatch.StartNew();
                var timedOut=await client.List(1,CancellationToken.None);
                Check(timedOut.Value==null && timedOut.Error.Contains("超时") && watch.Elapsed.TotalSeconds<20, "12-second deadline also cancels stalled response body");
                streamed=false;slowBody=false;
                overrideBody = null; type = "text/html";
                Check((await client.List(1, CancellationToken.None)).Value == null, "HTML login/error page rejected");
                type = "application/json"; status = 302;
                Check((await client.List(1, CancellationToken.None)).Value == null, "redirect response rejected");
                status = 200; overrideBody = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(catalog).Replace("\"apiVersion\":1", "\"apiVersion\":2"));
                Check((await client.List(1, CancellationToken.None)).Value == null, "unknown API version rejected");
                overrideBody = null; block = true;
                using (var cancel = new CancellationTokenSource())
                {
                    Task<CloudResult<CloudBlueprintPage>> pending = client.List(1, cancel.Token); cancel.Cancel();
                    bool canceled = false; try { await pending; } catch (OperationCanceledException) { canceled = true; }
                    Check(canceled, "dialog cancellation aborts request without cached adoption");
                }
                block = false;
                await client.List(1, CancellationToken.None);
                injected=new HttpRequestException("runtime failure", new MissingMethodException("System.Int32.TryParse"));
                var missing=await client.List(1,CancellationToken.None);
                Check(missing.Value==null&&missing.Error.Contains("[BCL]"),"wrapped runtime API failure reports BCL rather than adopting stale cache");
                Check(logs[logs.Count-1].Contains("stage=http")&&logs[logs.Count-1].Contains("System.Int32.TryParse"),"runtime failure log retains stage, type and missing member");
                injected=new HttpRequestException("TLS",new System.Security.Authentication.AuthenticationException("certificate failure"));
                Check((await client.List(1,CancellationToken.None)).Error.Contains("[TLS]"),"TLS error is distinguished from JSON and runtime errors");
                injected=null;overrideBody=Encoding.UTF8.GetBytes("{");
                Check((await client.List(1,CancellationToken.None)).Error.Contains("[DATA]")&&logs[logs.Count-1].Contains("stage=decode"),"invalid JSON has DATA feedback and decode-stage diagnostics");
                overrideBody=null;
                var brokenSink=new BlueprintCloudClient(cache,transport,s=>{throw new IOException("log unavailable");});injected=new MissingMethodException("test");
                Check((await brokenSink.List(1,CancellationToken.None)).Error.Contains("[BCL]"),"broken diagnostic sink cannot replace the underlying error");injected=null;
                var unsupported = BlueprintImportService.FromString(json.Replace("PlanksBlock", "Nonexistent"), k => k != "Nonexistent");
                Check(!unsupported.Success, "current material callback rejects unsupported cloud material");
                var terrain = new Grid();
                var preview = SCIEPort.Features.Blueprints.Core.Workbench.BlueprintPlacementPreview.Inspect(downloaded.Value.Blueprint, new SCIEPort.Features.Blueprints.Core.CellPos(0,30,0), new SCIEPort.Features.Blueprints.VanillaBlockPalette(), terrain, false);
                Check(preview.Ready && terrain.Writes == 0, "downloaded blueprint can enter existing preview without terrain writes");
                string fixtures = Environment.GetEnvironmentVariable("BLUEPRINT_FIXTURES");
                if (!string.IsNullOrEmpty(fixtures))
                {
                    var serverPage = BlueprintCloudProtocol.Page(File.ReadAllBytes(Path.Combine(fixtures, "catalog.json")), 1);
                    Check(BlueprintCloudProtocol.Download(File.ReadAllBytes(Path.Combine(fixtures, "blueprint.json")), serverPage.Items[0]).Blueprint.CellCount == 1, "actual Nest/MySQL response is accepted unchanged by mod protocol and importer");
                }
            }
            finally { if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true); }
        }
    }
