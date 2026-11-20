using System;
using Engine;
using Game;
using SCIEPort.Features.Geology;

namespace SCIEPort.Features.TidePalace
{
    // World coordinates are derived from a single local layout. All operations clip to the
    // current 16x16 slice, including the vault walls and the underwater entrance to each air bell.
    internal sealed class PalaceLayout
    {
        internal const int LowY = -21;
        internal const int HighY = 18;
        private const int Height = HighY - LowY + 1;
        internal const int Unchanged = -1;
        private static readonly int Stone = Terrain.MakeBlockValue(BasaltBlock.Index);
        private static readonly int Trim = Terrain.MakeBlockValue(TravertineBlock.Index);
        private static readonly int Column = Terrain.MakeBlockValue(BasaltPillarBlock.Index);
        private static readonly int Wall = Terrain.MakeBlockValue(KarstLimestoneBlock.Index);
        private static readonly int Water = Terrain.MakeBlockValue(WaterBlock.Index);
        private static readonly int Glass = Terrain.MakeBlockValue(GlassBlock.Index);
        private static readonly int Lamp = TidePalaceBlock.Value(TidePalaceBlock.Lamp);

        private readonly int x0, z0;
        private readonly int[] cells = new int[16 * 16 * Height];

        internal PalaceLayout(PalaceSite site, Point2 origin)
        {
            x0 = origin.X - site.Origin.X;
            z0 = origin.Y - site.Origin.Y;
            for (int i = 0; i < cells.Length; i++) cells[i] = Unchanged;
            Build(site);
        }

        internal int At(int x, int y, int z) => cells[((z * 16 + x) * Height) + y - LowY];

        private void Build(PalaceSite site)
        {
            // Terraced foundations, with retaining piers sunk into the original sea bed.
            Fill(6, -1, 4, 57, -1, 38, Stone);
            Fill(3, 0, 7, 60, 0, 33, Wall);
            Fill(10, 0, 4, 53, 0, 39, Wall);
            Fill(23, 0, 4, 36, 0, 22, Trim);
            Fill(27, -2, 0, 32, -2, 5, Stone);
            Fill(26, -1, 2, 33, -1, 5, Trim);
            for (int x = 7; x <= 55; x += 8)
            for (int z = 8; z <= 36; z += 14)
                Fill(x, LowY, z, x + 2, -2, z + 2, Column);
            for (int x = 6; x <= 54; x += 8)
            {
                if (x >= 22 && x <= 38) continue;
                Pillar(x, 1, 12, 7);
                Fill(x, 7, 12, x + 5, 7, 13, Trim);
            }
            Tower(8, 6);
            Tower(49, 6);

            // Flooded galleries on either side of the central processional approach.
            Fill(5, 1, 16, 23, 1, 35, Stone);
            Fill(36, 1, 13, 58, 1, 30, Stone);
            for (int x = 7; x <= 21; x += 7)
            {
                Pillar(x, 2, 16, 8);
                Pillar(x, 2, 32, 8);
            }
            for (int x = 37; x <= 57; x += 5)
            {
                Pillar(x, 2, 13, 9);
                Pillar(x, 2, 29, 9);
            }
            Fill(36, 9, 13, 58, 9, 29, Trim);
            Fill(39, 9, 16, 54, 9, 25, Water);
            EastWing(site);
            WestWing();

            // The dry ante-room is entered from below. The full roof and walls keep
            // source water out. At the shaft, only the top water cell adjoins air above.
            Room(24, 4, 23, 34, 12, 31, 0);
            Fill(27, 1, 24, 31, 4, 26, Water);
            Fill(24, 5, 23, 34, 5, 23, Trim);
            Fill(25, 5, 24, 26, 5, 29, Trim);
            Fill(32, 5, 24, 33, 5, 29, Trim);
            Fill(25, 5, 27, 33, 5, 30, Trim);
            Fill(24, 13, 24, 34, 13, 30, Trim);
            Fill(25, 14, 25, 33, 14, 29, Stone);
            Fill(26, 9, 30, 32, 9, 30, Trim);
            Fill(28, 6, 28, 30, 6, 29, Trim);
            Fill(28, 7, 28, 28, 7, 28, Column);
            Fill(30, 7, 28, 30, 7, 28, Column);
            Fill(29, 7, 29, 29, 9, 29, Column);
            Set(PalaceSite.EastLight, TidePalaceBlock.Value(TidePalaceBlock.Signal));
            Set(PalaceSite.WestLight, TidePalaceBlock.Value(TidePalaceBlock.Signal));
            Fill(27, 6, 30, 31, 8, 30, TidePalaceBlock.Value(TidePalaceBlock.Gate));
            Fill(28, 11, 26, 30, 11, 26, Lamp);
            Vault();
        }

