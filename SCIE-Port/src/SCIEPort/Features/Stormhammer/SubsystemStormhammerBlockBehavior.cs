using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>Charge, visual lightning strike and bounded chain damage behavior.</summary>
    public sealed class SubsystemStormhammerBlockBehavior : SubsystemBlockBehavior
    {
        private readonly Dictionary<ComponentMiner, double> m_chargeStart = new Dictionary<ComponentMiner, double>();
        private readonly Dictionary<ComponentMiner, double> m_lastChargePulse = new Dictionary<ComponentMiner, double>();
        private SubsystemTime m_time;
        private SubsystemAudio m_audio;
        private SubsystemBodies m_bodies;

        /// <inheritdoc />
        public override int[] HandledBlocks => new[] { StormhammerBlock.Index };

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_time = Project.FindSubsystem<SubsystemTime>(true);
            m_audio = Project.FindSubsystem<SubsystemAudio>(true);
            m_bodies = Project.FindSubsystem<SubsystemBodies>(true);
        }

        /// <inheritdoc />
        public override bool OnAim(Ray3 aim, ComponentMiner miner, AimState state)
        {
            if (state == AimState.Cancelled)
            {
                m_chargeStart.Remove(miner);
                m_lastChargePulse.Remove(miner);
                return true;
            }

            if (state == AimState.InProgress)
            {
                double start;
                if (!m_chargeStart.TryGetValue(miner, out start))
                {
                    start = m_time.GameTime;
                    m_chargeStart.Add(miner, start);
                    m_lastChargePulse[miner] = start - 1.0;
                    m_audio.PlaySound("Audio/Stormhammer/Charge", 0.72f, 0f, miner.ComponentCreature.ComponentBody.Position, 4f, false);
                }

                double last;
                m_lastChargePulse.TryGetValue(miner, out last);
                if (m_time.GameTime - last >= 0.09)
                {
                    Vector3 source = miner.ComponentCreature.ComponentCreatureModel.EyePosition;
                    Vector3 end = source + Vector3.Normalize(aim.Direction) * 0.8f;
                    StormhammerVisuals.EmitArc(Project, source, end, 0.55f);
                    m_lastChargePulse[miner] = m_time.GameTime;
                }

                return true;
            }

            double chargeStart;
            double charge = m_chargeStart.TryGetValue(miner, out chargeStart) ? m_time.GameTime - chargeStart : 0.0;
            m_chargeStart.Remove(miner);
            m_lastChargePulse.Remove(miner);
            if (charge < 0.55)
            {
                miner.ComponentPlayer?.ComponentGui.DisplaySmallMessage("雷神之锤需要蓄力。", Color.Cyan, false, false);
                return true;
            }

            if (CommonLib.WorkType == WorkType.Client)
            {
                miner.ComponentPlayer?.ComponentGui.DisplaySmallMessage("召雷伤害需由主机执行；客户端同步待真机验证。", Color.Cyan, false, false);
                return true;
            }

            ExecuteStrike(aim, miner, MathUtils.Clamp((float)charge, 0.55f, 1.8f));
            return true;
        }

        private void ExecuteStrike(Ray3 aim, ComponentMiner miner, float charge)
        {
            BodyRaycastResult? bodyHit = miner.Raycast<BodyRaycastResult>(aim, RaycastMode.Digging, true, true, false);
            TerrainRaycastResult? terrainHit = miner.Raycast<TerrainRaycastResult>(aim, RaycastMode.Digging, true, false, false);
            Vector3 target = aim.Position + Vector3.Normalize(aim.Direction) * 8f;
            ComponentBody firstBody = null;
            if (bodyHit.HasValue)
            {
                firstBody = bodyHit.Value.ComponentBody;
                target = bodyHit.Value.HitPoint();
            }
            else if (terrainHit.HasValue)
            {
                target = terrainHit.Value.HitPoint();
            }

            Vector3 skyStart = target + Vector3.UnitY * 10f;
            StormhammerVisuals.EmitArc(Project, skyStart, target, 1.4f);
            StormhammerVisuals.EmitBurst(Project, target, 1.25f);
            m_audio.PlaySound("Audio/Stormhammer/Thunder", 1f, -0.06f, target, 12f, false);

            List<ComponentBody> struck = new List<ComponentBody>();
            if (firstBody != null)
            {
                Damage(miner, firstBody, target, 9f + charge * 5f);
                struck.Add(firstBody);
            }

            Vector3 chainStart = target;
            for (int link = 0; link < 3; link++)
            {
                ComponentBody next = FindNearestChainTarget(chainStart, miner.ComponentCreature.ComponentBody, struck, 6f);
                if (next == null)
                {
                    break;
                }

                Vector3 nextPoint = next.BoundingBox.Center();
                StormhammerVisuals.EmitArc(Project, chainStart, nextPoint, 1f - link * 0.18f);
                StormhammerVisuals.EmitBurst(Project, nextPoint, 0.8f);
                Damage(miner, next, nextPoint, 6f - link);
                struck.Add(next);
                chainStart = nextPoint;
            }

            miner.DamageActiveTool(1);
        }

        private ComponentBody FindNearestChainTarget(Vector3 position, ComponentBody owner, List<ComponentBody> excluded, float radius)
        {
            ComponentBody best = null;
            float bestDistance = radius * radius;
            foreach (ComponentBody body in m_bodies.Bodies)
            {
                if (body == owner || excluded.Contains(body) || body.Entity.FindComponent<ComponentCreature>() == null)
                {
                    continue;
                }

                float distance = Vector3.DistanceSquared(position, body.BoundingBox.Center());
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = body;
                }
            }

            return best;
        }

        private static void Damage(ComponentMiner miner, ComponentBody target, Vector3 hitPoint, float power)
        {
            Vector3 direction = Vector3.Normalize(target.BoundingBox.Center() - miner.ComponentCreature.ComponentBody.BoundingBox.Center() + Vector3.UnitY * 0.15f);
            ComponentMiner.AttackBody(target, miner.ComponentCreature, hitPoint, direction, power, false);
        }

        internal static void OnMeleeHit(ComponentMiner miner, ComponentBody body, Vector3 hitPoint)
        {
            Project project = miner.Project;
            StormhammerVisuals.EmitArc(project, miner.ComponentCreature.ComponentCreatureModel.EyePosition, hitPoint, 0.75f);
            StormhammerVisuals.EmitBurst(project, hitPoint, 0.55f);
            SubsystemAudio audio = project.FindSubsystem<SubsystemAudio>(false);
            if (audio != null)
            {
                audio.PlaySound("Audio/Stormhammer/Swing", 0.82f, 0f, body.Position, 4f, false);
            }
        }
    }
}
