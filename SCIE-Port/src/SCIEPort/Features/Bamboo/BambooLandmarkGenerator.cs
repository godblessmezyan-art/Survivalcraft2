using System;
using System.Collections.Generic;
using Engine;
using Game;
using SCIEPort.Features.KellyPlants;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Bamboo
{
    internal static class BambooLandmarkGenerator
    {
        private const int ChunkSize = 16;
        private const int MaxTrailLength = 13;
        private const int MinInlandDistance = 12;
        private static readonly Point2[] Directions = { new Point2(1, 0), new Point2(0, 1), new Point2(-1, 0), new Point2(0, -1) };
        private static readonly int[] FenceBits = { 1, 4, 2, 8 };

        internal static void Generate(TerrainChunk chunk, int worldSeed, Point3 grove, ITerrainContentsGenerator original)
        {
            if (!(BlocksManager.Blocks[BambooFenceBlock.Index] is BambooFenceBlock) ||
                !(BlocksManager.Blocks[BambooFenceGateBlock.Index] is BambooFenceGateBlock) ||
                !(BlocksManager.Blocks[BambooScreenBlock.Index] is BambooScreenBlock)) return;

            int seed = KellyPlantRules.GetChunkSeed(unchecked(worldSeed ^ BambooFenceBlock.Index * 16777619), chunk.Coords.X, chunk.Coords.Y);
            var random = new Game.Random(seed);
            bool tryPavilion = random.Bool(.40f);
            bool tryTrail = random.Bool(.58f);
            if (!tryPavilion && !tryTrail) return;

            var ground = new int[ChunkSize * ChunkSize];
            var water = new int[ChunkSize * ChunkSize];
            ScanSurface(chunk, ground, water);
            var center = new Point2(grove.X, grove.Z);
            int direction = random.Int(0, 3);
            if (tryPavilion && TryPavilion(chunk, original, ground, water, center, direction)) return;
            if (!tryTrail) return;

            for (int i = 0; i < Directions.Length; i++)
            {
                Point2 across = Directions[(i + direction) % Directions.Length];
                Point2 start = new Point2(center.X - 4 * across.X + across.Y, center.Y - 4 * across.Y - across.X);
                List<Point2> trail = FindTrail(ground, start, goal =>
                {
                    int x = goal.X - center.X, z = goal.Y - center.Y;
                    return x * across.X + z * across.Y >= 3 && Math.Abs(x * across.Y - z * across.X) <= 3;
                }, direction);
                if (trail == null) continue;
                PaintTrail(chunk, ground, trail);
                return;
            }
        }

        private static void ScanSurface(TerrainChunk chunk, int[] ground, int[] water)
        {
            for (int z = 0; z < ChunkSize; z++)
            for (int x = 0; x < ChunkSize; x++)
            {
                int top = chunk.CalculateTopmostCellHeight(x, z);
                if (top < 2 || top > 248) continue;
                int contents = Contents(chunk, x, top, z);
                int column = Column(x, z);
                if (contents == WaterBlock.Index)
                {
                    water[column] = top;
                    continue;
                }
                if (contents == TallGrassBlock.Index) contents = Contents(chunk, x, --top, z);
                if (!PlantHabitat.IsNaturalSoil(contents)) continue;
                int substrate = Contents(chunk, x, top - 1, z);
                if (BlocksManager.Blocks[substrate].IsCollidable && !(BlocksManager.Blocks[substrate] is FluidBlock))
                    ground[column] = top;
            }
        }

        private static bool TryPavilion(TerrainChunk chunk, ITerrainContentsGenerator original, int[] ground, int[] water, Point2 grove, int direction)
        {
            for (int z = 2; z < ChunkSize - 2; z++)
            for (int x = 2; x < ChunkSize - 2; x++)
            {
                var center = new Point2(x, z);
                int distance = Distance(center, grove);
                int groundY = ground[Column(x, z)];
                if (groundY == 0 || distance < 3 || distance > 11) continue;
                for (int i = 0; i < Directions.Length; i++)
                {
                    Point2 riverward = Directions[(i + direction) % Directions.Length];
                    if (!HasInlandBank(chunk, original, water, center, riverward, groundY)) continue;
                    Point2 front = new Point2(-riverward.X, -riverward.Y);
                    int deck = DeckHeight(ground, center, groundY);
                    if (deck == 0 || !RoofHasSpace(chunk, center, deck)) continue;

                    Point2 entry = Offset(center, front, 0, 2);
                    if (!Inside(entry)) continue;
                    int entryHeight = ground[Column(entry.X, entry.Y)];
                    if (entryHeight < deck - 1 || entryHeight > deck) continue;
                    // Keep the search outside the platform. The trail and the shelter are
                    // painted only after every floor, roof and approach cell has been accepted.
                    List<Point2> trail = FindTrail(ground, entry, goal => Distance(goal, grove) <= 3, direction, center);
                    if (trail == null) continue;
                    PaintPavilion(chunk, ground, center, front, deck);
                    PaintTrail(chunk, ground, trail);
                    return true;
                }
            }
            return false;
        }

        private static bool HasInlandBank(TerrainChunk chunk, ITerrainContentsGenerator original, int[] water, Point2 center, Point2 riverward, int groundY)
        {
            Point2 bank = Offset(center, riverward, 0, 2);
            int level = water[Column(bank.X, bank.Y)];
            if (level == 0)
            {
                bank = Offset(center, riverward, 0, 3);
                if (!Inside(bank)) return false;
                level = water[Column(bank.X, bank.Y)];
            }
            if (level == 0 || level > groundY || groundY - level > 3) return false;
            for (int along = -1; along <= 1; along++)
            {
                Point2 reach = Offset(bank, riverward, along, 0);
                if (!Inside(reach) || water[Column(reach.X, reach.Y)] != level) return false;
            }
            return original.CalculateOceanShoreDistance(chunk.Origin.X + bank.X, chunk.Origin.Y + bank.Y) >= MinInlandDistance;
        }

        private static int DeckHeight(int[] ground, Point2 center, int baseHeight)
        {
            int max = baseHeight, min = baseHeight;
            for (int z = center.Y - 1; z <= center.Y + 1; z++)
            for (int x = center.X - 1; x <= center.X + 1; x++)
            {
                int height = ground[Column(x, z)];
                if (height == 0) return 0;
                max = Math.Max(max, height);
                min = Math.Min(min, height);
            }
            return max - min <= 1 ? max : 0;
        }

        private static bool RoofHasSpace(TerrainChunk chunk, Point2 center, int deck)
        {
            for (int z = center.Y - 2; z <= center.Y + 2; z++)
            for (int x = center.X - 2; x <= center.X + 2; x++)
            {
                for (int y = deck + 4; y <= deck + 6; y++)
                    if (Contents(chunk, x, y, z) != 0) return false;
            }
            return true;
        }

        private static List<Point2> FindTrail(int[] ground, Point2 start, Func<Point2, bool> isGoal, int direction, Point2? platform = null)
        {
            if (!Inside(start) || ground[Column(start.X, start.Y)] == 0) return null;
            var parent = new Point2[ChunkSize * ChunkSize];
            var length = new int[parent.Length];
            var queue = new Queue<Point2>();
            length[Column(start.X, start.Y)] = 1;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Point2 current = queue.Dequeue();
                int column = Column(current.X, current.Y);
                int steps = length[column];
                if (steps >= (platform.HasValue ? 2 : 7) && isGoal(current))
                {
                    var trail = new List<Point2>(steps);
                    for (int i = 0; i < steps; i++)
                    {
                        trail.Add(current);
                        current = parent[Column(current.X, current.Y)];
                    }
                    return trail;
                }
                if (steps >= MaxTrailLength) continue;
                for (int i = 0; i < Directions.Length; i++)
                {
                    Point2 next = current + Directions[(i + direction) % Directions.Length];
                    if (!Inside(next) || (platform.HasValue && WithinPlatform(next, platform.Value))) continue;
                    int target = Column(next.X, next.Y);
                    if (length[target] != 0 || ground[target] == 0 || Math.Abs(ground[target] - ground[column]) > 1) continue;
                    length[target] = steps + 1;
                    parent[target] = current;
                    queue.Enqueue(next);
                }
            }
            return null;
        }

        private static void PaintTrail(TerrainChunk chunk, int[] ground, List<Point2> trail)
        {
            foreach (Point2 cell in trail)
            {
                int y = ground[Column(cell.X, cell.Y)];
                if (Contents(chunk, cell.X, y + 1, cell.Y) == TallGrassBlock.Index)
                    chunk.SetCellValueFast(cell.X, y + 1, cell.Y, 0);
                chunk.SetCellValueFast(cell.X, y, cell.Y, Terrain.MakeBlockValue(GravelBlock.Index));
            }
        }

        private static void PaintPavilion(TerrainChunk chunk, int[] ground, Point2 center, Point2 front, int deck)
        {
            int floor = Terrain.MakeBlockValue(BambooBlock.Index);
            int rotation = front.X == 0 ? 0 : 1;
            for (int v = -1; v <= 1; v++)
            for (int u = -1; u <= 1; u++)
            {
                Point2 cell = Offset(center, front, u, v);
                int natural = ground[Column(cell.X, cell.Y)];
                if (Contents(chunk, cell.X, natural + 1, cell.Y) == TallGrassBlock.Index)
                    chunk.SetCellValueFast(cell.X, natural + 1, cell.Y, 0);
                chunk.SetCellValueFast(cell.X, deck, cell.Y, floor);
                if (u != 0)
                {
                    int mask = 0;
                    for (int bit = 0; bit < Directions.Length; bit++)
                    {
                        Point2 adjacent = cell + Directions[bit];
                        int dx = adjacent.X - center.X, dz = adjacent.Y - center.Y;
                        int localU = dx * -front.Y + dz * front.X;
                        int localV = dx * front.X + dz * front.Y;
                        if (Math.Abs(localU) > 1 || Math.Abs(localV) > 1 || (localU == 0 && localV != 1)) continue;
                        // Neighbor ordering in the engine's fence variant is +X, -X, +Z, -Z.
                        mask |= FenceBits[bit];
                    }
                    chunk.SetCellValueFast(cell.X, deck + 1, cell.Y, Terrain.MakeBlockValue(BambooFenceBlock.Index, 0, FenceBlock.SetVariant(0, mask)));
                    if (v != 0)
                    {
                        chunk.SetCellValueFast(cell.X, deck + 2, cell.Y, Terrain.MakeBlockValue(BambooFenceBlock.Index));
                        chunk.SetCellValueFast(cell.X, deck + 3, cell.Y, Terrain.MakeBlockValue(BambooFenceBlock.Index));
                    }
                }
                else if (v == 1)
                {
                    chunk.SetCellValueFast(cell.X, deck + 1, cell.Y, Terrain.MakeBlockValue(BambooFenceGateBlock.Index, 0, FenceGateBlock.SetRotation(0, rotation)));
                }
                else if (v == -1)
                {
                    int screen = Terrain.MakeBlockValue(BambooScreenBlock.Index, 0, rotation);
                    chunk.SetCellValueFast(cell.X, deck + 1, cell.Y, screen);
                    chunk.SetCellValueFast(cell.X, deck + 2, cell.Y, screen);
                }
                if (v != 0)
                    chunk.SetCellValueFast(cell.X, deck + 4, cell.Y, floor);
            }

            for (int v = -2; v <= 2; v++)
            for (int u = -2; u <= 2; u++)
            {
                Point2 cell = Offset(center, front, u, v);
                int rise = 2 - Math.Abs(u);
                int value = u == 0 ? Terrain.MakeBlockValue(WoodenSlabBlock.Index) : StairTowardRidge(front, u);
                chunk.SetCellValueFast(cell.X, deck + 4 + rise, cell.Y, value);
            }
        }

        private static int StairTowardRidge(Point2 front, int u)
        {
            int side = u < 0 ? 1 : -1;
            int x = -front.Y * side, z = front.X * side;
            int rotation = x > 0 ? 3 : x < 0 ? 1 : z > 0 ? 2 : 0;
            return Terrain.MakeBlockValue(WoodenStairsBlock.Index, 0, StairsBlock.SetRotation(0, rotation));
        }

        private static Point2 Offset(Point2 center, Point2 front, int u, int v)
            => new Point2(center.X - front.Y * u + front.X * v, center.Y + front.X * u + front.Y * v);

        private static bool WithinPlatform(Point2 cell, Point2 center)
            => Math.Abs(cell.X - center.X) <= 1 && Math.Abs(cell.Y - center.Y) <= 1;

        private static int Distance(Point2 a, Point2 b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        private static bool Inside(Point2 cell) => cell.X >= 1 && cell.X < ChunkSize - 1 && cell.Y >= 1 && cell.Y < ChunkSize - 1;

        private static int Column(int x, int z) => x + z * ChunkSize;

        private static int Contents(TerrainChunk chunk, int x, int y, int z)
            => Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z));
    }
}
