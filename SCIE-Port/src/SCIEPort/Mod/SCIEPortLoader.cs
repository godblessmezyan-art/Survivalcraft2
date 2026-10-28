using SCIEPort.Features.AbyssReaper;
using Game;
using Engine;
using Game.NetWork;
using SCIEPort.Features.KellyPlants;
using SCIEPort.Features.Vehicles;
using SCIEPort.Features.OmniscientEye;
using SCIEPort.Features.DevouringEye;

namespace SCIEPort.Mod
{
    /// <summary>SCNET x25.01.17 loader entry for the SCIE port.</summary>
    public sealed class SCIEPortLoader : ModLoader
    {
        private bool m_kellyPlantsReady;
        private static bool s_networkPackageRegistered;

        /// <inheritdoc />
        public override void __ModInitialize()
        {
            if (!s_networkPackageRegistered)
            {
                PackageManager.RegisterPackage(new TwoSeatAssignmentPackage());
                s_networkPackageRegistered = true;
            }

            ModsManager.RegisterHook("BlocksInitalized", this);
            ModsManager.RegisterHook("OnTerrainContentsGenerated", this);
            ModsManager.RegisterHook("GuiUpdate", this);
            ModsManager.RegisterHook("UpdateInput", this);
        }

        /// <inheritdoc />
        public override void GuiUpdate(ComponentGui componentGui)
        {
            componentGui?.m_componentPlayer?.Project.FindSubsystem<SubsystemAbyssReaper>(false)?.TickGui(componentGui);
            componentGui?.m_componentPlayer?.Project.FindSubsystem<SubsystemOmniscientEye>(false)?.Tick(componentGui);
            componentGui?.m_componentPlayer?.Project.FindSubsystem<SubsystemDevouringEye>(false)?.Tick();
        }

        /// <inheritdoc />
        public override void UpdateInput(ComponentInput input, WidgetInput widgetInput)
        {
            ComponentPlayer player=input?.m_componentPlayer;
            if(player==null || Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue)!=DevouringEyeBlock.Index)return;
            // Entity taps/long presses use the same confirmation workflow, never vanilla hit/dig.
            input.m_playerInput.Interact=input.m_playerInput.Interact??input.m_playerInput.Hit??input.m_playerInput.Dig;
            input.m_playerInput.Hit=null;
            input.m_playerInput.Dig=null;
            input.m_playerInput.Aim=null;
        }

        /// <inheritdoc />
        public override void BlocksInitalized()
        {
            try
            {
                _ = KellyPlantTextures.Atlas;
                m_kellyPlantsReady = true;
                Log.Information("[SCIEPort] Kelly plant resources initialized (14 blocks, original inventory names).");
            }
            catch (System.Exception exception)
            {
                m_kellyPlantsReady = false;
                Log.Error("[SCIEPort] Kelly plant resources failed to initialize: " + exception);
            }
        }

        /// <inheritdoc />
        public override void OnTerrainContentsGenerated(TerrainChunk chunk)
        {
            try
            {
                if (m_kellyPlantsReady && KellyPlantWorldGeneration.IsReady)
                {
                    KellyPlantWorldGeneration.Generate(chunk);
                }
            }
            catch (System.Exception exception)
            {
                Log.Error("[SCIEPort] Kelly plant world generation failed: " + exception);
            }
        }
    }
}
