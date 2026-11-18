#pragma warning disable CS1591

using Game;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Crops
{
    public sealed class SubsystemCornBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { CornStalkBlock.Index };

        public override int[] HandledBlocks => s_handledBlocks;

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
            => AnnualCropGrowth.Poll(SubsystemTerrain, CornStalkBlock.Index, x, y, z, pollPass, CropRules.GetCornGrowthInterval);

        public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
            => AnnualCropGrowth.CheckSupport(SubsystemTerrain, CornStalkBlock.Index, x, y, z);
    }
}
