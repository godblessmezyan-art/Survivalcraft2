#pragma warning disable CS1591
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Creatures
{
    public sealed class ComponentWildArthropod : Component, IUpdateable
    {
        private readonly Game.Random random = new Game.Random();
        private ComponentCreature creature;
        private ComponentBody body;
        private ComponentLocomotion locomotion;
        private ComponentWildArthropodModel model;
        private SubsystemPlayers players;
        private SubsystemGameInfo gameInfo;
        private SubsystemTime time;
        private SubsystemTimeOfDay daylight;
        private SubsystemTerrain terrain;
        private ComponentPlayer nearby, marked;
        private Vector3? home, refuge;
        private Vector2 heading, strikeDirection;
        private double nextSense, roamUntil, nextStrike, strikeAt, thrustUntil, hideUntil, nextRefugeSearch;
        private bool scorpion, pending;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary values, IdToEntityMap map)
        {
            base.Load(values, map);
            scorpion = values.GetValue<bool>("IsScorpion");
            creature = Entity.FindComponent<ComponentCreature>(true);
            body = Entity.FindComponent<ComponentBody>(true);
            locomotion = Entity.FindComponent<ComponentLocomotion>(true);
            model = Entity.FindComponent<ComponentWildArthropodModel>(true);
            players = Project.FindSubsystem<SubsystemPlayers>(true);
            gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
            time = Project.FindSubsystem<SubsystemTime>(true);
            daylight = Project.FindSubsystem<SubsystemTimeOfDay>(true);
            terrain = Project.FindSubsystem<SubsystemTerrain>(true);
            if (values.GetValue<bool>("HasHome", false))
                home = values.GetValue<Vector3>("Home");
        }

        public override void Save(ValuesDictionary values, EntityToIdMap map)
        {
            base.Save(values, map);
            values.SetValue("HasHome", home.HasValue);
            if (home.HasValue) values.SetValue("Home", home.Value);
        }

        public void Update(float dt)
        {
            if (CommonLib.WorkType == WorkType.Client || creature.ComponentHealth.Health <= 0f || dt <= 0f) return;
            if (!home.HasValue)
            {
                if (!body.StandingOnValue.HasValue) return;
                home = body.Position;
            }
            double now = time.GameTime;
            if (now >= nextSense)
            {
                nextSense = now + random.Float(.17f, .27f);
                nearby = FindPlayer(scorpion ? 5f : 3.1f);
            }
            if (scorpion) Scorpion(now);
            else Crab(now);
        }

        private ComponentPlayer FindPlayer(float range)
        {
            ComponentPlayer closest = null;
            float nearest = range * range;
            foreach (ComponentPlayer player in players.ComponentPlayers)
            {
                if (player.ComponentHealth.Health <= 0f) continue;
                Vector3 point = player.ComponentBody.Position;
                float distance = Vector3.DistanceSquared(point, body.Position);
                if (distance >= nearest || !ClearTo(player.ComponentBody)) continue;
                nearest = distance;
                closest = player;
            }
            return closest;
        }

        private void Scorpion(double now)
        {
            if (pending)
            {
                Face(strikeDirection);
                if (now < strikeAt)
                {
                    if (now >= strikeAt - .08) Move(strikeDirection, .95f, false);
                    return;
                }
                pending = false;
                if (marked != null && marked.Entity.IsAddedToProject && marked.ComponentHealth.Health > 0f)
                {
                    ComponentBody target = marked.ComponentBody;
                    Vector3 delta = target.Position - body.Position;
                    if (CanSting(delta, strikeDirection) && ClearTo(target))
                        ComponentMiner.AttackBody(target, creature, target.BoundingBox.Center(),
                            new Vector3(strikeDirection.X, 0f, strikeDirection.Y), 4f, true);
                }
                marked = null;
            }
            if (now < thrustUntil)
            {
                Move(strikeDirection, .95f, false);
                return;
            }
            if (now < nextStrike - 1.55) return;

            bool night = ArthropodHabitat.IsNight(daylight.TimeOfDay);
            GameMode mode = gameInfo.WorldSettings.GameMode;
            if (nearby != null && mode != GameMode.Creative && mode != GameMode.Harmless)
            {
                Vector2 offset = (nearby.ComponentBody.Position - body.Position).XZ;
                float range = offset.Length();
                if (range > .05f && (night || range <= 1.55f))
                {
                    Vector2 direction = offset / range;
                    if (range <= 1.68f && body.StandingOnValue.HasValue && now >= nextStrike)
                    {
                        strikeDirection = direction;
                        strikeAt = now + ComponentWildArthropodModel.Windup + .08;
                        thrustUntil = now + ComponentWildArthropodModel.Windup + ComponentWildArthropodModel.Thrust;
                        nextStrike = strikeAt + 2.05f;
                        marked = nearby;
                        pending = true;
                        model.BeginSting();
                        if (CommonLib.WorkType == WorkType.Server)
                            CommonLib.Net?.QueuePackage(new ArthropodStingPackage { EntityId = Entity.EntityId });
                    }
                    else if (range > 1.55f)
                        Move(direction, .62f, false);
                    else Face(direction);
                    return;
                }
            }
            if (!night) return;
            Vector2 toHome = (home.Value - body.Position).XZ;
            if (toHome.LengthSquared() > 36f) Move(Vector2.Normalize(toHome), .45f, false);
            else Roam(now, .38f, false);
        }

        internal static bool CanSting(Vector3 delta, Vector2 direction)
        {
            float forward = Vector2.Dot(delta.XZ, direction);
            float lateral = delta.X * direction.Y - delta.Z * direction.X;
            return forward >= 0f && forward <= 1.82f && MathUtils.Abs(lateral) <= .44f
                && MathUtils.Abs(delta.Y) <= .9f;
        }

        private bool ClearTo(ComponentBody target)
            => !terrain.Raycast(body.Position + new Vector3(0f, .18f, 0f),
                target.Position + new Vector3(0f, .43f, 0f), false, true,
                (value, distance) => BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable).HasValue;

        private void Crab(double now)
        {
            if (nearby != null)
            {
                if (now >= hideUntil && now >= nextRefugeSearch && !refuge.HasValue)
                {
                    nextRefugeSearch = now + .55;
                    refuge = FindShallowWater(nearby.ComponentBody.Position);
                }
                if (refuge.HasValue && MoveTo(refuge.Value, .94f, true)) return;
                if (body.ImmersionFactor > .38f)
                {
                    hideUntil = now + 3.5;
                    return;
                }
                Vector2 away = (body.Position - nearby.ComponentBody.Position).XZ;
                if (away.LengthSquared() > .01f) Move(Vector2.Normalize(away), .80f, true);
                return;
            }
            if (now < hideUntil) return;
            if (refuge.HasValue || Vector3.DistanceSquared(body.Position, home.Value) > 2.8f)
            {
                if (MoveTo(home.Value, .48f, true)) return;
                refuge = null;
            }
            Roam(now, .32f, true);
        }

        private Vector3? FindShallowWater(Vector3 threat)
        {
            Vector3 origin = body.Position;
            Point3 cell = Terrain.ToCell(origin);
            Vector3? result = null;
            float best = float.NegativeInfinity;
            for (int distance = 1; distance <= 3; distance++)
            foreach (Point2 direction in ArthropodHabitat.Directions)
            {
                int x = cell.X + direction.X * distance;
                int z = cell.Z + direction.Y * distance;
                for (int y = cell.Y; y >= cell.Y - 1; y--)
                {
                    if (!ArthropodHabitat.ShallowWater(terrain.Terrain, x, y, z)) continue;
                    Vector3 point = new Vector3(x + .5f, y, z + .5f);
                    float score = (point - threat).XZ.LengthSquared() * .18f - (point - origin).XZ.LengthSquared();
                    if (score <= best) continue;
                    best = score;
                    result = point;
                }
            }
            return result;
        }

        private void Roam(double now, float speed, bool lateral)
        {
            if (now >= roamUntil)
            {
                heading = random.Vector2(1f);
                roamUntil = now + random.Float(2.1f, 4.2f);
            }
            if (roamUntil - now > 1.25f) Move(heading, speed, lateral);
        }

        private bool MoveTo(Vector3 point, float speed, bool lateral)
        {
            Vector2 direction = (point - body.Position).XZ;
            if (direction.LengthSquared() < .18f) return false;
            return Move(Vector2.Normalize(direction), speed, lateral);
        }

        private bool Move(Vector2 direction, float speed, bool lateral)
        {
            Face(lateral ? new Vector2(direction.Y, -direction.X) : direction);
            Vector3 ahead = body.Position + new Vector3(direction.X, 0f, direction.Y) * .46f;
            if (!HasFooting(Terrain.ToCell(ahead), lateral))
            {
                Point3 step = Terrain.ToCell(ahead);
                if (!lateral || body.ImmersionFactor < .35f
                    || !HasFooting(new Point3(step.X, step.Y + 1, step.Z), true)) return false;
                // A one-block bank needs a short climb from the bottom of the refuge.
                if (Vector2.Dot(body.Matrix.Right.XZ, direction) > .77f)
                    locomotion.JumpOrder = 1f;
            }
            Vector2 right = body.Matrix.Right.XZ, forward = body.Matrix.Forward.XZ;
            float x = Vector2.Dot(right, direction), y = Vector2.Dot(forward, direction);
            // Let the crab turn first so its escape remains visibly sideways.
            if ((lateral && MathUtils.Abs(x) > .77f) || (!lateral && y > .60f))
                locomotion.WalkOrder = lateral ? new Vector2(x * speed, 0f) : new Vector2(0f, y * speed);
            return true;
        }

        private bool HasFooting(Point3 cell, bool wet)
        {
            Terrain data = terrain.Terrain;
            int x = cell.X, y = cell.Y, z = cell.Z;
            Block block = ArthropodHabitat.At(data, x, y, z);
            if (block.IsCollidable || (!wet && block is WaterBlock)) return false;
            if (ArthropodHabitat.At(data, x, y - 1, z).IsCollidable) return true;
            return wet && (ArthropodHabitat.At(data, x, y - 1, z) is AirBlock
                || ArthropodHabitat.ShallowWater(data, x, y - 1, z))
                && ArthropodHabitat.At(data, x, y - 2, z).IsCollidable;
        }

        private void Face(Vector2 direction)
        {
            float angle = Vector2.Angle(body.Matrix.Forward.XZ, direction);
            locomotion.TurnOrder = new Vector2(MathUtils.Clamp(angle, -1f, 1f), 0f);
        }
    }
}
