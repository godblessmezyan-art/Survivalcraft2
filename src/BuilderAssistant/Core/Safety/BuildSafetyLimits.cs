namespace BuilderAssistant.Core.Safety
{
    /// <summary>
    /// Conservative defaults reserved for later terrain-editing phases.
    /// No Phase 1 code modifies terrain.
    /// </summary>
    public static class BuildSafetyLimits
    {
        /// <summary>
        /// Maximum number of cells in one operation (BuildTask transaction).
        /// Raised 1024 -&gt; 2756 (2026-10-20, to fit the giant treehouse in 1-2 phases),
        /// then 2756 -&gt; 10000 (2026-10-20) per project-owner decision that a single
        /// transaction of up to ~10k blocks is acceptable. This is only an aggregate
        /// per-transaction ceiling; the actual per-frame write cost is STILL governed by
        /// MaxBlocksPerBatch (32) so a large build streams in small batches rather than
        /// stalling a single frame, and spatial scope is STILL bounded by
        /// MaxSelectionAxisLength (64) per axis.
        /// </summary>
        public const int MaxBlocksPerOperation = 10000;

        /// <summary>Maximum number of cells applied by one future update batch.</summary>
        public const int MaxBlocksPerBatch = 32;

        /// <summary>Maximum length of any axis in a future selection.</summary>
        public const int MaxSelectionAxisLength = 64;

        /// <summary>
        /// How many consecutive Advance calls (≈ frames) a running build may make zero
        /// forward progress while its target chunks are only transiently not-ready
        /// (recoverable) before it gives up and rolls back safely. v0.6.3: our own writes
        /// trigger an engine re-mesh that can briefly drop a chunk's State below Valid; a
        /// bounded stall lets the build resume instead of aborting the whole cottage.
        /// </summary>
        public const int MaxIdleAdvanceCallsDuringBuild = 240;
    }
}
