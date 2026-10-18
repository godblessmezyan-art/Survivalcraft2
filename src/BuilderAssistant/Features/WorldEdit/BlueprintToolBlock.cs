using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Held by the player to browse and import saved blueprint JSON files from the
    /// Mod's blueprint library. Each tap advances to the next file and loads it (if
    /// valid) into the clipboard; an empty library prints the real folder path so the
    /// player knows where to drop an externally-generated (e.g. AI-authored) .json.
    ///
    /// A plain painted cube (glass texture, slot 15) so it needs no orientation data
    /// bits and is visually distinct from the build tool (901, cobblestone) and the
    /// undo tool (902, marble). Index 903 is above the native x25.01.17 range (0-263)
    /// and collides with neither the Speedboat (900) nor the other two tools. Whether
    /// 903 clashes with another mod's index on a given device is a separate, real
    /// -device concern (待验证) - SCNET keeps no global third-party index registry.
    ///
    /// This tool never writes terrain: it only reads files and validates them. Actual
    /// construction still goes through the build tool's two-tap confirm and the SAME
    /// planner/executor/undo path, so all AGENTS safety rules apply unchanged.
    /// </summary>
    public sealed class BlueprintToolBlock : PaintedCubeBlock
    {
        /// <summary>SCNET block content index reserved by this tool.</summary>
        public const int Index = 903;

        /// <summary>Constructs the tool using the glass cube texture slot.</summary>
        public BlueprintToolBlock() : base(15)
        {
        }
    }
}
