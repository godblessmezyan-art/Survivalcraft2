using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>Authoritative Skywrath V2 three-skill state machine.</summary>
    public sealed class SubsystemAwakenedStormhammerBlockBehavior : SubsystemBlockBehavior, IUpdateable
    {
        private sealed class DomainState
        {
            public ComponentPlayer Caster;
            public double EndTime;
            public double NextTick;
            public double NextVisual;
        }

        private sealed class UltimateState
        {
            public ComponentPlayer Caster;
            public Vector3 Center;
            public double StartTime;
            public int WarningStep = -1;
            public int StrikeStep = -1;
            public double NextAftershock;
        }

        private readonly Dictionary<ushort, double[]> m_cooldowns = new Dictionary<ushort, double[]>();
        private readonly Dictionary<ushort, ushort> m_sequences = new Dictionary<ushort, ushort>();
        private readonly Dictionary<ComponentBody, double> m_marks = new Dictionary<ComponentBody, double>();
        private readonly List<DomainState> m_domains = new List<DomainState>();
        private SubsystemBodies m_bodies;
        private SubsystemTime m_time;
        private SubsystemAudio m_audio;
        private UltimateState m_ultimate;

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
            m_audio = Project.FindSubsystem<SubsystemAudio>(true);
        }

        internal float GetCooldownRemaining(ComponentPlayer player, StormhammerSkill skill)
        {
            if (player == null)
            {
                return 0f;
            }

            double[] values;
            if (!m_cooldowns.TryGetValue(player.Entity.EntityId, out values))
            {
                return 0f;
            }

            return MathUtils.Max(0f, (float)(values[(int)skill] - m_time.GameTime));
        }

        internal void RequestSkill(ComponentPlayer player, StormhammerSkill skill, float charge)
        {
            if (!IsEquipped(player) || GetCooldownRemaining(player, skill) > 0f)
            {
                return;
            }

            ushort sequence;
            m_sequences.TryGetValue(player.Entity.EntityId, out sequence);
            sequence++;
            m_sequences[player.Entity.EntityId] = sequence;
            Vector3 origin = player.ComponentCreatureModel.EyePosition;
            Vector3 direction = player.GameWidget.ActiveCamera.ViewDirection;
            if (CommonLib.WorkType == WorkType.Client)
            {
                m_sequences[player.Entity.EntityId] = sequence;
                CommonLib.Net.QueuePackage(StormhammerSkillPackage.CreateRequest(player, sequence, skill, origin, direction, charge));
            }
            else
            {
                AcceptRequest(player, skill, origin, direction, charge, 0);
            }
        }

        internal void AcceptRequest(ComponentPlayer player, StormhammerSkill skill, Vector3 claimedOrigin, Vector3 direction, float charge, ushort sequence)
        {
            if (CommonLib.WorkType == WorkType.Client || player == null || !IsEquipped(player) || !IsValidSkill(skill) || GetCooldownRemaining(player, skill) > 0f)
            {
                return;
            }

            ushort previous;
            if (m_sequences.TryGetValue(player.Entity.EntityId, out previous) && sequence != 0 && !IsNewer(sequence, previous))
            {
                return;
            }
            if (sequence != 0)
            {
                m_sequences[player.Entity.EntityId] = sequence;
            }

            direction = SafeDirection(direction, Matrix.CreateFromQuaternion(player.ComponentCreatureModel.EyeRotation).Forward);
            float cooldown = Cooldown(skill);
            if (skill == StormhammerSkill.MyriadTribulation && m_ultimate != null)
            {
                player.ComponentGui.DisplaySmallMessage("万象雷劫正在演出中。", Color.Cyan, false, false);
                return;
            }

            SetCooldown(player.Entity.EntityId, skill, cooldown);
            Publish(StormhammerSkillPackage.CreateEvent(player.Entity.EntityId, skill, StormhammerSkillEvent.Accepted, Vector3.Zero, Vector3.Zero, cooldown));
            switch (skill)
            {
                case StormhammerSkill.ThunderLance:
                    CastLance(player, direction, MathUtils.Clamp(charge, 0.35f, 2.2f));
                    break;
                case StormhammerSkill.ThunderDomain:
                    StartDomain(player);
                    break;
                case StormhammerSkill.MyriadTribulation:
                    StartUltimate(player, direction);
                    break;
            }
        }

        internal void ApplyNetworkEvent(StormhammerSkillPackage package)
        {
            if (package.Event == StormhammerSkillEvent.Accepted)
            {
                SetCooldown(package.CasterId, package.Skill, package.Value);
            }
            AwakenedStormhammerVisuals.Apply(Project, package);
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            double now = m_time.GameTime;
            AwakenedStormhammerVisuals.Update(Project, now);
            ExpireMarks(now);
            if (CommonLib.WorkType == WorkType.Client)
            {
                return;
            }

            UpdateDomains(now);
            UpdateUltimate(now);
        }

        private void CastLance(ComponentPlayer player, Vector3 direction, float charge)
        {
            ComponentMiner miner = player.ComponentMiner;
            Ray3 ray = new Ray3(player.ComponentCreatureModel.EyePosition, direction);
            BodyRaycastResult? bodyHit = miner.Raycast<BodyRaycastResult>(ray, RaycastMode.Interaction, true, true, false);
            TerrainRaycastResult? terrainHit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction, true, false, false);
            Vector3 target = ray.Position + direction * 18f;
            ComponentBody first = null;
            if (bodyHit.HasValue)
            {
                first = bodyHit.Value.ComponentBody;
                target = bodyHit.Value.HitPoint();
            }
            else if (terrainHit.HasValue)
            {
                target = terrainHit.Value.HitPoint();
            }

            float factor = MathUtils.Saturate((charge - 0.35f) / 1.85f);
            float damage = MathUtils.Lerp(45f, 80f, factor);
            Publish(StormhammerSkillPackage.CreateEvent(player.Entity.EntityId, StormhammerSkill.ThunderLance, StormhammerSkillEvent.LanceFlight, ray.Position, target, factor));
            if (first == null)
            {
                return;
            }

            List<ComponentBody> struck = new List<ComponentBody> { first };
            Damage(player, first, target, damage);
            Mark(first);
            Publish(StormhammerSkillPackage.CreateEvent(player.Entity.EntityId, StormhammerSkill.ThunderLance, StormhammerSkillEvent.LanceImpact, target, target, damage, 0));
            Vector3 from = target;
            for (byte link = 1; link <= 4; link++)
            {
                ComponentBody next = FindNearest(from, player.ComponentBody, struck, 7.5f);
                if (next == null)
                {
                    break;
                }
                Vector3 point = next.BoundingBox.Center();
                float chainDamage = damage * (0.62f - 0.07f * (link - 1));
                Damage(player, next, point, chainDamage);
                Mark(next);
                Publish(StormhammerSkillPackage.CreateEvent(player.Entity.EntityId, StormhammerSkill.ThunderLance, StormhammerSkillEvent.LanceImpact, from, point, chainDamage, link));
                struck.Add(next);
                from = point;
            }
            miner.DamageActiveTool(1);
        }

        private void StartDomain(ComponentPlayer player)
        {
            double now = m_time.GameTime;
            m_domains.RemoveAll(d => d.Caster == player);
            m_domains.Add(new DomainState { Caster = player, EndTime = now + 5.0, NextTick = now, NextVisual = now });
            LimitedKnockback(player, 5f);
            Publish(StormhammerSkillPackage.CreateEvent(player.Entity.EntityId, StormhammerSkill.ThunderDomain, StormhammerSkillEvent.DomainStart, player.ComponentBody.Position, Vector3.Zero, 5f));
        }

        private void StartUltimate(ComponentPlayer player, Vector3 direction)
        {
            Vector3 center = ResolveAimPoint(player.ComponentMiner, player.ComponentCreatureModel.EyePosition, direction, 24f);
            m_ultimate = new UltimateState { Caster = player, Center = center, StartTime = m_time.GameTime, NextAftershock = double.MaxValue };
            Publish(StormhammerSkillPackage.CreateEvent(player.Entity.EntityId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateStart, center, Vector3.Zero, 6f));
        }

        private void UpdateDomains(double now)
        {
            for (int i = m_domains.Count - 1; i >= 0; i--)
            {
                DomainState domain = m_domains[i];
                if (domain.Caster == null || !domain.Caster.IsAddedToProject || now >= domain.EndTime)
                {
                    m_domains.RemoveAt(i);
                    continue;
                }
                Vector3 center = domain.Caster.ComponentBody.Position;
                if (now >= domain.NextTick)
                {
                    foreach (ComponentBody body in CreaturesInRadius(center, 5f, domain.Caster.ComponentBody))
                    {
                        float power = IsMarked(body, now) ? 7.2f : 6f;
                        Damage(domain.Caster, body, body.BoundingBox.Center(), power);
                    }
                    domain.NextTick += 1.0;
                }
                if (now >= domain.NextVisual)
                {
                    Publish(StormhammerSkillPackage.CreateEvent(domain.Caster.Entity.EntityId, StormhammerSkill.ThunderDomain, StormhammerSkillEvent.DomainStart, center, Vector3.Zero, (float)(domain.EndTime - now), 1));
                    domain.NextVisual = now + 0.32;
                }
            }
        }

        private void UpdateUltimate(double now)
        {
            if (m_ultimate == null)
            {
                return;
            }
            if (m_ultimate.Caster == null || !m_ultimate.Caster.IsAddedToProject)
            {
                m_ultimate = null;
                return;
            }
            double elapsed = now - m_ultimate.StartTime;
            double[] strikeTimes = { 0.95, 1.68, 2.41, 3.14, 3.87, 4.60, 5.40 };
            for (int step = 0; step < strikeTimes.Length; step++)
            {
                if (m_ultimate.WarningStep < step && elapsed >= strikeTimes[step] - 0.58)
                {
                    m_ultimate.WarningStep = step;
                    Vector3 point = UltimatePoint(m_ultimate.Center, step);
                    Publish(StormhammerSkillPackage.CreateEvent(m_ultimate.Caster.Entity.EntityId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateWarning, point, Vector3.Zero, step == 6 ? 2.8f : 1.8f, (byte)step));
                }
                if (m_ultimate.StrikeStep < step && elapsed >= strikeTimes[step])
                {
                    m_ultimate.StrikeStep = step;
                    bool main = step == 6;
                    Vector3 point = UltimatePoint(m_ultimate.Center, step);
                    ApplyAreaDamage(m_ultimate.Caster, point, main ? 4.8f : 2.4f, main ? 110f : 25f, main);
                    Publish(StormhammerSkillPackage.CreateEvent(m_ultimate.Caster.Entity.EntityId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateStrike, point, Vector3.Zero, main ? 110f : 25f, (byte)step));
                    if (main)
                    {
                        m_ultimate.NextAftershock = now + 1.0;
                    }
                }
            }

            if (m_ultimate.StrikeStep == 6 && elapsed < 8.4 && now >= m_ultimate.NextAftershock)
            {
                ApplyAreaDamage(m_ultimate.Caster, m_ultimate.Center, 4.5f, 8f, false);
                Publish(StormhammerSkillPackage.CreateEvent(m_ultimate.Caster.Entity.EntityId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateWarning, m_ultimate.Center, Vector3.Zero, 4.5f, 7));
                m_ultimate.NextAftershock += 1.0;
            }
            if (elapsed >= 8.4)
            {
                Publish(StormhammerSkillPackage.CreateEvent(m_ultimate.Caster.Entity.EntityId, StormhammerSkill.MyriadTribulation, StormhammerSkillEvent.UltimateEnd, m_ultimate.Center, Vector3.Zero, 0f));
                m_ultimate = null;
            }
        }

        private void ApplyAreaDamage(ComponentPlayer caster, Vector3 center, float radius, float power, bool falloff)
        {
            foreach (ComponentBody body in CreaturesInRadius(center, radius, caster.ComponentBody))
            {
                float actual = power;
                if (falloff)
                {
                    float distance = Vector3.Distance(center, body.BoundingBox.Center());
                    actual = distance <= 2f ? power : MathUtils.Lerp(power, power * 0.5f, MathUtils.Saturate((distance - 2f) / (radius - 2f)));
                }
                Damage(caster, body, body.BoundingBox.Center(), actual);
            }
        }

        private IEnumerable<ComponentBody> CreaturesInRadius(Vector3 center, float radius, ComponentBody excluded)
        {
            float radiusSquared = radius * radius;
            foreach (ComponentBody body in m_bodies.Bodies)
            {
                if (body != excluded && body.Entity.FindComponent<ComponentCreature>(false) != null && Vector3.DistanceSquared(center, body.BoundingBox.Center()) <= radiusSquared)
                {
                    yield return body;
                }
            }
        }

        private ComponentBody FindNearest(Vector3 center, ComponentBody owner, List<ComponentBody> excluded, float radius)
        {
            ComponentBody best = null;
            float bestDistance = radius * radius;
            foreach (ComponentBody body in m_bodies.Bodies)
            {
                float distance = Vector3.DistanceSquared(center, body.BoundingBox.Center());
                if (body != owner && !excluded.Contains(body) && body.Entity.FindComponent<ComponentCreature>(false) != null && distance < bestDistance)
                {
                    best = body;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void LimitedKnockback(ComponentPlayer player, float radius)
        {
            Vector3 center = player.ComponentBody.Position;
            foreach (ComponentBody body in CreaturesInRadius(center, radius, player.ComponentBody))
            {
                Vector3 delta = body.BoundingBox.Center() - center;
                delta.Y = 0.18f;
                float length = MathUtils.Max(delta.Length(), 0.5f);
                body.ApplyImpulse(delta / length * MathUtils.Min(body.Mass * 2.2f, 140f));
            }
        }

        private void Mark(ComponentBody body) => m_marks[body] = m_time.GameTime + 6.0;

        private bool IsMarked(ComponentBody body, double now)
        {
            double expiry;
            return m_marks.TryGetValue(body, out expiry) && expiry > now;
        }

        private void ExpireMarks(double now)
        {
            List<ComponentBody> expired = null;
            foreach (KeyValuePair<ComponentBody, double> pair in m_marks)
            {
                if (pair.Value <= now || pair.Key.Entity.Project == null)
                {
                    (expired ?? (expired = new List<ComponentBody>())).Add(pair.Key);
                }
            }
            if (expired != null)
            {
                foreach (ComponentBody body in expired) m_marks.Remove(body);
            }
        }

        private void Publish(StormhammerSkillPackage package)
        {
            ApplyNetworkEvent(package);
            if (CommonLib.WorkType == WorkType.Server)
            {
                CommonLib.Net.QueuePackage(package);
            }
        }

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

        private static bool IsEquipped(ComponentPlayer player) => player != null && Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue) == StormhammerAwakenedBlock.Index;
        private static bool IsValidSkill(StormhammerSkill skill) => skill >= StormhammerSkill.ThunderLance && skill <= StormhammerSkill.MyriadTribulation;
        private static float Cooldown(StormhammerSkill skill) => skill == StormhammerSkill.ThunderLance ? 9f : skill == StormhammerSkill.ThunderDomain ? 18f : 60f;
        private static bool IsNewer(ushort value, ushort previous) => value != previous && (ushort)(value - previous) < 32768;

        private static Vector3 SafeDirection(Vector3 value, Vector3 fallback)
        {
            if (value.LengthSquared() < 0.25f || float.IsNaN(value.X) || float.IsNaN(value.Y) || float.IsNaN(value.Z))
            {
                value = fallback;
            }
            return Vector3.Normalize(value);
        }

        private static Vector3 ResolveAimPoint(ComponentMiner miner, Vector3 origin, Vector3 direction, float range)
        {
            Ray3 ray = new Ray3(origin, SafeDirection(direction, Vector3.UnitZ));
            TerrainRaycastResult? terrain = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction, true, false, false);
            return terrain.HasValue && Vector3.DistanceSquared(origin, terrain.Value.HitPoint()) <= range * range ? terrain.Value.HitPoint() : origin + ray.Direction * range;
        }

        private static Vector3 UltimatePoint(Vector3 center, int step)
        {
            if (step >= 6) return center;
            float angle = step * MathUtils.PI / 3f + 0.35f;
            float radius = 4f + (step % 2) * 3f;
            return center + new Vector3(MathUtils.Cos(angle) * radius, 0f, MathUtils.Sin(angle) * radius);
        }

        private static void Damage(ComponentPlayer caster, ComponentBody target, Vector3 hitPoint, float power)
        {
            Vector3 direction = target.BoundingBox.Center() - caster.ComponentBody.BoundingBox.Center() + Vector3.UnitY * 0.12f;
            if (direction.LengthSquared() < 0.01f) direction = Vector3.UnitY;
            ComponentMiner.AttackBody(target, caster.ComponentMiner.ComponentCreature, hitPoint, Vector3.Normalize(direction), power, false);
        }
    }
}
