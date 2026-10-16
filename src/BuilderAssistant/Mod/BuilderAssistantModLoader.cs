using BuilderAssistant.Features.Speedboat;
using Engine;
using Game;

namespace BuilderAssistant.Mod
{
    /// <summary>
    /// Minimal x25.01.17 loading probe. Do not add gameplay hooks until their
    /// signatures and Android behavior have been verified against the target client.
    /// </summary>
    public sealed class BuilderAssistantModLoader : ModLoader
    {
        private readonly SpeedboatFeature m_speedboatFeature = new SpeedboatFeature();

        /// <summary>Writes a single line proving that SCNET instantiated the loader.</summary>
        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("GuiUpdate", this);
            Log.Information("[BuilderAssistant] Loaded (SCNET x25.01.17, v0.2.0, speedboat prototype enabled).");
        }

        /// <summary>Updates the local-player speedboat prototype from native touch input.</summary>
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
        }
    }
}
