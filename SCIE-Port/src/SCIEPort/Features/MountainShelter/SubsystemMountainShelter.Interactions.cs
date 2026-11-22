using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.MountainShelter
{
    public sealed partial class SubsystemMountainShelter
    {
        private void Interact(ComponentPlayer player, Point3 point, int face)
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
                if (Inside(player.ComponentBody, site) >= 0)
                {
                    if (ShelterLayout.SideFace(face, site.Rotation)) ToggleCurtain(player, site, part);
                    else ToggleLamp(player, site);
                }
                return;
            }
            if (part < 4)
            {
                TrySleep(player, site);
                return;
            }
            if (part >= ShelterLayout.StructureCount) return;
            if (Inside(player.ComponentBody, site) < 0) Pack(player, site);
            else if (ShelterLayout.SkylightPart(part)) ToggleSkylight(player, site, part);
        }

        private void ToggleDoor(ComponentPlayer player, Site site, int lane)
        {
            bool closing = !Closed(site, lane);
            if (!closing && Inside(player.ComponentBody, site) < 0 && (HasSleeper(site, 0) || HasSleeper(site, 1)))
            {
                Message(player, "有人在睡觉");
                return;
            }
            if (closing && DoorObstructed(site, lane == 0, lane == 1))
            {
                Message(player, "帐门被挡住");
                return;
            }
            var edits = new List<CellEdit>(2);
            if (PlanDoor(player, site, lane, closing, edits)) Commit(edits, player.ComponentMiner);
        }

        private bool PlanDoor(ComponentPlayer player, Site site, int lane, bool closing, List<CellEdit> edits)
        {
            Point3 lower = site.At(lane), upper = site.At(4 + lane);
            if (!Permitted(player, lower) || !Permitted(player, upper)) return false;
            edits.Add(Edit(lower, PartValue(lane, site.Rotation, closing)));
            edits.Add(Edit(upper, PartValue(4 + lane, site.Rotation, closing)));
            return true;
        }

        private bool CloseForSleep(ComponentPlayer player, Site site)
        {
            bool left = !Closed(site, 0), right = !Closed(site, 1);
            if (!left && !right) return true;
            if (DoorObstructed(site, left, right))
            {
                Message(player, "帐门被挡住");
                return false;
            }
            var edits = new List<CellEdit>(4);
            if ((left && !PlanDoor(player, site, 0, true, edits)) || (right && !PlanDoor(player, site, 1, true, edits)))
            {
                Message(player, "无法关闭帐门");
                return false;
            }
            return Commit(edits, player.ComponentMiner);
        }

        private void ToggleLamp(ComponentPlayer player, Site site)
        {
            Point3 left = site.At(6), right = site.At(7);
            if (!Permitted(player, left) || !Permitted(player, right)) return;
            ShelterLayout.Decode(Terrain.ExtractData(Value(left)), out _, out _, out _, out bool lit, out bool leftOpen);
            ShelterLayout.Decode(Terrain.ExtractData(Value(right)), out _, out _, out _, out _, out bool rightOpen);
            Commit(new List<CellEdit>
            {
                Edit(left, PartValue(6, site.Rotation, lit: !lit, viewOpen: leftOpen)),
                Edit(right, PartValue(7, site.Rotation, lit: !lit, viewOpen: rightOpen))
            }, player.ComponentMiner);
        }

        private void ToggleCurtain(ComponentPlayer player, Site site, int part)
        {
            Point3 point = site.At(part);
            ShelterLayout.Decode(Terrain.ExtractData(Value(point)), out _, out _, out _, out bool lit, out bool open);
            Commit(new List<CellEdit> { Edit(point, PartValue(part, site.Rotation, lit: lit, viewOpen: !open)) }, player.ComponentMiner);
        }

        private void ToggleSkylight(ComponentPlayer player, Site site, int part)
        {
            Point3 left = site.At(10), right = site.At(11);
            if (!Permitted(player, left) || !Permitted(player, right)) return;
            ShelterLayout.Decode(Terrain.ExtractData(Value(site.At(part))), out _, out _, out _, out _, out bool clear);
            Commit(new List<CellEdit>
            {
                Edit(left, PartValue(10, site.Rotation, viewOpen: !clear)),
                Edit(right, PartValue(11, site.Rotation, viewOpen: !clear))
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
            if (!(p.Y >= ShelterLayout.FloorHeight - .08f && p.Y < 1.80f
                && p.X > .06f && p.X < 1.94f && p.Z > .09f && p.Z < 1.94f)) return -1;
            return p.X < 1f ? 0 : 1;
        }

        private bool TryAt(ComponentBody body, out Site site)
        {
            var point = new Point3(Terrain.ToCell(body.Position.X), Terrain.ToCell(body.Position.Y), Terrain.ToCell(body.Position.Z));
            site = default;
            return Ready(point) && Locate(point, Value(point), out site, out int part) && part < 8 && Inside(body, site) >= 0;
        }

        internal static bool RequestGuiSleep(ComponentPlayer player)
        {
            var subsystem = player?.Project?.FindSubsystem<SubsystemMountainShelter>(false);
            return subsystem != null && subsystem.TryGuiSleep(player);
        }

        private bool TryGuiSleep(ComponentPlayer player)
        {
            if (!TryAt(player.ComponentBody, out Site site)) return false;
            if (CommonLib.WorkType == WorkType.Client)
            {
                Point3 point = new Point3(Terrain.ToCell(player.ComponentBody.Position.X),
                    Terrain.ToCell(player.ComponentBody.Position.Y), Terrain.ToCell(player.ComponentBody.Position.Z));
                CommonLib.Net.QueuePackage(new MountainShelterPackage
                {
                    Point = point,
                    Expected = Terrain.ReplaceLight(Value(point), 5),
                    Place = false
                });
            }
            else TrySleep(player, site);
            return true;
        }

        private bool HasSleeper(Site site, int lane)
        {
            foreach (var player in players.ComponentPlayers)
                if (IsSleeping(player) && Inside(player.ComponentBody, site) == lane) return true;
            return false;
        }

        private void TrySleep(ComponentPlayer player, Site site)
        {
            var body = player.ComponentBody;
            int lane = Inside(body, site);
            if (lane < 0 || body.Position.Y > site.Root.Y + ShelterLayout.FloorHeight + .14f)
            {
                Message(player, "请站到睡垫上");
                return;
            }
            if (HasSleeper(site, lane)) { Message(player, "铺位已占用"); return; }
            if (body.ImmersionDepth > 0f || player.ComponentVitalStats.Wetness > .95f)
            {
                Message(player, "身上太湿");
                return;
            }
            var settings = gameInfo.WorldSettings;
            if (settings.GameMode != GameMode.Creative && settings.AreAdventureSurvivalMechanicsEnabled && player.ComponentVitalStats.Sleep > .99f)
            {
                Message(player, "还不困");
                return;
            }
            if (!CloseForSleep(player, site)) return;
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
                    if (lane >= 0 && (!Loaded(pair.Value) || Complete(pair.Value) && DoorsClosed(pair.Value))) continue;
                    player.ComponentSleep.WakeUp();
                }
                endedSleep.Add(player);
            }
            foreach (var player in endedSleep) sleepers.Remove(player);
            foreach (var player in players.ComponentPlayers)
            {
                if (!IsSleeping(player) || sleepers.ContainsKey(player) || !TryAt(player.ComponentBody, out Site site)) continue;
                if (Loaded(site) && Complete(site) && DoorsClosed(site))
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

        private bool DoorObstructed(Site site, bool left, bool right)
        {
            QueryBodies(site);
            for (int i = 0; i < nearBodies.Count; i++)
            {
                var body = nearBodies.Array[i];
                BoundingBox box = body.BoundingBox;
                Vector3 a = ShelterLayout.Local(box.Min, site.Root, site.Rotation), b = ShelterLayout.Local(box.Max, site.Root, site.Rotation);
                Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
                if (max.Y > ShelterLayout.FloorHeight && min.Y < 2f && min.Z < .14f && max.Z > 0
                    && ((left && max.X > .06f && min.X < 1f) || (right && max.X > 1f && min.X < 1.94f)))
                    return true;
                if (Inside(body, site) >= 0 && IsAnimal(body.Entity.FindComponent<ComponentCreature>(false))) return true;
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
            return shelter != null && shelter.TryAt(target, out Site site)
                && shelter.Complete(site) && shelter.DoorsClosed(site);
        }
    }
}
