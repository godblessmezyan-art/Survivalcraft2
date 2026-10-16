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
        /// <summary>Writes a single line proving that SCNET instantiated the loader.</summary>
        public override void __ModInitialize()
        {
            Log.Information("[BuilderAssistant] Loaded (SCNET x25.01.17 probe, v0.1.0).");
        }
    }
}
