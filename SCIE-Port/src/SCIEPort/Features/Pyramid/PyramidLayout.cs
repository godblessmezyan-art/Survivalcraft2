using System;
using Engine;
using Game;

namespace SCIEPort.Features.Pyramid
{
    internal sealed class PyramidLayout
    {
        internal const int LowY = -14, HighY = 36;
        internal const int Unchanged = -1, Underpinning = -2;
        private const int Height = HighY - LowY + 1;
        private static readonly int Sandstone = Terrain.MakeBlockValue(SandstoneBlock.Index);
        private static readonly int Marble = Terrain.MakeBlockValue(MarbleBlock.Index);
        private static readonly int Basalt = Terrain.MakeBlockValue(BasaltBlock.Index);
        private static readonly int Limestone = Terrain.MakeBlockValue(LimestoneBlock.Index);
        private static readonly int Lamp = PyramidMechanismBlock.Value(PyramidMechanismBlock.Lamp);
        private static readonly int Relief = PyramidMechanismBlock.Value(PyramidMechanismBlock.Trim);
        private readonly int x0, z0;
        private readonly int[] cells = new int[16 * 16 * Height];

        internal PyramidLayout(PyramidSite site, Point2 origin)
        {
            x0 = origin.X - site.Origin.X;
            z0 = origin.Y - site.Origin.Y;
            for (int i = 0; i < cells.Length; i++) cells[i] = Unchanged;
            if (site.Maze != null)
            {
                if (site.Tomb) TombShell(site.ApproachFloor);
                else MazeShell(site.ApproachFloor);
                LabyrinthFloors(site.Maze);
                if (site.Tomb) BurialDetails();
                Decorate(site.Maze.Seed, site.Tomb);
                return;
            }
            Shell();
            MainHall(site);
            Bridge();
            Labyrinth();
            UpperChamber();
            Vault();
        }

        internal int At(int x, int y, int z) => cells[((z * 16 + x) * Height) + y - LowY];

        internal static int TombTop(int x, int z) => 2 + Math.Min(Math.Min(x - 5, 74 - x), Math.Min(z - 10, 79 - z));

        private void TombShell(int approachFloor)
        {
            Fill(5, LowY, 10, 74, 0, 79, Sandstone);
            for (int z = Math.Max(10, z0); z <= Math.Min(79, z0 + 15); z++)
            for (int x = Math.Max(5, x0); x <= Math.Min(74, x0 + 15); x++)
            {
                int top = TombTop(x, z);
                Fill(x, 1, z, x, top - 1, z, Sandstone);
                Set(x, top, z, Limestone);
            }
            Point3 entrance = PyramidMaze.TombEntrance;
            Approach(entrance, approachFloor, 9);
            Set(entrance.X, entrance.Y + 1, entrance.Z, Relief);
            Fill(37, 1, 10, 37, 1, 11, 0);
            Fill(37, 1, 12, 37, 2, 16, 0);
            Fill(36, 1, 16, 38, 3, 18, 0);
            Fill(35, 1, 18, 39, 3, 22, 0);
            Fill(36, 4, 18, 38, 5, 22, 0);
            Fill(37, 6, 18, 37, 6, 22, 0);
            Set(35, 3, 20, Lamp);
            Set(39, 3, 20, Lamp);
        }

        private void BurialDetails()
        {
            // Recessed chamber with masonry piers and a stone sarcophagus beside the processional aisle.
            Fill(58, -12, 66, 64, -12, 67, Limestone);
            Fill(58, -11, 66, 64, -8, 67, 0);
            Fill(58, -11, 62, 59, -11, 64, Basalt);
            Fill(58, -10, 62, 59, -10, 64, Marble);
            Fill(64, -11, 64, 64, -8, 65, Basalt);
            Fill(60, -7, 64, 62, -7, 67, Relief);
            Set(61, -8, 66, Lamp);
        }

