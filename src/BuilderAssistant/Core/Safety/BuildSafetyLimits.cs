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
        /// Raised from 1024 to 2756 on 2026-10-20 per project-owner decision,
        /// sized to keep the giant treehouse (2765 blocks) within 1-2 phases.
        /// Per-frame write cost is still governed by MaxBlocksPerBatch (32) and
        /// spatial scope by MaxSelectionAxisLength (64).
        /// </summary>
        public const int MaxBlocksPerOperation = 2756;

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
