using System;
using System.Collections.Generic;
using Engine;
using Game;
using SCIEPort.Features.Geology;
using SCIEPort.Features.KellyPlants;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Landmarks
{
    internal static class NaturalLandmarks
    {
        internal static void Generate(TerrainChunk chunk, ITerrainContentsGenerator generator, int worldSeed, TerrainGenerationMode mode)
        {
            if (chunk.IsLoaded || !GeologySites.Enabled(mode)) return;
            bool hasCandidates = LandmarkDeck.Choose(worldSeed, chunk.Coords, out LandmarkKind? first, out LandmarkKind? second);
            int waterfallSlot = GeologySites.Grain(chunk.Coords.X >> 1, chunk.Coords.Y >> 1, worldSeed ^ 0x482c71, 4);
            bool hasWaterfallAttempt = waterfallSlot == (chunk.Coords.X & 1) + 2 * (chunk.Coords.Y & 1);
            if (!hasCandidates && !hasWaterfallAttempt) return;

            int seed = KellyPlantRules.GetChunkSeed(worldSeed ^ 0x42d5961, chunk.Coords.X, chunk.Coords.Y);
            var random = new Game.Random(seed);
            var terrain = new LandmarkTerrain(chunk, generator, worldSeed, random.Int(0, 255), random.Int(0, 3));
            LandmarkPlan plan = Plan(first, terrain, random) ?? Plan(second, terrain, random);
            if (plan == null && hasWaterfallAttempt && first != LandmarkKind.Waterfall && second != LandmarkKind.Waterfall)
                plan = StreamLandmarks.Waterfall(terrain, random.Bool(.18f));
            plan?.Commit();
        }

        private static LandmarkPlan Plan(LandmarkKind? kind, LandmarkTerrain terrain, Game.Random random)
        {
            switch (kind)
            {
                case LandmarkKind.Waterfall: return StreamLandmarks.Waterfall(terrain, random.Bool(.18f));
                case LandmarkKind.Ford: return StreamLandmarks.Ford(terrain, random.Bool(.20f));
                case LandmarkKind.Blossom: return SakuraCreek.Generate(terrain);
                case LandmarkKind.Arch: return RockLandmarks.SeaArch(terrain);
                case LandmarkKind.Shelter: return RockLandmarks.SnowShelter(terrain);
                case LandmarkKind.StoneCircle: return StoneLandmarks.StoneCircle(terrain);
                case LandmarkKind.Hoodoo: return StoneLandmarks.HoodooField(terrain);
                case LandmarkKind.Serac: return WildLandmarks.Seracs(terrain);
                case LandmarkKind.DuneRidge: return WildLandmarks.DuneRidge(terrain);
                case LandmarkKind.SeaStacks: return RockLandmarks.SeaStacks(terrain);
                case LandmarkKind.Sinkhole: return StoneLandmarks.Sinkhole(terrain);
                case LandmarkKind.ScreeFan: return SurfaceLandmarks.ScreeFan(terrain);
                case LandmarkKind.MeadowHummocks: return SurfaceLandmarks.MeadowHummocks(terrain);
                case LandmarkKind.FrostPolygons: return SurfaceLandmarks.FrostPolygons(terrain);
                case LandmarkKind.TidePools: return TerrainDetails.TidePools(terrain);
                case LandmarkKind.MarshIslets: return TerrainDetails.MarshIslets(terrain);
                case LandmarkKind.SnowCornice: return TerrainDetails.SnowCornice(terrain);
                case LandmarkKind.SandstoneSlot: return TerrainDetails.SandstoneSlot(terrain);
                default: return null;
            }
        }
    }

    internal sealed class LandmarkTerrain
    {
        private static readonly Point2[] Directions = { new Point2(1, 0), new Point2(0, 1), new Point2(-1, 0), new Point2(0, -1) };
        private readonly int start;
        private readonly int bearing;
        private readonly ITerrainContentsGenerator generator;
        private readonly int[] surfaces = new int[256];
        private readonly float[] shores = new float[256];

        internal readonly TerrainChunk Chunk;
        internal readonly int Seed;
        internal int Sea => generator.OceanLevel;

        internal LandmarkTerrain(TerrainChunk chunk, ITerrainContentsGenerator generator, int seed, int start, int bearing)
        {
            Chunk = chunk;
            Seed = seed;
            this.generator = generator;
            this.start = start;
            this.bearing = bearing;
            for (int z = 1; z <= 14; z++)
            for (int x = 1; x <= 14; x++)
            {
                int y = chunk.CalculateTopmostCellHeight(x, z);
                if (y < 4 || y > 243) continue;
                if (IsCover(Read(new Point2(x, z), y))) y--;
                surfaces[x + 16 * z] = y;
                shores[x + 16 * z] = float.NaN;
            }
        }

        internal IEnumerable<Point2> Candidates()
        {
            for (int i = 0; i < 256; i++)
            {
                // Odd stride permutes all 256 columns and avoids preferring one chunk corner.
                int index = (start + 109 * i) & 255;
                if (surfaces[index] != 0) yield return new Point2(index & 15, index >> 4);
            }
        }

        internal IEnumerable<Point2> Bearings()
        {
            for (int i = 0; i < Directions.Length; i++) yield return Directions[(bearing + i) & 3];
        }

        internal int Surface(Point2 column) => Inside(column) ? surfaces[column.X + 16 * column.Y] : 0;
        internal int SurfaceBlock(Point2 column) => Read(column, Surface(column));
        internal int WaterSurface(Point2 column) => SurfaceBlock(column) == WaterBlock.Index ? Surface(column) : 0;

        internal float Shore(Point2 column)
        {
            int index = column.X + column.Y * 16;
            if (float.IsNaN(shores[index]))
                shores[index] = generator.CalculateOceanShoreDistance(Chunk.Origin.X + column.X, Chunk.Origin.Y + column.Y);
            return shores[index];
        }

        internal int Humidity(Point2 column) => Chunk.GetHumidityFast(column.X, column.Y);
        internal int Temperature(Point2 column, int y)
            => Chunk.GetTemperatureFast(column.X, column.Y) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);

        internal bool HasOpenGround(Point2 column, int level, int tolerance)
        {
            int surface = Surface(column);
            return surface != 0 && Math.Abs(surface - level) <= tolerance && Ground(SurfaceBlock(column)) &&
                Open(Read(column, surface + 1)) && Read(column, surface + 2) == 0;
        }

        internal bool HasPlateau(Point2 center, int level, int radius, int tolerance)
        {
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
                if (!HasOpenGround(center + new Point2(x, z), level, tolerance)) return false;
            return true;
        }

        internal int Read(Point2 column, int y)
            => Inside(column) && y >= 3 && y <= 250 ? Terrain.ExtractContents(Chunk.GetCellValueFast(column.X, y, column.Y)) : -1;

        internal int WaterBed(Point2 column, int level, int maxDepth)
        {
            for (int depth = 1; depth <= maxDepth; depth++)
            {
                int value = Read(column, level - depth);
                if (value != WaterBlock.Index) return Ground(value) ? level - depth : 0;
            }
            return 0;
        }

        internal static bool Inside(Point2 column) => column.X >= 1 && column.X <= 14 && column.Y >= 1 && column.Y <= 14;
        internal static Point2 At(Point2 origin, Point2 front, int side, int distance)
            => new Point2(origin.X + front.X * distance - front.Y * side, origin.Y + front.Y * distance + front.X * side);
        internal static bool IsCover(int block) => block == TallGrassBlock.Index || block == SnowBlock.Index;
        internal static bool Rock(int block) => block == GraniteBlock.Index || block == BasaltBlock.Index || block == SandstoneBlock.Index || block == LimestoneBlock.Index;
        internal static bool Ground(int block) => Rock(block) || PlantHabitat.IsNaturalSoil(block) || block == SandBlock.Index || block == GravelBlock.Index;
        internal static bool Open(int block) => block == 0 || IsCover(block);
        internal static bool PoolBed(int block) => Ground(block) || block == WaterBlock.Index || Open(block);
    }

    internal sealed class LandmarkPlan
    {
        private const int MaxWrites = 640;
        private readonly TerrainChunk chunk;
        private readonly Dictionary<Point3, int> writes = new Dictionary<Point3, int>();

        internal LandmarkPlan(LandmarkTerrain terrain) { chunk = terrain.Chunk; }

        internal int Read(Point2 column, int y)
        {
            if (!InBounds(column, y)) return -1;
            var cell = new Point3(column.X, y, column.Y);
            int value = writes.TryGetValue(cell, out int staged) ? staged : chunk.GetCellValueFast(cell.X, cell.Y, cell.Z);
            return Terrain.ExtractContents(value);
        }

        internal bool Set(Point2 column, int y, int value, Func<int, bool> canReplace)
        {
            if (!InBounds(column, y) || !canReplace(Read(column, y))) return false;
            var cell = new Point3(column.X, y, column.Y);
            if (writes.Count == MaxWrites && !writes.ContainsKey(cell)) return false;
            writes[cell] = value;
            return true;
        }

        internal bool TryAddTree(Point2 column, int baseY, TerrainBrush brush, Func<int, bool> supports)
        {
            if (writes.Count + brush.Cells.Length > MaxWrites) return false;
            foreach (TerrainBrush.Cell cell in brush.Cells)
            {
                var at = new Point2(column.X + cell.X, column.Y + cell.Z);
                int y = baseY + cell.Y;
                if (Read(at, y) != 0 || cell.Y == 0 && !supports(Read(at, y - 1))) return false;
            }
            foreach (TerrainBrush.Cell cell in brush.Cells)
                writes.Add(new Point3(column.X + cell.X, baseY + cell.Y, column.Y + cell.Z), cell.Value);
            return true;
        }

        internal void Commit()
        {
            if (writes.Count == 0) return;
            foreach (var edit in writes)
                chunk.SetCellValueFast(edit.Key.X, edit.Key.Y, edit.Key.Z, edit.Value);

            // Native pass 4 computes top flags before the mod runs. Update new water and the
            // existing water immediately below a changed cell using the final column contents.
            foreach (Point3 position in writes.Keys)
            {
                FixWaterSurface(position.X, position.Y, position.Z);
                FixWaterSurface(position.X, position.Y - 1, position.Z);
            }
            chunk.ModificationCounter++;
        }

        private void FixWaterSurface(int x, int y, int z)
        {
            int value = chunk.GetCellValueFast(x, y, z);
            if (Terrain.ExtractContents(value) != WaterBlock.Index) return;
            int data = FluidBlock.SetIsTop(Terrain.ExtractData(value), Terrain.ExtractContents(chunk.GetCellValueFast(x, y + 1, z)) != WaterBlock.Index);
            chunk.SetCellValueFast(x, y, z, Terrain.ReplaceData(value, data));
        }

        private static bool InBounds(Point2 column, int y) => LandmarkTerrain.Inside(column) && y >= 3 && y <= 250;
    }
}