        // Absolute-coordinate air check; never overwrite a non-air cell (seals, lamps, loot, masonry).
        private bool Air(int x, int y, int z)
        {
            if (x < x0 || x > x0 + 15 || z < z0 || z > z0 + 15 || y < LowY || y > HighY) return false;
            return cells[(((z - z0) * 16 + x - x0) * Height) + y - LowY] == 0;
        }
        // One shared block index; the kind lives in DATA. Only fills verified-air cells.
        private void Place(int x, int y, int z, int kind, int variant = 0, bool open = false, int rotation = 0)
        {
            if (Air(x, y, z))
                Set(x, y, z, Terrain.MakeBlockValue(PyramidDecorRules.Index, 0,
                    PyramidDecorRules.Encode(kind, variant, open, rotation)));
        }
        private void Decorate(int seed, bool tomb)
        {
            // Decoration is confined to the vault/burial chambers, whose carved alcoves are outside
            // the maze route. Maze rooms are left untouched so every chamber stays reachable.
            seed.ToString(); // deterministic hook retained for a future room-safe variant pass
            const int sar = PyramidDecorRules.Sarcophagus, jar = PyramidDecorRules.CanopicJar;
            // Obelisk first, so it claims a clear rear-corner cell before other decor.
            Place(59, -11, 65, PyramidDecorRules.Obelisk);
            // Vault chamber: x58..64, z59..65, carved y-11..-8. Keep the central route
            // (x60..62, z61..63,终点 61,62) clear; decor hugs the outer ring only.
            Place(58, -11, 59, PyramidDecorRules.AnubisStatue);
            Place(64, -11, 59, PyramidDecorRules.AnubisStatue);
            Place(58, -11, 62, PyramidDecorRules.Brazier);
            Place(64, -11, 62, PyramidDecorRules.Brazier, open: true);
            Place(58, -11, 64, jar, 0);
            Place(64, -11, 64, jar, 1);
            Place(58, -11, 65, jar, 2);
            Place(64, -11, 65, jar, 3);
            Place(59, -11, 59, sar);
            Place(60, -11, 59, PyramidDecorRules.OfferingTable);
            Place(62, -11, 59, PyramidDecorRules.BookOfDead);
            Place(63, -11, 59, PyramidDecorRules.StorageJar);
            if (tomb)
            {
                // Burial chamber: stone dais x58..64, z62..67, carved y-11..-8.
                // Away from the vault route terminus (61,-11,62).
                Place(61, -11, 67, sar);
                for (int i = 0; i < 4; i++) Place(59 + i, -11, 66, jar, i);
                Place(58, -11, 67, PyramidDecorRules.Shabti);
                Place(64, -11, 67, PyramidDecorRules.Shabti);
                Place(59, -11, 67, PyramidDecorRules.AnubisStatue);
                Place(63, -11, 67, PyramidDecorRules.OfferingTable);
                Place(58, -11, 66, PyramidDecorRules.StorageJar);
                Place(64, -11, 66, PyramidDecorRules.StorageJar);
            }
        }

        private void MazeShell(int approachFloor)
        {
            Fill(5, LowY, 12, 74, 0, 74, Sandstone);
            for (int z = Math.Max(12, z0); z <= Math.Min(74, z0 + 15); z++)
            for (int x = Math.Max(5, x0); x <= Math.Min(74, x0 + 15); x++)
            {
                int top = 2 + Math.Min(Math.Min(x - 5, 74 - x), Math.Min(z - 12, 74 - z));
                Fill(x, 1, z, x, top, z, Sandstone);
                if (top % 6 == 0) Set(x, top, z, Limestone);
                if (top == 33 && x >= 37 && x <= 42) Set(x, top, z, Relief);
            }
            Point3 entrance = PyramidMaze.Entrance;
            int door = entrance.X;
            Approach(entrance, approachFloor, 10);
            // Only the front cell is exposed. The threshold is flat for crouching in either direction.
            Set(door, entrance.Y, entrance.Z, 0);
            Set(door, entrance.Y + 1, entrance.Z, Relief);
            Fill(door - 1, 1, 13, door + 1, 2, 20, 0);
            Fill(door - 1, 3, 14, door + 1, 3, 20, 0);
        }

        private void Approach(Point3 entrance, int approachFloor, int rise)
        {
            int door = entrance.X;
            for (int z = 0; z < entrance.Z; z++)
            {
                int tread = Math.Max(approachFloor, Math.Min(0, z - rise));
                Fill(door - 2, LowY, z, door + 2, tread - 1, z, Underpinning);
                Fill(door - 2, tread, z, door + 2, tread, z, Limestone);
                Fill(door - 2, tread + 1, z, door + 2, 3, z, 0);
            }
        }

