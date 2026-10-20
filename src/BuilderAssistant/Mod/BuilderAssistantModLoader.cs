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
            Log.Information("[BuilderAssistant] Loaded (SCNET x25.01.17, v" + modVersion + "; speedboat behavior unchanged, world-edit builds run in single player or as the multiplayer host; remote clients are refused).");
            Log.Information("[BuilderAssistant] Multiplayer note: SCNET's server-side ConnectionRequestPackage.AcceptAclient (x25.01.17) requires every joining client's announced mod list to equal this host's ModsManager.ModList (same count, same PackageName+Version per entry, same ResourcesMd5 except for the survivalcraft built-in entry). Game.ModInfo has NO server-only / client-optional flag, so Builder Assistant installed only on the host will make non-mod clients refuse the join with '客户端没有安装服务器Mod[Builder Assistant 建筑助手:<version>]'. Either let the client use the in-game 'connect to mod server' flow (Game.NetWork.ModFileService.ModFileClient.DownloadModAndJoinServer, availability on Android 7723 待验证), or install this exact .netmod on every participant. Generated buildings still use ONLY vanilla blocks; the tool blocks 900-904 are hand-held items and are never written into the world. This Mod does not modify ConnectionRequestPackage, ResourcesMd5, ModsManager.ModList, or any network handshake surface.");
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
