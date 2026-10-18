using BuilderAssistant.Features.Speedboat;
using BuilderAssistant.Features.WorldEdit;
using Engine;
using Game;

namespace BuilderAssistant.Mod
{
    /// <summary>
    /// x25.01.17 loading entry point. It keeps the shipped speedboat behavior
    /// exactly as-is and only adds the world-edit build pipeline: the block
    /// palette is initialized once blocks are loaded, and the build/undo task is
    /// advanced a little each frame from the main-thread GuiUpdate hook.
    /// </summary>
    public sealed class BuilderAssistantModLoader : ModLoader
    {
        private readonly SpeedboatFeature m_speedboatFeature = new SpeedboatFeature();

        /// <summary>Writes a single line proving that SCNET instantiated the loader.</summary>
        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("GuiUpdate", this);
            ModsManager.RegisterHook("BlocksInitalized", this);
            string modVersion = typeof(BuilderAssistantModLoader).Assembly.GetName().Version.ToString();
            Log.Information("[BuilderAssistant] Loaded (SCNET x25.01.17, v" + modVersion + "; speedboat behavior unchanged, world-edit builds run only in a single-player test world).");
        }

        /// <summary>
        /// Fired once the game has finished initializing its block set. At this
        /// point BlocksManager can resolve vanilla block names to indices, so the
        /// material palette and terrain gateway become usable.
        /// </summary>
        public override void BlocksInitalized()
        {
            try
            {
                WorldEditRuntime.Initialize();
            }
            catch (System.Exception exception)
            {
                Log.Error("[BuilderAssistant] WorldEdit initialize failed: " + exception);
                ModsManager.DisableHook(this, "BlocksInitalized", "BuilderAssistant", "WorldEdit initialize failure");
            }
        }

        /// <summary>
        /// Updates the local-player speedboat prototype (unchanged) and then
        /// advances any in-flight build by a small batch, both on the main
        /// thread. Each subsystem is isolated in its own try/catch so a world-edit
        /// fault can never disable the speedboat hook, and vice versa.
        /// </summary>
        public override void GuiUpdate(ComponentGui componentGui)
        {
            try
            {
                m_speedboatFeature.Update(componentGui);
            }
            catch (System.Exception exception)
            {
                Log.Error("[BuilderAssistant] Speedboat update failed: " + exception);
                ModsManager.DisableHook(this, "GuiUpdate", exception.Message, "BuilderAssistant.Speedboat");
            }

            try
            {
                WorldEditRuntime.Tick(componentGui);
            }
            catch (System.Exception exception)
            {
                Log.Error("[BuilderAssistant] WorldEdit tick failed: " + exception);
                ModsManager.DisableHook(this, "GuiUpdate", "BuilderAssistant", "WorldEdit tick failure");
            }
        }
    }
}
