#pragma warning disable CS1591

using Engine;
using Game;
using SCIEPort.Features.Plants;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.KellyPlants
{
    public sealed class SubsystemSoybeanBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { SoybeanBlock.Index };

        public override int[] HandledBlocks { get { return s_handledBlocks; } }

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
            => AnnualCropGrowth.Poll(SubsystemTerrain, SoybeanBlock.Index, x, y, z, pollPass, KellyPlantRules.GetSoybeanGrowthInterval);

        public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
            => AnnualCropGrowth.CheckSupport(SubsystemTerrain, SoybeanBlock.Index, x, y, z);
    }

    public sealed class SubsystemWildBerryBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { WildBerryPlantBlock.Index,CloudberryPlantBlock.Index,WildBerryBlock.Index,CloudberryBlock.Index };
        private SubsystemSeasons m_seasons;
        private SubsystemGameInfo m_gameInfo;

        public override int[] HandledBlocks { get { return s_handledBlocks; } }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_seasons = Project.FindSubsystem<SubsystemSeasons>(false);
            m_gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
        }

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            if(CommonLib.WorkType==WorkType.Client||y<=0||y>=255) return;
            Terrain terrain=SubsystemTerrain.Terrain;
            value=terrain.GetCellValue(x,y,z);
            int id=Terrain.ExtractContents(value);
            if(id!=WildBerryPlantBlock.Index&&id!=CloudberryPlantBlock.Index) return;
            bool cloud=id==CloudberryPlantBlock.Index;
            int soil = terrain.GetCellValue(x, y - 1, z);
            if (!PlantHabitat.IsRootingSoil(Terrain.ExtractContents(soil)))
            {
                if (PlantGrowthBudget.Take(1)) SubsystemTerrain.DestroyCell(0, x, y, z, 0, true, true, null);
                return;
            }
            if (!PlantGrowthSite.IsLiving(SubsystemTerrain)) return;
            bool fruiting = m_seasons == null || !m_gameInfo.WorldSettings.AreSeasonsChanging ||
                m_seasons.Season == Season.Summer || !cloud && m_seasons.Season == Season.Spring;
            int data = Terrain.ExtractData(value);
            bool hydrated = Terrain.ExtractContents(soil) == SoilBlock.Index && SoilBlock.GetHydration(Terrain.ExtractData(soil));
            bool suitable = PlantHabitat.For(id).AllowsGrowth(PlantHabitat.TemperatureAt(terrain, x, y, z),
                terrain.GetSeasonalHumidity(x, z), terrain.GetCellLight(x, y + 1, z), hydrated);
            int next=KellyPlantRules.NextBerry(data,cloud,fruiting,suitable,pollPass%(cloud ? 4 : 3)==0);
            if (next != data && PlantGrowthBudget.Take(1))
            {
                SubsystemTerrain.ChangeCell(
                    x,
                    y,
                    z,
                    Terrain.ReplaceData(value, next),
                    true,
                    null);
            }
        }

        public override void OnItemHarvested(int x,int y,int z,int oldValue,ref BlockDropValue dropValue,ref int newValue)
        {
            if(CommonLib.WorkType==WorkType.Client||Terrain.ExtractContents(newValue)!=0) return;
            int id=Terrain.ExtractContents(oldValue),fruit=Terrain.ExtractContents(dropValue.Value),data=Terrain.ExtractData(oldValue);
            if(id==WildBerryPlantBlock.Index&&fruit==WildBerryBlock.Index&&(data&1)!=0)
                newValue=Terrain.ReplaceData(oldValue,data&~15);
            else if(id==CloudberryPlantBlock.Index&&fruit==CloudberryBlock.Index&&(data&3)==0)
                newValue=Terrain.ReplaceData(oldValue,(data&~3)|1);
        }
    }

    public sealed class SubsystemKellyFruitBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks =
            { OakLeavesBlock.Index, CactusFruitBlock.Index, CattailBlock.Index, CattailShootBlock.Index };
        private readonly Game.Random m_random = new Game.Random();
        private SubsystemPickables m_pickables;
        private SubsystemSeasons m_seasons;

        public override int[] HandledBlocks { get { return s_handledBlocks; } }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_pickables = Project.FindSubsystem<SubsystemPickables>(true);
            m_seasons = Project.FindSubsystem<SubsystemSeasons>(false);
        }

        public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
        {
            if (CommonLib.WorkType == WorkType.Client || Terrain.ExtractContents(value) != OakLeavesBlock.Index ||
                Terrain.ExtractContents(newValue) == OakLeavesBlock.Index)
            {
                return;
            }

            float chance = m_seasons != null && m_seasons.Season == Season.Autumn ? 0.15f : 0.04f;
            if (m_random.Float(0f, 1f) <= chance)
            {
                m_pickables.AddPickable(
                    Terrain.MakeBlockValue(AcornBlock.Index),
                    1,
                    new Vector3(x + 0.5f, y + 0.3f, z + 0.5f),
                    Vector3.Zero,
                    null);
            }
        }

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            if (CommonLib.WorkType == WorkType.Client || y < 2 || y >= 255) return;
            Terrain terrain = SubsystemTerrain.Terrain;
            value = terrain.GetCellValue(x, y, z);
            int id = Terrain.ExtractContents(value);
            if (id != CattailBlock.Index && id != CactusFruitBlock.Index) return;

            bool cattail = id == CattailBlock.Index;
            if (!CheckSupport(id, x, y, z)) return;

            int data = Terrain.ExtractData(value);
            if (!PlantGrowthSite.IsLiving(SubsystemTerrain) || (data & 3) == 0 || pollPass % (cattail ? 3 : 5) != 0) return;
            if (cattail && !PlantGrowthSite.HasWater(x, y, z, terrain.GetCellContents)) return;
            bool suitable = PlantHabitat.For(id).AllowsGrowth(PlantHabitat.TemperatureAt(terrain, x, y, z),
                terrain.GetSeasonalHumidity(x, z), terrain.GetCellLight(x, y + 1, z), cattail);
            if (suitable && PlantGrowthBudget.Take(1))
                SubsystemTerrain.ChangeCell(x, y, z, Terrain.ReplaceData(value, (data & ~3) | ((data + 1) & 3)), true, null);
        }

        public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
        {
            if (CommonLib.WorkType == WorkType.Client || y < 2 || y >= 255 || neighborY != y - 1) return;
            int id = SubsystemTerrain.Terrain.GetCellContents(x, y, z);
            if (id == CattailBlock.Index || id == CactusFruitBlock.Index) CheckSupport(id, x, y, z);
        }

        private bool CheckSupport(int plant, int x, int y, int z)
        {
            if (HasSupport(plant, SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z))) return true;
            if (PlantGrowthBudget.Take(1)) SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, true, null);
            return false;
        }

        internal static bool HasSupport(int plant, int ground)
            => plant == CattailBlock.Index ? PlantHabitat.IsRootingSoil(ground) || ground == SandBlock.Index : ground == CactusBlock.Index;

        public override void OnItemHarvested(int x, int y, int z, int oldValue, ref BlockDropValue drop, ref int newValue)
        {
            if (CommonLib.WorkType == WorkType.Client || y < 2 || Terrain.ExtractContents(newValue) != 0) return;
            int plant = Terrain.ExtractContents(oldValue);
            int fruit = Terrain.ExtractContents(drop.Value);
            if ((plant == CattailBlock.Index && fruit == CattailShootBlock.Index ||
                plant == CactusFruitBlock.Index && fruit == CactusFruitBlock.Index) &&
                HasSupport(plant, SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z)))
            {
                newValue = Terrain.ReplaceData(oldValue, (Terrain.ExtractData(oldValue) & ~3) | 1);
            }
        }
    }

    public sealed class SubsystemKellyMushroomBlockBehavior : SubsystemPollableBlockBehavior
    {
        internal const int Established = 32;
        internal const int StageMask = 3 << 6;
        internal const int Immature = 1 << 6;
        private const int DryAgeMask = 31;
        private const int MaxDryAge = 30;
        private static readonly int[] s_handledBlocks =
            { MushroomPatchBlock.Index, FlyAgaricPatchBlock.Index, WildMushroomBlock.Index, FlyAgaricBlock.Index };

        private SubsystemWeather m_weather;
        public override int[] HandledBlocks => s_handledBlocks;

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_weather = Project.FindSubsystem<SubsystemWeather>(true);
        }

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            if (CommonLib.WorkType == WorkType.Client || y <= 0 || y >= 255) return;
            Terrain terrain = SubsystemTerrain.Terrain;
            value = terrain.GetCellValue(x, y, z);
            int id = Terrain.ExtractContents(value);
            if (id != MushroomPatchBlock.Index && id != FlyAgaricPatchBlock.Index) return;
            int ground = terrain.GetCellContents(x, y - 1, z);
            if (!PlantHabitat.IsRootingSoil(ground))
            {
                if (PlantGrowthBudget.Take(1)) SubsystemTerrain.DestroyCell(0, x, y, z, 0, true, true, null);
                return;
            }

            int data = Terrain.ExtractData(value);
            if (!PlantGrowthSite.IsLiving(SubsystemTerrain)) return;
            if ((data & Established) != 0)
            {
                int stage = (data & StageMask) >> 6;
                if (stage == 0 || pollPass % (id == MushroomPatchBlock.Index ? 3 : 5) != 0) return;
                bool wet = ground == SoilBlock.Index && SoilBlock.GetHydration(Terrain.ExtractData(terrain.GetCellValue(x, y - 1, z)));
                if (PlantHabitat.For(id).AllowsGrowth(PlantHabitat.TemperatureAt(terrain, x, y, z),
                    terrain.GetSeasonalHumidity(x, z), terrain.GetCellLight(x, y + 1, z), wet) && PlantGrowthBudget.Take(1))
                {
                    int next = (data & ~StageMask) | (((stage + 1) & 3) << 6);
                    SubsystemTerrain.ChangeCell(x, y, z, Terrain.ReplaceData(value, next), true, null);
                }
                return;
            }

            // Old temporary rain patches retain their original drying rule.
            int age = data & DryAgeMask;
            int nextAge = m_weather.GetPrecipitationShaftInfo(x, z).Intensity > .05f ? 0 : age + 1;
            if (nextAge == age || !PlantGrowthBudget.Take(1)) return;
            if (nextAge >= MaxDryAge)
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, true, true, null);
            else
                SubsystemTerrain.ChangeCell(x, y, z, Terrain.ReplaceData(value, (data & ~DryAgeMask) | nextAge), true, null);
        }

        public override void OnItemHarvested(int x, int y, int z, int oldValue, ref BlockDropValue drop, ref int newValue)
        {
            if (CommonLib.WorkType == WorkType.Client || y <= 0 || Terrain.ExtractContents(newValue) != 0) return;
            int id = Terrain.ExtractContents(oldValue);
            if (id != MushroomPatchBlock.Index && id != FlyAgaricPatchBlock.Index) return;
            int fruit = id == MushroomPatchBlock.Index ? WildMushroomBlock.Index : FlyAgaricBlock.Index;
            if (Terrain.ExtractContents(drop.Value) != fruit) return;
            int data = Terrain.ExtractData(oldValue);
            if ((data & Established) != 0 && (data & StageMask) == 0 &&
                PlantHabitat.IsRootingSoil(SubsystemTerrain.Terrain.GetCellContents(x, y - 1, z)))
            {
                newValue = Terrain.ReplaceData(oldValue, data | Immature);
            }
        }
    }
}
