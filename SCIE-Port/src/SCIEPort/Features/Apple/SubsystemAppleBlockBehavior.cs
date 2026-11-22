#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using SCIEPort.Features.Plants;
using TemplatesDatabase;

namespace SCIEPort.Features.Apple
{
    public sealed class SubsystemAppleBlockBehavior : SubsystemPollableBlockBehavior, IUpdateable
    {
        private static readonly int[] s_handledBlocks =
        {
            ApplePlantBlock.Index, AppleBlock.Index, AppleWoodBlock.Index,
            AppleLeavesBlock.Index, AppleFruitingLeavesBlock.Index
        };

        private SubsystemSeasons m_seasons;
        private SubsystemGameInfo m_gameInfo;
        private int m_budget = AppleTreeRules.FrameWrites;
        private bool m_faulted;
        private bool m_applying;

        public override int[] HandledBlocks => s_handledBlocks;
        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        public void Update(float dt) => m_budget = AppleTreeRules.FrameWrites;

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_seasons = Project.FindSubsystem<SubsystemSeasons>(false);
            m_gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
            if (SubsystemTerrain.TerrainContentsGenerator != null &&
                !(SubsystemTerrain.TerrainContentsGenerator is PlantTerrainGenerator))
            {
                SubsystemTerrain.TerrainContentsGenerator =
                    new PlantTerrainGenerator(SubsystemTerrain.TerrainContentsGenerator, m_gameInfo);
            }
        }

        private int SeasonCode
        {
            get
            {
                if (m_seasons == null || m_gameInfo != null && !m_gameInfo.WorldSettings.AreSeasonsChanging)
                    return -1;
                switch (m_seasons.Season)
                {
                    case Season.Winter: return 0;
                    case Season.Spring: return 1;
                    case Season.Summer: return 2;
                    default: return 3;
                }
            }
        }

        internal static bool IsGround(int id) => PlantHabitat.IsRootingSoil(id);

        private Terrain TerrainData => SubsystemTerrain.Terrain;

        private int Read(Point3 p) => Terrain.ReplaceLight(TerrainData.GetCellValue(p.X, p.Y, p.Z), 0);

        private bool Loaded(Point3 p)
        {
            TerrainChunk chunk = TerrainData.GetChunkAtCell(p.X, p.Z);
            return p.Y >= 0 && p.Y <= 255 && chunk != null && chunk.State > TerrainChunkState.InvalidContents4;
        }

        private bool Apply(List<Point3> cells, List<int> after)
        {
            if (m_faulted || m_applying || CommonLib.WorkType == WorkType.Client ||
                cells.Count == 0 || cells.Count > AppleTreeRules.MaxChanges || m_budget < cells.Count * 2)
                return false;

            var before = new int[cells.Count];
            int[] normalizedAfter = after.ToArray();
            for (int i = 0; i < before.Length; i++)
            {
                if (!Loaded(cells[i])) return false;
                before[i] = Read(cells[i]);
                normalizedAfter[i] = Terrain.ReplaceLight(normalizedAfter[i], 0);
            }

            m_budget -= cells.Count * 2;
            bool undoFailed;
            Exception error;
            bool applied;
            m_applying = true;
            try
            {
                applied = AppleTreeTransaction.Apply(before, normalizedAfter, i => Read(cells[i]), (i, value) =>
                    SubsystemTerrain.ChangeCell(cells[i].X, cells[i].Y, cells[i].Z, value, true, null), out undoFailed, out error);
            }
            finally
            {
                m_applying = false;
            }

            if (undoFailed)
            {
                m_faulted = true;
                Log.Error("[SCIEPort:Apple] Growth halted after undo failure: " + error);
            }
            else if (error != null)
                Log.Warning("[SCIEPort:Apple] Growth rolled back: " + error.Message);
            return applied;
        }

        private void Change(Point3 p, int value) => Apply(new List<Point3> { p }, new List<int> { value });

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            if (CommonLib.WorkType == WorkType.Client || m_faulted || m_applying ||
                !AppleTreeRules.InBounds(x, y, z) || !AppleTreeLayout.IsRegistered) return;