        private void LabyrinthFloors(PyramidMaze maze)
        {
            for (int level = 0; level < PyramidMaze.Levels; level++)
            for (int cell = 0; cell < PyramidMaze.Count; cell++)
            {
                Point3 room = PyramidMaze.Room(level, cell);
                int x = room.X, y = room.Y, z = room.Z;
                Fill(x - 1, y, z - 1, x + 1, y, z + 1, Limestone);
                Fill(x - 1, y + 1, z - 1, x + 1, y + 3, z + 1, 0);
                if (maze.East(level, cell))
                    Fill(x + 2, y + 1, z - 1, x + 4, y + 3, z + 1, 0);
                if (maze.South(level, cell))
                    Fill(x - 1, y + 1, z + 2, x + 1, y + 3, z + 4, 0);
                if (cell % 4 == 1) Set(x, y + 4, z, Lamp);
            }

            // Two side stairwells are offset by one row between floors, with masonry around each shaft.
            Transfer(61, 67, 62, 56, 0);
            Transfer(19, 12, 20, 26, -6);
            Fill(58, -12, 59, 64, -12, 65, Limestone);
            Fill(58, -11, 59, 64, -8, 65, 0);
            Fill(60, -11, 64, 62, -11, 65, Marble);
            Set(PyramidMaze.Display.X, PyramidMaze.Display.Y, PyramidMaze.Display.Z,
                PyramidMechanismBlock.Value(PyramidMechanismBlock.Seal));
            Set(58, -7, 61, Lamp);
            Set(64, -7, 61, Lamp);
        }

        private void Transfer(int roomX, int stairX, int upperZ, int lowerZ, int upperY)
        {
            int lo = Math.Min(roomX, stairX), hi = Math.Max(roomX, stairX);
            Fill(lo, upperY + 1, upperZ - 1, hi, upperY + 3, upperZ + 1, 0);
            Fill(lo, upperY - 5, lowerZ - 1, hi, upperY - 3, lowerZ + 1, 0);
            int step = Math.Sign(lowerZ - upperZ);
            for (int i = 0; i <= 6; i++)
                Stair(stairX - 1, stairX + 1, upperZ + i * step, upperY - i);
        }

        private void Shell()
        {
            Fill(2, -14, 4, 77, -1, 77, Underpinning);
            Fill(2, 0, 4, 77, 0, 77, Limestone);
            for (int z = Math.Max(5, z0); z <= Math.Min(74, z0 + 15); z++)
            for (int x = Math.Max(5, x0); x <= Math.Min(74, x0 + 15); x++)
            {
                int top = 2 + Math.Min(Math.Min(x - 5, 74 - x), Math.Min(z - 5, 74 - z));
                Fill(x, 1, z, x, top, z, Sandstone);
                if (top == HighY) Set(x, top, z, Relief);
                else if (top % 6 == 0) Set(x, top, z, Limestone);
            }
            // Broad approach supported by solid masonry. Low terraces cover the sample relief.
            for (int z = 0; z <= 5; z++)
            {
                int tread = z - 5;
                Fill(32, -14, z, 47, tread - 1, z, Underpinning);
                Fill(32, tread, z, 47, tread, z, z == 5 ? Marble : Limestone);
                Fill(36, tread + 1, z, 43, Math.Max(0, tread + 3), z, 0);
            }
            Fill(31, 1, 4, 35, 7, 7, Sandstone);
            Fill(44, 1, 4, 48, 7, 7, Sandstone);
            Fill(31, 8, 4, 48, 8, 7, Marble);
            Fill(35, 6, 4, 44, 6, 12, Sandstone);
            Fill(34, 4, 4, 34, 6, 4, Relief);
            Fill(45, 4, 4, 45, 6, 4, Relief);
            Set(34, 3, 4, Lamp);
            Set(45, 3, 4, Lamp);
        }

        private void MainHall(PyramidSite site)
        {
            Fill(36, 1, 4, 43, 5, 16, 0);
            Fill(28, 1, 14, 51, 9, 44, 0);
            Fill(36, 0, 5, 43, 0, 44, Marble);
            Fill(38, 0, 6, 41, 0, 44, Basalt);
            for (int z = 17; z <= 41; z += 6)
            {
                Pillar(31, 1, z, 9);
                Pillar(48, 1, z, 9);
                Set(32, 4, z, Lamp);
                Set(47, 4, z, Lamp);
            }
            Fill(35, 4, 44, 44, 7, 44, Relief);
            for (int step = 0; step < 3; step++)
            {
                int x = 36 + step * 3;
                Set(x, 6, 43, PyramidMechanismBlock.Value(PyramidMechanismBlock.Clue + site.SealAt(step)));
                for (int pip = 0; pip <= step; pip++) Set(x - 1 + pip, 5, 43, Lamp);
            }
            Fill(37, 1, PyramidSite.GateZ, 42, 4, PyramidSite.GateZ, PyramidMechanismBlock.Value(PyramidMechanismBlock.Gate));
        }

