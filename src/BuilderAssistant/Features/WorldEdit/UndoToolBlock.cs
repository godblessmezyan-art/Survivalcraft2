using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Held by the player to cancel an in-flight build or undo the most recent
    /// completed transaction on Android. A plain painted cube (marble texture,
    /// slot 51) chosen so it is visually distinct from the build tool. Index 902
    /// is above the native range and collides with neither Speedboat (900) nor
    /// the build tool (901).
    /// </summary>
    public sealed class UndoToolBlock : PaintedCubeBlock
    {
        /// <summary>SCNET block content index reserved by this tool.</summary>
        public const int Index = 902;

        /// <summary>Constructs the tool using the marble cube texture slot.</summary>
        public UndoToolBlock() : base(51)
        {
        }
    }
}
