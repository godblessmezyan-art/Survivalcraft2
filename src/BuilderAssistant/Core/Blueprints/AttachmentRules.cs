namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>Exact CellFace vectors from the target assembly, never a guessed compass convention.</summary>
    internal static class AttachmentRules
    {
        public static bool TryGetSupport(CellPos position, BlockFamily family, int data, out CellPos support, out int face)
        {
            support = position;
            face = data;
            if (family != BlockFamily.Ladder && family != BlockFamily.Torch) return false;
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
