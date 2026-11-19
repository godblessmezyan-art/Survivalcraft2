using System;
using System.Collections.Generic;
using Engine;
using Game;
using SCIEPort.Features.KellyPlants;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.SpecialTrees
{
    internal sealed class TreeSpecies
    {
        internal const int RootFlag = TexturedTreeWoodBlock.GrownRoot;
        internal const int MaxCells = 160;
        internal const int AtlasStride = 9;
        internal const int FruitSlot = 6;
        internal const int FruitLeafSlot = 7;

        internal static readonly TreeSpecies Palm = new TreeSpecies(0, 981, 982, 983, 0, 4, 8, .19f);
        internal static readonly TreeSpecies Coconut = new TreeSpecies(1, 984, 985, 986, 987, 5, 6, .33f);
        internal static readonly TreeSpecies Baobab = new TreeSpecies(2, 988, 989, 990, 991, 3, 11, .15f);
        internal static readonly TreeSpecies Sakura = new TreeSpecies(3, 992, 993, 994, 0, 3, 7, .20f);
        internal static readonly TreeSpecies[] All = { Palm, Coconut, Baobab, Sakura };

        internal readonly int Kind;
        internal readonly int Sapling;
        internal readonly int Wood;
        internal readonly int Leaves;
        internal readonly int Fruit;
        internal readonly int Variants;
        internal readonly int GrowthInterval;
        internal readonly float GenerationChance;

        private readonly TerrainBrush[] young;
        private readonly TerrainBrush[] wild;

        private TreeSpecies(int kind, int sapling, int wood, int leaves, int fruit, int variants, int interval, float chance)
        {
            Kind = kind;
            Sapling = sapling;
            Wood = wood;
            Leaves = leaves;
            Fruit = fruit;
            Variants = variants;
            GrowthInterval = interval;
            GenerationChance = chance;
            young = new TerrainBrush[Variants];
            wild = new TerrainBrush[Variants];
            for (int variant = 0; variant < Variants; variant++)
            {
                young[variant] = MakeBrush(variant, false);
                wild[variant] = MakeBrush(variant, true);
            }
        }

        internal int AtlasStart => Kind * AtlasStride;
        internal int MaxHeight => Kind == 0 ? 22 : Kind == 1 ? 11 : Kind == 2 ? 10 : 8;

        internal bool Registered => BlocksManager.Blocks[Sapling] is SpecialTreeSapling &&
            BlocksManager.Blocks[Wood] is SpecialTreeWood &&
            BlocksManager.Blocks[Leaves] is SpecialTreeLeaves &&
            (Fruit == 0 || BlocksManager.Blocks[Fruit] is SpecialTreeFruit);

        internal bool Supports(int ground)
            => PlantHabitat.IsRootingSoil(ground) || Kind != 3 && ground == SandBlock.Index;

        internal bool WildSite(int ground, int temp, int humidity, int height, float shore)
        {
            if (Kind == 1)
                return ground == SandBlock.Index && temp >= 10 && humidity >= 5 &&
                    height >= 0 && height <= 8 && shore >= 0f && shore <= 24f;
            if (!PlantHabitat.IsNaturalSoil(ground)) return false;
            switch (Kind)
            {
                case 0: return temp >= 9 && humidity >= 8 && height >= 0 && height <= 15 && shore >= 1f && shore <= 48f;
                case 2: return temp >= 10 && humidity >= 2 && humidity <= 8 && height >= 1 && height <= 42 && shore >= 24f;
                default: return temp >= 5 && temp <= 13 && humidity >= 5 && height >= 2 && height <= 50 && shore >= 12f;
            }
        }

        internal bool CanGrow(int temperature, int humidity, int light, bool watered)
        {
            if (light < 9) return false;
            switch (Kind)
            {
                case 0: return temperature >= 6 && (watered || humidity >= 6);
                case 1: return temperature >= 7 && (watered || humidity >= 4);
                case 2: return temperature >= 7 && humidity <= 12;
                default: return temperature >= 2 && temperature <= 18 && (watered || humidity >= 3);
            }
        }

        internal TerrainBrush GetBrush(int variant, bool ripe)
            => (ripe ? wild : young)[variant];

        internal int VariantAt(int seed, int x, int z)
            => (int)((uint)KellyPlantRules.GetChunkSeed(seed ^ Sapling, x, z) % Variants);

        private TerrainBrush MakeBrush(int variant, bool ripe)
        {
            var cells = new Dictionary<Point3, int>();
            int vertical = Terrain.MakeBlockValue(Wood);
            int root = Terrain.MakeBlockValue(Wood, 0, RootFlag);
            int leaf = Terrain.MakeBlockValue(Leaves);
            cells.Add(new Point3(0, 0, 0), root);

            if (Kind == 0 || Kind == 1)
                MakePalm(cells, variant, ripe, vertical, leaf);
            else
                MakeBroadleaf(cells, variant, ripe, vertical, leaf);

            if (cells.Count > MaxCells) throw new InvalidOperationException("Special tree exceeds the terrain transaction budget.");
            var brush = new TerrainBrush();
            foreach (var cell in cells)
                brush.AddCell(cell.Key.X, cell.Key.Y, cell.Key.Z, cell.Value);
            brush.Compile();
            return brush;
        }

        private void MakePalm(Dictionary<Point3, int> cells, int variant, bool ripe, int wood, int leaf)
        {
            bool tall = Kind == 0;
            int height = tall ? 16 + variant * 2 : 7 + variant;
            int crown = height - 2;
            int bend = crown / 2;
            Point2 lean = tall ? Point2.Zero : Cardinal(variant % 4);
            for (int y = 1; y < crown; y++)
            {
                if (!tall && y == bend + 1)
                {
                    int face = lean.X == 0 ? 0 : 1;
                    cells[new Point3(0, y, 0)] = Terrain.MakeBlockValue(Wood, 0, WoodBlock.SetCutFace(SpecialTreeWood.BarkAtEnds, face));
                }
                Point2 offset = y > bend ? lean : Point2.Zero;
                cells[new Point3(offset.X, y, offset.Y)] = wood;
            }

            AddLeaf(cells, lean.X, crown, lean.Y, leaf);
            AddLeaf(cells, lean.X, crown + 1, lean.Y, leaf);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                int length = tall && (dx == 0 || dz == 0) ? 3 : 2;
                for (int radius = 1; radius <= length; radius++)
                {
                    int y = crown - radius / 2;
                    AddLeaf(cells, lean.X + dx * radius, y, lean.Y + dz * radius, leaf);
                    if (radius == 2 && (dx == 0 || dz == 0))
                        AddLeaf(cells, lean.X + dx, y, lean.Y + dz, leaf);
                }
            }

            if (!tall)
            {
                for (int i = -1; i <= 1; i++)
                {
                    Point2 side = Cardinal((variant + i + 4) % 4);
                    PlaceFruit(cells, lean.X + side.X, crown - 2, lean.Y + side.Y, ripe);
                }
            }
        }

        private void MakeBroadleaf(Dictionary<Point3, int> cells, int variant, bool ripe, int wood, int leaf)
        {
            bool broad = Kind == 2;
            int height = (broad ? 8 : 6) + variant;
            if (broad)
                for (int i = 0; i < 4; i++)
                {
                    Point2 foot = Cardinal(i);
                    cells[new Point3(foot.X, 0, foot.Y)] = wood;
                }
            for (int y = 1; y < height - 2; y++)
            {
                int radius = broad && y <= 4 ? 1 : 0;
                for (int x = -radius; x <= radius; x++)
                for (int z = -radius; z <= radius; z++)
                {
                    if (y == 4 && Math.Abs(x) + Math.Abs(z) > 1) continue;
                    cells[new Point3(x, y, z)] = wood;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                Point2 side = Cardinal(i);
                int facing = side.X == 0 ? 0 : 1;
                int branch = Terrain.MakeBlockValue(Wood, 0, WoodBlock.SetCutFace(SpecialTreeWood.BarkAtEnds, facing));
                cells[new Point3(side.X, height - 4, side.Y)] = branch;
                cells[new Point3(side.X * 2, height - 4, side.Y * 2)] = branch;
                cells[new Point3(side.X * 2, height - 3, side.Y * 2)] = wood;
                if (broad) PlaceFruit(cells, side.X * 2, height - 5, side.Y * 2, ripe);
            }

            for (int y = height - 3; y < height; y++)
            {
                int radius = y == height - 1 ? 2 : 3;
                for (int x = -radius; x <= radius; x++)
                for (int z = -radius; z <= radius; z++)
                {
                    int distance = Math.Abs(x) + Math.Abs(z);
                    if (distance > radius + (y == height - 2 ? 1 : 0)) continue;
                    if (broad && y == height - 1 && distance == 2 && x != 0 && z != 0) continue;
                    AddLeaf(cells, x, y, z, leaf);
                }
            }
        }

        private void PlaceFruit(Dictionary<Point3, int> cells, int x, int y, int z, bool ripe)
        {
            int state = ripe ? Kind == 1 ? FruitStage.RipeTwo : FruitStage.RipeOne : 0;
            AddLeaf(cells, x, y, z, Terrain.MakeBlockValue(Leaves, 0, FruitStage.Encode(x, y, z) | state));
        }

        private static void AddLeaf(Dictionary<Point3, int> cells, int x, int y, int z, int value)
        {
            var position = new Point3(x, y, z);
            if (!cells.ContainsKey(position)) cells.Add(position, value);
        }

        private static Point2 Cardinal(int i)
        {
            switch (i)
            {
                case 0: return new Point2(1, 0);
                case 1: return new Point2(0, 1);
                case 2: return new Point2(-1, 0);
                default: return new Point2(0, -1);
            }
        }
    }

    internal static class FruitStage
    {
        internal const int Owned = 8;
        internal const int Mask = 7;
        internal const int RipeOne = 4;
        internal const int RipeTwo = 5;
        internal const int MaxNearbyCoconuts = 2;

        internal static int Encode(int x, int y, int z) => Owned | ((x + 3) << 4) | ((z + 3) << 7) | (y << 10);

        internal static Point3 RootAt(Point3 leaf, int data)
            => new Point3(leaf.X - (((data >> 4) & 7) - 3), leaf.Y - ((data >> 10) & 15), leaf.Z - (((data >> 7) & 7) - 3));

        internal static int Count(int data)
        {
            int stage = data & Mask;
            return stage == RipeTwo ? 2 : stage == RipeOne ? 1 : 0;
        }

        internal static int AfterDrop(int data) => (data & ~Mask) | (Count(data) == 2 ? RipeOne : 0);
    }
}
