#pragma warning disable CS1591

using Game;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Flax
{
    public sealed class SubsystemFlaxBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { FlaxPlantBlock.Index };

        public override int[] HandledBlocks => s_handledBlocks;

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
            => AnnualCropGrowth.Poll(SubsystemTerrain, FlaxPlantBlock.Index, x, y, z, pollPass, FlaxRules.GetFlaxGrowthInterval);

        public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
            => AnnualCropGrowth.CheckSupport(SubsystemTerrain, FlaxPlantBlock.Index, x, y, z);
    }
}
