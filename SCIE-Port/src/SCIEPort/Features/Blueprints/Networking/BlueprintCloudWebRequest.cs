using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace SCIEPort.Features.Blueprints.Networking
{
    // The reported Android runtime has no HttpClientHandler.AllowAutoRedirect setter,
    // and also lacks the generic TaskFactory.FromAsync overload used to wrap the
    // Begin/End APM pair. Use only the base HttpWebRequest APM methods plus a
    // completion source; do not change global TLS/certificate policy.
    internal static class BlueprintCloudWebRequest
    {
        internal static HttpWebRequest Create(Uri uri)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.AllowAutoRedirect = false;
            // New HttpWebRequests have no cookie container, credentials or decompressor.
            request.Accept = "application/json";
            request.Headers[HttpRequestHeader.AcceptEncoding] = "identity";
            return request;
        }

        // TaskFactory.FromAsync<TResult>(Func<AsyncCallback,object,IAsyncResult>,
        // Func<IAsyncResult,TResult>,object) is missing on the Android/7723 runtime.
        // BeginGetResponse/EndGetResponse belong to the base WebRequest APM surface
        // and are present there, so bridge them with a TaskCompletionSource instead.
        private static Task<WebResponse> GetResponseAsync(HttpWebRequest request)
        {
            var completion = new TaskCompletionSource<WebResponse>();
            try
            {
                request.BeginGetResponse(delegate(IAsyncResult result)
                {
                    try { completion.TrySetResult(request.EndGetResponse(result)); }
                    catch (Exception error) { completion.TrySetException(error); }
                }, null);
            }
            catch (Exception error) { completion.TrySetException(error); }
            return completion.Task;
        }

        internal static async Task<byte[]> Get(Uri uri, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            HttpWebRequest request = Create(uri);
            var observer = new RequestObserver(request, token);
            try
            {
                HttpWebResponse response;
                try
                {
                    response = (HttpWebResponse)await GetResponseAsync(request).ConfigureAwait(false);
                }
                catch (WebException error)
                {
                    // HTTP 4xx/5xx must retain their status for cache/removal policy.
                    response = error.Response as HttpWebResponse;
                    if (response == null) throw;
                }
                using (response)
                {
                    // Reject status before opening/reading an error or redirect body.
                    if (response.StatusCode != HttpStatusCode.OK) throw new BlueprintHttpException((int)response.StatusCode);
                    return ReadBody(response, token);
                }
            }
            catch (Exception error)
            {
                // Abort can surface as WebException, IOException or ObjectDisposedException.
                token.ThrowIfCancellationRequested();
                var network = error as WebException;
                if (network == null) throw;
                if (network.Status == WebExceptionStatus.TrustFailure || network.Status == WebExceptionStatus.SecureChannelFailure)
                    throw new AuthenticationException("蓝图库 TLS 连接失败。", error);
                throw new HttpRequestException("蓝图库连接失败。", error);
            }
            finally
            {
                await observer.Stop().ConfigureAwait(false);
            }
        }

        // Equivalent to BlueprintCloudTransport.ReadResponse, but reads the
        // HttpWebResponse directly. Avoids HttpResponseMessage/StreamContent and
        // Stream.ReadAsync, which are additional BCL surface on the target runtime.
        private static byte[] ReadBody(HttpWebResponse response, CancellationToken token)
        {
            string contentType = response.ContentType ?? string.Empty;
            int semi = contentType.IndexOf(';');
            string mediaType = (semi < 0 ? contentType : contentType.Substring(0, semi)).Trim();
            if (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("蓝图库返回了非 JSON 响应。");
            if (semi >= 0)
            {
                foreach (string part in contentType.Substring(semi + 1).Split(';'))
                {
                    int equals = part.IndexOf('=');
                    if (equals < 0 || !string.Equals(part.Substring(0, equals).Trim(), "charset", StringComparison.OrdinalIgnoreCase)) continue;
                    string charset = part.Substring(equals + 1).Trim().Trim('"');
                    if (!string.IsNullOrEmpty(charset) && !string.Equals(charset, "utf-8", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("蓝图库响应编码或长度无效。");
                }
            }
            if (!string.IsNullOrEmpty(response.Headers["Content-Encoding"])) throw new InvalidDataException("蓝图库响应编码或长度无效。");
            if (response.ContentLength > BlueprintCloudProtocol.MaxBytes) throw new InvalidDataException("蓝图库响应过大。");
            using (Stream input = response.GetResponseStream())
            using (var output = new MemoryStream())
            {
                var buffer = new byte[4096];
                int read;
                while ((read = input.Read(buffer, 0, Math.Min(buffer.Length, BlueprintCloudProtocol.MaxBytes + 1 - (int)output.Length))) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                    if (output.Length > BlueprintCloudProtocol.MaxBytes) throw new InvalidDataException("蓝图库响应过大。");
                }
                token.ThrowIfCancellationRequested();
                return output.ToArray();
            }
        }

        private sealed class RequestObserver
        {
            private volatile bool finished;
            private readonly Task task;
            internal RequestObserver(HttpWebRequest request, CancellationToken token) { task = Observe(request, token); }
            internal async Task Stop() { finished = true; await task.ConfigureAwait(false); }
            private async Task Observe(HttpWebRequest request, CancellationToken token)
            {
                // Abort also interrupts a stalled synchronous body read; no
                // ReadAsync(token) is needed to observe the cancellation.
                while (!finished)
                {
                    if (token.IsCancellationRequested) { request.Abort(); return; }
                    await Task.Delay(25).ConfigureAwait(false);
                }
            }
        }
    }
}
