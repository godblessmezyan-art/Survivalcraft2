using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SCIEPort.Features.Blueprints.Networking;

namespace SCIEPort.Blueprints.Tests
{
    internal static class BlueprintHttpTests
    {
        private static void Check(bool valid, string name) { if (!valid) throw new Exception(name); }
        internal static async Task Run()
        {
            using (var handler = (HttpClientHandler)BlueprintHttpPolicy.Create())
                Check(!handler.AllowAutoRedirect && !handler.UseCookies && handler.AutomaticDecompression == DecompressionMethods.None, "desktop HTTP policy enforced");
            var trimmed = new BlueprintTrimmedHandler();
            BlueprintHttpPolicy.Configure(trimmed);
            Check(!trimmed.AllowAutoRedirect && !trimmed.UseCookies && trimmed.AutomaticDecompression == DecompressionMethods.None, "trimmed Android layout works without public setters");
            foreach (object unsupported in new object[] { new BlueprintUnknownHandler(), new BlueprintIgnoredSetterHandler() })
            {
                bool rejected = false;
                try { BlueprintHttpPolicy.Configure(unsupported); } catch (NotSupportedException) { rejected = true; }
                Check(rejected, "unknown backend or ignored policy fails before sending");
            }
            Check(BlueprintHttpPolicy.IsJson("application/json") && BlueprintHttpPolicy.IsJson("Application/Json; charset=utf-8"), "JSON content type without trimmed MediaType getter");
            foreach (string invalid in new[] { null, "", "application/jsonp", "application/json-evil; charset=utf-8", "text/html", "text/plain; application/json" })
                Check(!BlueprintHttpPolicy.IsJson(invalid), "exact JSON media type boundary");

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            string address = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            var requests = new List<string>();
            Task server = Task.Run(async () =>
            {
                for (int i = 0; i < 2; i++)
                using (TcpClient connection = await listener.AcceptTcpClientAsync())
                using (NetworkStream stream = connection.GetStream())
                using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                {
                    var headers = new StringBuilder(); string line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync())) headers.AppendLine(line);
                    requests.Add(headers.ToString());
                    byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: " + address + "/followed\r\nSet-Cookie: blueprint-test=1; Path=/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(response, 0, response.Length);
                }
            });
            try
            {
                using (var client = new HttpClient(BlueprintHttpPolicy.Create()))
                {
                    client.Timeout = TimeSpan.FromSeconds(3);
                    for (int i = 0; i < 2; i++)
                    using (HttpResponseMessage response = await client.GetAsync(address + "/blueprints"))
                        Check(response.StatusCode == HttpStatusCode.Found, "real handler returns redirect without following");
                }
                await server;
                Check(requests.Count == 2 && requests[1].StartsWith("GET /blueprints ", StringComparison.Ordinal) && requests[1].IndexOf("Cookie:", StringComparison.OrdinalIgnoreCase) < 0, "real handler neither follows Location nor replays cookies");
            }
            finally { listener.Stop(); }
        }
    }
}

// Test-owned layout doubles, not shipped or copied from proprietary assemblies.
#pragma warning disable CS0436
namespace System.Net.Http
{
    internal sealed class SocketsHttpHandler
    {
        internal readonly HttpConnectionSettings _settings = new HttpConnectionSettings();
    }
    internal sealed class HttpConnectionSettings
    {
        internal bool _allowAutoRedirect = true;
        internal bool _useCookies = true;
        internal DecompressionMethods _automaticDecompression = DecompressionMethods.GZip;
    }
}
internal sealed class BlueprintTrimmedHandler
{
    private readonly System.Net.Http.SocketsHttpHandler _delegatingHandler = new System.Net.Http.SocketsHttpHandler();
    public bool AllowAutoRedirect { get { return _delegatingHandler._settings._allowAutoRedirect; } }
    public bool UseCookies { get { return _delegatingHandler._settings._useCookies; } }
    public DecompressionMethods AutomaticDecompression { get { return _delegatingHandler._settings._automaticDecompression; } }
}
#pragma warning restore CS0436
internal sealed class BlueprintUnknownHandler
{
    public bool AllowAutoRedirect { get { return true; } }
    public bool UseCookies { get { return true; } }
    public DecompressionMethods AutomaticDecompression { get { return DecompressionMethods.GZip; } }
}
internal sealed class BlueprintIgnoredSetterHandler
{
    public bool AllowAutoRedirect { get { return true; } set { } }
    public bool UseCookies { get { return true; } set { } }
    public DecompressionMethods AutomaticDecompression { get { return DecompressionMethods.GZip; } set { } }
}
