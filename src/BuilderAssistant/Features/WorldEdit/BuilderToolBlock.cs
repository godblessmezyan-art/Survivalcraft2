using Game;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Held by the player to select and confirm a build drop point. It is a
    /// plain painted cube (cobblestone texture, slot 69) so it needs no
    /// orientation data bits; its only job is to be selectable as an active tool
    /// so its SubsystemBlockBehavior.OnUse fires on the main thread. Index 901
    /// is above the native x25.01.17 range (0-263) and does not collide with the
    /// Speedboat item (900) or SCIE-Port indices.
    /// </summary>
    public sealed class BuilderToolBlock : PaintedCubeBlock
    {
        /// <summary>SCNET block content index reserved by this tool.</summary>
        public const int Index = 901;

        /// <summary>Constructs the tool using the cobblestone cube texture slot.</summary>
        public BuilderToolBlock() : base(69)
        {
        }
    }
}
