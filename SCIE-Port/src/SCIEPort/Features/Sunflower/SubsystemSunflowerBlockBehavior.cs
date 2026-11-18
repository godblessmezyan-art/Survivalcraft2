#pragma warning disable CS1591

using Game;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Sunflower
{
    public sealed class SubsystemSunflowerBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { SunflowerPlantBlock.Index };

        public override int[] HandledBlocks => s_handledBlocks;

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
            => AnnualCropGrowth.Poll(SubsystemTerrain, SunflowerPlantBlock.Index, x, y, z, pollPass, SunflowerRules.GetSunflowerGrowthInterval);

        public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
            => AnnualCropGrowth.CheckSupport(SubsystemTerrain, SunflowerPlantBlock.Index, x, y, z);
    }
}
