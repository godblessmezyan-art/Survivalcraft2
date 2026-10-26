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
    public sealed class SubsystemAwakenedStormhammerBlockBehavior : SubsystemBlockBehavior, IUpdateable
    {
        internal const float PhysicalMeleePower = 40f;
        internal const float LightningMeleePower = 25f;
        internal const float RetaliationPower = 5f;
        internal const float JudgmentCooldown = 12f;
        internal const float UltimateCooldown = 55f;

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

        private sealed class UltimateTarget
        {
            public ComponentBody Body;
            public ushort EntityId;
            public double WarningTime;
            public double StrikeTime;
            public bool Warned;
            public bool Struck;
        }

        private sealed class UltimateState
        {
            public ComponentPlayer Caster;
            public Vector3 Center;
            public uint CastId;
            public double StartTime;
            public readonly List<UltimateTarget> Targets = new List<UltimateTarget>();
            public bool MainWarned;
            public bool MainStruck;
            public int AftershockTicks;
            public double NextAftershock;
        }

        private readonly Dictionary<ushort, double[]> m_cooldowns = new Dictionary<ushort, double[]>();
        private readonly Dictionary<ushort, ushort> m_sequences = new Dictionary<ushort, ushort>();
        private readonly Dictionary<ComponentHealth, AvatarState> m_avatars = new Dictionary<ComponentHealth, AvatarState>();
        private readonly List<PendingLightning> m_pendingLightning = new List<PendingLightning>();
        private readonly Dictionary<long, double> m_meleeDedup = new Dictionary<long, double>();
        private SubsystemBodies m_bodies;
        private SubsystemTime m_time;
        private SubsystemGameInfo m_gameInfo;
        private uint m_nextCastId = 1;
        private UltimateState m_ultimate;
        private bool m_applyingRetaliation;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { StormhammerAwakenedBlock.Index };
        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_bodies = Project.FindSubsystem<SubsystemBodies>(true);
            m_time = Project.FindSubsystem<SubsystemTime>(true);
            m_gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
        }

        internal float GetCooldownRemaining(ComponentPlayer player, StormhammerSkill skill)
        {
            if (player == null) return 0f;
            double[] values;
            return m_cooldowns.TryGetValue(player.Entity.EntityId, out values)
                ? MathUtils.Max(0f, (float)(values[(int)skill] - m_time.GameTime))
                : 0f;
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
            if (skill == StormhammerSkill.ThunderousJudgment) CastJudgment(player, castId);
            else StartUltimate(player, castId, SafeDirection(direction, player.ComponentBody.Matrix.Forward));
        }

        internal void ApplyNetworkEvent(StormhammerSkillPackage package)
        {
            if (package.Event == StormhammerSkillEvent.Accepted) SetCooldown(package.CasterId, package.Skill, package.Value);
            AwakenedStormhammerVisuals.Apply(Project, package);
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
            AwakenedStormhammerVisuals.Update(Project, now);
            UpdateAvatarSubscriptions();
            if (CommonLib.WorkType == WorkType.Client) return;
            ApplyPendingLightning(now);
            UpdateUltimate(now);
            ExpireDedup(now);
        }

        private void CastJudgment(ComponentPlayer player, uint castId)
        {
            Vector3 center = player.ComponentBody.BoundingBox.Center();
            Publish(Event(player, castId, StormhammerSkill.ThunderousJudgment, StormhammerSkillEvent.JudgmentCharge, center, Vector3.UnitY, 0.42f));
            HashSet<ComponentBody> struck = new HashSet<ComponentBody>();
            foreach (ComponentBody body in ValidTargets(center, 9f, player))
            {
                if (!struck.Add(body)) continue;
                float distance = Vector3.Distance(center, body.BoundingBox.Center());
                float power = distance <= 4f ? 110f : MathUtils.Lerp(110f, 70f, MathUtils.Saturate((distance - 4f) / 5f));
                DealAttackPower(player.ComponentMiner.ComponentCreature, body, power, "雷霆万钧", false);
            }
            Publish(Event(player, castId, StormhammerSkill.ThunderousJudgment, StormhammerSkillEvent.JudgmentBurst, center, Vector3.UnitY, 9f));
            player.ComponentMiner.DamageActiveTool(1);
        }

        private void StartUltimate(ComponentPlayer player, uint castId, Vector3 direction)
        {
            Vector3 center = ResolveAimPoint(player.ComponentMiner, player.ComponentCreatureModel.EyePosition, direction, 24f);
            UltimateState state = new UltimateState { Caster = player, Center = center, CastId = castId, StartTime = m_time.GameTime };
            int index = 0;
            foreach (ComponentBody body in ValidTargets(center, 24f, player))
            {
                if (index >= 24) break;
                state.Targets.Add(new UltimateTarget
                {
                    Body = body,
                    EntityId = body.Entity.EntityId,
                    WarningTime = 1.55 + index * 0.075,
                    StrikeTime = 2.30 + index * 0.11
                });
                index++;
            }
            m_ultimate = state;
            Publish(Event(player, castId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateStart, center, Vector3.UnitY, 10f));
            Diagnostic("ultimate start cast=" + castId + " targets=" + state.Targets.Count);
        }

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
            for (int i = 0; i < state.Targets.Count; i++)
            {
                UltimateTarget target = state.Targets[i];
                if (!target.Warned && elapsed >= target.WarningTime)
                {
                    target.Warned = true;
                    Vector3 p = TargetPoint(target, state.Center);
                    Publish(Event(state.Caster, state.CastId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateLock, p, Vector3.UnitY, 0.75f, (byte)i, target.EntityId));
                }
                if (!target.Struck && elapsed >= target.StrikeTime)
                {
                    target.Struck = true;
                    Vector3 p = TargetPoint(target, state.Center);
                    if (IsLiveTarget(target.Body)) DealAttackPower(state.Caster.ComponentMiner.ComponentCreature, target.Body, 130f, "万象雷劫", false);
                    Publish(Event(state.Caster, state.CastId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateStrike, p, Vector3.UnitY, 130f, (byte)i, target.EntityId));
                }
            }
            if (!state.MainWarned && elapsed >= 5.05)
            {
                state.MainWarned = true;
                Publish(Event(state.Caster, state.CastId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateMainWarning, state.Center, Vector3.UnitY, 7f));
            }
            if (!state.MainStruck && elapsed >= 5.70)
            {
                state.MainStruck = true;
                foreach (ComponentBody body in ValidTargets(state.Center, 7f, state.Caster))
                {
                    float distance = Vector3.Distance(state.Center, body.BoundingBox.Center());
                    DealAttackPower(state.Caster.ComponentMiner.ComponentCreature, body, distance <= 2.2f ? 220f : 100f, "终焉主雷", false);
                }
                Publish(Event(state.Caster, state.CastId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateMainStrike, state.Center, Vector3.UnitY, 220f));
                state.NextAftershock = now + 1.0;
            }
            while (state.MainStruck && state.AftershockTicks < 4 && now >= state.NextAftershock)
            {
                foreach (ComponentBody body in ValidTargets(state.Center, 7f, state.Caster)) DealAttackPower(state.Caster.ComponentMiner.ComponentCreature, body, 18f, "雷霆余震", false);
                state.AftershockTicks++;
                Publish(Event(state.Caster, state.CastId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateAftershock, state.Center, Vector3.UnitY, 18f, (byte)state.AftershockTicks));
                state.NextAftershock += 1.0;
            }
            if (elapsed >= 10.2)
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
                if (health != null) DiagnosticDamage("physical", pending.Attacker.Entity.EntityId, pending.Target.Entity.EntityId, PhysicalMeleePower, pending.PhysicalBefore, health.Health);
                if (!IsLiveTarget(pending.Target)) continue;
                DealElementalPower(pending.Attacker, pending.Target, LightningMeleePower, "苍穹之怒·雷电追加");
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
                target.Injure(RetaliationPower / MathUtils.Max(target.AttackResilience, 0.001f), state.Player.ComponentMiner.ComponentCreature, false, "苍穹之怒·雷电反伤");
                DiagnosticDamage("retaliation", state.Player.Entity.EntityId, attacker.Entity.EntityId, RetaliationPower, targetBefore, target.Health);
            }
            finally { m_applyingRetaliation = false; }
            StormhammerVisuals.EmitArc(Project, state.Player.ComponentBody.BoundingBox.Center(), attacker.ComponentBody.BoundingBox.Center(), 0.9f);
        }

        private void DealElementalPower(ComponentCreature attacker, ComponentBody target, float power, string cause)
        {
            ComponentHealth health = target.Entity.FindComponent<ComponentHealth>(false);
            if (health == null || health.Health <= 0f) return;
            float before = health.Health;
            health.Injure(power / MathUtils.Max(health.AttackResilience, 0.001f), attacker, false, cause);
            DiagnosticDamage("lightning", attacker.Entity.EntityId, target.Entity.EntityId, power, before, health.Health);
        }

        private void DealAttackPower(ComponentCreature attacker, ComponentBody target, float power, string source, bool melee)
        {
            ComponentHealth health = target.Entity.FindComponent<ComponentHealth>(false);
            float before = health != null ? health.Health : -1f;
            Vector3 direction = target.BoundingBox.Center() - attacker.ComponentBody.BoundingBox.Center() + Vector3.UnitY * 0.12f;
            if (direction.LengthSquared() < 0.01f) direction = Vector3.UnitY;
            ComponentMiner.AttackBody(target, attacker, target.BoundingBox.Center(), Vector3.Normalize(direction), power, melee);
            if (health != null) DiagnosticDamage(source, attacker.Entity.EntityId, target.Entity.EntityId, power, before, health.Health);
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

        private static Vector3 TargetPoint(UltimateTarget target, Vector3 fallback) => IsLiveTarget(target.Body) ? target.Body.BoundingBox.Center() : fallback;

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

        private static Vector3 ResolveAimPoint(ComponentMiner miner, Vector3 origin, Vector3 direction, float range)
        {
            Ray3 ray = new Ray3(origin, SafeDirection(direction, Vector3.UnitZ));
            TerrainRaycastResult? terrain = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction, true, false, false);
            return terrain.HasValue && Vector3.DistanceSquared(origin, terrain.Value.HitPoint()) <= range * range ? terrain.Value.HitPoint() : origin + ray.Direction * range;
        }

        private static void Diagnostic(string text)
        {
            if (StormhammerOptions.Diagnostics) Log.Information("[SCIEPort Skywrath V3.2] " + text);
        }

        private static void DiagnosticDamage(string source, ushort attacker, ushort target, float power, float before, float after)
        {
            if (StormhammerOptions.Diagnostics) Log.Information(string.Format(System.Globalization.CultureInfo.InvariantCulture, "[SCIEPort Skywrath V3.2] {0} attacker={1} target={2} power={3:0.##} health={4:0.####}->{5:0.####}", source, attacker, target, power, before, after));
        }
    }
}
