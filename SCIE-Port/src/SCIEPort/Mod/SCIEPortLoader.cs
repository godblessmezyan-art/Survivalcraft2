using Game;
using Engine;
using Game.NetWork;
using SCIEPort.Features.Creatures;
using SCIEPort.Features.IndustrialFurniture;
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
            ModsManager.RegisterHook("AttackBody", this);
            ModsManager.RegisterHook("SubsystemUpdate", this);
            ModsManager.RegisterHook("GuiUpdate", this);
            ModsManager.RegisterHook("OnModelRendererDrawExtra", this);
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

            attackPower = MathUtils.Max(attackPower, contents == StormhammerAwakenedBlock.Index ? 40f : 12f);
            playerProbability = 1f;
            creatureProbability = 1f;
            SubsystemStormhammerBlockBehavior.OnMeleeHit(miner, componentBody, hitPoint);
        }

        /// <inheritdoc />
        public override bool AttackBody(ComponentBody target, ComponentCreature attacker, Vector3 hitPoint, Vector3 hitDirection, ref float attackPower, bool isMeleeAttack)
        {
            if (target != null && attacker != null && attacker.Project != null)
            {
                SubsystemAwakenedStormhammerBlockBehavior behavior = attacker.Project.FindSubsystem<SubsystemAwakenedStormhammerBlockBehavior>(false);
                behavior?.PrepareAwakenedMelee(target, attacker, hitPoint, ref attackPower, isMeleeAttack);
            }
            return false;
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
        public override void OnModelRendererDrawExtra(SubsystemModelsRenderer modelsRenderer, ComponentModel componentModel, Camera camera, float? alphaThreshold)
        {
            AwakenedStormhammerVisuals.TouchAvatar(componentModel, camera);
        }

        /// <inheritdoc />
        public override Color ChangeSkyColor(Color oldColor, Vector3 direction, float timeOfDay, float precipitationIntensity, int temperature)
        {
            float blend = AwakenedStormhammerVisuals.GetStormBlend();
            if (blend <= 0f) return oldColor;
            float horizon = MathUtils.Saturate(0.5f + direction.Y * 0.5f);
            Color storm = Color.Lerp(new Color(35, 22, 67), new Color(10, 16, 49), horizon);
            return Color.Lerp(oldColor, storm, 0.93f * blend);
        }

        /// <inheritdoc />
        public override void ViewFogColor(float viewUnderWaterDepth, float viewUnderMagmaDepth, ref Color viewFogColor)
        {
            float blend = AwakenedStormhammerVisuals.GetStormBlend();
            if (blend > 0f && viewUnderWaterDepth <= 0f && viewUnderMagmaDepth <= 0f)
            {
                viewFogColor = Color.Lerp(viewFogColor, new Color(20, 29, 66), 0.82f * blend);
            }
        }

        /// <inheritdoc />
        public override void SaveSettings(XElement element)
        {
            element.SetAttributeValue("SCIEPortStormhammerVfxQuality", StormhammerOptions.Quality.ToString());
            element.SetAttributeValue("SCIEPortReducedLightningFlashes", StormhammerOptions.ReducedFlashes);
            element.SetAttributeValue("SCIEPortStormhammerCameraShake", StormhammerOptions.CameraShake);
            element.SetAttributeValue("SCIEPortStormhammerDiagnostics", StormhammerOptions.Diagnostics);
            element.SetAttributeValue("SCIEPortFurnitureCollisionDiagnostics", FurnitureCollisionOptions.Diagnostics);
            element.SetAttributeValue("SCIEPortVehicleAuditDiagnostics", VehicleAuditOptions.Diagnostics);
        }

        /// <inheritdoc />
        public override void LoadSettings(XElement element)
        {
            StormhammerEffectQuality quality;
            XAttribute qualityAttribute = element.Attribute("SCIEPortStormhammerVfxQuality");
            if (qualityAttribute != null && System.Enum.TryParse(qualityAttribute.Value, true, out quality)) StormhammerOptions.Quality = quality;
            bool value;
            XAttribute flashes = element.Attribute("SCIEPortReducedLightningFlashes");
            if (flashes != null && bool.TryParse(flashes.Value, out value)) StormhammerOptions.ReducedFlashes = value;
            XAttribute shake = element.Attribute("SCIEPortStormhammerCameraShake");
            if (shake != null && bool.TryParse(shake.Value, out value)) StormhammerOptions.CameraShake = value;
            XAttribute diagnostics = element.Attribute("SCIEPortStormhammerDiagnostics");
            if (diagnostics != null && bool.TryParse(diagnostics.Value, out value)) StormhammerOptions.Diagnostics = value;
            XAttribute furnitureDiagnostics = element.Attribute("SCIEPortFurnitureCollisionDiagnostics");
            if (furnitureDiagnostics != null && bool.TryParse(furnitureDiagnostics.Value, out value)) FurnitureCollisionOptions.Diagnostics = value;
            XAttribute vehicleAudit = element.Attribute("SCIEPortVehicleAuditDiagnostics");
            if (vehicleAudit != null && bool.TryParse(vehicleAudit.Value, out value)) VehicleAuditOptions.Diagnostics = value;
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
