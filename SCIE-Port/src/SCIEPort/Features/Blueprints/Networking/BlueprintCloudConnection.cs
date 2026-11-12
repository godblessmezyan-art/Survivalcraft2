using System;
using System.Globalization;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SCIEPort.Features.Blueprints.Networking
{
    // The reported Android BCL lacks both HTTP handlers' AllowAutoRedirect setters.
    // This read-only HTTP/1.1 connection never processes Location or authentication
    // challenges. SslStream uses platform certificate/hostname validation, no callback.
    internal static class BlueprintCloudConnection
    {
        internal static async Task<byte[]> Get(Uri uri, CancellationToken token)
        {
            // HTTP is exclusively for isolated loopback regression tests. Production
            // requests are additionally restricted by BlueprintCloudTransport.
            if (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
                throw new InvalidDataException("蓝图库需要 HTTPS 连接。");
            token.ThrowIfCancellationRequested();
            using (var client = new TcpClient())
            {
                var connected = new TaskCompletionSource<bool>();
                var observer = new ConnectionObserver(client, connected, token);
                try
                {
                    try
                    {
                        client.BeginConnect(uri.DnsSafeHost, uri.Port, result =>
                        {
                            try { client.EndConnect(result); connected.TrySetResult(true); }
                            catch (Exception error) { connected.TrySetException(error); }
                        }, null);
                    }
                    catch (Exception error) { connected.TrySetException(error); }
                    await connected.Task.ConfigureAwait(false);
                    // TLS handshake and bounded synchronous reads run off the UI thread.
                    return await Task.Run(() => Exchange(client, uri, token)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    token.ThrowIfCancellationRequested();
                    throw;
                }
                finally { await observer.Stop().ConfigureAwait(false); }
            }
        }

        private static byte[] Exchange(TcpClient client, Uri uri, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (Stream network = client.GetStream())
            {
                if (uri.Scheme == "https")
                {
                    using (var tls = new SslStream(network, false))
                    {
                        // The target host supplies SNI and certificate name validation.
                        // Let the OS choose its TLS protocol; no global TLS changes.
                        tls.AuthenticateAsClient(uri.DnsSafeHost);
                        return SendAndRead(tls, uri, token);
                    }
                }
                return SendAndRead(network, uri, token);
            }
        }

        private static byte[] SendAndRead(Stream stream, Uri uri, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string host = uri.DnsSafeHost;
            if (host.IndexOf(':') >= 0) host = "[" + host + "]";
            if (!uri.IsDefaultPort) host += ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
            byte[] request = Encoding.ASCII.GetBytes("GET " + uri.PathAndQuery + " HTTP/1.1\r\nHost: " + host
                + "\r\nAccept: application/json\r\nAccept-Encoding: identity\r\nConnection: close\r\n\r\n");
            stream.Write(request, 0, request.Length);
            stream.Flush();
            return BlueprintHttpReader.Read(stream, token);
        }

        private sealed class ConnectionObserver
        {
            private volatile bool finished;
            private readonly Task task;
            internal ConnectionObserver(TcpClient client, TaskCompletionSource<bool> connected, CancellationToken token)
            { task = Observe(client, connected, token); }
            internal async Task Stop() { finished = true; await task.ConfigureAwait(false); }
            private async Task Observe(TcpClient client, TaskCompletionSource<bool> connected, CancellationToken token)
            {
                while (!finished)
                {
                    if (token.IsCancellationRequested)
                    {
                        // Close interrupts connect/TLS/headers/body. Completing the
                        // APM bridge also bounds cancellation during DNS resolution.
                        client.Close();
                        connected.TrySetCanceled();
                        return;
                    }
                    await Task.Delay(25).ConfigureAwait(false);
                }
            }
        }
    }
}
