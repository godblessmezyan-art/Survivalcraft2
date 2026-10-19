using Engine;
using Game;
using TemplatesDatabase;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>
    /// Wires the workbench tool (item 904) to the guided <see cref="WorkbenchSession"/>.
    /// OnUse runs on the game main thread (a tap), so it may safely read terrain for the
    /// read-only footprint preview and, at the final confirm step, hand a validated build
    /// to <see cref="WorldEditRuntime.WorkbenchBeginBuild"/>. This adapter holds NO policy:
    /// the authority gate, bounds, caps, no-overwrite rule, confirmation-invalidation and
    /// the terrain re-plan all live in the game-free Core/Workbench state machine, exactly
    /// like the other tools' behavior adapters. It also (re-)initializes the shared
    /// WorldEdit runtime on load so the palette is ready, mirroring the build tool.
    /// </summary>
    public sealed class SubsystemWorkbenchToolBlockBehavior : SubsystemBlockBehavior
    {
        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { WorkbenchToolBlock.Index };

        /// <inheritdoc />
        public override bool OnUse(Ray3 ray, ComponentMiner componentMiner)
        {
            if (Terrain.ExtractContents(componentMiner.ActiveBlockValue) != WorkbenchToolBlock.Index)
            {
                return false;
            }

            return WorkbenchSession.OnWorkbenchTool(componentMiner, ray);
        }

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            WorldEditRuntime.Initialize();
        }
    }
}
