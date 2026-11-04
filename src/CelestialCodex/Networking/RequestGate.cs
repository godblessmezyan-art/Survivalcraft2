using System;
using System.Threading;
using System.Threading.Tasks;

namespace CelestialCodex.Networking
{
    // Workers publish data only. No callback, Widget, player or Project crosses this boundary.
    internal sealed class RequestGate : IDisposable
    {
        private readonly object gate = new object();
        private int generation;
        private bool closed;
        private CancellationTokenSource active;
        private ReaderResult pending;
        internal void Begin(Func<CancellationToken, Task<ReaderResult>> work)
        {
            lock (gate)
            {
                if (closed) return;
                if (active != null) active.Cancel();
                int id = ++generation; pending = null;
                var source = new CancellationTokenSource(); active = source;
                Task.Run(async () =>
                {
                    ReaderResult result;
                    try { result = await work(source.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { result = new ReaderResult { Error = "连接超时" }; }
                    catch (Exception) { result = new ReaderResult { Error = "书库暂不可用" }; }
                    lock (gate)
                    {
                        if (!closed && id == generation) pending = result;
                        if (active == source) active = null;
                        source.Dispose();
                    }
                });
            }
        }
        internal ReaderResult Take() { lock (gate) { var result = pending; pending = null; return result; } }
        internal void Cancel() { lock (gate) { generation++; pending = null; if (active != null) active.Cancel(); } }
        public void Dispose() { lock (gate) { closed = true; generation++; pending = null; if (active != null) active.Cancel(); } }
    }
}
