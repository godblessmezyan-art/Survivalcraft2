using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using SCIEPort.Features.Home;
using TemplatesDatabase;

namespace SCIEPort.Features.MountainShelter
{
    /// <summary>Owns shelter placement, interactions, and sleep on the simulation thread.</summary>
    public sealed partial class SubsystemMountainShelter : SubsystemBlockBehavior, IUpdateable
    {
        private const int MaxPending = 64;
        private readonly Queue<Request> requests = new Queue<Request>();
        private readonly Queue<Site> dirtySites = new Queue<Site>();
        private readonly HashSet<Point3> dirtyRoots = new HashSet<Point3>();
        private readonly Dictionary<ComponentPlayer, Site> sleepers = new Dictionary<ComponentPlayer, Site>();
        private readonly List<ComponentPlayer> endedSleep = new List<ComponentPlayer>();
        private readonly DynamicArray<ComponentBody> nearBodies = new DynamicArray<ComponentBody>();
        private SubsystemPlayers players;
        private SubsystemBodies bodies;
        private SubsystemPickables pickables;
        private bool writing;
        private bool writeHalted;

        private struct Request
        {
            internal ComponentPlayer Player;
            internal MountainShelterPackage Packet;
            internal Client Client;
        }

        private struct CellEdit
        {
            internal Point3 Point;
            internal int Before;
            internal int After;
        }

        private struct Site
        {
            internal Point3 Root;
            internal int Rotation;
            internal Point3 At(int part) => Root + ShelterLayout.Offset(part, Rotation);
        }

        /// <inheritdoc/>
        public override int[] HandledBlocks => new[] { MountainShelterBlock.Index };
        /// <inheritdoc/>
        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        private static bool Authority => CommonLib.WorkType != WorkType.Client;

        /// <inheritdoc/>
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            players = Project.FindSubsystem<SubsystemPlayers>(true);
            bodies = Project.FindSubsystem<SubsystemBodies>(true);
            pickables = Project.FindSubsystem<SubsystemPickables>(true);
        }

