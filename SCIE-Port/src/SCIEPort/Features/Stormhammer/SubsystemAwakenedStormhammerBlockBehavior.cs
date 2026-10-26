using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
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
        }

        private sealed class AvatarState
        {
            public ComponentPlayer Player;
            public ComponentHealth Health;
            public float LastHealth;
            public Action<ComponentCreature> Handler;
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
        private readonly Dictionary<ushort, ushort> m_sequences = new Dictionary<ushort, ushort>();
        private readonly Dictionary<ComponentHealth, AvatarState> m_avatars = new Dictionary<ComponentHealth, AvatarState>();
        private readonly List<PendingLightning> m_pendingLightning = new List<PendingLightning>();
        private readonly List<PendingJudgment> m_pendingJudgment = new List<PendingJudgment>();
        private readonly Dictionary<long, double> m_meleeDedup = new Dictionary<long, double>();
        private SubsystemBodies m_bodies;
        private SubsystemTime m_time;
        private SubsystemGameInfo m_gameInfo;
        private SubsystemTerrain m_terrain;
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
            m_scene = new LightningScene();
            m_scene.Load(Project);
            AwakenedStormhammerVisuals.RegisterScene(Project, m_scene);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            m_scene?.Clear();
            m_thunderGod.Clear();
            m_pendingJudgment.Clear();
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
            else
            {
                StartUltimate(player, castId, SafeDirection(direction, player.ComponentBody.Matrix.Forward));
            }
        }

        internal void ApplyNetworkEvent(StormhammerSkillPackage package)
        {
            if (package.Event == StormhammerSkillEvent.Accepted) SetCooldown(package.CasterId, package.Skill, package.Value);
            AwakenedStormhammerVisuals.Apply(Project, package, m_scene, m_time.GameTime);
        }

        /// <summary>Called from the verified AttackBody hook before native armor/resilience processing.</summary>
        internal void PrepareAwakenedMelee(ComponentBody target, ComponentCreature attacker, Vector3 hitPoint, ref float attackPower, bool isMeleeAttack)
        {
            if (CommonLib.WorkType == WorkType.Client || !isMeleeAttack || target == null || attacker == null) return;
            ComponentPlayer player = attacker.Entity.FindComponent<ComponentPlayer>(false);
            if (!IsEquipped(player)) return;
            attackPower = PhysicalMeleePower;
            long key = ((long)attacker.Entity.EntityId << 32) | target.Entity.EntityId;
            double last;
            if (m_meleeDedup.TryGetValue(key, out last) && m_time.GameTime - last < 0.20) return;
            m_meleeDedup[key] = m_time.GameTime;
            ComponentHealth health = target.Entity.FindComponent<ComponentHealth>(false);
            m_pendingLightning.Add(new PendingLightning { Attacker = attacker, Target = target, HitPoint = hitPoint, ApplyTime = m_time.GameTime + 0.001, PhysicalBefore = health != null ? health.Health : -1f });
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            double now = m_time.GameTime;
            AwakenedStormhammerVisuals.Update(Project, now, m_scene);
            AwakenedStormhammerVisuals.DecayFlash(Project, dt);
            m_scene?.Update(now);
            UpdateAvatarSubscriptions();
            if (CommonLib.WorkType == WorkType.Client) return;
            UpdateThunderGod(now);
            ApplyPendingLightning(now);
            UpdatePendingJudgment(now);
            UpdateUltimate(now);
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
                ComponentHealth health = pending.Target.Entity.FindComponent<ComponentHealth>(false);
                if (health != null) DiagnosticDamage("physical", pending.Attacker.Entity.EntityId, pending.Target.Entity.EntityId, PhysicalMeleePower, 1f, pending.PhysicalBefore, health.Health);
                if (!IsLiveTarget(pending.Target)) continue;
                ComponentPlayer attackerPlayer = pending.Attacker.Entity.FindComponent<ComponentPlayer>(false);
                float multiplier = IsThunderGodActive(attackerPlayer) ? ThunderGodPowerMultiplier : 1f;
                DealElementalPower(pending.Attacker, pending.Target, LightningMeleePower, "妙尔尼尔·雷电追加", multiplier);
                StormhammerVisuals.EmitArc(Project, pending.Attacker.ComponentBody.BoundingBox.Center(), pending.HitPoint, 1.15f);
                StormhammerVisuals.EmitBurst(Project, pending.HitPoint, 0.9f);
            }
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
                values = new double[3];
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
        private static bool IsValidSkill(StormhammerSkill skill) => skill == StormhammerSkill.ThunderousJudgment || skill == StormhammerSkill.MyriadTribulation;
        private static float Cooldown(StormhammerSkill skill) => skill == StormhammerSkill.ThunderousJudgment ? JudgmentCooldown : UltimateCooldown;
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
