using Engine;

namespace SCIEPort.Features.Plants
{
    // Native BlocksScanner calls growth on the game update thread. Share a cap across annuals and berries.
    internal static class PlantGrowthBudget
    {
        private static int frame=-1;
        private static int remaining;
        internal static bool Take(int writes)
        {
            if(frame!=Time.FrameIndex){frame=Time.FrameIndex;remaining=64;}
            if(writes<1||writes>remaining) return false;
            remaining-=writes;return true;
        }
    }
}
