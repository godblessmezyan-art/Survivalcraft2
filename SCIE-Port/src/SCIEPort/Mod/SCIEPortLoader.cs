using Game;
using Engine;
using Game.NetWork;
using SCIEPort.Features.Creatures;
using SCIEPort.Features.KellyPlants;
using SCIEPort.Features.Vehicles;

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

            try
            {
                // Natural fox/rabbit seeding is 待验证 on-device: the authority gate, seed
                // determinism, per-chunk budget and population caps are enforced by the
                // offline-tested EcologyRules, but the live entity spawn during generation
                // has not yet been observed to succeed or sync correctly on Android/7723.
                if (CreatureWorldSpawn.IsReady)
                {
                    CreatureWorldSpawn.TrySpawnNaturalCreatures(chunk);
                }
            }
            catch (System.Exception exception)
            {
                Log.Error("[SCIEPort] Natural creature world spawn failed: " + exception);
            }
        }
    }
}
