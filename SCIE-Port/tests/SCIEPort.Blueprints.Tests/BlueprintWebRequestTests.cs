using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SCIEPort.Features.Blueprints.Networking;

internal static partial class BlueprintTests
{
    // Exercises the production adapter, rather than just the injected HttpClient mocks.
    private static async Task TestBlueprintWebRequest()
    {
        Console.WriteLine("[Production WebRequest transport against loopback HTTP]");
        var reserve = new TcpListener(IPAddress.Loopback, 0);
        reserve.Start();
        int port = ((IPEndPoint)reserve.LocalEndpoint).Port;
        reserve.Stop();
        string address = "http://127.0.0.1:" + port + "/";
        using (var listener = new HttpListener())
        {
            listener.Prefixes.Add(address);
            listener.Start();
            var request = BlueprintCloudWebRequest.Create(new Uri(address));
            Check(!request.AllowAutoRedirect && request.CookieContainer == null && request.Credentials == null && request.AutomaticDecompression == DecompressionMethods.None,
                "production requests disable redirects and have no cookies, credentials or decompression");
            Check(request.ServerCertificateValidationCallback == null, "production requests retain platform certificate validation");
            request.Abort();
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
                Task<byte[]> get = BlueprintCloudWebRequest.Get(new Uri(address), token);
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
                    Check(cancelled && watch.Elapsed.TotalSeconds < 3, "Abort interrupts pending " + (body ? "body" : "headers") + " and preserves caller cancellation");
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
        try { await BlueprintCloudWebRequest.Get(new Uri(address), CancellationToken.None); }
        catch (Exception error) { networkFailure = BlueprintNetworkFailure.Code(error) == "NET"; }
        Check(networkFailure, "production connection failure retains NET classification");
    }
}
