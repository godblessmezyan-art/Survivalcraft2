using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>Authoritative Skywrath V3.2 combat, cooldown and presentation event state.</summary>
    public sealed class SubsystemAwakenedStormhammerBlockBehavior : SubsystemBlockBehavior, IUpdateable, IDrawable
    {
        internal const float PhysicalMeleePower = 40f;
        internal const float LightningMeleePower = 25f;
        internal const float RetaliationPower = 5f;

        /// <summary>
        /// Fixed lightning add-on of the fake hammer. It never receives the Thunder God
        /// multiplier: the fake copy has no connection to the real storm.
        /// </summary>
        internal const float BaseHammerLightningPower = 5f;

        /// <summary>
        /// The melee add-on lands this long after the physical hit, so the white melee number
        /// and the cyan lightning number are read as two separate hits instead of one blur.
        /// </summary>
        internal const float LightningDisplayDelay = 0.4f;
        internal const float JudgmentCooldown = 8f;
        internal const float UltimateCooldown = 55f;

        /// <summary>Duration of the post-ultimate Thunder God window.</summary>
        internal const float ThunderGodDuration = 12f;

        /// <summary>Lightning damage multiplier inside the Thunder God window.</summary>
        internal const float ThunderGodPowerMultiplier = 2f;

        private sealed class PendingLightning
        {
            public ComponentCreature Attacker;
            public ComponentBody Target;
            public Vector3 HitPoint;
            public double ApplyTime;
            public float PhysicalBefore;
            public float Power;
            public string Cause;
            /// <summary>True only for the awakened hammer; the fake copy never receives the buff.</summary>
            public bool Multiplied;
        }

        private sealed class AvatarState
        {
            public ComponentPlayer Player;
            public ComponentHealth Health;
            public float LastHealth;
            public Action<ComponentCreature> Handler;
        }

        private sealed class EquipmentState
        {
            internal ComponentPlayer Player;
            internal float WalkDelta;
        }

        private sealed class ThunderGodState
        {
            public ComponentPlayer Player;
            public ushort EntityId;
            public double EndTime;
            public bool JudgmentBoostUsed;
        }

        private sealed class PendingJudgment
        {
            public ComponentPlayer Player;
            public uint CastId;
            public double ApplyAt;
        }

        /// <summary>雷霆王域: one domain per caster, following the caster and paying one tick per second.</summary>
        private sealed class DomainState
        {
            public ComponentPlayer Caster;
            public ushort EntityId;
            public uint CastId;
            public double StartTime;
            public int Ticks;
            public double NextRing;
            public double NextArc;
        }

        private sealed class UltimateState
        {
            public ComponentPlayer Caster;
            public Vector3 Center;
            public uint CastId;
            public double StartTime;
            public bool MainWarned;
            public bool MainStruck;
            public int AftershockTicks;
            public double NextAftershock;
        }

        private readonly Dictionary<ushort, double[]> m_cooldowns = new Dictionary<ushort, double[]>();
        private readonly Dictionary<ushort, ThunderGodState> m_thunderGod = new Dictionary<ushort, ThunderGodState>();
        private readonly Dictionary<ushort, DomainState> m_domains = new Dictionary<ushort, DomainState>();
        private readonly Dictionary<ushort, ushort> m_sequences = new Dictionary<ushort, ushort>();
        private readonly Dictionary<ComponentHealth, AvatarState> m_avatars = new Dictionary<ComponentHealth, AvatarState>();
        private readonly Dictionary<ushort, EquipmentState> m_equipment = new Dictionary<ushort, EquipmentState>();
        private readonly List<PendingLightning> m_pendingLightning = new List<PendingLightning>();
        private readonly List<PendingJudgment> m_pendingJudgment = new List<PendingJudgment>();
        private readonly Dictionary<long, double> m_meleeDedup = new Dictionary<long, double>();
        private SubsystemBodies m_bodies;
        private SubsystemTime m_time;
        private SubsystemGameInfo m_gameInfo;
        private SubsystemTerrain m_terrain;
        private SubsystemPlayers m_players;
        private LightningScene m_scene;
        private uint m_nextCastId = 1;
        private UltimateState m_ultimate;
        private bool m_applyingRetaliation;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { StormhammerAwakenedBlock.Index };
        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        private static readonly int[] s_drawOrders = { 105 };

        /// <summary>Lightning is drawn after entity models (99) and before glow points (110).</summary>
        public int[] DrawOrders => s_drawOrders;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_bodies = Project.FindSubsystem<SubsystemBodies>(true);
            m_time = Project.FindSubsystem<SubsystemTime>(true);
            m_gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
            m_terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_players = Project.FindSubsystem<SubsystemPlayers>(true);
            m_scene = new LightningScene();
            m_scene.Load(Project);
            AwakenedStormhammerVisuals.RegisterScene(Project, m_scene);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            MjolnirDisplay.ResetAll();
            m_scene?.Clear();
            m_thunderGod.Clear();
            m_domains.Clear();
            m_pendingJudgment.Clear();
            ClearEquipmentModifiers();
            AwakenedStormhammerVisuals.Reset(Project);
            base.Dispose();
        }

        /// <inheritdoc />
        public void Draw(Camera camera, int drawOrder)
        {
            if (m_scene == null || m_scene.ActiveBolts == 0)
            {
                return;
            }

            int scale = StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 1
                : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 2 : 4;
            m_scene.Draw(camera, scale);
        }

        internal float GetCooldownRemaining(ComponentPlayer player, StormhammerSkill skill)
        {
            if (player == null) return 0f;
            double[] values;
            return m_cooldowns.TryGetValue(player.Entity.EntityId, out values)
                ? MathUtils.Max(0f, (float)(values[(int)skill] - m_time.GameTime))
                : 0f;
        }

        /// <summary>Seconds left on the Thunder God window, or 0 when inactive.</summary>
        internal float GetThunderGodRemaining(ComponentPlayer player)
        {
            if (player == null) return 0f;
            ThunderGodState state;
            if (!m_thunderGod.TryGetValue(player.Entity.EntityId, out state)) return 0f;
            return MathUtils.Max(0f, (float)(state.EndTime - m_time.GameTime));
        }

        /// <summary>True while the caster's lightning damage is doubled.</summary>
        internal bool IsThunderGodActive(ComponentPlayer player)
        {
            return GetThunderGodRemaining(player) > 0f;
        }

        internal void RequestSkill(ComponentPlayer player, StormhammerSkill skill)
        {
            if (!IsEquipped(player) || GetCooldownRemaining(player, skill) > 0f) return;
            ushort sequence;
            m_sequences.TryGetValue(player.Entity.EntityId, out sequence);
            sequence++;
            m_sequences[player.Entity.EntityId] = sequence;
            Vector3 origin = player.ComponentCreatureModel.EyePosition;
            Vector3 direction = player.GameWidget.ActiveCamera.ViewDirection;
            if (CommonLib.WorkType == WorkType.Client)
            {
                CommonLib.Net.QueuePackage(StormhammerSkillPackage.CreateRequest(player, sequence, skill, origin, direction));
            }
            else
            {
                AcceptRequest(player, skill, origin, direction, 0);
            }
        }

        internal void AcceptRequest(ComponentPlayer player, StormhammerSkill skill, Vector3 claimedOrigin, Vector3 direction, ushort sequence)
        {
            if (CommonLib.WorkType == WorkType.Client || player == null || !IsEquipped(player) || !IsValidSkill(skill) || GetCooldownRemaining(player, skill) > 0f) return;
            ushort previous;
            if (m_sequences.TryGetValue(player.Entity.EntityId, out previous) && sequence != 0 && !IsNewer(sequence, previous)) return;
            if (sequence != 0) m_sequences[player.Entity.EntityId] = sequence;
            if (skill == StormhammerSkill.MyriadTribulation && m_ultimate != null) return;

            float cooldown = Cooldown(skill);
            uint castId = m_nextCastId++;
            SetCooldown(player.Entity.EntityId, skill, cooldown);
            Publish(Event(player, castId, skill, StormhammerSkillEvent.Accepted, Vector3.Zero, Vector3.Zero, cooldown));
            if (skill == StormhammerSkill.ThunderousJudgment)
            {
                Vector3 center = player.ComponentBody.BoundingBox.Center();
                Publish(Event(player, castId, StormhammerSkill.ThunderousJudgment, StormhammerSkillEvent.JudgmentCharge, center, Vector3.UnitY, 0.42f));
                m_pendingJudgment.Add(new PendingJudgment { Player = player, CastId = castId, ApplyAt = m_time.GameTime + 0.42 });
                player.ComponentMiner.DamageActiveTool(1);
            }
            else if (skill == StormhammerSkill.Dominion)
            {
                StartDomain(player, castId);
            }
            else
            {
                StartUltimate(player, castId, SafeDirection(direction, player.ComponentBody.Matrix.Forward));
            }
        }

        internal void ApplyNetworkEvent(StormhammerSkillPackage package)
        {
            if (package.Event == StormhammerSkillEvent.Accepted) SetCooldown(package.CasterId, package.Skill, package.Value);
            ApplyDomainEvent(package);
            AwakenedStormhammerVisuals.Apply(Project, package, m_scene, m_time.GameTime);
        }

        /// <summary>Called from the verified AttackBody hook before native armor/resilience processing.</summary>
        internal void PrepareAwakenedMelee(ComponentBody target, ComponentCreature attacker, Vector3 hitPoint, ref float attackPower, bool isMeleeAttack)
        {
            if (CommonLib.WorkType == WorkType.Client || !isMeleeAttack || target == null || attacker == null) return;
            ComponentPlayer player = attacker.Entity.FindComponent<ComponentPlayer>(false);
            if (player == null || !player.IsAddedToProject) return;
            int contents = Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue);
            float power;
            string cause;
            bool multiplied;
            if (contents == StormhammerAwakenedBlock.Index)
            {
                attackPower = PhysicalMeleePower;
                power = LightningMeleePower;
                cause = "妙尔尼尔·雷电追加";
                multiplied = true;
            }
            else if (contents == StormhammerBlock.Index)
            {
                // The fake hammer keeps its native melee (the loader floor of 12 stays) and
                // gains a fixed 5 point lightning add-on with no multiplier, ever.
                power = BaseHammerLightningPower;
                cause = "雷神之锤·伪·雷电追加";
                multiplied = false;
            }
            else
            {
                return;
            }

            long key = ((long)attacker.Entity.EntityId << 32) | target.Entity.EntityId;
            double last;
            if (m_meleeDedup.TryGetValue(key, out last) && m_time.GameTime - last < 0.20) return;
            m_meleeDedup[key] = m_time.GameTime;
            ComponentHealth health = target.Entity.FindComponent<ComponentHealth>(false);
            m_pendingLightning.Add(new PendingLightning
            {
                Attacker = attacker,
                Target = target,
                HitPoint = hitPoint,
                ApplyTime = m_time.GameTime + LightningDisplayDelay,
                PhysicalBefore = health != null ? health.Health : -1f,
                Power = power,
                Cause = cause,
                Multiplied = multiplied
            });
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            double now = m_time.GameTime;
            AwakenedStormhammerVisuals.Update(Project, now, m_scene);
            AwakenedStormhammerVisuals.DecayFlash(Project, dt);
            m_scene?.Update(now);
            MjolnirDisplay.Update(Project, now);
            ApplyEquipmentModifiers();
            UpdateAvatarSubscriptions();
            UpdateDomainVisuals(now);
            if (CommonLib.WorkType == WorkType.Client) return;
            UpdateThunderGod(now);
            ApplyPendingLightning(now);
            UpdatePendingJudgment(now);
            UpdateUltimate(now);
            UpdateDomains(now);
            ExpireDedup(now);
        }

        /// <summary>
        /// Grants the 12 s Thunder God window once per successful ultimate and refreshes the
        /// Thunderous Judgment cooldown exactly once. Re-casting overwrites the expiry instead of
        /// stacking, so the multiplier can never compound.
        /// </summary>
        private void ActivateThunderGod(ComponentPlayer player, uint castId)
        {
            ushort id = player.Entity.EntityId;
            m_thunderGod[id] = new ThunderGodState
            {
                Player = player,
                EntityId = id,
                EndTime = m_time.GameTime + ThunderGodDuration,
                JudgmentBoostUsed = false
            };

            // Refresh only the judgment cooldown; the ultimate keeps its own 55 s.
            SetCooldown(id, StormhammerSkill.ThunderousJudgment, 0f);
            Publish(Event(player, castId, StormhammerSkill.ThunderousJudgment, StormhammerSkillEvent.Accepted, player.ComponentBody.BoundingBox.Center(), Vector3.UnitY, 0f));
            Publish(Event(player, castId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.ThunderGodStart, player.ComponentBody.BoundingBox.Center(), Vector3.UnitY, ThunderGodDuration));
            Diagnostic("thunder god start cast=" + castId + " until=" + (m_time.GameTime + ThunderGodDuration).ToString("0.00"));
        }

        private void UpdateThunderGod(double now)
        {
            if (m_thunderGod.Count == 0) return;
            List<ushort> expired = null;
            foreach (KeyValuePair<ushort, ThunderGodState> pair in m_thunderGod)
            {
                ThunderGodState state = pair.Value;
                bool invalid = state.Player == null || !state.Player.IsAddedToProject
                    || state.Player.ComponentHealth == null || state.Player.ComponentHealth.Health <= 0f
                    || !IsEquipped(state.Player);
                if (!invalid && now < state.EndTime) continue;
                (expired ?? (expired = new List<ushort>())).Add(pair.Key);
                if (state.Player != null && state.Player.IsAddedToProject)
                {
                    Publish(Event(state.Player, 0u, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.ThunderGodEnd, state.Player.ComponentBody.BoundingBox.Center(), Vector3.Zero, 0f));
                }

                Diagnostic("thunder god end entity=" + pair.Key + " reason=" + (invalid ? "invalid" : "timeout"));
            }

            if (expired == null) return;
            foreach (ushort id in expired) m_thunderGod.Remove(id);
        }

        /// <summary>
        /// Applies Thunderous Judgment once the 0.42 s charge completes. The charge lives on the
        /// authoritative timeline, so the release visuals and the damage share one clock.
        /// </summary>
        private void UpdatePendingJudgment(double now)
        {
            for (int i = m_pendingJudgment.Count - 1; i >= 0; i--)
            {
                PendingJudgment pending = m_pendingJudgment[i];
                if (now < pending.ApplyAt) continue;
                m_pendingJudgment.RemoveAt(i);
                ComponentPlayer player = pending.Player;
                if (player == null || !player.IsAddedToProject || !IsEquipped(player)) continue;
                if (player.ComponentHealth == null || player.ComponentHealth.Health <= 0f) continue;
                Vector3 center = player.ComponentBody.BoundingBox.Center();
                ThunderGodState god;
                m_thunderGod.TryGetValue(player.Entity.EntityId, out god);
                bool boosted = god != null && now < god.EndTime;
                float multiplier = boosted ? ThunderGodPowerMultiplier : 1f;
                bool visualBoost = boosted && god != null && !god.JudgmentBoostUsed;
                if (visualBoost) god.JudgmentBoostUsed = true;

                HashSet<ComponentBody> struck = new HashSet<ComponentBody>();
                foreach (ComponentBody body in ValidTargets(center, LightningRules.JudgmentRadius, player))
                {
                    if (!struck.Add(body)) continue;
                    float distance = Vector3.Distance(center, body.BoundingBox.Center());
                    float power = LightningRules.JudgmentDamage(distance);
                    if (power <= 0f) continue;
                    DealAttackPower(player.ComponentMiner.ComponentCreature, body, power, "雷霆万钧", false, multiplier);
                }

                Publish(Event(player, pending.CastId, StormhammerSkill.ThunderousJudgment, StormhammerSkillEvent.JudgmentBurst, center, Vector3.UnitY, LightningRules.JudgmentRadius, visualBoost ? (byte)1 : (byte)0));
                Diagnostic("judgment burst cast=" + pending.CastId + " thunderGod=" + boosted + " x" + multiplier);
            }
        }

        private void StartUltimate(ComponentPlayer player, uint castId, Vector3 direction)
        {
            Vector3 aim = ResolveAimPoint(player.ComponentMiner, player.ComponentCreatureModel.EyePosition, direction);
            UltimateState state = new UltimateState { Caster = player, Center = aim, CastId = castId, StartTime = m_time.GameTime };
            m_ultimate = state;
            // The direction is frozen here: every client gets the same cone for the backdrop storm
            // even if the caster keeps turning afterwards.
            Publish(Event(player, castId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateStart, aim, direction, AwakenedStormhammerVisuals.StormReleaseTime));
            ActivateThunderGod(player, castId);
            Diagnostic("ultimate start cast=" + castId + " aim=" + aim.X.ToString("0.0") + "," + aim.Y.ToString("0.0") + "," + aim.Z.ToString("0.0"));
        }

        /// <summary>
        /// The V4 timeline: one authoritative main strike, then four bounded aftershock ticks.
        /// Every damage application happens exactly once and is keyed to the same clock the
        /// presentation uses, so no client can double-resolve it.
        /// </summary>
        private void UpdateUltimate(double now)
        {
            UltimateState state = m_ultimate;
            if (state == null) return;
            if (state.Caster == null || !state.Caster.IsAddedToProject)
            {
                m_ultimate = null;
                return;
            }
            if (state.Caster.ComponentHealth.Health <= 0f || !IsEquipped(state.Caster))
            {
                Publish(Event(state.Caster, state.CastId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateEnd, state.Center, Vector3.Zero));
                m_ultimate = null;
                return;
            }

            double elapsed = now - state.StartTime;
            if (!state.MainWarned && elapsed >= 0.02)
            {
                state.MainWarned = true;
                Publish(Event(state.Caster, state.CastId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateMainWarning, state.Center, Vector3.UnitY, LightningRules.TribulationRadius));
            }

            if (!state.MainStruck && elapsed >= AwakenedStormhammerVisuals.MainStrikeTime)
            {
                state.MainStruck = true;
                int hit = 0;
                foreach (ComponentBody body in ValidTargets(state.Center, LightningRules.TribulationRadius, state.Caster))
                {
                    float distance = Vector3.Distance(state.Center, body.BoundingBox.Center());
                    float power = LightningRules.TribulationDamage(distance);
                    if (power <= 0f) continue;
                    DealAttackPower(state.Caster.ComponentMiner.ComponentCreature, body, power, "终焉主雷", false);
                    hit++;
                }

                Publish(Event(state.Caster, state.CastId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateMainStrike, state.Center, Vector3.UnitY, 220f));
                state.NextAftershock = now + 1.0;
                Diagnostic("main strike cast=" + state.CastId + " hits=" + hit);
            }

            while (state.MainStruck && state.AftershockTicks < 4 && now >= state.NextAftershock)
            {
                foreach (ComponentBody body in ValidTargets(state.Center, LightningRules.TribulationRadius, state.Caster))
                {
                    DealAttackPower(state.Caster.ComponentMiner.ComponentCreature, body, LightningRules.AftershockDamage, "雷霆余震", false);
                }

                state.AftershockTicks++;
                Publish(Event(state.Caster, state.CastId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateAftershock, state.Center, Vector3.UnitY, LightningRules.AftershockDamage, (byte)state.AftershockTicks));
                state.NextAftershock += 1.0;
            }

            if (elapsed >= 10.8)
            {
                Publish(Event(state.Caster, state.CastId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateEnd, state.Center, Vector3.Zero));
                m_ultimate = null;
            }
        }

        private void ApplyPendingLightning(double now)
        {
            for (int i = m_pendingLightning.Count - 1; i >= 0; i--)
            {
                PendingLightning pending = m_pendingLightning[i];
                if (now < pending.ApplyTime) continue;
                m_pendingLightning.RemoveAt(i);
                if (pending.Attacker == null || !pending.Attacker.Entity.IsAddedToProject || pending.Target == null) continue;
                if (!IsLiveTarget(pending.Target)) continue;
                ComponentPlayer attackerPlayer = pending.Attacker.Entity.FindComponent<ComponentPlayer>(false);
                float multiplier = pending.Multiplied && IsThunderGodActive(attackerPlayer) ? ThunderGodPowerMultiplier : 1f;
                DealElementalPower(pending.Attacker, pending.Target, pending.Power, pending.Cause, multiplier);
                if (pending.Multiplied)
                {
                    StormhammerVisuals.EmitArc(Project, pending.Attacker.ComponentBody.BoundingBox.Center(), pending.HitPoint, 1.15f);
                    StormhammerVisuals.EmitBurst(Project, pending.HitPoint, 0.9f);
                }
                else
                {
                    // The fake copy cracks, it does not storm: a short single arc at the hit point.
                    StormhammerVisuals.EmitArc(Project, pending.HitPoint + Vector3.UnitY * 1.4f, pending.HitPoint, 0.55f);
                }
            }
        }

        private void ApplyEquipmentModifiers()
        {
            HashSet<ushort> seen = new HashSet<ushort>();
            foreach (ComponentPlayer player in m_players.ComponentPlayers)
            {
                if (player == null || !player.IsAddedToProject) continue;
                ushort id = player.Entity.EntityId;
                seen.Add(id);
                EquipmentState state;
                if (!m_equipment.TryGetValue(id, out state))
                {
                    state = new EquipmentState { Player = player };
                    m_equipment.Add(id, state);
                }
                ComponentLocomotion locomotion = player.ComponentLocomotion;
                locomotion.WalkSpeed -= state.WalkDelta;
                state.WalkDelta = 0f;
                bool enabled = HoldsHammer(player)
                    && !locomotion.IsCreativeFlyEnabled && player.ComponentRider.Mount == null;
                if (!enabled) continue;
                state.WalkDelta = MathUtils.Max(0f, locomotion.WalkSpeed) * (StormhammerEquipmentRules.MovementFactor - 1f);
                locomotion.WalkSpeed += state.WalkDelta;
            }
            List<ushort> remove = null;
            foreach (KeyValuePair<ushort, EquipmentState> pair in m_equipment)
                if (!seen.Contains(pair.Key)) (remove ?? (remove = new List<ushort>())).Add(pair.Key);
            if (remove == null) return;
            foreach (ushort id in remove)
            {
                EquipmentState state = m_equipment[id];
                if (state.Player != null && state.Player.IsAddedToProject) state.Player.ComponentLocomotion.WalkSpeed -= state.WalkDelta;
                m_equipment.Remove(id);
            }
        }

        private void ClearEquipmentModifiers()
        {
            foreach (EquipmentState state in m_equipment.Values)
                if (state.Player != null && state.Player.IsAddedToProject) state.Player.ComponentLocomotion.WalkSpeed -= state.WalkDelta;
            m_equipment.Clear();
        }

        internal static void ApplyAttackSpeed(ComponentMiner miner)
        {
            if (miner == null || Terrain.ExtractContents(miner.ActiveBlockValue) != StormhammerAwakenedBlock.Index) return;
            // Hit() writes m_lastHitTime before invoking OnMinerHit. Advancing that timestamp
            // by the exact interval delta changes only the next melee gate (0.66 / 1.10).
            miner.m_lastHitTime -= StormhammerEquipmentRules.LastHitTimeAdvance;
        }

        private void UpdateAvatarSubscriptions()
        {
            HashSet<ComponentHealth> seen = new HashSet<ComponentHealth>();
            foreach (ComponentBody body in m_bodies.Bodies)
            {
                ComponentPlayer player = body.Entity.FindComponent<ComponentPlayer>(false);
                if (!IsEquipped(player) || player.ComponentHealth.Health <= 0f) continue;
                ComponentHealth health = player.ComponentHealth;
                seen.Add(health);
                AvatarState state;
                if (!m_avatars.TryGetValue(health, out state))
                {
                    state = new AvatarState { Player = player, Health = health, LastHealth = health.Health };
                    state.Handler = attacker => OnAvatarAttacked(state, attacker);
                    m_avatars.Add(health, state);
                    health.Attacked += state.Handler;
                }
                state.LastHealth = health.Health;
            }
            List<ComponentHealth> remove = null;
            foreach (KeyValuePair<ComponentHealth, AvatarState> pair in m_avatars)
            {
                if (!seen.Contains(pair.Key)) (remove ?? (remove = new List<ComponentHealth>())).Add(pair.Key);
            }
            if (remove != null)
            {
                foreach (ComponentHealth health in remove)
                {
                    AvatarState state = m_avatars[health];
                    health.Attacked -= state.Handler;
                    m_avatars.Remove(health);
                }
            }
        }

        private void OnAvatarAttacked(AvatarState state, ComponentCreature attacker)
        {
            float before = state.LastHealth;
            float after = state.Health.Health;
            state.LastHealth = after;
            if (CommonLib.WorkType == WorkType.Client || !IsEquipped(state.Player) || m_applyingRetaliation || before <= after || attacker == null || attacker == state.Player.ComponentMiner.ComponentCreature) return;
            ComponentHealth target = attacker.ComponentHealth;
            if (target == null || target.Health <= 0f) return;
            m_applyingRetaliation = true;
            try
            {
                float targetBefore = target.Health;
                target.Injure(RetaliationPower / MathUtils.Max(target.AttackResilience, 0.001f), state.Player.ComponentMiner.ComponentCreature, false, "妙尔尼尔·雷电反伤");
                DiagnosticDamage("retaliation", state.Player.Entity.EntityId, attacker.Entity.EntityId, RetaliationPower, 1f, targetBefore, target.Health);
            }
            finally { m_applyingRetaliation = false; }
            StormhammerVisuals.EmitArc(Project, state.Player.ComponentBody.BoundingBox.Center(), attacker.ComponentBody.BoundingBox.Center(), 0.9f);
        }

        /// <summary>
        /// Native elemental damage. The multiplier is applied once here and never re-enters the
        /// buff path, so a doubled hit cannot be doubled again.
        /// </summary>
        private void DealElementalPower(ComponentCreature attacker, ComponentBody target, float basePower, string cause, float multiplier)
        {
            ComponentHealth health = target.Entity.FindComponent<ComponentHealth>(false);
            if (health == null || health.Health <= 0f) return;
            float before = health.Health;
            float finalPower = basePower * multiplier;
            health.Injure(finalPower / MathUtils.Max(health.AttackResilience, 0.001f), attacker, false, cause);
            DiagnosticDamage(cause, attacker.Entity.EntityId, target.Entity.EntityId, basePower, multiplier, before, health.Health);
            ShowHitValue(target, finalPower);
        }

        /// <summary>
        /// health.Injure is the authoritative path but never draws a damage number: hit values
        /// are spawned by ComponentMiner's own melee code, which this add-on deliberately
        /// bypasses. This reproduces the native pattern (ComponentMiner lines 447-458): queue
        /// the package for remote clients, then add the particle locally.
        /// </summary>
        private void ShowHitValue(ComponentBody target, float finalPower)
        {
            if (CommonLib.WorkType == WorkType.Client) return;
            Vector3 position = target.BoundingBox.Center() + new Vector3(0f, 0.55f, 0f);
            // Slight upward drift with a touch of the attacker's motion, matching the native look.
            Vector3 velocity = new Vector3(0.35f, 1.6f, 0.35f);
            string text = ((int)MathUtils.Round(finalPower)).ToString();
            Color color = new Color(150, 220, 255, 255);
            CommonLib.Net.QueuePackage(new ComponentHealthPackage(position, velocity, color, text));
            HitValueParticleSystem system = new HitValueParticleSystem(position, velocity, color, text);
            Project.FindSubsystem<SubsystemParticles>(throwOnError: true).AddParticleSystem(system);
        }

        /// <summary>Native attack path (impulse + armor). The multiplier is applied once, before the call.</summary>
        private void DealAttackPower(ComponentCreature attacker, ComponentBody target, float basePower, string source, bool melee, float multiplier = 1f)
        {
            ComponentHealth health = target.Entity.FindComponent<ComponentHealth>(false);
            float before = health != null ? health.Health : -1f;
            float finalPower = basePower * multiplier;
            Vector3 direction = target.BoundingBox.Center() - attacker.ComponentBody.BoundingBox.Center() + Vector3.UnitY * 0.12f;
            if (direction.LengthSquared() < 0.01f) direction = Vector3.UnitY;
            ComponentMiner.AttackBody(target, attacker, target.BoundingBox.Center(), Vector3.Normalize(direction), finalPower, melee);
            if (health != null) DiagnosticDamage(source, attacker.Entity.EntityId, target.Entity.EntityId, basePower, multiplier, before, health.Health);
        }

        /// <summary>雷霆王域: bound to the caster, 7 s, one tick per whole second, never multiplied.</summary>
        private void StartDomain(ComponentPlayer player, uint castId)
        {
            ushort id = player.Entity.EntityId;
            m_domains[id] = new DomainState { Caster = player, EntityId = id, CastId = castId, StartTime = m_time.GameTime };
            Vector3 center = player.ComponentBody.BoundingBox.Center();
            Publish(Event(player, castId, StormhammerSkill.Dominion, StormhammerSkillEvent.DomainStart, center, Vector3.UnitY, StormhammerDomainRules.Duration, (byte)StormhammerDomainRules.TickCount));
            PublishRune(center, 0.8f, StormhammerDomainRules.Radius, 0.55);
            Diagnostic("domain start cast=" + castId);
        }

        private void UpdateDomains(double now)
        {
            if (m_domains.Count == 0) return;
            List<ushort> ended = null;
            foreach (KeyValuePair<ushort, DomainState> pair in m_domains)
            {
                DomainState state = pair.Value;
                ComponentPlayer caster = state.Caster;
                bool alive = caster != null && caster.IsAddedToProject && caster.ComponentHealth != null && caster.ComponentHealth.Health > 0f;
                double elapsed = now - state.StartTime;
                if (StormhammerDomainRules.ShouldEnd(elapsed, alive, alive && IsEquipped(caster)))
                {
                    if (caster != null && caster.IsAddedToProject)
                        Publish(Event(caster, state.CastId, StormhammerSkill.Dominion, StormhammerSkillEvent.DomainEnd, caster.ComponentBody.BoundingBox.Center(), Vector3.Zero));
                    (ended ?? (ended = new List<ushort>())).Add(pair.Key);
                    continue;
                }
                int due = StormhammerDomainRules.TicksDue(elapsed);
                while (state.Ticks < due && state.Ticks < StormhammerDomainRules.TickCount)
                {
                    state.Ticks++;
                    TickDomain(caster, state, state.Ticks);
                }
            }
            if (ended == null) return;
            foreach (ushort id in ended) m_domains.Remove(id);
        }

        /// <summary>
        /// One damage cycle. The centre is sampled from the living caster, so the field follows
        /// the player; every struck body is deduplicated inside the cycle and the power is a
        /// fixed 12 with no Thunder God multiplier.
        /// </summary>
        private void TickDomain(ComponentPlayer caster, DomainState state, int tick)
        {
            Vector3 center = caster.ComponentBody.BoundingBox.Center();
            HashSet<ComponentBody> struck = new HashSet<ComponentBody>();
            int published = 0;
            foreach (ComponentBody body in ValidHostileTargets(center, StormhammerDomainRules.Radius, caster))
            {
                if (!struck.Add(body)) continue;
                DealAttackPower(caster.ComponentMiner.ComponentCreature, body, StormhammerDomainRules.TickPower, "雷霆王域", false, 1f);
                if (published < 12)
                {
                    published++;
                    Publish(Event(caster, state.CastId, StormhammerSkill.Dominion, StormhammerSkillEvent.DomainTick,
                        body.BoundingBox.Center(), Vector3.UnitY, StormhammerDomainRules.Radius, (byte)tick, body.Entity.EntityId));
                }
            }
            Diagnostic("domain tick cast=" + state.CastId + " tick=" + tick + " hits=" + struck.Count);
        }

        /// <summary>Runs for every peer: the ring and its three rotating arcs follow the caster locally.</summary>
        private void UpdateDomainVisuals(double now)
        {
            if (m_domains.Count == 0) return;
            List<ushort> expired = null;
            foreach (KeyValuePair<ushort, DomainState> pair in m_domains)
            {
                DomainState state = pair.Value;
                ComponentPlayer caster = state.Caster;
                if (caster == null || !caster.IsAddedToProject || caster.ComponentHealth == null || caster.ComponentHealth.Health <= 0f) continue;
                double elapsed = now - state.StartTime;
                // Defence in depth: a client that missed the end event still drops the visual state.
                if (elapsed >= StormhammerDomainRules.Duration) { expired = expired ?? new List<ushort>(); expired.Add(pair.Key); continue; }
                if (!StormhammerDomainRules.Active(elapsed)) continue;
                float radius = StormhammerDomainRules.DrawnRadius(elapsed);
                if (radius <= 0.4f) continue;
                Vector3 center = caster.ComponentBody.BoundingBox.Center();
                bool platinum = IsThunderGodActive(caster);
                Color core = platinum ? new Color(255, 248, 224) : new Color(232, 248, 255);
                Color halo = platinum ? new Color(255, 228, 158, 200) : new Color(86, 172, 255, 190);
                if (now >= state.NextRing)
                {
                    state.NextRing = now + 0.55;
                    m_scene.SpawnRune(center, MathUtils.Max(0.6f, radius * 0.93f), radius, now, 0.52, halo);
                }
                if (now >= state.NextArc)
                {
                    state.NextArc = now + 0.14;
                    int arcs = StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 1 : 3;
                    for (int i = 0; i < arcs; i++)
                    {
                        float angle = (float)(now * 1.1) + i * 6.283185f / arcs;
                        Vector3 radial = new Vector3(MathUtils.Cos(angle), 0f, MathUtils.Sin(angle));
                        Vector3 start = center + radial * (radius * 0.45f) + Vector3.UnitY * 0.10f;
                        Vector3 end = center + radial * (radius + 0.25f) + Vector3.UnitY * 0.04f;
                        uint seed = LightningRules.Mix(state.CastId, (uint)(i * 7919 + (int)(now * 60)));
                        m_scene.Spawn(LightningBoltKind.Radial, start, end, seed, now, 0.15, 0.05f, 0.17f, core, halo, 1, 0.8f, false);
                    }
                }
            }
            if (expired == null) return;
            foreach (ushort id in expired) m_domains.Remove(id);
        }

        /// <summary>Short arc on one struck target, shared by the authority and remote clients.</summary>
        private void DomainTickArc(Vector3 position)
        {
            if (m_scene == null) return;
            double now = m_time.GameTime;
            m_scene.Spawn(LightningBoltKind.Near, position + Vector3.UnitY * 3.4f, position,
                LightningRules.Mix((uint)(now * 60.0), (uint)(position.X * 13f + position.Z * 7f)), now, 0.18,
                0.045f, 0.13f, new Color(238, 250, 255), new Color(94, 178, 255, 190), 1, 0.5f, true);
        }

        private void PublishRune(Vector3 center, float startRadius, float endRadius, double life)
        {
            if (m_scene == null) return;
            m_scene.SpawnRune(center, startRadius, endRadius, m_time.GameTime, life, new Color(120, 200, 255, 200));
        }

        /// <summary>
        /// Hostile-only variant used by automatic area effects: neutral animals, livestock and
        /// tamed creatures are never struck, matching the shared targeting rule.
        /// </summary>
        private IEnumerable<ComponentBody> ValidHostileTargets(Vector3 center, float radius, ComponentPlayer caster)
        {
            float radiusSquared = radius * radius;
            foreach (ComponentBody body in m_bodies.Bodies)
            {
                if (body == caster.ComponentBody || Vector3.DistanceSquared(center, body.BoundingBox.Center()) > radiusSquared) continue;
                ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>(false);
                if (creature == null || creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0f) continue;
                if (!SCIEPort.Features.Targeting.CreatureTargetRules.IsHostile((int)creature.Category, body.Player != null,
                    false, false, m_gameInfo.WorldSettings.IsFriendlyFireEnabled)) continue;
                yield return body;
            }
        }

        /// <summary>
        /// Client-side domain lifecycle. The start event rebuilds the local visual state from the
        /// caster entity, so a late or remote viewer still sees the ring follow the caster.
        /// </summary>
        private void ApplyDomainEvent(StormhammerSkillPackage package)
        {
            if (package.Event == StormhammerSkillEvent.DomainStart)
            {
                ComponentPlayer caster = FindPlayer(package.CasterId);
                if (caster == null) return;
                m_domains[package.CasterId] = new DomainState
                {
                    Caster = caster,
                    EntityId = package.CasterId,
                    CastId = package.CastId,
                    StartTime = m_time.GameTime
                };
                PublishRune(package.Position, 0.8f, StormhammerDomainRules.Radius, 0.55);
                return;
            }
            if (package.Event == StormhammerSkillEvent.DomainEnd)
            {
                m_domains.Remove(package.CasterId);
                return;
            }
            if (package.Event == StormhammerSkillEvent.DomainTick && package.TargetId != 0)
            {
                DomainTickArc(package.Position);
            }
        }

        private ComponentPlayer FindPlayer(ushort id)
        {
            foreach (ComponentPlayer player in m_players.ComponentPlayers)
                if (player.Entity.EntityId == id) return player;
            return null;
        }

        private IEnumerable<ComponentBody> ValidTargets(Vector3 center, float radius, ComponentPlayer caster)
        {
            float radiusSquared = radius * radius;
            foreach (ComponentBody body in m_bodies.Bodies)
            {
                if (body == caster.ComponentBody || Vector3.DistanceSquared(center, body.BoundingBox.Center()) > radiusSquared) continue;
                ComponentCreature creature = body.Entity.FindComponent<ComponentCreature>(false);
                if (creature == null || creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0f) continue;
                if (body.Player != null && !m_gameInfo.WorldSettings.IsFriendlyFireEnabled) continue;
                yield return body;
            }
        }

        private static bool IsLiveTarget(ComponentBody body)
        {
            if (body == null || !body.Entity.IsAddedToProject) return false;
            ComponentHealth health = body.Entity.FindComponent<ComponentHealth>(false);
            return health != null && health.Health > 0f;
        }


        private void Publish(StormhammerSkillPackage package)
        {
            ApplyNetworkEvent(package);
            if (CommonLib.WorkType == WorkType.Server) CommonLib.Net.QueuePackage(package);
        }

        private StormhammerSkillPackage Event(ComponentPlayer caster, uint castId, StormhammerSkill skill, StormhammerSkillEvent kind, Vector3 position, Vector3 direction, float value = 0f, byte step = 0, ushort targetId = 0)
            => StormhammerSkillPackage.CreateEvent(caster.Entity.EntityId, castId, m_time.GameTime, skill, kind, position, direction, value, step, targetId);

        private void SetCooldown(ushort casterId, StormhammerSkill skill, float duration)
        {
            double[] values;
            if (!m_cooldowns.TryGetValue(casterId, out values))
            {
                values = new double[4];
                m_cooldowns.Add(casterId, values);
            }
            values[(int)skill] = m_time.GameTime + duration;
        }

        private void ExpireDedup(double now)
        {
            if (m_meleeDedup.Count < 32) return;
            List<long> remove = new List<long>();
            foreach (KeyValuePair<long, double> pair in m_meleeDedup) if (now - pair.Value > 2.0) remove.Add(pair.Key);
            foreach (long key in remove) m_meleeDedup.Remove(key);
        }

        internal static bool IsEquipped(ComponentPlayer player) => player != null && player.IsAddedToProject && Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue) == StormhammerAwakenedBlock.Index;
        /// <summary>Both hammer forms carry the artifact default +20% ground movement.</summary>
        internal static bool HoldsHammer(ComponentPlayer player)
        {
            if (player == null || !player.IsAddedToProject || player.ComponentHealth.Health <= 0f) return false;
            int contents = Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue);
            return contents == StormhammerAwakenedBlock.Index || contents == StormhammerBlock.Index;
        }
        private static bool IsValidSkill(StormhammerSkill skill) => skill == StormhammerSkill.ThunderousJudgment || skill == StormhammerSkill.MyriadTribulation || skill == StormhammerSkill.Dominion;
        private static float Cooldown(StormhammerSkill skill) => skill == StormhammerSkill.ThunderousJudgment ? JudgmentCooldown : skill == StormhammerSkill.Dominion ? StormhammerDomainRules.Cooldown : UltimateCooldown;
        private static bool IsNewer(ushort value, ushort previous) => value != previous && (ushort)(value - previous) < 32768;

        private static Vector3 SafeDirection(Vector3 value, Vector3 fallback)
        {
            if (value.LengthSquared() < 0.25f || float.IsNaN(value.X) || float.IsNaN(value.Y) || float.IsNaN(value.Z)) value = fallback;
            return Vector3.Normalize(value);
        }

        /// <summary>
        /// Resolves the one legal landing point for the tribulation bolt.
        /// 1) a terrain hit inside the aim range (never through a wall),
        /// 2) otherwise the first walkable column along the frozen forward line between 10 and 16
        ///    blocks that is not behind solid rock,
        /// 3) otherwise a clamped point in front of the caster so the cast always resolves.
        /// </summary>
        private Vector3 ResolveAimPoint(ComponentMiner miner, Vector3 origin, Vector3 direction)
        {
            Vector3 forward = SafeDirection(direction, Vector3.UnitZ);
            Ray3 ray = new Ray3(origin, forward);
            TerrainRaycastResult? terrain = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction, true, false, false);
            if (terrain.HasValue)
            {
                float distance = Vector3.Distance(origin, terrain.Value.HitPoint());
                if (distance >= 2f && distance <= 16f) return terrain.Value.HitPoint();
            }

            SubsystemTerrain terrainSubsystem = m_terrain;
            if (terrainSubsystem == null) return origin + forward * 12f;
            for (float step = 10f; step <= 16f; step += 1f)
            {
                Vector3 sample = origin + forward * step;
                int x = Terrain.ToCell(sample.X);
                int z = Terrain.ToCell(sample.Z);
                TerrainChunk chunk = terrainSubsystem.Terrain.GetChunkAtCell(x, z);
                if (chunk == null || chunk.State < TerrainChunkState.InvalidLight) continue;
                int startY = MathUtils.Clamp(Terrain.ToCell(sample.Y), 2, 250);
                if (Terrain.ExtractContents(terrainSubsystem.Terrain.GetCellValue(x, startY, z)) != 0)
                {
                    // Blocked by a wall: do not tunnel through it.
                    break;
                }

                for (int y = startY; y > 1; y--)
                {
                    if (Terrain.ExtractContents(terrainSubsystem.Terrain.GetCellValue(x, y - 1, z)) != 0)
                    {
                        return new Vector3(sample.X, y, sample.Z);
                    }
                }
            }

            return origin + forward * 12f;
        }

        private static void Diagnostic(string text)
        {
            if (StormhammerOptions.Diagnostics) Log.Information("[SCIEPort Skywrath V3.2] " + text);
        }

        private static void DiagnosticDamage(string source, ushort attacker, ushort target, float basePower, float multiplier, float before, float after)
        {
            if (!StormhammerOptions.Diagnostics) return;
            Log.Information(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "[SCIEPort Skywrath V5] {0} attacker={1} target={2} base={3:0.##} x{4:0.##} final={5:0.##} health={6:0.####}->{7:0.####}",
                source, attacker, target, basePower, multiplier, basePower * multiplier, before, after));
        }
    }
}
