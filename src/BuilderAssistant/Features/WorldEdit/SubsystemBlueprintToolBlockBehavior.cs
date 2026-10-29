using Engine;
using Game;
using TemplatesDatabase;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Wires the blueprint tool (item 903) to the WorldEdit runtime's import browser.
    /// OnUse runs on the game main thread; the runtime only reads the blueprint library
    /// and validates files, never writes terrain here. All policy (single-player gate,
    /// bounds, material support, no-overwrite) is enforced inside WorldEditRuntime and
    /// the core planner/serializer, not in this adapter.
    /// </summary>
    public sealed class SubsystemBlueprintToolBlockBehavior : SubsystemBlockBehavior
    {
        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { BlueprintToolBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            if (Terrain.ExtractContents(componentMiner.ActiveBlockValue) != BlueprintToolBlock.Index)
            {
                return false;
            }

            return BlueprintPlacementSession.OnUse(componentMiner, ray);
        }
    }
}
