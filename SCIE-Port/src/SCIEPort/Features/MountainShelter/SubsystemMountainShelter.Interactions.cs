using System.Collections.Generic;
using Engine;
using Game;

namespace SCIEPort.Features.MountainShelter
{
    public sealed partial class SubsystemMountainShelter
    {
        private void Interact(ComponentPlayer player, Point3 point)
        {
            if (!Permitted(player, point) || !Locate(point, Value(point), out Site site, out int part) || !Complete(site))
                return;
            int lane = part & 1;
            if (part == 4 || part == 5)
            {
                ToggleDoor(player, site, lane);
                return;
            }
            if (ShelterLayout.LampPart(part))
            {
                if (Inside(player.ComponentBody, site) == lane) ToggleLamp(player, site);
                return;
            }
            if (part < 4)
            {
                TrySleep(player, site, lane);
                return;
            }
            if (part < ShelterLayout.StructureCount) Pack(player, site);
        }

        private void ToggleDoor(ComponentPlayer player, Site site, int lane)
        {
            bool closing = !Closed(site, lane);
            bool inside = Inside(player.ComponentBody, site) == lane;
            if (!closing && !inside && HasSleeper(site, lane))
            {
                Message(player, "有人在睡觉");
                return;
            }
            if (closing && BlocksDoor(site, lane))
            {
                Message(player, "帐门被挡住");
                return;
            }
            Point3 lower = site.At(lane), upper = site.At(4 + lane);
            if (!Permitted(player, lower) || !Permitted(player, upper)) return;
            Commit(new List<CellEdit>
            {
                Edit(lower, PartValue(lane, site.Rotation, closing)),
                Edit(upper, PartValue(4 + lane, site.Rotation, closing))
            }, player.ComponentMiner);
        }

        private void ToggleLamp(ComponentPlayer player, Site site)
        {
            Point3 left = site.At(6), right = site.At(7);
            if (!Permitted(player, left) || !Permitted(player, right)) return;
            Decode(Value(left), out _, out _, out _, out bool lit);
            Commit(new List<CellEdit>
            {
                Edit(left, PartValue(6, site.Rotation, lit: !lit)),
                Edit(right, PartValue(7, site.Rotation, lit: !lit))
            }, player.ComponentMiner);
        }

        private void Pack(ComponentPlayer player, Site site)
        {
            if (AnyBodyInside(site))
            {
                Message(player, "帐内有人");
                return;
            }
            var edits = new List<CellEdit>(ShelterLayout.CellCount);
            for (int i = 0; i < ShelterLayout.CellCount; i++)
            {
                if (!Matches(site, i)) continue;
                Point3 p = site.At(i);
                if (!Permitted(player, p)) return;
                edits.Add(Edit(p, 0));
            }
            if (!Commit(edits, player.ComponentMiner)) return;
            var inventory = player.ComponentMiner.Inventory;
            if (inventory == null || ComponentInventoryBase.AcquireItems(inventory, MountainShelterBlock.ItemValue, 1) > 0)
                pickables.AddPickable(MountainShelterBlock.ItemValue, 1, player.ComponentBody.Position + new Vector3(0, .5f, 0), null, null);
        }

        private static int Inside(ComponentBody body, Site site)
        {
            Vector3 p = ShelterLayout.Local(body.Position, site.Root, site.Rotation);
            if (p.Y >= ShelterLayout.FloorHeight - .04f && p.Y < 1.80f && p.Z > .09f && p.Z < 1.94f)
            {
                if (p.X > .06f && p.X < .94f) return 0;
                if (p.X > 1.06f && p.X < 1.94f) return 1;
            }
            return -1;
        }

        private bool TryAt(ComponentBody body, out Site site, out int lane)
        {
            var point = new Point3(Terrain.ToCell(body.Position.X), Terrain.ToCell(body.Position.Y), Terrain.ToCell(body.Position.Z));
            lane = -1;
            site = default;
            if (!Ready(point) || !Locate(point, Value(point), out site, out int part) || part >= 8)
                return false;
            lane = Inside(body, site);
            return lane >= 0;
        }

        private bool HasSleeper(Site site, int lane)
        {
            foreach (var player in players.ComponentPlayers)
                if (IsSleeping(player) && Inside(player.ComponentBody, site) == lane) return true;
            return false;
        }

