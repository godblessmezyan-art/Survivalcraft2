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
        /// <summary>Pre-flight / cube resolution: no authored orientation (data 0).</summary>
        bool TryResolve(string materialKey, out int value);

        /// <summary>
        /// Schema-v2 resolution: pack <paramref name="data"/> (authored orientation)
        /// into the terrain value, masked to the bits the resolved block's family
        /// actually owns. Plain cubes ignore <paramref name="data"/> and always pack 0.
        /// </summary>
        bool TryResolve(string materialKey, int data, out int value);
    }
}
