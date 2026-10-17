namespace BuilderAssistant.Core.Transactions
{
    /// <summary>
    /// Maps a blueprint logical material key to a concrete, runtime-verified
    /// block value. Implemented by the WorldEdit vanilla palette. If a key is
    /// unknown (e.g. its block is missing in this build) resolution fails and
    /// the plan aborts safely instead of writing garbage.
    /// </summary>
    internal interface IBlockValueResolver
    {
        bool TryResolve(string materialKey, out int value);
    }
}
