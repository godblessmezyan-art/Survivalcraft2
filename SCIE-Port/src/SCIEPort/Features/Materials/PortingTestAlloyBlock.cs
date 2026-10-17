using Game;

namespace SCIEPort.Features.Materials
{
    /// <summary>
    /// Creative-only registration probe. It intentionally uses a runtime game
    /// texture slot and contains no copied SCIE art or behavior.
    /// </summary>
    public sealed class PortingTestAlloyBlock : CubeBlock
    {
        /// <summary>
        /// Provisional content ID. Build validation checks it against the pinned
        /// target assembly; a persistent ID registry is required before release.
        /// </summary>
        public const int Index = 1000;
    }
}

