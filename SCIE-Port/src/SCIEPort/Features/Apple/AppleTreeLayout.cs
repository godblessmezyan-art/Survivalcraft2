using Game;

namespace SCIEPort.Features.Apple
{
    internal static class AppleTreeLayout
    {
        private static readonly TerrainBrush[] brushes = { MakeBrush(0), MakeBrush(1), MakeBrush(4), MakeBrush(15) };

        internal static TerrainBrush ForStage(int stage)
            => brushes[stage == 15 ? 3 : stage == 4 ? 2 : stage == 1 ? 1 : 0];

        private static TerrainBrush MakeBrush(int stage)
        {
            var brush = new TerrainBrush();
            brush.AddCell(0, 0, 0, Terrain.MakeBlockValue(ApplePlantBlock.Index, 0, AppleTreeRules.Root | 7));
            foreach (int[] part in AppleTreeRules.Parts())
            {
                int data = AppleTreeRules.Encode(part[3], part[0], part[1], part[2]);
                if (AppleTreeRules.BearsFruit(data)) data |= stage;
                brush.AddCell(part[0], part[1], part[2], Terrain.MakeBlockValue(ApplePlantBlock.Index, 0, data));
            }
            brush.Compile();
            return brush;
        }
    }
}