        private void TrySleep(ComponentPlayer player, Site site, int lane)
        {
            var body = player.ComponentBody;
            if (Inside(body, site) != lane || !body.StandingOnValue.HasValue
                || !Decode(body.StandingOnValue.Value, out int floor, out _, out _, out _) || floor >= 4)
            {
                Message(player, "请站到睡垫上");
                return;
            }
            if (!Closed(site, lane)) { Message(player, "请先关帐门"); return; }
            if (HasSleeper(site, lane)) { Message(player, "铺位已占用"); return; }
            if (body.ImmersionDepth > 0f || player.ComponentVitalStats.Wetness > .95f)
            {
                Message(player, "身上太湿");
                return;
            }
            if (player.ComponentVitalStats.Sleep > .99f) { Message(player, "还不困"); return; }
            player.ComponentSleep.Sleep(true);
            if (player.ComponentSleep.IsSleeping) sleepers[player] = site;
        }

        private static bool IsSleeping(ComponentPlayer player)
            => player.IsAddedToProject && player.ComponentHealth.Health > 0 && player.ComponentSleep?.IsSleeping == true;

        private void MaintainSleep()
        {
            endedSleep.Clear();
            foreach (var pair in sleepers)
            {
                var player = pair.Key;
                if (IsSleeping(player))
                {
                    int lane = Inside(player.ComponentBody, pair.Value);
                    // Keep the reference across brief chunk reloads.
                    if (lane >= 0 && (!Loaded(pair.Value) || Complete(pair.Value) && Closed(pair.Value, lane))) continue;
                    player.ComponentSleep.WakeUp();
                }
                endedSleep.Add(player);
            }
            foreach (var player in endedSleep) sleepers.Remove(player);
            foreach (var player in players.ComponentPlayers)
            {
                if (!IsSleeping(player) || sleepers.ContainsKey(player) || !TryAt(player.ComponentBody, out Site site, out int lane)) continue;
                if (Loaded(site) && Complete(site) && Closed(site, lane))
                    sleepers[player] = site;
            }
        }

        private bool AnyBodyInside(Site site)
        {
            QueryBodies(site);
            for (int i = 0; i < nearBodies.Count; i++)
            {
                BoundingBox box = nearBodies.Array[i].BoundingBox;
                Vector3 a = ShelterLayout.Local(box.Min, site.Root, site.Rotation), b = ShelterLayout.Local(box.Max, site.Root, site.Rotation);
                Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
                if (max.X > 0 && min.X < 2 && max.Z > 0 && min.Z < 2 && max.Y > 0 && min.Y < 2.48f) return true;
            }
            return false;
        }

        private bool BlocksDoor(Site site, int lane)
        {
            QueryBodies(site);
            for (int i = 0; i < nearBodies.Count; i++)
            {
                var body = nearBodies.Array[i];
                BoundingBox box = body.BoundingBox;
                Vector3 a = ShelterLayout.Local(box.Min, site.Root, site.Rotation), b = ShelterLayout.Local(box.Max, site.Root, site.Rotation);
                Vector3 min = Vector3.Min(a,b), max = Vector3.Max(a,b);
                if (max.X <= lane + .06f || min.X >= lane + .94f || max.Y <= ShelterLayout.FloorHeight || min.Y >= 2f) continue;
                if (min.Z < .14f && max.Z > 0) return true;
                if (Inside(body, site) == lane && body.Entity.FindComponent<ComponentCreature>(false) is ComponentCreature creature
                    && IsAnimal(creature)) return true;
            }
            return false;
        }

        private void QueryBodies(Site site)
        {
            nearBodies.Clear();
            var center = new Vector2(site.Root.X + .5f, site.Root.Z + .5f);
            bodies.FindBodiesInArea(center - new Vector2(3f), center + new Vector2(3f), nearBodies);
        }

        private static bool IsAnimal(ComponentCreature creature)
        {
            const CreatureCategory animals = CreatureCategory.LandPredator | CreatureCategory.LandOther | CreatureCategory.WaterPredator
                | CreatureCategory.WaterOther | CreatureCategory.Bird;
            return creature != null && (creature.Category & animals) != 0 && creature.Entity.FindComponent<ComponentPlayer>(false) == null;
        }

        internal static bool Protects(ComponentBody target, ComponentCreature attacker)
        {
            if (!IsAnimal(attacker) || target?.Entity.FindComponent<ComponentPlayer>(false) == null
                || attacker.Project != target.Project) return false;
            var shelter = target.Project.FindSubsystem<SubsystemMountainShelter>(false);
            return shelter != null && shelter.TryAt(target, out Site site, out int lane)
                && shelter.Complete(site) && shelter.Closed(site, lane);
        }
    }
}
