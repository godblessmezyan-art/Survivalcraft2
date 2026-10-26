using Game;
using Engine;
using Game.NetWork;
using SCIEPort.Features.Creatures;
using SCIEPort.Features.KellyPlants;
using SCIEPort.Features.Tanks;
using SCIEPort.Features.Vehicles;
using SCIEPort.Features.Stormhammer;
using System.Xml.Linq;

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
                PackageManager.RegisterPackage(new TankControlPackage());
                PackageManager.RegisterPackage(new VehicleActivationPackage());
                PackageManager.RegisterPackage(new StormhammerSkillPackage());
                s_networkPackageRegistered = true;
            }

            ModsManager.RegisterHook("BlocksInitalized", this);
            ModsManager.RegisterHook("OnTerrainContentsGenerated", this);
            ModsManager.RegisterHook("OnMinerHit", this);
            ModsManager.RegisterHook("SubsystemUpdate", this);
            ModsManager.RegisterHook("GuiUpdate", this);
            ModsManager.RegisterHook("ChangeSkyColor", this);
            ModsManager.RegisterHook("ViewFogColor", this);
            ModsManager.RegisterHook("SaveSettings", this);
            ModsManager.RegisterHook("LoadSettings", this);
        }

        /// <inheritdoc />
        public override void OnMinerHit(
            ComponentMiner miner,
            ComponentBody componentBody,
            Vector3 hitPoint,
            Vector3 hitDirection,
            ref float attackPower,
            ref float playerProbability,
            ref float creatureProbability,
            out bool hitted)
        {
            hitted = false;
            int contents = Terrain.ExtractContents(miner.ActiveBlockValue);
            if (contents != StormhammerBlock.Index && contents != StormhammerAwakenedBlock.Index)
            {
                return;
            }

            attackPower = MathUtils.Max(attackPower, contents == StormhammerAwakenedBlock.Index ? 16f : 12f);
            playerProbability = 1f;
            creatureProbability = 1f;
            SubsystemStormhammerBlockBehavior.OnMeleeHit(miner, componentBody, hitPoint);
        }

        /// <inheritdoc />
        public override void SubsystemUpdate(float dt)
        {
            StormhammerVisuals.Update();
        }

        /// <inheritdoc />
        public override void GuiUpdate(ComponentGui componentGui)
        {
            StormhammerSkillHud.Update(componentGui);
        }

        /// <inheritdoc />
        public override Color ChangeSkyColor(Color oldColor, Vector3 direction, float timeOfDay, float precipitationIntensity, int temperature)
        {
            float blend = AwakenedStormhammerVisuals.GetStormBlend();
            if (blend <= 0f) return oldColor;
            Color storm = direction.Y > 0.15f ? new Color(24, 31, 78) : new Color(42, 35, 86);
            return Color.Lerp(oldColor, storm, 0.82f * blend);
        }

        /// <inheritdoc />
        public override void ViewFogColor(float viewUnderWaterDepth, float viewUnderMagmaDepth, ref Color viewFogColor)
        {
            float blend = AwakenedStormhammerVisuals.GetStormBlend();
            if (blend > 0f && viewUnderWaterDepth <= 0f && viewUnderMagmaDepth <= 0f)
            {
                viewFogColor = Color.Lerp(viewFogColor, new Color(33, 47, 91), 0.64f * blend);
            }
        }

        /// <inheritdoc />
        public override void SaveSettings(XElement element)
        {
            element.SetAttributeValue("SCIEPortReducedLightningEffects", StormhammerOptions.ReducedEffects);
        }

        /// <inheritdoc />
        public override void LoadSettings(XElement element)
        {
            XAttribute attribute = element.Attribute("SCIEPortReducedLightningEffects");
            bool reduced;
            if (attribute != null && bool.TryParse(attribute.Value, out reduced))
            {
                StormhammerOptions.ReducedEffects = reduced;
            }
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
