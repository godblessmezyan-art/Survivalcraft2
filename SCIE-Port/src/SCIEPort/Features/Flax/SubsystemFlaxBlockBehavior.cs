#pragma warning disable CS1591

using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Flax
{
    public sealed class SubsystemFlaxBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { FlaxPlantBlock.Index };

        public override int[] HandledBlocks { get { return s_handledBlocks; } }

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            // Ordinary clients must not drive flax growth that will not
            // synchronize back from the authoritative host.
            if (CommonLib.WorkType == WorkType.Client)
            {
                return;
            }

            Terrain terrain = SubsystemTerrain.Terrain;
            if (Terrain.ExtractLight(terrain.GetCellValueFast(x, y + 1, z)) < FlaxRules.MinLightToGrow)
            {
                return;
            }

            int data = Terrain.ExtractData(value);
            int size = FlaxRules.GetSize(data);
            if (size >= FlaxRules.MaxFlaxSize)
            {
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
            int interval = FlaxRules.GetFlaxGrowthInterval(
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
                Terrain.ReplaceData(value, FlaxRules.SetSize(data, size + 1)),
                true,
                null);

            if (size + 1 == FlaxRules.MaxFlaxSize)
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
            Terrain terrain = SubsystemTerrain.Terrain;
            int value = terrain.GetCellValue(x, y, z);
            if (Terrain.ExtractContents(value) != FlaxPlantBlock.Index || y <= 0)
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
