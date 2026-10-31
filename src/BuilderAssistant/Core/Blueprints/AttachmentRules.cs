namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>Exact CellFace vectors from the target assembly, never a guessed compass convention.</summary>
    internal static class AttachmentRules
    {
        public static bool RequiresSupport(BlockFamily family)
        {
            return family == BlockFamily.Ladder || family == BlockFamily.Torch || family == BlockFamily.Falling ||
                family == BlockFamily.Plant || family == BlockFamily.Snow || family == BlockFamily.Carpet ||
                family == BlockFamily.GroundCover || family == BlockFamily.Gravestone || family == BlockFamily.Lantern || family == BlockFamily.Cairn;
        }
        public static bool TryGetSupport(CellPos position, BlockFamily family, int data, out CellPos support, out int face)
        {
            support = position;
            face = data;
            if (!RequiresSupport(family)) return false;
            // Native TryCollapseColumn returns immediately at world floor Y=0.
            if (family == BlockFamily.Falling && position.Y == 0) return false;
            if (family != BlockFamily.Ladder && family != BlockFamily.Torch)
            { support = position.Add(new CellPos(0,-1,0)); face = 4; return true; }
            switch (face)
            {
                case 0: support = position.Add(new CellPos(0, 0, -1)); break;
                case 1: support = position.Add(new CellPos(-1, 0, 0)); break;
                case 2: support = position.Add(new CellPos(0, 0, 1)); break;
                case 3: support = position.Add(new CellPos(1, 0, 0)); break;
                case 4: support = position.Add(new CellPos(0, -1, 0)); break;
                default: return false;
            }
            return true;
        }
    }
}
