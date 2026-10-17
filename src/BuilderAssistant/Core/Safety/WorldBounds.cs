namespace BuilderAssistant.Core.Safety
{
    /// <summary>
    /// Hard world coordinate limits taken from the x25.01.17 assembly.
    /// Game.Terrain.IsCellValid only checks 0 &lt;= y &lt; 256; X/Z are unbounded
    /// (chunk-allocation decides), so Builder Assistant adds its own extents.
    /// </summary>
    internal static class WorldBounds
    {
        /// <summary>Minimum valid cell Y (inclusive).</summary>
        public const int MinY = 0;

        /// <summary>Maximum valid cell Y (exclusive). TerrainChunk.Height == 256.</summary>
        public const int MaxYExclusive = 256;

        /// <summary>True when a cell height is inside the terrain column.</summary>
        public static bool IsYValid(int y)
        {
            return y >= MinY && y < MaxYExclusive;
        }
    }
}
