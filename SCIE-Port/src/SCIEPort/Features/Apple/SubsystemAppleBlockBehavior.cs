#pragma warning disable CS1591

using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Apple
{
    public sealed class SubsystemAppleBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { ApplePlantBlock.Index };

        private SubsystemSeasons m_seasons;

        public override int[] HandledBlocks { get { return s_handledBlocks; } }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_seasons = Project.FindSubsystem<SubsystemSeasons>(false);
        }

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            // Ordinary clients must not drive apple growth or fruiting that will not
            // synchronize back from the authoritative host.
            if (CommonLib.WorkType == WorkType.Client)
            {
                return;
            }

            Terrain terrain = SubsystemTerrain.Terrain;
            if (Terrain.ExtractLight(terrain.GetCellValueFast(x, y + 1, z)) < AppleRules.MinLightToGrow)
            {
                return;
            }

            int data = Terrain.ExtractData(value);
            int size = AppleRules.GetSize(data);

            // A mature plant no longer grows; instead its ripe-fruit bit follows the
            // season, mirroring the audited WildBerry season-driven fruiting but gated
            // on full maturity.
            if (size >= AppleRules.MaxAppleSize)
            {
                bool fruitSeason = m_seasons == null || m_seasons.Season == Season.Autumn;
                bool desired = AppleRules.ShouldBearFruit(true, fruitSeason);
                if (AppleRules.GetFruiting(data) != desired)
                {
                    SubsystemTerrain.ChangeCell(
                        x,
                        y,
                        z,
                        Terrain.ReplaceData(value, AppleRules.SetFruiting(data, desired)),
                        true,
                        null);
                }
                return;
            }

            int soilValue = terrain.GetCellValueFast(x, y - 1, z);
            if (Terrain.ExtractContents(soilValue) != SoilBlock.Index)
            {
                return;
            }

            int soilData = Terrain.ExtractData(soilValue);

            int temperature = terrain.GetSeasonalTemperature(x, z)
                + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
            int interval = AppleRules.GetAppleGrowthInterval(
                SoilBlock.GetHydration(soilData),
                SoilBlock.GetNitrogen(soilData),
                temperature);

            if (pollPass % interval != 0)
            {
                return;
            }

            SubsystemTerrain.ChangeCell(
                x,
                y,
                z,
                Terrain.ReplaceData(value, AppleRules.SetSize(data, size + 1)),
                true,
                null);

            if (size + 1 == AppleRules.MaxAppleSize)
            {
                int nitrogen = MathUtils.Max(SoilBlock.GetNitrogen(soilData) - 1, 0);
                SubsystemTerrain.ChangeCell(
                    x,
                    y - 1,
                    z,
                    Terrain.ReplaceData(soilValue, SoilBlock.SetNitrogen(soilData, nitrogen)),
                    true,
                    null);
            }
        }

        public override void OnNeighborBlockChanged(
            int x,
            int y,
            int z,
            int neighborX,
            int neighborY,
            int neighborZ)
        {
            // Mirror the authoritative-only rule already applied in OnPoll: a normal
            // client must not issue an independent terrain destroy that could desync
            // from the authoritative host's own neighbor-change reaction.
            if (CommonLib.WorkType == WorkType.Client)
            {
                return;
            }

            Terrain terrain = SubsystemTerrain.Terrain;
            int value = terrain.GetCellValue(x, y, z);
            if (Terrain.ExtractContents(value) != ApplePlantBlock.Index || y <= 0)
            {
                return;
            }
            if (Terrain.ExtractContents(terrain.GetCellValue(x, y - 1, z)) != SoilBlock.Index)
            {
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, true, true, null);
            }
        }
    }
}
