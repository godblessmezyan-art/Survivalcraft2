#pragma warning disable CS1591
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Creatures
{
    public sealed class ComponentCloudwingRay : Component, IUpdateable
    {
        private enum Flight { Cruise, Approach, Perched, Climb }
        private readonly Game.Random random = new Game.Random();
        private ComponentCreature creature;
        private ComponentBody body;
        private ComponentLocomotion locomotion;
        private SubsystemTerrain terrain;
        private SubsystemPlayers players;
        private SubsystemTime clock;
        private Vector3? home;
        private Vector3 perch, order;
        private Vector2 facing;
        private Flight state;
        private double thinkAt, landingAt, stageUntil;
        private float angle, radius;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary values, IdToEntityMap map)
        {
            base.Load(values, map);
            creature = Entity.FindComponent<ComponentCreature>(true);
            body = Entity.FindComponent<ComponentBody>(true);
            locomotion = Entity.FindComponent<ComponentLocomotion>(true);
            terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            players = Project.FindSubsystem<SubsystemPlayers>(true);
            clock = Project.FindSubsystem<SubsystemTime>(true);
            if (values.GetValue<bool>("HasRange", false)) home = values.GetValue<Vector3>("Range");
            creature.ComponentHealth.Attacked += OnAttacked;
        }

        public override void Save(ValuesDictionary values, EntityToIdMap map)
        {
            base.Save(values, map);
            values.SetValue("HasRange", home.HasValue);
            if (home.HasValue) values.SetValue("Range", home.Value);
        }

        public void Update(float dt)
        {
            if (CommonLib.WorkType == WorkType.Client || creature.ComponentHealth.Health <= 0f || dt <= 0f) return;
            double now = clock.GameTime;
            if (radius == 0f)
            {
                if (!home.HasValue)
                    home = body.Position - Vector3.UnitY * LargeWildlifeHabitat.RayLaunchHeight;
                angle = body.Rotation.ToYawPitchRoll().X;
                radius = random.Float(12f, 19f);
                landingAt = now + random.Float(18f, 32f);
                BeginClimb(now);
            }
            if (now >= thinkAt)
            {
                thinkAt = now + random.Float(.37f, .53f);
                Plan(now);
            }

            if (state == Flight.Perched) return;
            locomotion.FlyOrder = order;
            if (facing.LengthSquared() > .25f)
                locomotion.TurnOrder = new Vector2(
                    MathUtils.Clamp(Vector2.Angle(body.Matrix.Forward.XZ, facing), -.72f, .72f), 0f);
        }

        private void OnAttacked(ComponentCreature attacker)
        {
            if (CommonLib.WorkType != WorkType.Client && creature.ComponentHealth.Health > 0f
                && (state == Flight.Perched || state == Flight.Approach)) BeginClimb(clock.GameTime);
        }

        private void Plan(double now)
        {
            Vector3 pos = body.Position;
            if (state == Flight.Perched)
            {
                if (now < stageUntil && (!body.StandingOnValue.HasValue ? now < stageUntil - 5 : !PlayerNear(pos, 3f))) return;
                BeginClimb(now);
            }
            if (state == Flight.Climb)
            {
                if (now < stageUntil && pos.Y < 247f)
                {
                    order = new Vector3(0, .64f, 0);
                    return;
                }
                state = Flight.Cruise;
            }
            if (state == Flight.Approach)
            {
                if (Approach(now, pos)) return;
                BeginClimb(now);
                return;
            }
            if (now >= landingAt)
            {
                landingAt = now + random.Float(24f, 40f);
                if (FindPerch())
                {
                    stageUntil = now + 22;
                    state = Flight.Approach;
                    if (Approach(now, pos)) return;
                    BeginClimb(now);
                    return;
                }
            }

            Vector3 center = home.Value;
            Vector3 turn = center + new Vector3(MathUtils.Cos(angle) * radius, 8f, MathUtils.Sin(angle) * radius);
            if ((turn - pos).XZ.LengthSquared() < 12f)
            {
                angle = MathUtils.Remainder(angle + .94f, MathUtils.PI * 2f);
                turn = center + new Vector3(MathUtils.Cos(angle) * radius, 8f, MathUtils.Sin(angle) * radius);
            }
            Vector2 course = Unit((turn - pos).XZ);
            if (!TryCorridor(pos, course, out float height))
            {
                course = Unit((center - pos).XZ);
                if (!TryCorridor(pos, course, out height))
                {
                    order = new Vector3(0f, pos.Y < 245f ? .36f : 0f, 0f);
                    return;
                }
            }
            float altitude = MathUtils.Min(247f, MathUtils.Max(turn.Y, height + 6.8f));
            float glide = pos.Y < height + 3.5f ? .25f : .86f;
            SetCourse(course, glide, MathUtils.Clamp((altitude - pos.Y) * .19f, -.36f, .60f));
        }

        private bool FindPerch()
        {
            Vector3 center = home.Value;
            for (int i = 0; i < 5; i++)
            {
                Vector2 scatter = i == 0 ? Vector2.Zero : random.Vector2(random.Float(4f, 12f));
                Vector3 candidate = center + new Vector3(scatter.X, 0f, scatter.Y);
                if (LargeWildlifeHabitat.TryPerch(terrain.Terrain, candidate, out perch) && !PlayerNear(perch, 6.5f))
                    return true;
            }
            return false;
        }

        private bool Approach(double now, Vector3 pos)
        {
            if (now > stageUntil || PlayerNear(perch, 4f)) return false;
            Vector2 path = (perch - pos).XZ;
            float distance = path.Length();
            Vector2 dir = Unit(path);
            if (distance > .9f)
            {
                if (!TryCorridor(pos, dir, out float floor)) return false;
                float altitude = MathUtils.Min(247f, MathUtils.Max(perch.Y + 4.5f, floor + 4.3f));
                SetCourse(dir, MathUtils.Clamp(distance * .15f, .16f, .64f),
                    MathUtils.Clamp((altitude - pos.Y) * .21f, -.33f, .54f));
                return true;
            }
            if (!LargeWildlifeHabitat.TryPerch(terrain.Terrain, perch, out Vector3 clear) || MathUtils.Abs(clear.Y - perch.Y) > .12f)
                return false;
            if (pos.Y <= perch.Y + .20f)
            {
                state = Flight.Perched;
                stageUntil = now + random.Float(8f, 14f);
                order = Vector3.Zero;
                return true;
            }
            // Keep the low-speed approach aligned over the large, tested landing footprint.
            order = new Vector3(path.X * .30f, -.32f, path.Y * .30f);
            return true;
        }

        private void BeginClimb(double now)
        {
            state = Flight.Climb;
            stageUntil = now + 2.45;
            if (landingAt < now + 8) landingAt = now + 17;
            thinkAt = now;
            order = new Vector3(0, .64f, 0);
        }

        private bool TryCorridor(Vector3 from, Vector2 direction, out float floor)
        {
            if (LargeWildlifeHabitat.TryFlightFloor(terrain.Terrain, from, out floor)
                && LargeWildlifeHabitat.TryFlightFloor(terrain.Terrain, from + new Vector3(direction.X, 0f, direction.Y) * 5.2f, out float next))
            {
                floor = MathUtils.Max(floor, next);
                return true;
            }
            return false;
        }

        private void SetCourse(Vector2 direction, float speed, float vertical)
        {
            facing = direction;
            order = new Vector3(direction.X * speed, vertical, direction.Y * speed);
        }

        private bool PlayerNear(Vector3 point, float distance)
        {
            foreach (ComponentPlayer player in players.ComponentPlayers)
                if (player.ComponentHealth.Health > 0f && Vector3.DistanceSquared(point, player.ComponentBody.Position) < distance * distance)
                    return true;
            return false;
        }

        private static Vector2 Unit(Vector2 vector)
            => vector.LengthSquared() > .001f ? Vector2.Normalize(vector) : Vector2.Zero;
    }
}