        private void Bridge()
        {
            Fill(10, 1, 19, 27, 6, 32, 0);
            Fill(26, 1, 24, 29, 5, 27, 0);
            Fill(13, -5, 19, 24, -5, 32, Basalt);
            Fill(13, -4, 19, 24, 0, 32, 0);
            for (int x = 13; x <= 22; x += 3)
            {
                Fill(x, -4, 24, x + 1, 0, 27, Sandstone);
                Fill(x, 0, 24, x + 1, 0, 24, Marble);
            }
            // Missed jumps land on the lower floor. This stair leads back to the near bank.
            for (int x = 21; x <= 25; x++)
                Fill(x, -5, 30, x, x - 25, 32, Sandstone);
            Fill(10, 1, 24, 11, 1, 26, Marble);
            Set(11, 2, 25, PyramidMechanismBlock.Value(0));
            Set(11, 4, 23, Lamp);
            Set(26, 4, 28, Lamp);
        }

        private void Labyrinth()
        {
            Fill(51, 1, 18, 68, 7, 42, 0);
            Fill(50, 1, 25, 53, 5, 28, 0);
            for (int lane = 0; lane < 3; lane++)
            {
                int x = 54 + lane * 5, opening = lane == 1 ? 38 : 20;
                Fill(x, 1, 18, x, 8, 42, Sandstone);
                Fill(x, 1, opening, x, 4, opening + 2, 0);
                Fill(x + 1, 5, opening, x + 1, 5, opening + 2, Marble);
                Set(x + 1, 4, opening + 1, Lamp);
            }
            Fill(57, 1, 28, 58, 4, 30, Limestone);
            Fill(61, 1, 33, 62, 4, 36, Limestone);
            Fill(66, 1, 36, 68, 1, 38, Marble);
            Set(67, 2, 37, PyramidMechanismBlock.Value(1));
            Set(68, 4, 41, Lamp);
        }

        private void UpperChamber()
        {
            Fill(33, 18, 24, 48, 20, 32, 0);
            Fill(33, 17, 24, 48, 17, 31, Limestone);
            Fill(35, 9, 34, 46, 9, 36, Marble);
            Fill(35, 10, 34, 46, 13, 36, 0);
            for (int z = 26; z <= 34; z++)
            {
                Stair(35, 38, z, z - 25);
                Stair(43, 46, z, 43 - z);
            }
            Fill(38, 18, 24, 40, 18, 26, Marble);
            Set(39, 19, 25, PyramidMechanismBlock.Value(2));
            Set(36, 20, 26, Lamp);
            Set(45, 20, 29, Lamp);
        }

        private void Vault()
        {
            Fill(26, -13, 54, 53, -2, 73, Basalt);
            Fill(27, -12, 55, 52, -3, 72, Sandstone);
            Fill(28, -11, 56, 51, -4, 71, 0);
            Fill(37, -11, 61, 43, -11, 67, Limestone);
            Fill(38, -10, 63, 42, -10, 66, Marble);
            Set(PyramidSite.Display.X, PyramidSite.Display.Y, PyramidSite.Display.Z, PyramidMechanismBlock.Value(PyramidMechanismBlock.Seal));
            for (int z = 59; z <= 69; z += 5)
            {
                Pillar(31, -11, z, -4);
                Pillar(48, -11, z, -4);
                Set(32, -7, z, Lamp);
                Set(47, -7, z, Lamp);
            }
            for (int z = 46; z <= 57; z++)
            {
                int floor = 46 - z;
                Stair(38, 41, z, floor);
                if (z >= 56) continue;
                Fill(37, floor, z, 37, floor + 5, z, Basalt);
                Fill(42, floor, z, 42, floor + 5, z, Basalt);
                Fill(38, floor + 5, z, 41, floor + 5, z, Sandstone);
            }
        }

        private void Pillar(int x, int y, int z, int top)
        {
            Fill(x - 1, y, z - 1, x + 1, y, z + 1, Basalt);
            Fill(x, y + 1, z, x, top - 1, z, Limestone);
            Fill(x - 1, top, z - 1, x + 1, top, z + 1, Marble);
        }

        private void Stair(int left, int right, int z, int floor)
        {
            Fill(left, floor, z, right, floor, z, Sandstone);
            Fill(left, floor + 1, z, right, floor + 4, z, 0);
        }

        private void Set(int x, int y, int z, int value) => Fill(x, y, z, x, y, z, value);

        private void Fill(int left, int bottom, int front, int right, int top, int back, int value)
        {
            for (int z = Math.Max(front, z0); z <= Math.Min(back, z0 + 15); z++)
            for (int x = Math.Max(left, x0); x <= Math.Min(right, x0 + 15); x++)
            {
                int offset = (((z - z0) * 16 + x - x0) * Height) - LowY;
                for (int y = bottom; y <= top; y++) cells[offset + y] = value;
            }
        }
    }
}