        private void Tower(int x, int z)
        {
            Fill(x - 1, 1, z - 1, x + 6, 2, z + 6, Trim);
            Room(x, 3, z, x + 5, 15, z + 5, Water);
            Fill(x + 1, 3, z - 1, x + 4, 6, z + 1, Water);
            Fill(x + 1, 8, z, x + 4, 10, z + 1, Water);
            Fill(x + 1, 11, z + 1, x + 4, 14, z + 4, 0);
            Fill(x, 16, z, x + 5, 16, z + 5, Trim);
            Fill(x, 17, z, x, 18, z + 3, Column);
            Fill(x + 4, 17, z + 2, x + 5, 18, z + 5, Column);
            Fill(x + 2, 10, z, x + 3, 10, z, Lamp);
            Fill(x + 2, 14, z + 3, x + 3, 14, z + 3, Lamp);
            Fill(x + 1, 13, z, x + 4, 14, z, Glass);
        }

        private void EastWing(PalaceSite site)
        {
            // Overlook reached from either end, with all three carvings visible below.
            Room(39, 6, 15, 55, 12, 21, Water);
            Fill(40, 10, 16, 54, 11, 20, 0);
            Fill(39, 9, 17, 39, 11, 19, Glass);
            Fill(55, 9, 17, 55, 11, 19, Glass);
            Fill(40, 7, 21, 54, 11, 21, Glass);
            Fill(40, 9, 20, 54, 9, 20, Trim);
            Fill(42, 6, 17, 44, 6, 19, Water);
            Fill(50, 6, 17, 52, 6, 19, Water);
            Fill(45, 10, 14, 49, 10, 14, Trim);
            Fill(46, 11, 16, 48, 11, 16, Lamp);
            for (int dial = 0; dial < PalaceSite.Dials.Length; dial++)
            {
                Point3 point = PalaceSite.Dials[dial];
                Fill(point.X - 1, 2, 23, point.X + 1, 2, 25, Trim);
                Fill(point.X, 1, 21, point.X, 1, 28, Column);
                Fill(point.X, 2, 27, point.X, 2, 28, TidePalaceBlock.Value(TidePalaceBlock.Clue + PalaceSite.Digit(site.Solution, dial)));
                Fill(point.X, 1, 29, point.X, 1, 29, Lamp);
                Set(point, TidePalaceBlock.Value(dial * 3));
            }
        }

        private void WestWing()
        {
            // Two submerged bends and one recessed air pocket above the second bend.
            Room(7, -8, 19, 20, 4, 32, Water);
            Fill(16, 4, 20, 19, 4, 23, Water);
            Fill(13, -3, 20, 14, 3, 26, Column);
            Fill(8, -4, 27, 16, 2, 28, Stone);
            Fill(10, -7, 24, 12, -6, 26, Trim);
            Fill(15, -5, 26, 15, -5, 26, Lamp);
            Fill(8, -3, 23, 8, -3, 23, Lamp);
            Room(8, 3, 23, 12, 9, 27, 0);
            Fill(9, 3, 24, 11, 3, 26, Water);
            Fill(9, 8, 25, 11, 8, 25, Lamp);
            Set(PalaceSite.West, TidePalaceBlock.Value(TidePalaceBlock.Weight));
        }

