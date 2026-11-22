#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using SCIEPort.Features.Apple;
using SCIEPort.Features.Plants;
using TemplatesDatabase;

namespace SCIEPort.Features.SpecialTrees
{
    public sealed class SubsystemSpecialTreeBehavior : SubsystemPollableBlockBehavior, IUpdateable
    {
        private static readonly int[] s_handled =
        {
            TallPalmSaplingBlock.Index, CoconutSaplingBlock.Index, BaobabSaplingBlock.Index, SakuraSaplingBlock.Index,
            CoconutLeavesBlock.Index, BaobabLeavesBlock.Index, CoconutFruitBlock.Index, BaobabFruitBlock.Index
        };

        private readonly Game.Random random = new Game.Random();
        private SubsystemGameInfo gameInfo;
        private SubsystemPickables pickables;
        private int writes = 2 * TreeSpecies.MaxCells;
        private bool halted;

        public override int[] HandledBlocks => s_handled;
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
            pickables = Project.FindSubsystem<SubsystemPickables>(true);
        }

        public void Update(float dt) => writes = 2 * TreeSpecies.MaxCells;

        private Terrain TerrainData => SubsystemTerrain.Terrain;

        private bool Loaded(Point3 p)
        {
            TerrainChunk chunk = TerrainData.GetChunkAtCell(p.X, p.Z);
            return p.Y >= 1 && p.Y <= 254 && chunk != null && chunk.State > TerrainChunkState.InvalidContents4;
        }

        private int Read(Point3 p) => Terrain.ReplaceLight(TerrainData.GetCellValue(p.X, p.Y, p.Z), 0);

        private void Write(Point3 p, int value) => SubsystemTerrain.ChangeCell(p.X, p.Y, p.Z, value, true, null);

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            if (CommonLib.WorkType == WorkType.Client || halted || writes < 2 ||
                x <= int.MinValue + 4 || x >= int.MaxValue - 4 || z <= int.MinValue + 4 || z >= int.MaxValue - 4 ||
                !PlantGrowthSite.IsLiving(SubsystemTerrain)) return;

