using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SCIEPort.Features.CelestialCodex.Core;

namespace SCIEPort.Features.CelestialCodex.Networking
{
    internal static class SourcePolicy
    {
        internal const string DefaultEndpoint = "https://wxhappylife.top/digital-study/api/game-library/v1";
        internal static string Validate(string endpoint)
        {
            if (endpoint == null || endpoint.Length > 1024) throw new InvalidDataException("API source");
            var uri = new Uri(endpoint, UriKind.Absolute);
            if (uri.Scheme != "https" || uri.Host != "wxhappylife.top" || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
                (uri.AbsolutePath != "/digital-study/api/game-library/v1" && uri.AbsolutePath != "/api/game-library/v1")) throw new InvalidDataException("API source");
            return uri.AbsoluteUri;
        }
    }
    internal sealed class LibraryHttpException : Exception
    {
        internal readonly int Status;
        internal LibraryHttpException(int status) : base("HTTP " + status) { Status = status; }
    }
    internal sealed class LibraryTransport
    {
        private readonly string endpoint;
        private readonly Func<HttpMessageHandler> handlerFactory;
        internal LibraryTransport(string endpoint, Func<HttpMessageHandler> handlerFactory = null) { this.endpoint = SourcePolicy.Validate(endpoint); this.handlerFactory = handlerFactory; }
        internal async Task<byte[]> Get(string path, CancellationToken token, Action<string> progress = null)
        {
            // All paths are constructed by Request; never accept response links or redirects.
            if (!path.StartsWith("/books", StringComparison.Ordinal) || path.IndexOf("..", StringComparison.Ordinal) >= 0 || path.IndexOf('\\') >= 0 || path.IndexOf('#') >= 0) throw new InvalidDataException("Request path");
            if (progress != null) progress("http/deadline");
            var deadline = new RequestDeadline(token, 12000);
            try
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (progress != null) progress("http/handler");
                using (HttpMessageHandler handler = handlerFactory == null ? HttpHandlerPolicy.Create() : handlerFactory())
                using (var client = new HttpClient(handler))
                {
                    if (progress != null) progress("http/send");
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.DefaultRequestHeaders.Add("Accept-Encoding", "identity");
                    using (HttpResponseMessage response = await client.GetAsync(new Uri(endpoint + path), HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                    {
                        if (response.StatusCode != HttpStatusCode.OK) throw new LibraryHttpException((int)response.StatusCode);
                        if (progress != null) progress("http/headers");
                        var content = response.Content;
                        if (content == null || content.Headers.ContentType == null || !IsJson(content.Headers.ContentType.ToString())) throw new InvalidDataException("Response content type");
                        string charset = content.Headers.ContentType.CharSet;
                        if (!string.IsNullOrEmpty(charset) && !string.Equals(charset.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Response encoding");
                        if (content.Headers.ContentEncoding.Count != 0 || content.Headers.ContentLength > Limits.MaxFileBytes) throw new InvalidDataException("Response size/encoding");
                        if (progress != null) progress("http/body");
                        using (Stream input = await content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var output = new MemoryStream())
                        {
                            var buffer = new byte[4096]; int read;
                            while ((read = await input.ReadAsync(buffer, 0, Math.Min(buffer.Length, Limits.MaxFileBytes + 1 - (int)output.Length), deadline.Token).ConfigureAwait(false)) > 0)
                            { output.Write(buffer, 0, read); if (output.Length > Limits.MaxFileBytes) throw new InvalidDataException("Response size"); }
                            deadline.Token.ThrowIfCancellationRequested(); return output.ToArray();
                        }
                    }
                }
            }
            finally { await deadline.Stop().ConfigureAwait(false); }
        }
        internal static bool IsJson(string contentType)
        {
            if (contentType == null) return false;
            int separator = contentType.IndexOf(';');
            return string.Equals((separator < 0 ? contentType : contentType.Substring(0, separator)).Trim(), "application/json", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal sealed class Request
    {
        internal readonly string Slug, Chapter, Revision;
        internal readonly int Page;
        internal Request(int page) { if (page < 1 || page > 10000) throw new InvalidDataException("Page"); Page = page; }
        internal Request(string slug, string chapter = null, string revision = null)
        {
            if (!Protocol.SafeSlug(slug)) throw new InvalidDataException("Slug");
            if (chapter != null && (!Protocol.ChapterId(chapter) || !Protocol.Revision(revision))) throw new InvalidDataException("Chapter");
            Slug = slug; Chapter = chapter; Revision = revision;
        }
        internal string Path { get { return Slug == null ? "/books?page=" + Page + "&pageSize=20" : "/books/" + Slug + (Chapter == null ? "" : "/chapters/" + Chapter + "?revision=" + Revision); } }
        internal Document Decode(byte[] bytes)
        {
            var doc = Protocol.Decode(bytes, Slug, Chapter, Revision);
            if (Slug == null && doc.Page != Page) throw new InvalidDataException("Wrong catalog page"); return doc;
        }
    }
    internal sealed class ReaderResult
    {
        internal Request Request;
        internal Document Document;
        internal ReadingPosition Position;
        internal bool Offline, CacheFailed;
        internal string Error;
        internal int HttpStatus;
    }
    internal sealed class LibraryClient
    {
        private readonly BoundedCache cache;
        private readonly LibraryTransport transport;
        private readonly string source;
        private readonly Action<string> diagnostics;
        internal LibraryClient(BoundedCache cache, string endpoint, LibraryTransport transport = null, Action<string> diagnostics = null) { this.cache = cache; source = SourcePolicy.Validate(endpoint); this.transport = transport ?? new LibraryTransport(source); this.diagnostics = diagnostics; }
        internal async Task<ReaderResult> Read(Request request, CancellationToken token, bool offlineOnly = false)
        {
            string key = BoundedCache.Key(source + request.Path);
            var result = new ReaderResult { Request = request, Position = ReadingPosition.Decode(cache.Read("position.json")) };
            if (offlineOnly)
            {
                byte[] snapshot = cache.Read(key);
                if (snapshot != null) try { result.Document = request.Decode(snapshot); result.Offline = true; return result; } catch (Exception) { cache.Delete(key); }
                result.Error = "尚未缓存"; return result;
            }
            string stage = "http";
            try
            {
                byte[] bytes = await transport.Get(request.Path, token, value => stage = value).ConfigureAwait(false);
                stage = "decode";
                result.Document = request.Decode(bytes); token.ThrowIfCancellationRequested(); result.CacheFailed = !cache.Write(key, bytes);
            }
            catch (OperationCanceledException error) { if (token.IsCancellationRequested) throw; result.Error = NetworkFailure.Message("TIME"); NetworkFailure.Report(diagnostics, stage, "TIME", error); }
            catch (LibraryHttpException e)
            {
                NetworkFailure.Report(diagnostics, stage, "HTTP-" + e.Status, e);
                result.HttpStatus = e.Status;
                // An authoritative removal, permission denial, or update must never resurrect stale content.
                if (e.Status == 404 || e.Status == 403 || e.Status == 401 || e.Status == 409) { cache.Delete(key); result.Error = e.Status == 409 ? "章节已更新" : "内容不可用"; return result; }
                result.Error = "书库暂不可用";
            }
            catch (Exception error) { string code = NetworkFailure.Code(error); result.Error = NetworkFailure.Message(code); NetworkFailure.Report(diagnostics, stage, code, error); }
            if (result.Document == null)
            {
                byte[] bytes = cache.Read(key);
                if (bytes != null) try { result.Document = request.Decode(bytes); result.Offline = true; result.Error = null; } catch (Exception) { cache.Delete(key); }
            }
            return result;
        }
    }
}
