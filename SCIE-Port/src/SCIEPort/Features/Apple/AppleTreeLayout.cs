using Game;

namespace SCIEPort.Features.Apple
{
    internal static class AppleTreeLayout
    {
        private static readonly TerrainBrush[] brushes = { MakeBrush(0), MakeBrush(1), MakeBrush(4), MakeBrush(15) };

        internal static TerrainBrush ForStage(int stage)
            => brushes[stage == 15 ? 3 : stage == 4 ? 2 : stage == 1 ? 1 : 0];

        internal static bool IsRegistered => BlocksManager.Blocks[AppleWoodBlock.Index] is AppleWoodBlock &&
            BlocksManager.Blocks[AppleLeavesBlock.Index] is AppleLeavesBlock &&
            BlocksManager.Blocks[AppleFruitingLeavesBlock.Index] is AppleFruitingLeavesBlock;

        internal static int LeafValue(int data)
            => Terrain.MakeBlockValue(AppleRules.GetFruiting(data) ? AppleFruitingLeavesBlock.Index : AppleLeavesBlock.Index, 0, data);

        private static TerrainBrush MakeBrush(int stage)
        {
            var brush = new TerrainBrush();
            brush.AddCell(0, 0, 0, Terrain.MakeBlockValue(AppleWoodBlock.Index, 0, AppleWoodBlock.GrownRoot));
            foreach (int[] part in AppleTreeRules.Parts())
            {
                int data = AppleTreeRules.Encode(part[3], part[0], part[1], part[2]);
                if (AppleTreeRules.BearsFruit(data)) data |= stage;
                int value = part[3] == AppleTreeRules.Trunk ? Terrain.MakeBlockValue(AppleWoodBlock.Index) : LeafValue(data);
                brush.AddCell(part[0], part[1], part[2], value);
            }
            brush.Compile();
            return brush;
        }
    }
}