            var point = new Point3(x, y, z);
            if (!Loaded(point)) return;
            value = Read(point);
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
            if (block is SpecialTreeSapling seedling && seedling.Species.Registered)
                PollSapling(point, value, seedling.Species, pollPass);
            else if (block is SpecialTreeLeaves leaf && leaf.Species.Fruit != 0 && leaf.Species.Registered)
                PollFruit(point, value, leaf.Species, pollPass);
        }

        private void PollSapling(Point3 root, int before, TreeSpecies species, int pollPass)
        {
            int soil = TerrainData.GetCellValue(root.X, root.Y - 1, root.Z);
            int ground = Terrain.ExtractContents(soil);
            if (!species.Supports(ground))
            {
                // As with native saplings, unsupported seedlings are removed without growing.
                Change(root, 0);
                return;
            }

            bool watered = ground == SoilBlock.Index && SoilBlock.GetHydration(Terrain.ExtractData(soil));
            int temperature = PlantHabitat.TemperatureAt(TerrainData, root.X, root.Y, root.Z);
            if (!species.CanGrow(temperature, TerrainData.GetSeasonalHumidity(root.X, root.Z),
                TerrainData.GetCellLight(root.X, root.Y + 1, root.Z), watered)) return;

            int interval = watered ? species.GrowthInterval - 2 : species.GrowthInterval;
            if (pollPass % interval != 0) return;

            int stage = Terrain.ExtractData(before);
            if (stage < 5)
                Change(root, Terrain.ReplaceData(before, stage + 1));
            else
                Grow(root, before, species.GetBrush(species.VariantAt(gameInfo.WorldSeed, root.X, root.Z), false), species);
        }

        private void Grow(Point3 root, int sapling, TerrainBrush brush, TreeSpecies species)
        {
            int count = brush.Cells.Length;
            if (writes < count * 2) return;
            var points = new Point3[count];
            var before = new int[count];
            var after = new int[count];
            for (int i = 0; i < count; i++)
            {
                TerrainBrush.Cell cell = brush.Cells[i];
                var point = new Point3(root.X + cell.X, root.Y + cell.Y, root.Z + cell.Z);
                int expected = point == root ? sapling : 0;
                if (!Loaded(point) || Read(point) != expected) return;
                if (cell.Y == 0 && !species.Supports(TerrainData.GetCellContents(point.X, point.Y - 1, point.Z))) return;
                points[i] = point;
                before[i] = expected;
                after[i] = cell.Value;
            }

            writes -= count * 2;
            bool undoFailed;
            Exception error;
            AppleTreeTransaction.Apply(before, after, i => Read(points[i]), (i, value) => Write(points[i], value),
                out undoFailed, out error, TreeSpecies.MaxCells);
            if (undoFailed)
            {
                halted = true;
                Log.Error("[SCIEPort:Trees] Native terrain write could not be restored: " + error);
            }
            else if (error != null)
                Log.Warning("[SCIEPort:Trees] Tree growth rolled back: " + error.Message);
        }

        private bool Change(Point3 p, int next)
        {
            if (writes < 2) return false;
            writes -= 2;
            Write(p, next);
            return Read(p) == next;
        }

        private void PollFruit(Point3 point, int value, TreeSpecies species, int pollPass)
        {
            int data = Terrain.ExtractData(value);
            if ((data & FruitStage.Owned) == 0) return;
            int count = FruitStage.Count(data);
            int interval = species == TreeSpecies.Coconut ? 4 : 7;
            if (count == 0 && pollPass % interval != 0) return;
            if (count > 0 && (species != TreeSpecies.Coconut || !random.Bool(.13f))) return;

            Point3 root = FruitStage.RootAt(point, data);
            if (!Loaded(root)) return;
            int wood = Read(root);
            if (Terrain.ExtractContents(wood) != species.Wood || (Terrain.ExtractData(wood) & TreeSpecies.RootFlag) == 0)
                return;
            if (!HasNearbyWood(point, species.Wood)) return;

            if (count > 0)
            {
                TryShedCoconut(point, root, value);
                return;
            }

            int temperature = PlantHabitat.TemperatureAt(TerrainData, root.X, root.Y, root.Z);
            int soil = TerrainData.GetCellValue(root.X, root.Y - 1, root.Z);
            bool watered = Terrain.ExtractContents(soil) == SoilBlock.Index && SoilBlock.GetHydration(Terrain.ExtractData(soil));
            if (!species.CanGrow(temperature, TerrainData.GetSeasonalHumidity(root.X, root.Z), 15, watered)) return;
            int stage = data & FruitStage.Mask;
            int next = stage >= 3 ? species == TreeSpecies.Coconut ? FruitStage.RipeTwo : FruitStage.RipeOne : stage + 1;
            Change(point, Terrain.ReplaceData(value, (data & ~FruitStage.Mask) | next));
        }

        private bool HasNearbyWood(Point3 point, int wood)
        {
            for (int x = point.X - 1; x <= point.X + 1; x++)
            for (int z = point.Z - 1; z <= point.Z + 1; z++)
            for (int y = point.Y - 1; y <= point.Y + 2; y++)
                if (TerrainData.GetCellContents(x, y, z) == wood) return true;
            return false;
        }

        private bool TryShedCoconut(Point3 point, Point3 root, int expect)
        {
            if (CommonLib.WorkType == WorkType.Client || pickables == null || Read(point) != expect ||
                TerrainData.GetCellContents(point.X, point.Y - 1, point.Z) != 0) return false;
            int data = Terrain.ExtractData(expect);
            if (Terrain.ExtractContents(expect) != CoconutLeavesBlock.Index || FruitStage.Count(data) == 0) return false;

            var items = pickables.Pickables;
            if (items.Count > 4096) return false;
            int existing = 0;
            foreach (Pickable item in items)
            {
                if (item.ToRemove || Terrain.ExtractContents(item.Value) != CoconutFruitBlock.Index) continue;
                float dx = item.Position.X - (root.X + .5f);
                float dz = item.Position.Z - (root.Z + .5f);
                if (dx * dx + dz * dz > 20.25f) continue;
                existing += item.Count;
                if (existing >= FruitStage.MaxNearbyCoconuts) return false;
            }

            int depleted = Terrain.ReplaceData(expect, FruitStage.AfterDrop(data));
            if (!Change(point, depleted)) return false;
            try
            {
                return pickables.AddPickable(Terrain.MakeBlockValue(CoconutFruitBlock.Index), 1,
                    new Vector3(point.X + .5f, point.Y - .12f, point.Z + .5f), Vector3.Zero, null) != null;
            }
            catch (Exception error)
            {
                // A native network exception may happen after insertion into the pickables collection.
                // Keep the consumed fruit so retrying can never duplicate the drop.
                Log.Warning("[SCIEPort:Trees] Coconut drop could not be confirmed: " + error.Message);
                return false;
            }
        }

        public override void OnItemHarvested(int x, int y, int z, int old, ref BlockDropValue drop, ref int next)
        {
            if (CommonLib.WorkType == WorkType.Client || Terrain.ExtractContents(next) != 0) return;
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(old)];
            int data = Terrain.ExtractData(old);
            if (block is SpecialTreeLeaves leaf && leaf.Species.Fruit == Terrain.ExtractContents(drop.Value) &&
                (data & FruitStage.Owned) != 0 && FruitStage.Count(data) > 0)
                next = Terrain.ReplaceData(old, data & ~FruitStage.Mask);
        }

        public override void OnNeighborBlockChanged(int x, int y, int z, int nx, int ny, int nz)
        {
            if (CommonLib.WorkType == WorkType.Client || halted || y < 1 || ny != y - 1 || nx != x || nz != z) return;
            int value = TerrainData.GetCellContents(x, y, z);
            if (BlocksManager.Blocks[value] is SpecialTreeSapling seedling &&
                !seedling.Species.Supports(TerrainData.GetCellContents(x, y - 1, z)) && writes >= 2)
            {
                writes -= 2;
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
            }
        }
    }
}
