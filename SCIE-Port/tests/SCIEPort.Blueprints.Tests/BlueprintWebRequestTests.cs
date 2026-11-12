using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SCIEPort.Features.Blueprints.Networking;

internal static partial class BlueprintTests
{
    // Exercises the production adapter, rather than just the injected HttpClient mocks.
    private static async Task TestBlueprintWebRequest()
    {
        Console.WriteLine("[Production TCP/TLS transport against loopback HTTP]");
        TestBlueprintHttpReader();
        await TestBlueprintTls();
        var reserve = new TcpListener(IPAddress.Loopback, 0);
        reserve.Start();
        int port = ((IPEndPoint)reserve.LocalEndpoint).Port;
        reserve.Stop();
        string address = "http://127.0.0.1:" + port + "/";
        using (var listener = new HttpListener())
        {
            listener.Prefixes.Add(address);
            listener.Start();
            byte[] json = Encoding.UTF8.GetBytes("{\"apiVersion\":1,\"page\":1,\"pageSize\":20,\"total\":0,\"items\":[]}");
            Func<HttpListenerResponse, Task> sendJson = async response =>
            {
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = json.Length;
                await response.OutputStream.WriteAsync(json, 0, json.Length);
            };
            Func<Func<HttpListenerResponse, Task>, CancellationToken, Task<byte[]>> exchange = async (respond, token) =>
            {
                Task<HttpListenerContext> accept = listener.GetContextAsync();
                Task<byte[]> get = BlueprintCloudConnection.Get(new Uri(address), token);
                Task first = await Task.WhenAny(accept, get, Task.Delay(5000));
                if (!accept.IsCompleted)
                {
                    if (first == get) return await get;
                    throw new Exception("Loopback request did not reach listener within 5 seconds.");
                }
                HttpListenerContext context = await accept;
                Check(context.Request.HttpMethod == "GET" && context.Request.Headers["Accept"] == "application/json" && context.Request.Headers["Accept-Encoding"] == "identity"
                    && context.Request.Headers["Authorization"] == null && context.Request.Headers["Cookie"] == null,
                    "actual wire request has JSON/identity headers and no authorization or cookie");
                Task server = Task.Run(async () =>
                {
                    try { await respond(context.Response); }
                    catch (HttpListenerException) { /* Expected when a rejected/cancelled response is closed. */ }
                    catch (ObjectDisposedException) { }
                    finally
                    {
                        try { context.Response.Close(); }
                        catch (InvalidOperationException) { context.Response.Abort(); } // Deliberately incomplete test bodies.
                    }
                });
                try
                {
                    if (await Task.WhenAny(get, Task.Delay(17000)) != get) throw new Exception("Production HTTP request exceeded bounded test deadline.");
                    return await get;
                }
                finally
                {
                    if (await Task.WhenAny(server, Task.Delay(3000)) != server) throw new Exception("Loopback response did not stop within 3 seconds.");
                    await server;
                }
            };
            Check(BlueprintCloudProtocol.Page(await exchange(sendJson, CancellationToken.None), 1).Total == 0,
                "production adapter reads and decodes a real response");
            foreach (int status in new[] { 302, 404, 409, 429, 503 })
            {
                Console.WriteLine("  production HTTP " + status);
                bool rejected = false;
                try
                {
                    await exchange(response =>
                    {
                        response.StatusCode = status;
                        if (status == 302) response.RedirectLocation = address + "must-not-follow";
                        return Task.FromResult(0);
                    }, CancellationToken.None);
                }
                catch (BlueprintHttpException error) { rejected = error.Status == status; }
                Check(rejected, "production adapter preserves HTTP " + status + " without following a redirect or reading error body");
            }
            foreach (string contentType in new[] { "text/html", "application/json; charset=utf-16" })
            {
                Console.WriteLine("  production content type " + contentType);
                bool rejected = false;
                try { await exchange(response => { response.ContentType = contentType; return Task.FromResult(0); }, CancellationToken.None); }
                catch (InvalidDataException) { rejected = true; }
                Check(rejected, "production adapter rejects " + contentType);
            }
            bool encodingRejected = false;
            Console.WriteLine("  production encoding and size limits");
            try { await exchange(response => { response.ContentType = "application/json"; response.Headers["Content-Encoding"] = "gzip"; return Task.FromResult(0); }, CancellationToken.None); }
            catch (InvalidDataException) { encodingRejected = true; }
            Check(encodingRejected, "production adapter does not silently decompress a compressed response");
            bool oversized = false;
            try
            {
                await exchange(async response =>
                {
                    response.ContentType = "application/json";
                    response.ContentLength64 = BlueprintCloudProtocol.MaxBytes + 1;
                    await response.OutputStream.WriteAsync(new byte[] { (byte)'{' }, 0, 1);
                    await response.OutputStream.FlushAsync();
                }, CancellationToken.None);
            }
            catch (InvalidDataException) { oversized = true; }
            Check(oversized, "production adapter rejects excessive declared length before reading body");
            oversized = false;
            try
            {
                await exchange(async response =>
                {
                    response.ContentType = "application/json";
                    response.SendChunked = true;
                    byte[] body = new byte[BlueprintCloudProtocol.MaxBytes + 1];
                    await response.OutputStream.WriteAsync(body, 0, body.Length);
                }, CancellationToken.None);
            }
            catch (InvalidDataException) { oversized = true; }
            Check(oversized, "production adapter bounds chunked body without Content-Length");
            foreach (bool body in new[] { false, true })
            {
                Console.WriteLine("  production cancellation: " + (body ? "body" : "headers"));
                using (var caller = new CancellationTokenSource())
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    bool cancelled = false;
                    try
                    {
                        await exchange(async response =>
                        {
                            if (body)
                            {
                                response.ContentType = "application/json";
                                response.ContentLength64 = 100;
                                await response.OutputStream.WriteAsync(new byte[] { (byte)'{' }, 0, 1);
                                await response.OutputStream.FlushAsync();
                            }
                            await Task.Delay(150);
                            caller.Cancel();
                            await Task.Delay(150);
                        }, caller.Token);
                    }
                    catch (OperationCanceledException) { cancelled = true; }
                    Check(cancelled && watch.Elapsed.TotalSeconds < 3, "socket close interrupts pending " + (body ? "body" : "headers") + " and preserves caller cancellation");
                }
            }
            var deadline = new BlueprintRequestDeadline(CancellationToken.None);
            Console.WriteLine("  production 12-second body timeout");
            try
            {
                bool timedOut = false;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    await exchange(async response =>
                    {
                        response.ContentType = "application/json";
                        response.ContentLength64 = 100;
                        await response.OutputStream.WriteAsync(new byte[] { (byte)'{' }, 0, 1);
                        await response.OutputStream.FlushAsync();
                        while (!deadline.Token.IsCancellationRequested) await Task.Delay(25);
                    }, deadline.Token);
                }
                catch (OperationCanceledException) { timedOut = true; }
                Check(timedOut && watch.Elapsed.TotalSeconds >= 11 && watch.Elapsed.TotalSeconds < 16,
                    "production socket body read is interrupted by the actual 12-second deadline");
            }
            finally { await deadline.Stop(); }
        }
        // A refused TCP connection must still be eligible for existing network cache fallback.
        bool networkFailure = false;
        try { await BlueprintCloudConnection.Get(new Uri(address), CancellationToken.None); }
        catch (Exception error) { networkFailure = BlueprintNetworkFailure.Code(error) == "NET"; }
        Check(networkFailure, "production connection failure retains NET classification");
    }

    private static void TestBlueprintHttpReader()
    {
        Func<string, byte[]> read = text =>
        {
            using (var input = new MemoryStream(Encoding.ASCII.GetBytes(text)))
                return BlueprintHttpReader.Read(input, CancellationToken.None);
        };
        string head = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n";
        Check(Encoding.ASCII.GetString(read(head + "Content-Length: 2\r\n\r\n{}")) == "{}", "fixed-length JSON response");
        Check(Encoding.ASCII.GetString(read(head + "\r\n{}")) == "{}", "close-delimited JSON response");
        Check(Encoding.ASCII.GetString(read("HTTP/1.1 103 Early Hints\r\nLink: </ignore>\r\n\r\n" + head
            + "Transfer-Encoding: chunked\r\n\r\n1;ext=yes\r\n{\r\n1\r\n}\r\n0\r\nX-Checksum: ignored\r\n\r\n")) == "{}", "bounded interim response, chunk extensions and harmless trailers");
        foreach (string response in new[] {
            "HTTP/1.1 abc OK\r\n\r\n", "HTTP/1.1 2000 OK\r\n\r\n", "HTTP/2 200 OK\r\n\r\n", "HTTP/1.1 200 OK\n\n",
            head + "Content-Length: 2\r\nContent-Length: 2\r\n\r\n{}",
            head + "Content-Length: 2\r\nTransfer-Encoding: chunked\r\n\r\n{}",
            head + "Transfer-Encoding: gzip, chunked\r\n\r\n", head + "Transfer-Encoding: chunked\r\nTransfer-Encoding: chunked\r\n\r\n",
            head + "Content-Length: +2\r\n\r\n{}", head + "Content-Length: -1\r\n\r\n", head + "Content-Length: 999999999999999999999\r\n\r\n",
            head + "Content-Type: application/json\r\n\r\n{}", head + "Content-Encoding: gzip\r\nContent-Encoding: \r\n\r\n{}",
            head + " Invalid: folded-header\r\n\r\n{}", head + "X-Test: " + new string('a', 32768) + "\r\n\r\n{}",
            head + "Transfer-Encoding: chunked\r\n\r\n+2\r\n{}\r\n0\r\n\r\n",
            head + "Transfer-Encoding: chunked\r\n\r\n2\r\n{}X\n0\r\n\r\n",
            head + "Transfer-Encoding: chunked\r\n\r\n0\r\nContent-Length: 2\r\n\r\n",
            "HTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 100 Continue\r\n\r\nHTTP/1.1 100 Continue\r\n\r\n" + head + "\r\n{}"
        })
        {
            bool invalid = false;
            try { read(response); } catch (InvalidDataException) { invalid = true; }
            Check(invalid, "ambiguous, malformed or excessive HTTP framing rejected");
        }
        foreach (string response in new[] { head + "Content-Length: 3\r\n\r\n{}", head + "Transfer-Encoding: chunked\r\n\r\n3\r\n{}", head })
        {
            bool incomplete = false;
            try { read(response); } catch (IOException) { incomplete = true; }
            Check(incomplete, "truncated headers/body never become valid cached JSON");
        }
        foreach (int status in new[] { 101, 301, 302, 303, 307, 308, 401, 403, 404, 409, 429, 503 })
        {
            bool rejected = false;
            try { read("HTTP/1.1 " + status + " Rejected\r\n"); }
            catch (BlueprintHttpException error) { rejected = error.Status == status; }
            Check(rejected, "HTTP " + status + " rejected before headers/body, preserving original status");
        }
        bool oversize = false;
        try { read(head + "\r\n" + new string('x', BlueprintCloudProtocol.MaxBytes + 1)); } catch (InvalidDataException) { oversize = true; }
        Check(oversize, "close-delimited body bounded without Content-Length");
        Check(read(head + "Content-Length: " + BlueprintCloudProtocol.MaxBytes + "\r\n\r\n" + new string('x', BlueprintCloudProtocol.MaxBytes)).Length == BlueprintCloudProtocol.MaxBytes,
            "exact 2MiB boundary accepted");
    }

    private static async Task TestBlueprintTls()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        string address = "https://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/";
        try
        {
            using (var rsa = RSA.Create(2048))
            using (var generated = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)))
            // Schannel on net472 needs an imported private-key credential rather
            // than CertificateRequest's ephemeral generated key.
            using (var certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx)))
            {
                Task<TcpClient> accept = listener.AcceptTcpClientAsync();
                Task<byte[]> get = BlueprintCloudConnection.Get(new Uri(address), CancellationToken.None);
                using (TcpClient server = await accept)
                {
                    bool sentGet = false;
                    Task serve = Task.Run(() =>
                    {
                        using (var tls = new SslStream(server.GetStream(), false))
                        {
                            try { tls.AuthenticateAsServer(certificate, false, SslProtocols.Tls12, false); sentGet = tls.ReadByte() >= 0; }
                            catch (AuthenticationException) { }
                            catch (IOException) { }
                        }
                    });
                    bool rejected = false;
                    try { await get; } catch (Exception error) { rejected = BlueprintNetworkFailure.Code(error) == "TLS"; }
                    await serve;
                    Check(rejected && !sentGet, "untrusted/mismatched TLS certificate rejected before sending HTTP, without validation callbacks");
                }
            }
            using (var caller = new CancellationTokenSource())
            {
                Task<TcpClient> accept = listener.AcceptTcpClientAsync();
                Task<byte[]> get = BlueprintCloudConnection.Get(new Uri(address), caller.Token);
                using (TcpClient server = await accept)
                {
                    await Task.Delay(150);
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    caller.Cancel();
                    bool cancelled = false;
                    try { await get; } catch (OperationCanceledException) { cancelled = true; }
                    Check(cancelled && watch.Elapsed.TotalSeconds < 3, "cancellation interrupts a stalled TLS handshake");
                }
            }
        }
        finally { listener.Stop(); }
    }
}
