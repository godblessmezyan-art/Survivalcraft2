using Game;
using SCIEPort.Features.AbyssReaper;
using SCIEPort.Features.DevouringEye;
using SCIEPort.Features.OmniscientEye;
using SCIEPort.Features.Poseidon;
using SCIEPort.Features.Inventory;
using Engine;
using Game.NetWork;
using SCIEPort.Features.Creatures;
using SCIEPort.Features.KellyPlants;
using SCIEPort.Features.Tanks;
using SCIEPort.Features.Vehicles;
using SCIEPort.Features.PortableFortress;
using SCIEPort.Features.Stormhammer;
using SCIEPort.Features.Gungnir;
using SCIEPort.Features.FourSaints;
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
                PackageManager.RegisterPackage(new GungnirPackage());
                PackageManager.RegisterPackage(new FourSaintsPackage());
                PackageManager.RegisterPackage(new PoseidonPackage());
                PackageManager.RegisterPackage(new WorldCutPackage());
                s_networkPackageRegistered = true;
            }

            ModsManager.RegisterHook("BlocksInitalized", this);
            ModsManager.RegisterHook("OnTerrainContentsGenerated", this);
            ModsManager.RegisterHook("OnMinerHit", this);
            ModsManager.RegisterHook("AttackBody", this);
            ModsManager.RegisterHook("OnCreatureInjure", this);
            ModsManager.RegisterHook("UpdateInput", this);
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
            ComponentPlayer player = miner?.ComponentPlayer;
            SubsystemFourSaints fourSaints = player?.Project.FindSubsystem<SubsystemFourSaints>(false);
            if (fourSaints != null && fourSaints.IsActionLocked(player))
            {
                attackPower = 0f; playerProbability = 0f; creatureProbability = 0f; hitted = true;
                return;
            }
            int contents = Terrain.ExtractContents(miner.ActiveBlockValue);
            if (contents == StormhammerBlock.Index || contents == StormhammerAwakenedBlock.Index)
            {
                attackPower = MathUtils.Max(attackPower, contents == StormhammerAwakenedBlock.Index ? 40f : 12f);
                playerProbability = 1f; creatureProbability = 1f;
                SubsystemStormhammerBlockBehavior.OnMeleeHit(miner, componentBody, hitPoint);
            }
            float frequency = fourSaints != null ? fourSaints.AttackFrequency(player) : (contents == StormhammerAwakenedBlock.Index ? 1.1f : 1f);
            if (frequency > 1f) miner.m_lastHitTime -= .66 * (1.0 - 1.0 / frequency);
        }

        /// <inheritdoc />
        public override bool AttackBody(ComponentBody target, ComponentCreature attacker, Vector3 hitPoint, Vector3 hitDirection, ref float attackPower, bool isMeleeAttack)
        {
            ComponentPlayer attackingPlayer = attacker?.Entity.FindComponent<ComponentPlayer>(false);
            SubsystemFourSaints fourSaints = attackingPlayer?.Project.FindSubsystem<SubsystemFourSaints>(false);
            if (fourSaints != null && fourSaints.IsActionLocked(attackingPlayer)) { attackPower = 0f; return false; }
            if (isMeleeAttack && attacker is ComponentPlayer && SubsystemGungnir.Equipped((ComponentPlayer)attacker)) { attackPower = 0; return false; }
            if (fourSaints != null && attackPower > 0f)
                attackPower += FourSaintsRules.DamageBonus(SubsystemFourSaints.Wears(attackingPlayer, ClothingSlot.Legs, FourSaintsRules.BaihuIndex), fourSaints.IsAwakened(attackingPlayer));
            if (target != null && attacker != null && attacker.Project != null)
            {
                SubsystemAwakenedStormhammerBlockBehavior behavior = attacker.Project.FindSubsystem<SubsystemAwakenedStormhammerBlockBehavior>(false);
                behavior?.PrepareAwakenedMelee(target, attacker, hitPoint, ref attackPower, isMeleeAttack);
            }
            return false;
        }

        /// <inheritdoc />
        public override void OnCreatureInjure(ComponentHealth componentHealth, float amount, ComponentCreature attacker, bool ignoreInvulnerability, string cause, out bool Skip)
        {
            if (SubsystemPoseidon.BlocksDrowning(componentHealth, attacker, cause)) { Skip = true; return; }
            Skip = componentHealth?.Project.FindSubsystem<SubsystemFourSaints>(false)?.HandleInjury(componentHealth, amount, attacker, ignoreInvulnerability, cause) == true;
        }

        /// <inheritdoc />
        public override void UpdateInput(ComponentInput componentInput, WidgetInput widgetInput)
        {
            ComponentPlayer player = componentInput?.m_componentPlayer;
            SubsystemFourSaints runtime = player?.Project.FindSubsystem<SubsystemFourSaints>(false);
            if (player != null && Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue) == DevouringEyeBlock.Index)
            {
                componentInput.m_playerInput.Interact = componentInput.m_playerInput.Interact ?? componentInput.m_playerInput.Hit ?? componentInput.m_playerInput.Dig;
                componentInput.m_playerInput.Hit = null;
                componentInput.m_playerInput.Dig = null;
                componentInput.m_playerInput.Aim = null;
            }
            if (runtime == null || !runtime.IsActionLocked(player)) return;
            componentInput.m_playerInput.Move = Vector3.Zero;
            componentInput.m_playerInput.SneakMove = Vector3.Zero;
            componentInput.m_playerInput.Jump = false;
            componentInput.m_playerInput.Hit = null;
            componentInput.m_playerInput.Dig = null;
            componentInput.m_playerInput.Aim = null;
            componentInput.m_playerInput.Interact = null;
        }

        /// <inheritdoc />
        public override void SubsystemUpdate(float dt)
        {
            StormhammerVisuals.Update();
            // Natural creatures are queued from the chunk hook and only created here, once the
            // owning chunk has finished generating. Spawning from inside chunk generation is not
            // a supported x25.01.17 path and could stall a fresh world on the loading screen.
            CreatureWorldSpawn.Update();
        }

        /// <inheritdoc />
        public override void GuiUpdate(ComponentGui componentGui)
        {
            componentGui?.m_componentPlayer?.Project.FindSubsystem<SubsystemAbyssReaper>(false)?.TickGui(componentGui);
            componentGui?.m_componentPlayer?.Project.FindSubsystem<SubsystemDevouringEye>(false)?.Tick();
            FourSaintsClothingPreview.Update(componentGui);
            StormhammerSkillHud.Update(componentGui);
            GungnirHud.Update(componentGui);
            componentGui?.m_componentPlayer?.Project.FindSubsystem<SubsystemPoseidon>(false)?.TickGui(componentGui);
            FourSaintsHud.Update(componentGui);
            componentGui?.m_componentPlayer?.Project.FindSubsystem<SubsystemOmniscientEye>(false)?.Tick(componentGui);
            componentGui?.m_componentPlayer?.Project.FindSubsystem<SubsystemFourSaints>(false)?.RefreshLocalHealthBar(componentGui.m_componentPlayer);
        }

        /// <inheritdoc />
        public override void OnModelRendererDrawExtra(SubsystemModelsRenderer modelsRenderer, ComponentModel componentModel, Camera camera, float? alphaThreshold)
        {
            QinglongCrownRenderer.Draw(componentModel, camera);
            FourSaintsArmorRenderer.Draw(componentModel, camera);
            AwakenedStormhammerVisuals.TouchAvatar(componentModel, camera);
            FourSaintsVisuals.TouchAvatar(componentModel, camera);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Mjolnir indigo and Gungnir dark-teal/gold skies are composited rather than overwriting
        /// one another. Both providers filter sky tint to the local caster; world geometry remains
        /// visible to bystanders through their authoritative event/snapshot paths.
        /// </remarks>
        public override Color ChangeSkyColor(Color oldColor, Vector3 direction, float timeOfDay, float precipitationIntensity, int temperature)
        {
            float stormBlend = AwakenedStormhammerVisuals.GetStormBlend();
            float stormFlash = AwakenedStormhammerVisuals.GetFlash();
            float divineBlend = GungnirSkyVisuals.GetSkyBlend();
            float divineFlash = GungnirSkyVisuals.GetFlash();
            float abyssBlend = GameManager.Project?.FindSubsystem<SubsystemAbyssReaper>(false)?.Horizon?.SkyBlend ?? 0f;
            if (stormBlend <= 0f && stormFlash <= 0f && divineBlend <= 0f && divineFlash <= 0f)
                return abyssBlend > 0f ? Color.Lerp(oldColor, new Color(24, 8, 37), .8f * abyssBlend) : oldColor;
            float stormGradient = MathUtils.Saturate(0.5f + direction.Y * 0.5f);
            float zenith = MathUtils.Saturate(direction.Y);
            Color storm = Color.Lerp(new Color(46, 26, 92), new Color(9, 10, 34), stormGradient);
            Color divine = Color.Lerp(new Color(92, 76, 50), new Color(10, 39, 42), zenith);
            float blend = GungnirSkyRules.Combine(stormBlend, divineBlend);
            Color effect;
            if (stormBlend > 0f && divineBlend > 0f)
                effect = Color.Lerp(storm, divine, GungnirSkyRules.DivineShare(stormBlend, divineBlend));
            else effect = divineBlend > 0f ? divine : storm;
            Color result = blend > 0f ? Color.Lerp(oldColor, effect, (divineBlend > 0f ? 0.88f : 0.93f) * blend) : oldColor;
            if (stormFlash > 0f)
            {
                Color struck = Color.Lerp(new Color(126, 158, 232), new Color(214, 232, 255), stormGradient);
                float retain = 1f - 0.55f * GungnirSkyRules.DivineShare(stormBlend, divineBlend);
                result = Color.Lerp(result, struck, 0.60f * stormFlash * retain);
            }
            if (divineFlash > 0f)
                result = Color.Lerp(result, Color.Lerp(new Color(238, 203, 132), new Color(255, 255, 239), zenith), 0.72f * divineFlash);

            return abyssBlend > 0f ? Color.Lerp(result, new Color(24, 8, 37), .8f * abyssBlend) : result;
        }

        /// <inheritdoc />
        /// <remarks>Underwater/magma fog is untouched. Artifact fog shares the same explicit blend as the sky.</remarks>
        public override void ViewFogColor(float viewUnderWaterDepth, float viewUnderMagmaDepth, ref Color viewFogColor)
        {
            if (viewUnderWaterDepth > 0f || viewUnderMagmaDepth > 0f) return;
            float stormBlend = AwakenedStormhammerVisuals.GetStormBlend();
            float stormFlash = AwakenedStormhammerVisuals.GetFlash();
            float divineBlend = GungnirSkyVisuals.GetSkyBlend();
            float divineFlash = GungnirSkyVisuals.GetFlash();
            float blend = GungnirSkyRules.Combine(stormBlend, divineBlend);
            if (blend > 0f)
            {
                Color stormFog = new Color(22, 26, 62);
                Color divineFog = new Color(31, 48, 44);
                Color effect = stormBlend > 0f && divineBlend > 0f
                    ? Color.Lerp(stormFog, divineFog, GungnirSkyRules.DivineShare(stormBlend, divineBlend))
                    : divineBlend > 0f ? divineFog : stormFog;
                viewFogColor = Color.Lerp(viewFogColor, effect, (divineBlend > 0f ? 0.76f : 0.84f) * blend);
            }
            if (stormFlash > 0f) viewFogColor = Color.Lerp(viewFogColor, new Color(96, 122, 186), 0.42f * stormFlash * MathUtils.Max(stormBlend, 0.35f));
            if (divineFlash > 0f) viewFogColor = Color.Lerp(viewFogColor, new Color(226, 205, 151), 0.48f * divineFlash);
        }

        /// <inheritdoc />
        public override void SaveSettings(XElement element)
        {
            element.SetAttributeValue("SCIEPortQinglongCrownV3Preview", QinglongCrownRenderer.PreviewEnabled);
            element.SetAttributeValue("SCIEPortFourSaintsArmorV3Preview", FourSaintsArmorRenderer.PreviewEnabled);
            element.SetAttributeValue("SCIEPortFourSaintsArmorDiagnostics", FourSaintsArmorRenderer.Diagnostics);
            element.SetAttributeValue("SCIEPortQinglongCrownDiagnostics", QinglongCrownRenderer.Diagnostics);
            element.SetAttributeValue("SCIEPortStormhammerVfxQuality", StormhammerOptions.Quality.ToString());
            element.SetAttributeValue("SCIEPortReducedLightningFlashes", StormhammerOptions.ReducedFlashes);
            element.SetAttributeValue("SCIEPortStormhammerCameraShake", StormhammerOptions.CameraShake);
            element.SetAttributeValue("SCIEPortStormhammerDiagnostics", StormhammerOptions.Diagnostics);
            element.SetAttributeValue("SCIEPortFortressDiagnostics", FortressOptions.Diagnostics);
            element.SetAttributeValue("SCIEPortFortressQuality", FortressOptions.Quality.ToString());
        }

        /// <inheritdoc />
        public override void LoadSettings(XElement element)
        {
            StormhammerEffectQuality quality;
            XAttribute qualityAttribute = element.Attribute("SCIEPortStormhammerVfxQuality");
            if (qualityAttribute != null && System.Enum.TryParse(qualityAttribute.Value, true, out quality)) StormhammerOptions.Quality = quality;
            bool value;
            XAttribute armorPreview = element.Attribute("SCIEPortFourSaintsArmorV3Preview");
            if (armorPreview != null && bool.TryParse(armorPreview.Value, out value)) FourSaintsArmorRenderer.PreviewEnabled = value;
            XAttribute armorDiagnostics = element.Attribute("SCIEPortFourSaintsArmorDiagnostics");
            if (armorDiagnostics != null && bool.TryParse(armorDiagnostics.Value, out value)) FourSaintsArmorRenderer.Diagnostics = value;
            XAttribute crownPreview = element.Attribute("SCIEPortQinglongCrownV3Preview");
            if (crownPreview != null && bool.TryParse(crownPreview.Value, out value)) QinglongCrownRenderer.PreviewEnabled = value;
            XAttribute crownDiagnostics = element.Attribute("SCIEPortQinglongCrownDiagnostics");
            if (crownDiagnostics != null && bool.TryParse(crownDiagnostics.Value, out value)) QinglongCrownRenderer.Diagnostics = value;
            XAttribute flashes = element.Attribute("SCIEPortReducedLightningFlashes");
            if (flashes != null && bool.TryParse(flashes.Value, out value)) StormhammerOptions.ReducedFlashes = value;
            XAttribute shake = element.Attribute("SCIEPortStormhammerCameraShake");
            if (shake != null && bool.TryParse(shake.Value, out value)) StormhammerOptions.CameraShake = value;
            XAttribute diagnostics = element.Attribute("SCIEPortStormhammerDiagnostics");
            if (diagnostics != null && bool.TryParse(diagnostics.Value, out value)) StormhammerOptions.Diagnostics = value;
            XAttribute fortressDiagnostics = element.Attribute("SCIEPortFortressDiagnostics");
            if (fortressDiagnostics != null && bool.TryParse(fortressDiagnostics.Value, out value)) FortressOptions.Diagnostics = value;
            FortressQuality fortressQuality;
            XAttribute fortressQualityAttribute = element.Attribute("SCIEPortFortressQuality");
            if (fortressQualityAttribute != null && System.Enum.TryParse(fortressQualityAttribute.Value, true, out fortressQuality)) FortressOptions.Quality = fortressQuality;
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

            // The `.clo` merge can silently drop entries (load order, one unreadable asset), which
            // left some or all Four Saints outfits missing from the clothing tab. Register whatever
            // the `.clo` did not deliver, one guarded item at a time.
            try
            {
                FourSaintsClothing.EnsureRegistered();
            }
            catch (System.Exception exception)
            {
                Log.Error("[SCIEPort] Four Saints clothing registration failed: " + exception);
            }
            PortInventoryCatalog.Initialize();
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
