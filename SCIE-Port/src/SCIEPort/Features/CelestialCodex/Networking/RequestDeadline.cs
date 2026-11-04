using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SCIEPort.Features.CelestialCodex.Networking
{
    // The Android BCL has no CreateLinkedTokenSource(CancellationToken[]).
    // Own one source and observe caller cancellation without linked/register/timer APIs.
    internal sealed class RequestDeadline
    {
        private readonly CancellationTokenSource source;
        private readonly Task observer;
        private volatile bool finished;
        internal readonly CancellationToken Token;
        internal RequestDeadline(CancellationToken caller, int milliseconds)
        {
            if (milliseconds <= 0 || milliseconds > 60000) throw new ArgumentOutOfRangeException("milliseconds");
            source = new CancellationTokenSource();
            Token = source.Token;
            observer = Observe(caller, Stopwatch.GetTimestamp(), Stopwatch.Frequency * milliseconds / 1000);
        }
        internal static bool HasElapsed(long started, long now, long budget) { return unchecked(now - started) >= budget; }
        private async Task Observe(CancellationToken caller, long started, long budget)
        {
            while (!finished)
            {
                if (caller.IsCancellationRequested || HasElapsed(started, Stopwatch.GetTimestamp(), budget)) { source.Cancel(); return; }
                await Task.Delay(25).ConfigureAwait(false);
            }
        }
        internal async Task Stop()
        {
            finished = true;
            // Join the observer before disposal: it can no longer race Cancel with Dispose.
            try { await observer.ConfigureAwait(false); }
            finally { source.Dispose(); }
        }
    }
}