        private void Vault()
        {
            // Faceted round chamber. One-cell diagonal boundaries are always backed by
            // at least one cardinal wall, even when clipped across a chunk edge.
            for (int z = 32; z <= 46; z++)
            for (int x = 19; x <= 39; x++)
            {
                if (!InsideVault(x, z)) continue;
                bool wall = !InsideVault(x - 1, z) || !InsideVault(x + 1, z) ||
                    !InsideVault(x, z - 1) || !InsideVault(x, z + 1);
                Fill(x, -7, z, x, 6, z, wall ? Stone : 0);
                Fill(x, -7, z, x, -7, z, Trim);
                Fill(x, 6, z, x, 6, z, Column);
            }
            for (int z = 31; z <= 41; z++)
            {
                int step = 36 - z;
                Fill(27, step, z, 31, step, z, Trim);
                Fill(27, step + 1, z, 31, step + 4, z, 0);
                if (z > 34) continue;
                Fill(26, step, z, 26, step + 5, z, Stone);
                Fill(32, step, z, 32, step + 5, z, Stone);
                Fill(26, step + 5, z, 32, step + 5, z, Stone);
            }
            // The ante-room's back wall and stair head overlap; only the gate plane stops entry.
            Fill(27, 5, 31, 31, 5, 31, Trim);
            Fill(27, 6, 31, 31, 9, 31, 0);
            Fill(27, -6, 42, 31, -6, 44, Wall);
            Fill(28, -5, 42, 30, -5, 44, Trim);
            Set(PalaceSite.Display, TidePalaceBlock.Value(TidePalaceBlock.Seal));
            Curtain(23);
            Curtain(34);
            Fill(28, 4, 40, 30, 4, 40, Lamp);

            // Dry exit leads into a sump. Seawater connects below both air floors,
            // so this remains a second route with normal water physics.
            Room(36, -7, 38, 44, -1, 42, 0);
            Fill(36, -6, 39, 37, -3, 41, 0);
            Room(41, -12, 38, 53, -4, 42, Water);
            Fill(41, -7, 39, 44, -4, 41, 0);
            Fill(41, -8, 39, 44, -8, 41, Water);
            Fill(45, -7, 39, 45, -4, 41, Stone);
            Room(49, -12, 38, 53, 8, 42, Water);
            Fill(50, 8, 39, 52, 8, 41, Water);
            Fill(49, -10, 39, 49, -8, 41, Water);
            Fill(50, 4, 38, 52, 4, 38, Lamp);
        }

        private static bool InsideVault(int x, int z) =>
            (x - 29) * (x - 29) * 49 + (z - 39) * (z - 39) * 100 <= 4900;

        private void Curtain(int x)
        {
            Fill(x - 1, -6, 36, x + 1, 3, 39, Glass);
            Fill(x, -5, 37, x, 2, 38, Water);
            Fill(x - 1, 3, 36, x + 1, 3, 39, Trim);
            Fill(x, -6, 37, x, -6, 38, Lamp);
        }

        private void Pillar(int x, int y, int z, int top)
        {
            Fill(x - 1, y, z - 1, x + 1, y, z + 1, Trim);
            Fill(x, y + 1, z, x, top - 1, z, Column);
            Fill(x - 1, top, z - 1, x + 1, top, z + 1, Trim);
        }

        private void Room(int x, int y, int z, int right, int top, int back, int inside)
        {
            Fill(x, y, z, right, top, back, Stone);
            Fill(x + 1, y + 1, z + 1, right - 1, top - 1, back - 1, inside);
        }

        private void Set(Point3 point, int value) =>
            Fill(point.X, point.Y, point.Z, point.X, point.Y, point.Z, value);

        private void Fill(int x, int y, int z, int right, int top, int back, int value)
        {
            for (int dz = Math.Max(z, z0); dz <= Math.Min(back, z0 + 15); dz++)
            for (int dx = Math.Max(x, x0); dx <= Math.Min(right, x0 + 15); dx++)
            {
                int offset = (((dz - z0) * 16 + dx - x0) * Height) - LowY;
                for (int dy = y; dy <= top; dy++) cells[offset + dy] = value;
            }
        }
    }
}
