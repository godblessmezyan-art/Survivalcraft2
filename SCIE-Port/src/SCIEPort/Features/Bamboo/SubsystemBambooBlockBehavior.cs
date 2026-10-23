#pragma warning disable CS1591

using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Bamboo
{
    public sealed class SubsystemBambooBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { BambooPlantBlock.Index };

        public override int[] HandledBlocks { get { return s_handledBlocks; } }

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            // Ordinary clients must not drive column growth that will not
            // synchronize back from the authoritative host.
            if (CommonLib.WorkType == WorkType.Client)
            {
                return;
            }

            int data = Terrain.ExtractData(value);

            // Only the planted base advances the column so a single culm has one
            // grower. Upper segments are inert with respect to growth.
            if (!BambooRules.IsBase(data) || y <= 0)
            {
                return;
            }

            Terrain terrain = SubsystemTerrain.Terrain;

            int belowValue = terrain.GetCellValueFast(x, y - 1, z);
            if (!HasValidSubstrate(Terrain.ExtractContents(belowValue)))
            {
                return;
            }

            // Count the contiguous column already standing above the base.
            int topY = y;
            int height = 1;
            while (height < BambooRules.MaxHeight + 1
                && Terrain.ExtractContents(terrain.GetCellValueFast(x, topY + 1, z)) == BambooPlantBlock.Index)
            {
                topY++;
                height++;
            }

            if (!BambooRules.CanExtend(height))
            {
                return;
            }

            int aboveValue = terrain.GetCellValueFast(x, topY + 1, z);
            if (Terrain.ExtractContents(aboveValue) != 0)
            {
                return;
            }

            if (Terrain.ExtractLight(terrain.GetCellValueFast(x, topY + 2, z)) < BambooRules.MinLightToGrow)
            {
                return;
            }

            bool hydrated = false;
            if (Terrain.ExtractContents(belowValue) == SoilBlock.Index)
            {
                hydrated = SoilBlock.GetHydration(Terrain.ExtractData(belowValue));
            }

            int temperature = terrain.GetSeasonalTemperature(x, z)
                + SubsystemWeather.GetTemperatureAdjustmentAtHeight(topY + 1);
            int interval = BambooRules.GetGrowthInterval(hydrated, temperature);

            if (pollPass % interval != 0)
            {
                return;
            }

            // Add a plain upper segment (base flag cleared) on top of the column.
            SubsystemTerrain.ChangeCell(
                x,
                topY + 1,
                z,
                Terrain.MakeBlockValue(BambooPlantBlock.Index, 0, 0),
                true,
                null);
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
            if (Terrain.ExtractContents(value) != BambooPlantBlock.Index || y <= 0)
            {
                return;
            }

            int below = Terrain.ExtractContents(terrain.GetCellValue(x, y - 1, z));
            if (BambooRules.IsBase(Terrain.ExtractData(value)))
            {
                if (!HasValidSubstrate(below))
                {
                    SubsystemTerrain.DestroyCell(0, x, y, z, 0, true, true, null);
                }
            }
            else if (below != BambooPlantBlock.Index)
            {
                // A detached upper segment loses its support and breaks, letting a
                // harvested or flooded column collapse segment by segment.
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, true, true, null);
            }
        }

        private static bool HasValidSubstrate(int contents)
        {
            return contents == SoilBlock.Index
                || contents == SandBlock.Index
                || contents == DirtBlock.Index;
        }
    }
}
