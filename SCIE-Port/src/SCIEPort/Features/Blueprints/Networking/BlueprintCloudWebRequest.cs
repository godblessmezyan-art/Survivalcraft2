using System;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace SCIEPort.Features.Blueprints.Networking
{
    // The reported Android runtime has no HttpClientHandler.AllowAutoRedirect setter.
    // Use the older request API without changing global TLS/certificate policy.
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
                    response = (HttpWebResponse)await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null).ConfigureAwait(false);
                }
                catch (WebException error)
                {
                    // HTTP 4xx/5xx must retain their status for cache/removal policy.
                    response = error.Response as HttpWebResponse;
                    if (response == null) throw;
                }
                using (response)
                using (var message = new HttpResponseMessage(response.StatusCode))
                {
                    // Reject status before opening/reading an error or redirect body.
                    if (response.StatusCode != HttpStatusCode.OK) throw new BlueprintHttpException((int)response.StatusCode);
                    message.Content = new StreamContent(response.GetResponseStream());
                    CopyHeader(response, message, "Content-Type");
                    CopyHeader(response, message, "Content-Encoding");
                    if (response.ContentLength >= 0) message.Content.Headers.ContentLength = response.ContentLength;
                    return await BlueprintCloudTransport.ReadResponse(message, token).ConfigureAwait(false);
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

        private static void CopyHeader(HttpWebResponse response, HttpResponseMessage message, string name)
        {
            string value = response.Headers[name];
            if (!string.IsNullOrEmpty(value)) message.Content.Headers.Add(name, value);
        }

        private sealed class RequestObserver
        {
            private volatile bool finished;
            private readonly Task task;
            internal RequestObserver(HttpWebRequest request, CancellationToken token) { task = Observe(request, token); }
            internal async Task Stop() { finished = true; await task.ConfigureAwait(false); }
            private async Task Observe(HttpWebRequest request, CancellationToken token)
            {
                // Abort also interrupts a stalled body read; ReadAsync(token) alone
                // need not cancel an already pending read on every implementation.
                while (!finished)
                {
                    if (token.IsCancellationRequested) { request.Abort(); return; }
                    await Task.Delay(25).ConfigureAwait(false);
                }
            }
        }
    }
}
