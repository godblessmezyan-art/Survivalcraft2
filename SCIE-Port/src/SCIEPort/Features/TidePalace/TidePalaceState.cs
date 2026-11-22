namespace SCIEPort.Features.TidePalace
{
    internal sealed class TidePalaceState
    {
        internal readonly PalaceSite Site;
        internal volatile int Slices;
        internal int Dials { get; private set; }
        internal bool East { get; private set; }
        internal bool West { get; private set; }
        internal bool Stocked { get; private set; }
        internal bool Unsealed => East && West;

        internal TidePalaceState(PalaceSite site, int slices = 0, int dials = 0, int flags = 0)
        {
            Site = site;
            Slices = slices & PalaceSite.Complete;
            Dials = dials >= 0 && dials < 27 ? dials : 0;
            East = (flags & 1) != 0;
            West = (flags & 2) != 0;
            Stocked = East && West && (flags & 4) != 0;
        }

        internal int SaveFlags => (East ? 1 : 0) | (West ? 2 : 0) | (Stocked ? 4 : 0);

        internal bool Turn(int dial)
        {
            if (Slices != PalaceSite.Complete || East || dial < 0 || dial >= PalaceSite.Dials.Length) return false;
            Dials = PalaceSite.Advance(Dials, dial);
            East = Dials == Site.Solution;
            return true;
        }

        internal bool Lift()
        {
            if (Slices != PalaceSite.Complete || West) return false;
            West = true;
            return true;
        }

        internal bool CanStock => Slices == PalaceSite.Complete && Unsealed && !Stocked;

        internal void MarkStocked()
        {
            if (CanStock) Stocked = true;
        }
    }
}
