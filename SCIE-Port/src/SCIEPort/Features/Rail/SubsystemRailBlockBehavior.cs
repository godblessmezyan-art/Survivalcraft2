using Engine;
using Game;

namespace SCIEPort.Features.Rail
{
    /// <summary>Recomputes adjacent SCIE rail shapes after placement.</summary>
    public sealed class SubsystemRailBlockBehavior : SubsystemBlockBehavior
    {
        private static readonly Point3[,] Neighbors =
        {
            { new Point3(0, 0, -1), new Point3(0, 1, -1), new Point3(0, -1, -1) },
            { new Point3(-1, 0, 0), new Point3(-1, 1, 0), new Point3(-1, -1, 0) },
            { new Point3(0, 0, 1), new Point3(0, 1, 1), new Point3(0, -1, 1) },
            { new Point3(1, 0, 0), new Point3(1, 1, 0), new Point3(1, -1, 0) }
        };

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { RailBlock.Index };

        /// <inheritdoc />
        public override void OnBlockAdded(int value, int oldValue, int x, int y, int z) => UpdateShape(value, x, y, z, 1);

        private void UpdateShape(int value, int x, int y, int z, int depth)
        {
            bool[] same = new bool[4];
            bool[] raised = new bool[4];
            for (int direction = 0; direction < 4; direction++)
            {
                for (int level = 0; level < 3; level++)
                {
                    Point3 offset = Neighbors[direction, level];
                    int neighbor = SubsystemTerrain.Terrain.GetCellValueFastChunkExists(x + offset.X, y + offset.Y, z + offset.Z);
                    if (Terrain.ExtractContents(neighbor) != RailBlock.Index) continue;
                    bool connects = RailBlock.CanConnectTo(RailBlock.GetRailType(Terrain.ExtractData(neighbor)), direction);
                    if (level == 1) raised[direction] = connects; else same[direction] = connects;
                    if (depth > 0) UpdateShape(neighbor, x + offset.X, y + offset.Y, z + offset.Z, depth - 1);
                    break;
                }
            }

            int? result = null;
            if (same[0] && same[3]) result = 1;
            else if (same[0] && same[1]) result = 2;
            else if (same[1] && same[2]) result = 3;
            else if (same[2] && same[3]) result = 0;
            else if (raised[2]) result = 6;
            else if (raised[0]) result = 8;
            else if (raised[3]) result = 7;
            else if (raised[1]) result = 9;
            else if (same[0] || same[2]) result = 4;
            else if (same[1] || same[3]) result = 5;

            if (result.HasValue && RailBlock.GetRailType(Terrain.ExtractData(value)) != result.Value)
            {
                SubsystemTerrain.ChangeCell(x, y, z, Terrain.MakeBlockValue(RailBlock.Index, 0, RailBlock.SetRailType(0, result.Value)), true, null);
            }
        }
    }
}
