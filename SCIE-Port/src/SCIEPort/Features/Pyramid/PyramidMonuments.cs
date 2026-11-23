using Engine;
using Game;

namespace SCIEPort.Features.Pyramid
{
    /// <summary>Cell footprints for the fixed, world-generated furnishings in the main tomb.</summary>
    internal static class PyramidMonuments
    {
        internal const int Coffin = 9, Guardian = 10, Brazier = 11, Obelisk = 12;
        private const int PartShift = 10;
        private static readonly BoundingBox[][][] Collision = CreateCollision();

        internal static bool IsMonument(int data)
        {
            int kind = PyramidDecorRules.Kind(data);
            int part = Part(data);
            return kind >= Coffin && kind <= Obelisk && part >= 0 && part < Size(kind) &&
                data == Data(kind, part, PyramidDecorRules.IsOpen(data));
        }

        internal static bool Switchable(int kind) => kind == Coffin || kind == Brazier;
        internal static int Size(int kind) => kind == Coffin ? 12 : kind == Brazier ? 2 : 3;
        internal static int Part(int data) => data >> PartShift;
        internal static int Data(int kind, int part, bool open = false)
            => RenderKey(kind, part, part == 0 && Switchable(kind) && open);

        internal static int RenderKey(int kind, int part, bool open)
            => kind | (part << PartShift) | (open ? PyramidDecorRules.StateBit : 0);

        internal static int Value(int kind, int part, bool open = false)
            => Terrain.MakeBlockValue(PyramidDecorRules.Index, 0, Data(kind, part, open));

        internal static Point3 Offset(int kind, int part)
            => kind == Coffin ? new Point3(part % 2, part / 6, part / 2 % 3) : new Point3(0, part, 0);

        internal static string Model(int kind, bool open)
        {
            switch (kind)
            {
                case Coffin: return open ? "PyramidRoyalCoffinOpen" : "PyramidRoyalCoffin";
                case Guardian: return "PyramidStoneGuardian";
                case Brazier: return open ? "PyramidRitualBrazierLit" : "PyramidRitualBrazier";
                default: return "PyramidTombObelisk";
            }
        }

        internal static string Name(int kind)
        {
            switch (kind)
            {
                case Coffin: return "法老石棺";
                case Guardian: return "阿努比斯石像";
                case Brazier: return "墓室火盆";
                default: return "墓室方尖碑";
            }
        }

        internal static BoundingBox[] CollisionBoxes(int kind, int part) => Collision[kind - Coffin][part];

        private static BoundingBox[][][] CreateCollision()
        {
            var boxes = new BoundingBox[Obelisk - Coffin + 1][][];
            for (int kind = Coffin; kind <= Obelisk; kind++)
            {
                var parts = new BoundingBox[Size(kind)][];
                for (int part = 0; part < parts.Length; part++)
                    parts[part] = new[] { Bounds(kind, part) };
                boxes[kind - Coffin] = parts;
            }
            return boxes;
        }

        private static BoundingBox Bounds(int kind, int part)
        {
            Point3 offset = Offset(kind, part);
            if (kind == Coffin)
            {
                return offset.Y == 0
                    ? Clip(offset, new Vector3(.12f, 0, .08f), new Vector3(1.88f, 1f, 2.92f))
                    : Clip(offset, new Vector3(.32f, 1f, .16f), new Vector3(1.68f, 1.34f, 2.99f));
            }
            float radius = kind == Brazier ? .43f : offset.Y == 0 ? .44f : kind == Obelisk ? .24f : .32f;
            float height = kind == Brazier ? 1.28f : kind == Obelisk ? 2.60f : 2.85f;
            float front = kind == Guardian && offset.Y == 2 ? .06f : .5f - radius;
            return Clip(offset, new Vector3(.5f - radius, 0, front),
                new Vector3(.5f + radius, height, .5f + radius));
        }

        private static BoundingBox Clip(Point3 cell, Vector3 min, Vector3 max)
        {
            var origin = new Vector3(cell);
            return new BoundingBox(Vector3.Max(Vector3.Zero, min - origin), Vector3.Min(Vector3.One, max - origin));
        }
    }
}
