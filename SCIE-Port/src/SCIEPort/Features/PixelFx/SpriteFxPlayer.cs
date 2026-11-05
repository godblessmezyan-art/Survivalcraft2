using System;

namespace SCIEPort.Features.PixelFx
{
    internal enum SpriteFxFacing { Screen, Vertical }

    /// <summary>Immutable, build-validated frame layout; no runtime JSON/BCL parser dependency.</summary>
    internal sealed class SpriteFxClip
    {
        internal readonly string Name;
        internal readonly int FirstFrame, FrameCount, Fps, AtlasWidth, AtlasHeight, Columns, Cell, Priority;
        internal readonly float PivotX, PivotY;
        internal readonly SpriteFxFacing Facing;
        internal float Duration => (float)FrameCount / Fps;

        internal SpriteFxClip(string name, int first, int count, int fps, int width, int height,
            int columns, int cell, float pivotX, float pivotY, SpriteFxFacing facing, int priority)
        {
            if (string.IsNullOrEmpty(name) || first < 0 || count < 1 || fps < 1 || fps > 120 || cell < 1 ||
                columns < 1 || columns > width / cell || height / cell < 1 ||
                (long)first + count > (long)columns * (height / cell) ||
                !Finite(pivotX) || !Finite(pivotY) || pivotX < 0 || pivotX > 1 || pivotY < 0 || pivotY > 1 ||
                priority < 0 || priority > 3 || (facing != SpriteFxFacing.Screen && facing != SpriteFxFacing.Vertical))
                throw new ArgumentException("Invalid sprite effect layout.");
            Name = name; FirstFrame = first; FrameCount = count; Fps = fps; AtlasWidth = width; AtlasHeight = height;
            Columns = columns; Cell = cell; PivotX = pivotX; PivotY = pivotY; Facing = facing; Priority = priority;
        }

        internal void Uv(int localFrame, out float u0, out float v0, out float u1, out float v1)
        {
            int f = FirstFrame + Math.Max(0, Math.Min(FrameCount - 1, localFrame));
            // Half-texel inset plus the exporter's transparent gutter prevent adjacent-frame bleed.
            u0 = (f % Columns * Cell + 0.5f) / AtlasWidth;
            v0 = (f / Columns * Cell + 0.5f) / AtlasHeight;
            u1 = (f % Columns * Cell + Cell - 0.5f) / AtlasWidth;
            v1 = (f / Columns * Cell + Cell - 0.5f) / AtlasHeight;
        }

        internal static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }

    internal struct SpriteFxBudget
    {
        internal int Instances, Quads, SampleFps;
        internal float Distance;
        internal static SpriteFxBudget ForQuality(int quality)
        {
            if (quality <= 0) return new SpriteFxBudget { Instances = 8, Quads = 6, SampleFps = 12, Distance = 32 };
            if (quality == 1) return new SpriteFxBudget { Instances = 16, Quads = 12, SampleFps = 18, Distance = 56 };
            return new SpriteFxBudget { Instances = 32, Quads = 24, SampleFps = 24, Distance = 80 };
        }
    }

    internal struct SpriteFxInstance
    {
        internal SpriteFxClip Clip;
        internal float X, Y, Z, Size, Life;
        internal double Birth;
    }

    /// <summary>Bounded presentation-only pool. Update uses the world's existing game clock.</summary>
    internal sealed class SpriteFxPlayer
    {
        internal const int Capacity = 32;
        private readonly SpriteFxInstance[] m_items = new SpriteFxInstance[Capacity];
        private double m_lastTime = double.NaN;
        internal int ActiveCount { get; private set; }
        internal int DroppedCount { get; private set; }
        internal SpriteFxInstance At(int index) => m_items[index];

        internal void Clear()
        {
            Array.Clear(m_items, 0, m_items.Length);
            ActiveCount = 0; DroppedCount = 0; m_lastTime = double.NaN;
        }

        internal void Update(double now, int quality)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return;
            if (!double.IsNaN(m_lastTime) && now < m_lastTime) Clear();
            m_lastTime = now; ActiveCount = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (m_items[i].Clip == null) continue;
                if (now - m_items[i].Birth >= m_items[i].Life) m_items[i] = default(SpriteFxInstance);
                else ActiveCount++;
            }
            int limit = SpriteFxBudget.ForQuality(quality).Instances;
            while (ActiveCount > limit) { m_items[Weakest()] = default(SpriteFxInstance); ActiveCount--; DroppedCount++; }
        }

        private int Weakest()
        {
            int found = -1;
            for (int i = 0; i < Capacity; i++)
            {
                if (m_items[i].Clip == null) continue;
                if (found < 0 || m_items[i].Clip.Priority < m_items[found].Clip.Priority ||
                    (m_items[i].Clip.Priority == m_items[found].Clip.Priority && m_items[i].Birth < m_items[found].Birth)) found = i;
            }
            return found;
        }

        internal bool Play(SpriteFxClip clip, float x, float y, float z, float size, double now, int quality, float life = 0)
        {
            if (clip == null || !SpriteFxClip.Finite(x) || !SpriteFxClip.Finite(y) || !SpriteFxClip.Finite(z) ||
                !SpriteFxClip.Finite(size) || size <= 0 || size > 32 || !SpriteFxClip.Finite(life) || life < 0 || life > 8 ||
                double.IsNaN(now) || double.IsInfinity(now)) return false;
            Update(now, quality);
            int slot = -1;
            if (ActiveCount < SpriteFxBudget.ForQuality(quality).Instances)
                for (int i = 0; i < Capacity; i++) if (m_items[i].Clip == null) { slot = i; break; }
            if (slot < 0)
            {
                slot = Weakest(); DroppedCount++;
                if (slot < 0 || m_items[slot].Clip.Priority > clip.Priority) return false;
            }
            else ActiveCount++;
            m_items[slot] = new SpriteFxInstance { Clip = clip, X = x, Y = y, Z = z, Size = size,
                Birth = now, Life = life > 0 ? life : clip.Duration };
            return true;
        }

        internal static int Frame(SpriteFxInstance item, double now, int sampleFps)
        {
            double age = now - item.Birth;
            if (item.Clip == null || double.IsNaN(age) || double.IsInfinity(age) || age < 0 || age >= item.Life || sampleFps < 1) return -1;
            double sampled = Math.Floor(age * sampleFps + 1e-7) / sampleFps;
            return Math.Min(item.Clip.FrameCount - 1, (int)(sampled / item.Life * item.Clip.FrameCount));
        }
    }
}
