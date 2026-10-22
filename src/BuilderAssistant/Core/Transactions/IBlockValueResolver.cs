using BuilderAssistant.Core.Blueprints;

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
        /// Legacy schema-v2 resolution retained for existing callers. Implementations
        /// must reject non-canonical DATA rather than masking away unsupported state.
        /// </summary>
        bool TryResolve(string materialKey, int data, out int value);

        /// <summary>
        /// Strict schema-v2 resolution. The declared family must match capability
        /// metadata and DATA must be canonical; failure includes a diagnostic reason.
        /// </summary>
        bool TryResolve(
            string materialKey,
            BlockFamily family,
            int data,
            out int value,
            out string error);
    }
}
