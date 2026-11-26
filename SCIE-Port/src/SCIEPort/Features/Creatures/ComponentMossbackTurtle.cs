#pragma warning disable CS1591
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Creatures
{
    public sealed class ComponentMossbackTurtle : Component, IUpdateable
    {
        private readonly Game.Random random = new Game.Random();
        private ComponentCreature creature;
        private ComponentBody body;
        private ComponentLocomotion locomotion;
        private ComponentLargeWildlifeModel model;
        private SubsystemTerrain terrain;
        private SubsystemTime clock;
        private SubsystemTimeOfDay daylight;
        private SubsystemGameInfo game;
        private Vector3? shore, water, wander;
        private ComponentPlayer aggressor;
        private Vector2 direction;
        private double thinkAt, wanderAt, guardedUntil, pushedAt, retreatUntil, repeatUntil, counterAt, poseAgain;
        private int blows;
        private float pace;
        private byte pose;
        private bool returning;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        internal bool Guarding => pose == 1 && clock.GameTime < guardedUntil;

        public override void Load(ValuesDictionary values, IdToEntityMap map)
        {
            base.Load(values, map);
            creature = Entity.FindComponent<ComponentCreature>(true);
            body = Entity.FindComponent<ComponentBody>(true);
            locomotion = Entity.FindComponent<ComponentLocomotion>(true);
            model = Entity.FindComponent<ComponentLargeWildlifeModel>(true);
            terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            clock = Project.FindSubsystem<SubsystemTime>(true);
            daylight = Project.FindSubsystem<SubsystemTimeOfDay>(true);
            game = Project.FindSubsystem<SubsystemGameInfo>(true);
            if (values.GetValue<bool>("HasShore", false)) shore = values.GetValue<Vector3>("Shore");
            creature.ComponentHealth.Attacked += OnAttacked;
        }

        public override void Save(ValuesDictionary values, EntityToIdMap map)
        {
            base.Save(values, map);
            values.SetValue("HasShore", shore.HasValue);
            if (shore.HasValue) values.SetValue("Shore", shore.Value);
        }

        public void Update(float dt)
        {
            if (CommonLib.WorkType == WorkType.Client || creature.ComponentHealth.Health <= 0f || dt <= 0f) return;
            if (!shore.HasValue)
            {
                if (!body.StandingOnValue.HasValue) return;
                shore = body.Position;
            }

            double now = clock.GameTime;
            if (now >= thinkAt)
            {
                thinkAt = now + random.Float(.26f, .42f);
                Plan(now);
            }
            if (direction.LengthSquared() < .5f) return;
            Vector2 forward = body.Matrix.Forward.XZ;
            float angle = Vector2.Angle(forward, direction);
            locomotion.TurnOrder = new Vector2(MathUtils.Clamp(angle, -1f, 1f), 0f);
            if (pace > 0f && MathUtils.Abs(angle) < .53f)
                locomotion.WalkOrder = new Vector2(0f, pace * (1f - .5f * MathUtils.Abs(angle)));
        }

        private void OnAttacked(ComponentCreature attacker)
        {
            if (CommonLib.WorkType == WorkType.Client || creature.ComponentHealth.Health <= 0f) return;
            ComponentPlayer player = attacker as ComponentPlayer;
            double now = clock.GameTime;
            guardedUntil = now + 2.8;
            if (player != null && game.WorldSettings.GameMode != GameMode.Harmless
                && game.WorldSettings.GameMode != GameMode.Creative && now >= retreatUntil)
            {
                blows = ReferenceEquals(player, aggressor) && now <= repeatUntil ? blows + 1 : 1;
                aggressor = player;
                repeatUntil = now + 4.5;
                if (blows == 2 && now >= pushedAt)
                {
                    counterAt = now + .58;
                    thinkAt = now;
                }
            }
            if (pose != 2) UpdatePose(1, now);
        }

        private void Plan(double now)
        {
            direction = Vector2.Zero;
            pace = 0f;
            if (counterAt > 0)
            {
                if (ValidAggressor())
                {
                    Vector2 towards = (aggressor.ComponentBody.Position - body.Position).XZ;
                    if (towards.LengthSquared() > .001f) direction = Vector2.Normalize(towards);
                    if (now < counterAt) return;
                    Counter(towards, now);
                    return;
                }
                counterAt = 0;
            }
            if (pose == 2 && now < pushedAt) return;
            if (now < guardedUntil && now >= retreatUntil)
            {
                UpdatePose(1, now);
                return;
            }
            UpdatePose(0, now);

            Vector3 position = body.Position;
            if (now < retreatUntil || ArthropodHabitat.IsNight(daylight.TimeOfDay))
            {
                if (!water.HasValue && now >= wanderAt)
                {
                    water = LargeWildlifeHabitat.FindTurtleWater(terrain.Terrain, shore.Value);
                    wanderAt = now + 3.2;
                }
                if (water.HasValue)
                {
                    returning = true;
                    if ((water.Value - position).XZ.LengthSquared() <= 1.02f || AimNear(water.Value, .68f)) return;
                    if (!ValidAggressor() || now >= retreatUntil) return;
                }
                if (now < retreatUntil && ValidAggressor())
                    AimNear(position + (position - aggressor.ComponentBody.Position), .76f);
                return;
            }
            if (returning)
            {
                if (AimNear(shore.Value, .58f)) return;
                Vector2 remaining = (shore.Value - position).XZ;
                if (remaining.LengthSquared() < 1.12f) returning = false;
                else return;
            }
            if (now >= wanderAt)
            {
                Vector2 offset = random.Vector2(random.Float(1.5f, 4.1f));
                Vector3 spot = shore.Value + new Vector3(offset.X, 0f, offset.Y);
                wander = LargeWildlifeHabitat.TurtleFooting(terrain.Terrain, spot, false, out _) ? spot : (Vector3?)null;
                wanderAt = now + random.Float(9f, 16f);
            }
            if (wander.HasValue && now < wanderAt - 4.0)
                AimNear(wander.Value, .43f);
        }

        private bool ValidAggressor()
            => aggressor != null && aggressor.Entity.IsAddedToProject && aggressor.ComponentHealth.Health > 0f;

        private void Counter(Vector2 toward, double now)
        {
            counterAt = 0;
            pushedAt = now + .48;
            retreatUntil = now + 11.0;
            guardedUntil = 0;
            UpdatePose(2, now);
            ComponentBody target = aggressor.ComponentBody;
            if (toward.LengthSquared() > 5.76f || toward.LengthSquared() < .002f ||
                MathUtils.Abs(target.Position.Y - body.Position.Y) > 1.5f ||
                Vector2.Dot(body.Matrix.Forward.XZ, direction) < .66f) return;
            Vector3 contact = target.BoundingBox.Center();
            if (terrain.Raycast(body.Position + new Vector3(0f, .78f, 0f), contact, false, true,
                (value, distance) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable).HasValue) return;
            ComponentMiner.AttackBody(target, creature, contact, new Vector3(direction.X, 0f, direction.Y), 2.5f, true);
        }

        private bool AimNear(Vector3 goal, float speed)
        {
            Vector3 center = body.Position;
            Vector2 offset = (goal - center).XZ;
            if (offset.LengthSquared() <= 1.02f) return false;
            Vector2 desired = Vector2.Normalize(offset);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float turn = attempt == 0 ? 0f : (attempt == 1 ? .64f : -.64f);
                float sine = MathUtils.Sin(turn), cosine = MathUtils.Cos(turn);
                Vector2 candidate = new Vector2(desired.X * cosine - desired.Y * sine, desired.X * sine + desired.Y * cosine);
                Vector3 probe = center + new Vector3(candidate.X, 0f, candidate.Y) * .74f;
                if (!LargeWildlifeHabitat.TurtleFooting(terrain.Terrain, probe, true, out _)) continue;
                direction = candidate;
                pace = speed;
                return true;
            }
            return false;
        }

        private void UpdatePose(byte next, double now)
        {
            if (next == pose && (next != 1 || now < poseAgain)) return;
            pose = next;
            poseAgain = now + 1.6;
            model.SetTurtlePose(next);
            if (CommonLib.WorkType == WorkType.Server)
                CommonLib.Net?.QueuePackage(new LargeWildlifePosePackage { EntityId = Entity.EntityId, Pose = next });
        }
    }
}
