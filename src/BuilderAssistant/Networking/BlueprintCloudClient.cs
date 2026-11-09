using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BuilderAssistant.Networking
{
    internal sealed class BlueprintCloudClient
    {
        private readonly BlueprintCloudCache cache;
        private readonly BlueprintCloudTransport transport;
        internal BlueprintCloudClient(BlueprintCloudCache cache, BlueprintCloudTransport transport = null)
        { this.cache = cache; this.transport = transport ?? new BlueprintCloudTransport(); }
        private static bool OfflineFailure(Exception error)
        {
            var http = error as BlueprintHttpException;
            return error is HttpRequestException || error is OperationCanceledException || (http != null && http.Status >= 500 && http.Status <= 599);
        }
        private static string Message(Exception error)
        {
            var http = error as BlueprintHttpException;
            if (http != null) return http.Status == 409 ? "蓝图已更新，请刷新列表。" : http.Status == 404 || http.Status == 401 || http.Status == 403 ? "蓝图已下架或不可用。" : "蓝图库暂不可用（" + http.Status + "）。";
            if (error is OperationCanceledException) return "请求超时。";
            if (error is HttpRequestException) return "无法连接蓝图库。";
            if (error is InvalidDataException) return error.Message;
            return "蓝图库响应无效。";
        }
        private async Task<CloudResult<T>> Read<T>(string path, string key, Func<byte[], T> decode, CancellationToken token, bool offlineOnly) where T : class
        {
            var result = new CloudResult<T>();
            token.ThrowIfCancellationRequested();
            if (!offlineOnly)
            {
                try
                {
                    byte[] bytes = await transport.Get(path, token).ConfigureAwait(false);
                    result.Value = decode(bytes); token.ThrowIfCancellationRequested(); result.CacheFailed = !cache.Write(key, bytes); return result;
                }
                catch (Exception error)
                {
                    token.ThrowIfCancellationRequested();
                    result.Error = Message(error);
                    // Invalid JSON/hash/version, redirects and authoritative removals never fall back.
                    if (!OfflineFailure(error)) { cache.Delete(key); return result; }
                }
            }
            byte[] snapshot = cache.Read(key);
            if (snapshot != null)
            {
                try { result.Value = decode(snapshot); result.Offline = true; result.Error = null; return result; }
                catch (Exception) { cache.Delete(key); }
            }
            if (result.Error == null) result.Error = "尚无此版本的离线缓存。";
            return result;
        }
        internal Task<CloudResult<CloudBlueprintPage>> List(int page, CancellationToken token, bool offlineOnly = false)
        { return Read(transport.ListPath(page), "page-" + page + ".cache", bytes => BlueprintCloudProtocol.Page(bytes, page), token, offlineOnly); }
        internal Task<CloudResult<CloudBlueprintDownload>> Download(CloudBlueprintEntry entry, CancellationToken token, bool offlineOnly = false)
        { return Read(transport.JsonPath(entry), "json-" + entry.Key + ".cache", bytes => BlueprintCloudProtocol.Download(bytes, entry), token, offlineOnly); }
    }
}
