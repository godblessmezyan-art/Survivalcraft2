using System;

namespace SCIEPort.Features.Pyramid
{
    internal sealed class PyramidState
    {
        internal readonly PyramidSite Site;
        internal volatile int Slices;
        internal int Progress { get; private set; }
        internal bool Stocked { get; private set; }
        internal int GuardiansSpawned { get; private set; }
        internal bool Solved => Progress == PyramidSite.Seals.Length;

        internal PyramidState(PyramidSite site, int slices = 0, int progress = 0, bool stocked = false, int guardiansSpawned = 0)
        {
            Site = site;
            Slices = slices & PyramidSite.Complete;
            Progress = Math.Min(PyramidSite.Seals.Length, Math.Max(0, progress));
            Stocked = Solved && stocked;
            GuardiansSpawned = guardiansSpawned & PyramidGuardians.All;
        }

        internal bool Press(int seal)
        {
            if (Slices != PyramidSite.Complete || Solved || seal < 0 || seal >= PyramidSite.Seals.Length) return false;
            int next = seal == Site.SealAt(Progress) ? Progress + 1 : seal == Site.SealAt(0) ? 1 : 0;
            if (next == Progress) return false;
            Progress = next;
            return true;
        }

        internal bool IsLit(int seal)
        {
            for (int step = 0; step < Progress; step++)
                if (Site.SealAt(step) == seal) return true;
            return false;
        }

        internal void MarkStocked()
        {
            if (Solved && Slices == PyramidSite.Complete) Stocked = true;
        }

        internal void MarkGuardianSpawned(int index) => GuardiansSpawned |= 1 << index;
    }
}