            var p = new Point3(x, y, z);
            if (!Loaded(p)) return;
            value = Read(p);
            int data = Terrain.ExtractData(value);
            switch (Terrain.ExtractContents(value))
            {
                case ApplePlantBlock.Index:
                    if (AppleTreeRules.Part(data) == 0) PollSapling(p, data, pollPass);
                    else MigrateLegacyPart(p, data);
                    break;
                case AppleWoodBlock.Index:
                    if ((data & AppleWoodBlock.GrownRoot) != 0 && PlantGrowthSite.IsLiving(SubsystemTerrain) &&
                        (SeasonCode == 1 || SeasonCode < 0 && pollPass % 8 == 0))
                        RestoreCanopy(p);
                    break;
                case AppleLeavesBlock.Index:
                case AppleFruitingLeavesBlock.Index:
                    PollCanopy(p, value, data, pollPass);
                    break;
            }
        }

        private void MigrateLegacyPart(Point3 p, int data)
        {
            switch (AppleTreeRules.Part(data))
            {
                case AppleTreeRules.Root:
                    Change(p, Terrain.MakeBlockValue(AppleWoodBlock.Index, 0, AppleWoodBlock.GrownRoot));
                    break;
                case AppleTreeRules.Trunk:
                    Change(p, Terrain.MakeBlockValue(AppleWoodBlock.Index));
                    break;
                case AppleTreeRules.Leaf:
                    if (TryGetRoot(p, data, out Point3 root) && Loaded(root))
                        Change(p, HasTrunk(root) ? AppleTreeLayout.LeafValue(data) : 0);
                    break;
            }
        }

        private static bool TryGetRoot(Point3 p, int data, out Point3 root)
        {
            int dx = AppleTreeRules.OffsetX(data);
            int dz = AppleTreeRules.OffsetZ(data);
            int height = AppleTreeRules.OffsetY(data);
            root = default(Point3);
            if (Math.Abs(dx) > 1 || Math.Abs(dz) > 1 || height < 2 || height > 4) return false;
            root = new Point3(p.X - dx, p.Y - height, p.Z - dz);
            return AppleTreeRules.InBounds(root.X, root.Y, root.Z);
        }

        private bool HasTrunk(Point3 root)
        {
            int value = Read(root);
            int content = Terrain.ExtractContents(value);
            int data = Terrain.ExtractData(value);
            bool grownRoot = content == AppleWoodBlock.Index && (data & AppleWoodBlock.GrownRoot) != 0 ||
                content == ApplePlantBlock.Index && AppleTreeRules.Part(data) == AppleTreeRules.Root;
            if (!grownRoot) return false;

            for (int h = 1; h <= 2; h++)
            {
                value = Read(new Point3(root.X, root.Y + h, root.Z));
                content = Terrain.ExtractContents(value);
                data = Terrain.ExtractData(value);
                if (content == AppleWoodBlock.Index && WoodBlock.GetCutFace(data) == 4 ||
                    content == ApplePlantBlock.Index && data == AppleTreeRules.Encode(AppleTreeRules.Trunk, 0, h, 0))
                    continue;

                return false;
            }

            return true;
        }

        private void PollCanopy(Point3 p, int value, int data, int pollPass)
        {
            // Placement clears ownership. Loose leaves use the native wood/leaf decay.
            if (AppleTreeRules.Part(data) != AppleTreeRules.Leaf ||
                !PlantGrowthSite.IsLiving(SubsystemTerrain)) return;
            bool fruitBranch = AppleTreeRules.BearsFruit(data);
            if (!fruitBranch && (data & 15) == 0) return;
            if (!TryGetRoot(p, data, out Point3 root) || !Loaded(root)) return;
            if (!HasTrunk(root))
            {
                // A detached canopy cannot produce fruit. Native decay removes it
                // once the last supporting nearby wood is gone.
                Change(p, Terrain.MakeBlockValue(AppleLeavesBlock.Index));
                return;
            }

            int season = SeasonCode;
            int next;
            if (fruitBranch)
            {
                int light = TerrainData.GetCellLight(p.X, p.Y + 1, p.Z);
                var outside = new Point3(p.X + AppleTreeRules.OffsetX(data), p.Y, p.Z + AppleTreeRules.OffsetZ(data));
                if (Loaded(outside))
                    light = Math.Max(light, TerrainData.GetCellLight(outside.X, outside.Y, outside.Z));

                int soil = TerrainData.GetCellValue(root.X, root.Y - 1, root.Z);
                bool watered = Terrain.ExtractContents(soil) == SoilBlock.Index && SoilBlock.GetHydration(Terrain.ExtractData(soil));
                int temperature = PlantHabitat.TemperatureAt(TerrainData, p.X, p.Y, p.Z);
                bool suitable = PlantHabitat.For(ApplePlantBlock.Index)
                    .AllowsGrowth(temperature, TerrainData.GetSeasonalHumidity(p.X, p.Z), light, watered);
                next = AppleTreeRules.NextFruit(data, season, suitable, pollPass % 4 == 0);
            }
            else
            {
                // Older trees may still have ripe fruit in an upper crown cell.
                next = season == 0 || season == 1 || !AppleRules.GetFruiting(data) ? data & ~15 : data;
            }

            int replacement = AppleTreeLayout.LeafValue(next);
            if (replacement != value) Change(p, replacement);
        }

        private void PollSapling(Point3 p, int data, int pollPass)
        {
            int soil = Read(new Point3(p.X, p.Y - 1, p.Z));
            int content = Terrain.ExtractContents(soil);
            if (!IsGround(content))
            {
                Change(p, 0);
                return;
            }
            if (!AppleTreeRules.CanFitTree(p.X, p.Y, p.Z) || !PlantGrowthSite.IsLiving(SubsystemTerrain)) return;

            int light = TerrainData.GetCellLight(p.X, p.Y + 1, p.Z);
            int temperature = PlantHabitat.TemperatureAt(TerrainData, p.X, p.Y, p.Z);
            int soilData = Terrain.ExtractData(soil);
            bool hydrated = content == SoilBlock.Index && SoilBlock.GetHydration(soilData);
            if (!AppleTreeRules.CanGrow(light, temperature, SeasonCode == 0) ||
                !PlantHabitat.For(ApplePlantBlock.Index).AllowsGrowth(
                    temperature, TerrainData.GetSeasonalHumidity(p.X, p.Z), light, hydrated)) return;

            int nitrogen = content == SoilBlock.Index ? SoilBlock.GetNitrogen(soilData) : 0;
            if (pollPass % AppleRules.GetAppleGrowthInterval(hydrated, nitrogen, temperature) != 0) return;
            int stage = AppleRules.GetSize(data);
            if (stage < 6)
            {
                int next = AppleRules.SetFruiting(AppleRules.SetSize(data, stage + 1), false);
                Change(p, Terrain.MakeBlockValue(ApplePlantBlock.Index, 0, next));
                return;
            }

            GrowTree(p, soil, nitrogen);
        }

        private void GrowTree(Point3 root, int soil, int nitrogen)
        {
            var cells = new List<Point3>();
            var values = new List<int>();
            foreach (TerrainBrush.Cell part in AppleTreeLayout.ForStage(SeasonCode == 1 ? 1 : 0).Cells)
            {
                var p = new Point3(root.X + part.X, root.Y + part.Y, root.Z + part.Z);
                if (!Loaded(p) || (part.Y != 0 && Read(p) != 0)) return;
                cells.Add(p);
                values.Add(part.Value);
            }

            if (nitrogen > 0)
            {
                cells.Add(new Point3(root.X, root.Y - 1, root.Z));
                values.Add(Terrain.ReplaceData(soil, SoilBlock.SetNitrogen(Terrain.ExtractData(soil), nitrogen - 1)));
            }
            Apply(cells, values);
        }

        public override void OnNeighborBlockChanged(int x, int y, int z, int nx, int ny, int nz)
        {
            if (CommonLib.WorkType == WorkType.Client || m_faulted || m_applying ||
                !AppleTreeRules.InBounds(x, y, z) || ny != y - 1) return;
            int value = TerrainData.GetCellValue(x, y, z);
            if (Terrain.ExtractContents(value) == ApplePlantBlock.Index && AppleTreeRules.Part(Terrain.ExtractData(value)) == 0 &&
                !IsGround(TerrainData.GetCellContents(x, y - 1, z)))
                Change(new Point3(x, y, z), 0);
        }

        private void RestoreCanopy(Point3 root)
        {
            if (!AppleTreeRules.CanFitTree(root.X, root.Y, root.Z) ||
                !IsGround(TerrainData.GetCellContents(root.X, root.Y - 1, root.Z)) || !HasTrunk(root)) return;

            var cells = new List<Point3>();
            var values = new List<int>();
            foreach (TerrainBrush.Cell part in AppleTreeLayout.ForStage(SeasonCode == 1 ? 1 : 0).Cells)
            {
                if (Terrain.ExtractContents(part.Value) != AppleLeavesBlock.Index) continue;
                var p = new Point3(root.X + part.X, root.Y + part.Y, root.Z + part.Z);
                if (!Loaded(p) || Read(p) != 0) continue;
                cells.Add(p);
                values.Add(part.Value);
            }
            Apply(cells, values);
        }

        public override void OnItemHarvested(int x, int y, int z, int value, ref BlockDropValue dropValue, ref int newValue)
        {
            if (m_applying || CommonLib.WorkType == WorkType.Client || !AppleTreeLayout.IsRegistered ||
                !AppleTreeRules.InBounds(x, y, z) || Terrain.ExtractContents(newValue) != 0 ||
                Terrain.ExtractContents(dropValue.Value) != AppleBlock.Index) return;

            int data = Terrain.ExtractData(value);
            int content = Terrain.ExtractContents(value);
            bool owned = AppleTreeRules.Part(data) == AppleTreeRules.Leaf;
            if (content != AppleFruitingLeavesBlock.Index &&
                !(content == ApplePlantBlock.Index && owned && AppleRules.GetFruiting(data))) return;

            // The native harvest callback changes the final terrain value in the same drop operation.
            int next = owned ? (data & ~15) | AppleTreeRules.Harvested : 0;
            newValue = Terrain.MakeBlockValue(AppleLeavesBlock.Index, 0, next);
        }
    }
}
