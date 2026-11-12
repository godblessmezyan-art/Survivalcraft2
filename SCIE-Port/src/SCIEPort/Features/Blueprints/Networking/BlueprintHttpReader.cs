using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SCIEPort.Features.Blueprints.Networking
{
    // Deliberately limited to one JSON GET response. No redirects, cookies,
    // decompression, authentication, connection pooling or unbounded buffering.
    internal sealed class BlueprintHttpReader
    {
        private const int MaxHeaders = 32768;
        private readonly Stream input;
        private readonly CancellationToken token;
        private readonly byte[] buffer = new byte[4096];
        private int position, available, headerBytes;
        private BlueprintHttpReader(Stream input, CancellationToken token) { this.input = input; this.token = token; }
        internal static byte[] Read(Stream input, CancellationToken token) { return new BlueprintHttpReader(input, token).Response(); }

        private bool Available()
        {
            token.ThrowIfCancellationRequested();
            if (position == available)
            {
                available = input.Read(buffer, 0, buffer.Length);
                position = 0;
                if (available == 0) return false;
            }
            return true;
        }

        private int Next() { return Available() ? buffer[position++] : -1; }

        private string Line()
        {
            var line = new System.Text.StringBuilder();
            while (true)
            {
                int c = Next();
                if (c < 0) throw new IOException("蓝图库响应被中断。");
                if (++headerBytes > MaxHeaders) throw new InvalidDataException("蓝图库响应头过大。");
                if (c == '\r')
                {
                    if (Next() != '\n') throw new InvalidDataException("蓝图库响应头无效。");
                    if (++headerBytes > MaxHeaders) throw new InvalidDataException("蓝图库响应头过大。");
                    return line.ToString();
                }
                if (c < 32 && c != '\t' || c > 126) throw new InvalidDataException("蓝图库响应头无效。");
                line.Append((char)c);
            }
        }

        private Dictionary<string, string> Headers()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string line;
            while ((line = Line()).Length != 0)
            {
                int colon = line.IndexOf(':');
                if (colon < 1) throw new InvalidDataException("蓝图库响应头无效。");
                string name = line.Substring(0, colon);
                foreach (char c in name)
                    if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || "!#$%&'*+-.^_`|~".IndexOf(c) >= 0))
                        throw new InvalidDataException("蓝图库响应头无效。");
                string value = line.Substring(colon + 1).Trim();
                // Ambiguous framing and duplicated security-relevant metadata fail closed.
                if (headers.ContainsKey(name))
                {
                    if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) || name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) || name.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("蓝图库响应头重复。");
                }
                else headers.Add(name, value);
            }
            return headers;
        }

        private static string Header(Dictionary<string, string> headers, string name)
        { string value; return headers.TryGetValue(name, out value) ? value : null; }

        private static int Number(string value, int radix)
        {
            if (string.IsNullOrEmpty(value)) throw new InvalidDataException("蓝图库响应长度无效。");
            int result = 0;
            foreach (char c in value)
            {
                int digit = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
                if (digit < 0 || digit >= radix || result > (BlueprintCloudProtocol.MaxBytes - digit) / radix)
                    throw new InvalidDataException("蓝图库响应过大或长度无效。");
                result = result * radix + digit;
            }
            return result;
        }

        private byte[] Response()
        {
            Dictionary<string, string> headers;
            int status, interim = 0;
            do
            {
                string line = Line();
                if (!(line.StartsWith("HTTP/1.1 ", StringComparison.Ordinal) || line.StartsWith("HTTP/1.0 ", StringComparison.Ordinal))
                    || line.Length < 12 || line.Length > 12 && line[12] != ' ')
                    throw new InvalidDataException("蓝图库 HTTP 状态无效。");
                status = 0;
                for (int i = 9; i < 12; i++)
                {
                    int digit = line[i] - '0';
                    if (digit < 0 || digit > 9) throw new InvalidDataException("蓝图库 HTTP 状态无效。");
                    status = status * 10 + digit;
                }
                if (status < 100 || status > 599) throw new InvalidDataException("蓝图库 HTTP 状态无效。");
                // Reject final errors without consuming their headers/body or Location.
                if (status >= 200 && status != 200 || status == 101) throw new BlueprintHttpException(status);
                headers = Headers();
                if (status < 200 && ++interim > 4) throw new InvalidDataException("蓝图库临时响应过多。");
            } while (status < 200);

            string contentType = Header(headers, "Content-Type") ?? string.Empty;
            int semi = contentType.IndexOf(';');
            string media = (semi < 0 ? contentType : contentType.Substring(0, semi)).Trim();
            if (!media.Equals("application/json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("蓝图库返回了非 JSON 响应。");
            if (semi >= 0)
            {
                bool hasCharset = false;
                foreach (string part in contentType.Substring(semi + 1).Split(';'))
                {
                    int equals = part.IndexOf('=');
                    if (equals < 0 || !part.Substring(0, equals).Trim().Equals("charset", StringComparison.OrdinalIgnoreCase)) continue;
                    if (hasCharset || !part.Substring(equals + 1).Trim().Trim('"').Equals("utf-8", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("蓝图库响应编码无效。");
                    hasCharset = true;
                }
            }
            if (!string.IsNullOrEmpty(Header(headers, "Content-Encoding"))) throw new InvalidDataException("蓝图库响应编码无效。");
            string length = Header(headers, "Content-Length"), transfer = Header(headers, "Transfer-Encoding");
            if (transfer != null && (length != null || !transfer.Equals("chunked", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("蓝图库响应传输编码无效。");
            using (var output = new MemoryStream())
            {
                if (transfer != null)
                {
                    int chunks = 0;
                    while (true)
                    {
                        if (++chunks > 16384) throw new InvalidDataException("蓝图库响应分块过多。");
                        string chunk = Line();
                        int extension = chunk.IndexOf(';');
                        int size = Number(extension < 0 ? chunk : chunk.Substring(0, extension), 16);
                        if (size == 0)
                        {
                            var trailers = Headers();
                            if (Header(trailers, "Content-Length") != null || Header(trailers, "Transfer-Encoding") != null
                                || Header(trailers, "Content-Type") != null || Header(trailers, "Content-Encoding") != null)
                                throw new InvalidDataException("蓝图库响应尾部无效。");
                            break;
                        }
                        Copy(output, size);
                        if (Next() != '\r' || Next() != '\n') throw new InvalidDataException("蓝图库响应分块无效。");
                    }
                }
                else if (length != null) Copy(output, Number(length, 10));
                else
                {
                    while (Available())
                    {
                        int count = available - position;
                        if (count > BlueprintCloudProtocol.MaxBytes - output.Length) throw new InvalidDataException("蓝图库响应过大。");
                        output.Write(buffer, position, count);
                        position += count;
                    }
                }
                token.ThrowIfCancellationRequested();
                return output.ToArray();
            }
        }

        private void Copy(MemoryStream output, int count)
        {
            if (count > BlueprintCloudProtocol.MaxBytes - output.Length) throw new InvalidDataException("蓝图库响应过大。");
            while (count > 0)
            {
                if (!Available()) throw new IOException("蓝图库响应被中断。");
                int take = Math.Min(count, available - position);
                output.Write(buffer, position, take);
                position += take;
                count -= take;
            }
        }
    }
}
