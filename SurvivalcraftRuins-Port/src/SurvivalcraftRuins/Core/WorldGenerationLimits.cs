namespace SurvivalcraftRuins.Core;

public static class WorldGenerationLimits
{
    /// <summary>
    /// Hard preflight cap for one 16x16 terrain chunk. The hook writes only a
    /// newly generated chunk and refuses the whole chunk before the first edit
    /// when this bound would be exceeded.
    /// </summary>
    public const int MaxCellWritesPerChunk = 32768;

    public const int MaxCellsPerStructure = 250000;
}
