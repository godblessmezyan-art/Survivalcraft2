using System;

namespace SCIEPort.Features.PixelFx
{
    /// <summary>Bounded presentation event history. Never serializes or touches gameplay state.</summary>
    internal sealed class SpriteFxEvents
    {
        internal const int Capacity = 128;
        private struct Entry { internal ulong Key; internal double Until; internal bool Active; }
        private readonly Entry[] m_entries = new Entry[Capacity];
        private double m_lastTime = double.NaN;

        internal void Clear() { Array.Clear(m_entries, 0, Capacity); m_lastTime = double.NaN; }

        internal bool Accept(ulong key, double now, double hold)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(hold) || double.IsInfinity(hold) || hold <= 0 || hold > 12) return false;
            if (now < m_lastTime) Clear();
            m_lastTime = now;
            int slot = -1, oldest = 0;
            for (int i = 0; i < Capacity; i++)
            {
                Entry item = m_entries[i];
                if (item.Active && item.Until > now && item.Key == key) return false;
                if ((!item.Active || item.Until <= now) && slot < 0) slot = i;
                if (item.Until < m_entries[oldest].Until) oldest = i;
            }
            if (slot < 0) slot = oldest;
            m_entries[slot] = new Entry { Active = true, Key = key, Until = now + hold };
            return true;
        }

        internal static ulong Key(ushort owner, uint sequence, byte family, byte phase)
            => sequence | ((ulong)owner << 32) | ((ulong)family << 48) | ((ulong)phase << 56);

        internal static uint Spatial(float x, float y, float z)
        {
            unchecked
            {
                uint h = 2166136261;
                h = (h ^ (uint)(int)Math.Floor(x * 4)) * 16777619;
                h = (h ^ (uint)(int)Math.Floor(y * 4)) * 16777619;
                return (h ^ (uint)(int)Math.Floor(z * 4)) * 16777619;
            }
        }
    }
}
