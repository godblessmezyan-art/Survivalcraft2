using Engine;
using Game;
using TemplatesDatabase;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Wires the build tool (item 901) to the WorldEdit runtime. OnUse runs on
    /// the game main thread, so it may safely read terrain and start a
    /// batched build. All policy (single-player gate, bounds, block cap,
    /// no-overwrite, chunk-loaded) is enforced inside WorldEditRuntime and the
    /// core planner, not here.
    /// </summary>
    public sealed class SubsystemBuilderToolBlockBehavior : SubsystemBlockBehavior
    {
        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { BuilderToolBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            if (Terrain.ExtractContents(componentMiner.ActiveBlockValue) != BuilderToolBlock.Index)
            {
                return false;
            }

            return WorldEditRuntime.OnBuildTool(componentMiner, ray);
        }

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            WorldEditRuntime.Initialize();
        }
    }
}
