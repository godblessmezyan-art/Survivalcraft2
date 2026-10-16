namespace BuilderAssistant.Core.Safety
{
    /// <summary>
    /// Conservative defaults reserved for later terrain-editing phases.
    /// No Phase 1 code modifies terrain.
    /// </summary>
    public static class BuildSafetyLimits
    {
        /// <summary>Maximum number of cells planned by one future operation.</summary>
        public const int MaxBlocksPerOperation = 1024;

        /// <summary>Maximum number of cells applied by one future update batch.</summary>
        public const int MaxBlocksPerBatch = 32;

        /// <summary>Maximum length of any axis in a future selection.</summary>
        public const int MaxSelectionAxisLength = 64;
    }
}