        /// <inheritdoc/>
        public override bool OnUse(Ray3 ray, ComponentMiner miner)
        {
            if (miner?.ComponentPlayer == null || Terrain.ReplaceLight(miner.ActiveBlockValue, 0) != MountainShelterBlock.ItemValue)
                return false;
            var hit = miner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Digging);
            if (hit.HasValue && hit.Value.CellFace.Face == 4)
                Submit(miner.ComponentPlayer, hit.Value.CellFace.Point, hit.Value.Value, true);
            return true;
        }

        /// <inheritdoc/>
        public override bool OnInteract(TerrainRaycastResult hit, ComponentMiner miner)
        {
            if (miner?.ComponentPlayer == null || !Decode(hit.Value, out _, out _, out _, out _))
                return false;
            Submit(miner.ComponentPlayer, hit.CellFace.Point, hit.Value, false);
            return true;
        }

        private void Submit(ComponentPlayer player, Point3 point, int expected, bool place)
        {
            var packet = new MountainShelterPackage { Point = point, Expected = Terrain.ReplaceLight(expected, 0), Place = place };
            if (Authority) Enqueue(player, packet, null);
            else if (ReferenceEquals(CommonLib.MainPlayer, player)) CommonLib.Net.QueuePackage(packet);
        }

        internal void Enqueue(ComponentPlayer player, MountainShelterPackage packet, Client client)
        {
            Point3 point = packet.Point;
            if (!Authority || !ShelterLayout.ValidRoot(new Point3(point.X, 2, point.Z)) || point.Y < 0 || point.Y > 254)
                return;
            lock (requests)
            {
                if (requests.Count < MaxPending)
                    requests.Enqueue(new Request { Player = player, Packet = packet, Client = client });
            }
        }

        /// <inheritdoc/>
        public void Update(float dt)
        {
            if (!Authority) return;
            MaintainSleep();
            if (writeHalted) return;
            // At most one site writes per update, with up to sixteen writes and sixteen restores.
            if (dirtySites.Count > 0)
            {
                Site site = dirtySites.Dequeue();
                dirtyRoots.Remove(site.Root);
                if (!Loaded(site)) MarkDirty(site);
                else if (!Complete(site)) { Clean(site); return; }
            }
            Request request;
            lock (requests)
            {
                if (requests.Count == 0) return;
                request = requests.Dequeue();
            }
            try
            {
                var player = request.Player;
                Point3 point = request.Packet.Point;
                if (!Available(player) || (request.Client != null && !ReferenceEquals(player.PlayerData.Client, request.Client))
                    || !Ready(point) || Signature(point) != request.Packet.Expected || !CanReach(player, point, request.Packet.Place))
                    return;
                if (request.Packet.Place) Deploy(player, point);
                else Interact(player, point);
            }
            catch (Exception ex) { Log.Error("[SCIEPort:MountainShelter] Interaction failed: " + ex); }
        }

        private bool Available(ComponentPlayer p)
            => p != null && p.IsAddedToProject && p.Project == Project && p.PlayerData?.IsReadyForPlaying == true
                && p.ComponentHealth.Health > 0 && p.ComponentSleep?.IsSleeping == false && p.ComponentRider?.Mount == null
                && p.ComponentBody.ParentBody == null;

        private bool CanReach(ComponentPlayer player, Point3 point, bool place)
        {
            Vector3 eye = player.ComponentCreatureModel.EyePosition;
            Vector3 offset = new Vector3(point);
            if (!(Vector3.DistanceSquared(eye, offset + new Vector3(.5f)) <= 25f)) return false;
            if (place) return Reaches(eye, offset + new Vector3(.5f, .99f, .5f), point, false, true);
            int value = Value(point);
            foreach (BoundingBox box in BlocksManager.Blocks[MountainShelterBlock.Index].GetCustomInteractionBoxes(SubsystemTerrain, value))
                if (Reaches(eye, offset + (box.Min + box.Max) * .5f, point, true, false)) return true;
            return false;
        }

        private bool Reaches(Vector3 eye, Vector3 end, Point3 point, bool interaction, bool top)
        {
            var hit = SubsystemTerrain.Raycast(eye, end, interaction, true, null);
            return hit.HasValue && hit.Value.CellFace.Point.Equals(point) && (!top || hit.Value.CellFace.Face == 4);
        }

        private void Deploy(ComponentPlayer player, Point3 support)
        {
            var inventory = player.ComponentMiner.Inventory;
            if (inventory == null) return;
            int slot = inventory.ActiveSlotIndex;
            if (slot < 0 || slot >= inventory.SlotsCount || inventory.GetSlotCount(slot) < 1
                || Terrain.ReplaceLight(inventory.GetSlotValue(slot), 0) != MountainShelterBlock.ItemValue) return;
            Vector3 direction = new Vector3(support.X + .5f, 0, support.Z + .5f) - player.ComponentBody.Position;
            var site = new Site { Root = support + new Point3(0, 1, 0), Rotation = ShelterLayout.Facing(direction) };
            var edits = new List<CellEdit>(ShelterLayout.CellCount);
            if (!PlanPlacement(player, site, edits))
            {
                Message(player, "这里无法搭帐");
                return;
            }
            if (!Commit(edits, player.ComponentMiner)) return;
            int beforeCount = inventory.GetSlotCount(slot);
            try
            {
                if (inventory.RemoveSlotItems(slot, 1) == 1) return;
            }
            catch (Exception ex) { Log.Error("[SCIEPort:MountainShelter] Inventory removal failed: " + ex); }
            if (inventory.GetSlotCount(slot) == beforeCount - 1) return;
            for (int i = 0; i < edits.Count; i++)
                edits[i] = new CellEdit { Point = edits[i].Point, Before = edits[i].After, After = 0 };
            if (!Commit(edits, player.ComponentMiner))
                Halt(new InvalidOperationException("Unable to restore terrain after inventory failure."));
        }

        private bool PlanPlacement(ComponentPlayer player, Site site, List<CellEdit> edits)
        {
            if (!Loaded(site) || AnyBodyInside(site)) return false;
            for (int lane = 0; lane < 2; lane++)
                if (!HasEntrance(site, lane)) return false;
            for (int part = 0; part < ShelterLayout.CellCount; part++)
            {
                Point3 p = site.At(part);
                if (part >= ShelterLayout.StructureCount)
                {
                    if (Supports(p)) continue;
                    if (!Supports(p - new Point3(0, 1, 0))) return false;
                }
                if (Signature(p) != 0 || !Permitted(player, p)) return false;
                edits.Add(Edit(p, PartValue(part, site.Rotation)));
            }
            return true;
        }

        private bool HasEntrance(Site site, int lane)
        {
            Point3 p = ShelterLayout.Entrance(site.Root, site.Rotation, lane);
            var up = new Point3(0, 1, 0);
            return Ready(p) && Signature(p) == 0 && Signature(p + up) == 0
                && (Supports(p - up) || Signature(p - up) == 0 && Supports(p - up - up));
        }

        private bool Supports(Point3 p)
        {
            if (!Ready(p)) return false;
            int value = Value(p);
            int contents = Terrain.ExtractContents(value);
            if (contents == 0 || contents == MountainShelterBlock.Index) return false;
            Block block = BlocksManager.Blocks[contents];
            if (!block.IsCollidable_(value)) return false;
            foreach (var box in block.GetCustomCollisionBoxes(SubsystemTerrain, value))
                if (box.Min.X <= .34f && box.Max.X >= .66f && box.Min.Z <= .34f && box.Max.Z >= .66f && box.Max.Y >= .995f)
                    return true;
            return false;
        }

        private bool Commit(List<CellEdit> edits, ComponentMiner miner, bool cleanup = false)
        {
            if (!Authority || writeHalted || edits.Count > ShelterLayout.CellCount) return false;
            int committed = 0;
            writing = true;
            try
            {
                foreach (var edit in edits)
                {
                    var result = HomeCellTransaction.Apply(() => Signature(edit.Point),
                        value => Write(edit.Point, value, miner, cleanup), edit.Before, edit.After, out Exception error);
                    if (result == HomeCellTransaction.Result.Applied) { committed++; continue; }
                    if (result == HomeCellTransaction.Result.RollbackFailed) Halt(error);
                    else if (error != null) Log.Error("[SCIEPort:MountainShelter] Write interrupted: " + error);
                    return false;
                }
                return true;
            }
            finally
            {
                if (committed != edits.Count)
                    for (int i = committed - 1; i >= 0; i--)
                    {
                        var edit = edits[i];
                        if (Signature(edit.Point) != edit.After) continue;
                        try
                        {
                            Write(edit.Point, edit.Before, miner, cleanup);
                            if (Signature(edit.Point) != edit.Before) Halt(new InvalidOperationException("Terrain rollback rejected."));
                        }
                        catch (Exception ex) { Halt(ex); }
                    }
                writing = false;
            }
        }

        private void Write(Point3 point, int value, ComponentMiner miner, bool cleanup)
        {
            if (cleanup)
            {
                SubsystemTerrain.ChangeCellNet(point.X, point.Y, point.Z, value);
                CommonLib.Net.QueuePackage(new SubsystemTerrainPackage(point.X, point.Y, point.Z, value));
            }
            else SubsystemTerrain.ChangeCell(point.X, point.Y, point.Z, value, true, miner);
        }

        private void Halt(Exception error)
        {
            writeHalted = true;
            Log.Error("[SCIEPort:MountainShelter] Terrain rollback failed. Shelter writes stopped: " + error);
        }

        private CellEdit Edit(Point3 point, int after) => new CellEdit { Point = point, Before = Signature(point), After = after };
        // Contents are available before relighting. Doors and lamps invalidate light in their neighboring chunks.
        private bool Ready(Point3 p) => p.Y >= 0 && p.Y < 255
            && SubsystemTerrain.Terrain.GetChunkAtCell(p.X, p.Z)?.State >= TerrainChunkState.InvalidLight;
        private int Value(Point3 p) => SubsystemTerrain.Terrain.GetCellValue(p.X, p.Y, p.Z);
        private int Signature(Point3 p) => Terrain.ReplaceLight(Value(p), 0);
        private static int PartValue(int part, int rotation, bool closed = false, bool lit = false)
            => Terrain.MakeBlockValue(MountainShelterBlock.Index, 0, ShelterLayout.Encode(part, rotation, closed, lit));

        private static bool Decode(int value, out int part, out int rotation, out bool closed, out bool lit)
        {
            bool valid = ShelterLayout.Decode(Terrain.ExtractData(value), out part, out rotation, out closed, out lit);
            return Terrain.ExtractContents(value) == MountainShelterBlock.Index && valid;
        }

        private static bool Locate(Point3 p, int value, out Site site, out int part)
        {
            bool valid = Decode(value, out part, out int rotation, out _, out _);
            site = valid ? new Site { Root = p - ShelterLayout.Offset(part, rotation), Rotation = rotation } : default;
            return valid && ShelterLayout.ValidRoot(site.Root);
        }

        private static bool Permitted(ComponentPlayer player, Point3 p)
            => !SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(p.X, p.Z, out Territoriy region)
                || (region.AllowBlockBehavior && (!region.IsVisible || !SubsystemBedrockBlockBehavior.IsInTerritoriyBorder(region, p.X, p.Z)))
                || SubsystemBedrockBlockBehavior.AllowPlayerAction(player, region);

        private bool Loaded(Site site)
        {
            if (!ShelterLayout.ValidRoot(site.Root)) return false;
            for (int i = 0; i < 4; i++) if (!Ready(site.At(i))) return false;
            return true;
        }

        private bool Matches(Site site, int part)
            => Decode(Value(site.At(part)), out int actual, out int rotation, out _, out _)
                && actual == part && rotation == site.Rotation;

        private bool Closed(Site site, int lane) => Terrain.ExtractData(Value(site.At(lane))) == ShelterLayout.Encode(lane, site.Rotation, true);

        private bool Complete(Site site)
        {
            if (!Loaded(site)) return false;
            for (int part = 0; part < ShelterLayout.StructureCount; part++)
                if (!Matches(site, part)) return false;
            for (int lane = 0; lane < 2; lane++)
                if (Signature(site.At(4 + lane)) != PartValue(4 + lane, site.Rotation, Closed(site, lane))) return false;
            for (int part = ShelterLayout.StructureCount; part < ShelterLayout.CellCount; part++)
            {
                Point3 p = site.At(part);
                if (Matches(site, part))
                {
                    if (!Supports(p - new Point3(0, 1, 0))) return false;
                }
                else if (!Supports(p)) return false;
            }
            return true;
        }

        private void MarkDirty(Site site)
        {
            if (Authority && !writing && dirtySites.Count < MaxPending && dirtyRoots.Add(site.Root)) dirtySites.Enqueue(site);
        }

        private void Clean(Site site)
        {
            // A valid root at this coordinate with a different rotation belongs to a different site.
            if (Locate(site.Root, Value(site.Root), out Site other, out int role) && (role != 0 || other.Rotation != site.Rotation))
                return;
            var edits = new List<CellEdit>(ShelterLayout.CellCount);
            for (int part = 0; part < ShelterLayout.CellCount; part++)
                if (Matches(site, part)) edits.Add(Edit(site.At(part), 0));
            if (edits.Count > 0) Commit(edits, null, cleanup: true);
        }

        /// <inheritdoc/>
        public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
        {
            if (Locate(new Point3(x, y, z), value, out Site site, out _)) MarkDirty(site);
        }

        /// <inheritdoc/>
        public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
        {
            var p = new Point3(x, y, z);
            if (!writing && neighborY < y && Locate(p, Value(p), out Site site, out _)) MarkDirty(site);
        }

        /// <inheritdoc/>
        public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
        {
            if (Locate(new Point3(x, y, z), value, out Site site, out _)) MarkDirty(site);
        }

        private void Message(ComponentPlayer p, string message)
        {
            if (p.PlayerData.IsMainPlayer) p.ComponentGui.DisplaySmallMessage(message, Color.White, false, false);
            else if (p.PlayerData.Client != null)
                CommonLib.Net.QueuePackage(new ComponentSleepPackage(p.ComponentSleep, ComponentSleepPackage.EventType.Sleep,
                    false, false, message) { To = p.PlayerData.Client });
        }
    }
}
