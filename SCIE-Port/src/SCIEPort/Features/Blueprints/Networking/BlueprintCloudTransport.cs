using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SCIEPort.Features.Blueprints.Networking
{
    internal sealed class BlueprintHttpException : Exception
    {
        internal readonly int Status;
        internal BlueprintHttpException(int status) : base("HTTP " + status) { Status = status; }
    }
    // Same Android-safe cancellation strategy as LibraryClient, without a cross-mod dependency.
    internal sealed class BlueprintRequestDeadline
    {
        private readonly CancellationTokenSource source = new CancellationTokenSource();
        private readonly Task observer;
        private volatile bool finished;
        internal CancellationToken Token { get { return source.Token; } }
        internal BlueprintRequestDeadline(CancellationToken caller)
        { observer = Observe(caller, Stopwatch.GetTimestamp()); }
        private async Task Observe(CancellationToken caller, long started)
        {
            while (!finished)
            {
                if (caller.IsCancellationRequested || unchecked(Stopwatch.GetTimestamp() - started) >= Stopwatch.Frequency * 12)
                { source.Cancel(); return; }
                await Task.Delay(25).ConfigureAwait(false);
            }
        }
        internal async Task Stop() { finished = true; try { await observer.ConfigureAwait(false); } finally { source.Dispose(); } }
    }
    internal sealed class BlueprintCloudTransport
    {
        internal const string DefaultEndpoint = "https://wxhappylife.top/digital-study/api/game-blueprints/v1/blueprints";
        private readonly string endpoint;
        private readonly Func<HttpMessageHandler> handlerFactory;
        internal BlueprintCloudTransport(string endpoint = DefaultEndpoint, Func<HttpMessageHandler> handlerFactory = null)
        {
            // Exact canonical endpoint prevents URI normalization, encoded traversal and alternate ports.
            if (endpoint != DefaultEndpoint && endpoint != "https://wxhappylife.top/api/game-blueprints/v1/blueprints") throw new InvalidDataException("蓝图库地址不在 HTTPS 白名单中。");
            this.endpoint = endpoint; this.handlerFactory = handlerFactory;
        }
        internal string ListPath(int page)
        { if (page < 1 || page > 10000) throw new InvalidDataException("分页超出范围。"); return "?page=" + page.ToString(CultureInfo.InvariantCulture) + "&pageSize=20"; }
        internal string JsonPath(CloudBlueprintEntry entry)
        {
            if (entry == null || !BlueprintCloudProtocol.Id(entry.Id) || entry.Revision < 1 || entry.Revision == int.MaxValue || !BlueprintCloudProtocol.Hex(entry.Sha256, 64)) throw new InvalidDataException("蓝图版本信息无效。");
            return "/" + entry.Id + "/json?revision=" + entry.Revision.ToString(CultureInfo.InvariantCulture);
        }
        private static bool PositiveDecimal(string text, int max, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text) || text.Length > 10 || text[0] == '0') return false;
            foreach (char c in text)
            {
                int digit = c - '0';
                if (digit < 0 || digit > 9 || value > (max - digit) / 10) return false;
                value = value * 10 + digit;
            }
            return value > 0 && value <= max;
        }
        internal async Task<byte[]> Get(string path, CancellationToken token)
        {
            // Only the two generated request shapes can reach the network.
            bool valid = false;
            if (path != null && path.StartsWith("?page=", StringComparison.Ordinal))
            { int page; int end = path.IndexOf('&'); valid = end > 6 && PositiveDecimal(path.Substring(6, end - 6), 10000, out page) && path == ListPath(page); }
            else if (path != null && path.Length >= 53 && path[0] == '/' && path.Substring(37).StartsWith("/json?revision=", StringComparison.Ordinal))
            { int revision; valid = BlueprintCloudProtocol.Id(path.Substring(1, 36)) && PositiveDecimal(path.Substring(52), int.MaxValue - 1, out revision) && path == "/" + path.Substring(1, 36) + "/json?revision=" + revision.ToString(CultureInfo.InvariantCulture); }
            if (!valid) throw new InvalidDataException("蓝图库请求路径无效。");
            var deadline = new BlueprintRequestDeadline(token);
            try
            {
                deadline.Token.ThrowIfCancellationRequested();
                var uri = new Uri(endpoint + path);
                if (handlerFactory == null)
                    return await BlueprintCloudConnection.Get(uri, deadline.Token).ConfigureAwait(false);
                using (HttpMessageHandler handler = handlerFactory())
                using (var client = new HttpClient(handler))
                {
                    client.DefaultRequestHeaders.Add("Accept", "application/json"); client.DefaultRequestHeaders.Add("Accept-Encoding", "identity");
                    using (HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                        return await ReadResponse(response, deadline.Token).ConfigureAwait(false);
                }
            }
            finally { await deadline.Stop().ConfigureAwait(false); }
        }
        internal static async Task<byte[]> ReadResponse(HttpResponseMessage response, CancellationToken token)
        {
            if (response.StatusCode != HttpStatusCode.OK) throw new BlueprintHttpException((int)response.StatusCode);
            var content = response.Content;
            if (content == null || content.Headers.ContentType == null || !string.Equals(content.Headers.ContentType.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("蓝图库返回了非 JSON 响应。");
            string charset = content.Headers.ContentType.CharSet;
            if ((!string.IsNullOrEmpty(charset) && !string.Equals(charset.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase)) || content.Headers.ContentEncoding.Count != 0 || content.Headers.ContentLength > BlueprintCloudProtocol.MaxBytes) throw new InvalidDataException("蓝图库响应编码或长度无效。");
            using (Stream input = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[4096]; int read;
                while ((read = await input.ReadAsync(buffer, 0, Math.Min(buffer.Length, BlueprintCloudProtocol.MaxBytes + 1 - (int)output.Length), token).ConfigureAwait(false)) > 0)
                { output.Write(buffer, 0, read); if (output.Length > BlueprintCloudProtocol.MaxBytes) throw new InvalidDataException("蓝图库响应过大。"); }
                token.ThrowIfCancellationRequested(); return output.ToArray();
            }
        }
    }
}
