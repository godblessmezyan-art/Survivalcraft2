namespace BuilderAssistant.Core.Blueprints
{
    /// <summary>
    /// One cell of a blueprint: a position relative to the blueprint origin
    /// plus a LOGICAL material key (never a raw block index). Resolving a key
    /// to a concrete block is the job of <see cref="Transactions.IBlockValueResolver"/>,
    /// which is what makes material substitution and future palette swaps easy.
    /// </summary>
    internal readonly struct BlueprintCell
    {
        public readonly CellPos Relative;
        public readonly string MaterialKey;

        public BlueprintCell(CellPos relative, string materialKey)
        {
            Relative = relative;
            MaterialKey = materialKey;
        }
    }
}
