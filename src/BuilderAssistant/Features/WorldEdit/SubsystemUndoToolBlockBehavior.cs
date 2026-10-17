using Engine;
using Game;
using TemplatesDatabase;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Wires the undo/cancel tool (item 902) to the WorldEdit runtime. A tap
    /// cancels an in-flight build (rolling it back) or, if nothing is running,
    /// undoes the last completed transaction without clobbering later player
    /// edits.
    /// </summary>
    public sealed class SubsystemUndoToolBlockBehavior : SubsystemBlockBehavior
    {
        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { UndoToolBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            if (Terrain.ExtractContents(componentMiner.ActiveBlockValue) != UndoToolBlock.Index)
            {
                return false;
            }

            return WorldEditRuntime.OnUndoTool(componentMiner);
        }
    }
}
