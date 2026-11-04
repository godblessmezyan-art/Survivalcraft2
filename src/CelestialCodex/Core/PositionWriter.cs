using System;
using System.Threading.Tasks;

namespace CelestialCodex.Core
{
    internal sealed class PositionWriter
    {
        private readonly object gate = new object();
        private readonly Action<byte[]> write;
        private byte[] pending;
        private bool running;
        internal PositionWriter(Action<byte[]> write) { this.write = write; }
        internal void Save(byte[] snapshot)
        {
            lock (gate)
            {
                pending = snapshot;
                if (running) return;
                running = true;
                Task.Run(() =>
                {
                    while (true)
                    {
                        byte[] bytes;
                        lock (gate) { bytes = pending; pending = null; if (bytes == null) { running = false; return; } }
                        try { write(bytes); } catch (Exception) { }
                    }
                });
            }
        }
    }
}
