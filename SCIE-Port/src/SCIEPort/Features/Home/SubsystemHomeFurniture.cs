using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.Home
{
    /// <summary>Host-owned bounded multi-cell occupancy, native storage and interaction requests.</summary>
    public sealed partial class SubsystemHomeFurniture : SubsystemChestBlockBehavior, IUpdateable
    {
        private readonly Queue<Point3> added = new Queue<Point3>();
        private readonly HashSet<Point3> waiting = new HashSet<Point3>();
        private readonly Dictionary<Point3, ComponentMiner> placers = new Dictionary<Point3, ComponentMiner>();
        private readonly Queue<Request> requests = new Queue<Request>();
        private readonly Dictionary<ushort, double> lastRequest = new Dictionary<ushort, double>();
        private readonly HashSet<Point3> removing = new HashSet<Point3>();
        private SubsystemTime clock;
        private bool placementHalted;
        private struct Request { internal ComponentPlayer Player; internal Point3 Point; internal int Expected; internal byte Action; internal Client Reply; }
        /// <inheritdoc/>
        public override int[] HandledBlocks => new[] { HomeRules.Fridge, HomeRules.Cabinet, HomeRules.Sink, HomeRules.Sofa, HomeRules.Table, HomeRules.Curtain, HomeRules.Upper,
            HomeRules.Fireplace, HomeRules.Dining, HomeRules.Bed, HomeRules.Nightstand, HomeRules.WallArt, HomeRules.DiningPart, HomeRules.BedPart };
        /// <inheritdoc/>
        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        internal bool MayPlace => !placementHalted && added.Count < 128;
        /// <inheritdoc/>
        public override void Load(ValuesDictionary values) { base.Load(values); clock = Project.FindSubsystem<SubsystemTime>(true); }
        /// <inheritdoc/>
        public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
        {
            if (CommonLib.WorkType == WorkType.Client || !HomeRules.IsHome(Terrain.ExtractContents(value))) return;
            Point3 p = new Point3(x, y, z);
            if (MayPlace && waiting.Add(p)) added.Enqueue(p);
            else if (!waiting.Contains(p)) RemoveRoot(p, null);
        }
        /// <inheritdoc/>
        public override void OnBlockAdded(int value, int oldValue, int x, int y, int z, ComponentMiner miner)
        {
            Point3 p = new Point3(x, y, z);
            if (CommonLib.WorkType != WorkType.Client && waiting.Contains(p)) placers[p] = miner;
        }
        /// <inheritdoc/>
        public void Update(float dt)
        {
            if (CommonLib.WorkType == WorkType.Client) return;
            for (int i = 0; i < 32 && added.Count > 0; i++)
            {
                Point3 p = added.Dequeue(); waiting.Remove(p);
                ComponentMiner miner; placers.TryGetValue(p, out miner); placers.Remove(p);
                InitializeAt(p, miner);
            }
            for (int i = 0; i < 32; i++)
            {
                Request request;
                lock (requests) { if (requests.Count == 0) break; request = requests.Dequeue(); }
                Interact(request);
            }
        }
        private bool Ready(Point3 p) => p.Y > 0 && p.Y < 255 && SubsystemTerrain.Terrain.GetChunkAtCell(p.X, p.Z)?.State >= TerrainChunkState.Valid;
        private int Value(Point3 p) => SubsystemTerrain.Terrain.GetCellValue(p.X, p.Y, p.Z);
        private void Change(Point3 p, int value, ComponentMiner miner) => SubsystemTerrain.ChangeCell(p.X, p.Y, p.Z, value, true, miner);
        private void InitializeAt(Point3 p, ComponentMiner miner)
        {
            if (!Ready(p)) return;
            int value = Value(p), id = Terrain.ExtractContents(value);
            if (!HomeRules.IsHome(id)) return; // Placement was cancelled/removed before this batch.
            if (HomeRules.HasFootprint(id)) { InitializeFootprint(p, value, miner); return; }
            if (id == HomeRules.WallArt)
            {
                bool supported;
                if (!HomeExtensionGeometry.TryWall(SubsystemTerrain, p, HomeRules.Rotation(Terrain.ExtractData(value)), out supported)) return;
                if (!supported) { RemoveRoot(p, miner); return; }
            }
            if (HomeRules.IsTall(id))
            {
                Point3 top = new Point3(p.X, p.Y + 1, p.Z);
                if (!Ready(top)) { RemoveRoot(p, miner); return; }
                int before = Value(top); // Capture before any write; air-only two-cell plan.
                int upper = Terrain.MakeBlockValue(HomeRules.Upper, 0, HomeRules.UpperData(id, Terrain.ExtractData(value)));
                if (HomeRules.Pair(id, Terrain.ExtractData(value), Terrain.ExtractContents(before), Terrain.ExtractData(before))) { EnsureStorage(p, id, miner); return; }
                if (Terrain.ExtractContents(before) != 0) { RemoveRoot(p, miner); return; }
                Exception error;
                var commit = HomeCellTransaction.Apply(
                    () => Terrain.ReplaceLight(Value(top), 0), v => Change(top, v, miner), Terrain.ReplaceLight(before, 0), upper, out error);
                if (commit != HomeCellTransaction.Result.Applied)
                {
                    if (commit == HomeCellTransaction.Result.RollbackFailed)
                    {
                        placementHalted = true; added.Clear(); waiting.Clear(); placers.Clear();
                        Log.Error("[SCIEPort:Home] Placement batches halted after undo failure: " + error);
                    }
                    else
                    {
                        RemoveRoot(p, miner);
                        if (error != null) Log.Error("[SCIEPort:Home] Two-cell placement rolled back: " + error);
                    }
                    return;
                }
            }
            EnsureStorage(p, id, miner);
        }
        private void EnsureStorage(Point3 p, int id, ComponentMiner miner)
        {
            if (HomeRules.IsStorage(id) && m_subsystemBlockEntities.GetBlockEntity(p.X, p.Y, p.Z) == null)
                m_subsystemBlockEntities.CreateBlockEntity("SCIEPortHomeStorage", p, miner);
        }
        private void RemoveRoot(Point3 p, ComponentMiner miner)
        {
            if (!removing.Add(p)) return;
            try
            {
                int old = Value(p), id = Terrain.ExtractContents(old);
                if (!HomeRules.IsHome(id)) return;
                // Structural cleanup is entered only after an authoritative removal of our
                // paired cell, or a failed air-only placement. Never touches foreign contents.
                SubsystemTerrain.ChangeCellNet(p.X, p.Y, p.Z, 0, true, miner);
                CommonLib.Net.QueuePackage(new SubsystemTerrainPackage(p.X, p.Y, p.Z, 0));
                if (Terrain.ExtractContents(Value(p)) == 0)
                    Project.FindSubsystem<SubsystemPickables>(true).AddPickable(Terrain.MakeBlockValue(id, 0, HomeRules.ItemData(id, Terrain.ExtractData(old))), 1, new Vector3(p.X + .5f, p.Y + .5f, p.Z + .5f), null, null);
            }
            finally { removing.Remove(p); }
        }
        /// <inheritdoc/>
        public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
        {
            int id = Terrain.ExtractContents(value); Point3 p = new Point3(x, y, z);
            if (CommonLib.WorkType == WorkType.Client)
            {
                var entity = m_subsystemBlockEntities.GetBlockEntity(x, y, z);
                if (entity?.Entity.FindComponent<ComponentHomeStorage>(false) != null) Project.RemoveEntity(entity.Entity, true);
                return;
            }
            if (HomeRules.IsStorage(id) && m_subsystemBlockEntities.GetBlockEntity(x, y, z)?.Entity.FindComponent<ComponentHomeStorage>(false) != null)
                base.OnBlockRemoved(value, newValue, x, y, z);
            if (HomeRules.IsFootprintPart(id)) { RemoveFootprintRootFromPart(p, value); return; }
            if (HomeRules.HasFootprint(id)) RemoveFootprintParts(p, value);
            if (id == HomeRules.Upper)
            {
                Point3 lower = new Point3(x, y - 1, z);
                if (Ready(lower) && Terrain.ExtractContents(Value(lower)) == HomeRules.LowerId(Terrain.ExtractData(value))) RemoveRoot(lower, null);
            }
            else if (HomeRules.IsTall(id))
            {
                Point3 top = new Point3(x, y + 1, z);
                if (Ready(top) && Terrain.ExtractContents(Value(top)) == HomeRules.Upper && HomeRules.LowerId(Terrain.ExtractData(Value(top))) == id)
                {
                    SubsystemTerrain.ChangeCellNet(top.X, top.Y, top.Z, 0);
                    CommonLib.Net.QueuePackage(new SubsystemTerrainPackage(top.X, top.Y, top.Z, 0));
                }
            }
            waiting.Remove(p); placers.Remove(p);
        }
        /// <inheritdoc/>
        public override void OnNeighborBlockChanged(int x, int y, int z, int nx, int ny, int nz)
        {
            if (CommonLib.WorkType == WorkType.Client) return;
            Point3 p = new Point3(x, y, z); int value = Value(p), id = Terrain.ExtractContents(value);
            if (HomeRules.HasFootprint(id)) { CheckFootprint(p, value); return; }
            if (id == HomeRules.WallArt)
            {
                bool supported;
                if (!waiting.Contains(p) && HomeExtensionGeometry.TryWall(SubsystemTerrain, p, HomeRules.Rotation(Terrain.ExtractData(value)), out supported) && !supported) RemoveRoot(p, null);
                return;
            }
            if (!HomeRules.IsTall(id) || waiting.Contains(p)) return;
            Point3 top = new Point3(x, y + 1, z);
            if (Ready(top) && (Terrain.ExtractContents(Value(top)) != HomeRules.Upper || HomeRules.LowerId(Terrain.ExtractData(Value(top))) != id)) RemoveRoot(p, null);
        }
        /// <inheritdoc/>
        public override bool OnInteract(TerrainRaycastResult hit, ComponentMiner miner)
        {
            Point3 p = hit.CellFace.Point;
            int value = Value(p), id = Terrain.ExtractContents(value);
            if (HomeRules.IsFootprintPart(id))
            {
                if (!TryFootprintRoot(p, value, out p)) return false;
                value = Value(p); id = Terrain.ExtractContents(value);
            }
            bool upper = id == HomeRules.Upper;
            if (upper) { p.Y--; value = Value(p); id = Terrain.ExtractContents(value); }
            if (!HomeRules.IsHome(id) || miner?.ComponentPlayer == null) return false;
            byte action = HomeRules.IsStorage(id) && !(id == HomeRules.Fridge && upper) ? (byte)1 : (byte)0;
            if (action == 0 && !HomeRules.IsSwitchable(id) && id != HomeRules.Bed) return false;
            int expected = Terrain.MakeBlockValue(id, 0, Terrain.ExtractData(value));
            if (CommonLib.WorkType == WorkType.Client)
            {
                if (ReferenceEquals(CommonLib.MainPlayer, miner.ComponentPlayer)) CommonLib.Net.QueuePackage(new HomeFurniturePackage { Point = p, Expected = expected, Action = action });
            }
            else Enqueue(miner.ComponentPlayer, p, expected, action);
            return true;
        }
        internal void Enqueue(ComponentPlayer player, Point3 point, int expected, byte action, Client reply = null)
        {
            lock (requests) { if (requests.Count < 128) requests.Enqueue(new Request { Player = player, Point = point, Expected = expected, Action = action, Reply = reply }); }
        }
        private void Interact(Request request)
        {
            ComponentPlayer player = request.Player; Point3 p = request.Point; int expected = request.Expected;
            if (player == null || !player.IsAddedToProject || !Ready(p)) return;
            int value = Value(p), id = Terrain.ExtractContents(value), data = Terrain.ExtractData(value);
            if (request.Action == 1 ? !HomeRules.IsStorage(id) : !HomeRules.IsSwitchable(id) && id != HomeRules.Bed) return;
            if (Terrain.MakeBlockValue(id, 0, data) != expected || Vector3.DistanceSquared(player.ComponentBody.Position, new Vector3(p.X + .5f, p.Y, p.Z + .5f)) > 36) return;
            double last; if (lastRequest.TryGetValue(player.Entity.EntityId, out last) && clock.GameTime - last < .25) return;
            var eye = player.ComponentCreatureModel;
            Vector3 direction = new Vector3(p.X + .5f, p.Y + .5f, p.Z + .5f) - eye.EyePosition;
            if (direction.LengthSquared() < .001f) return;
            var ray = new Ray3(eye.EyePosition, Vector3.Normalize(direction));
            TerrainRaycastResult? sight = player.ComponentMiner.Raycast<TerrainRaycastResult>(ray, RaycastMode.Interaction);
            if (!sight.HasValue || !InteractionBelongsTo(p, id, sight.Value.CellFace.Point)) return;
            Territoriy region;
            if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(p.X, p.Z, out region)
                && (!region.AllowBlockBehavior || region.IsVisible && SubsystemBedrockBlockBehavior.IsInTerritoriyBorder(region, p.X, p.Z))
                && !SubsystemBedrockBlockBehavior.AllowPlayerAction(player, region)) return;
            // ChangeCell enforces the real target's territory policy with this authenticated miner.
            if (lastRequest.Count >= 256) lastRequest.Clear();
            lastRequest[player.Entity.EntityId] = clock.GameTime;
            if (request.Action == 1)
            {
                EnsureStorage(p, id, player.ComponentMiner);
                var inventory = m_subsystemBlockEntities.GetBlockEntity(p.X, p.Y, p.Z)?.Entity.FindComponent<ComponentHomeStorage>(false);
                if (inventory == null) return;
                if (request.Reply != null) CommonLib.Net.QueuePackage(new BlockEditPackage(inventory) { To = request.Reply });
                else if (player.PlayerData.IsMainPlayer) player.ComponentGui.ModalPanelWidget = new HomeStorageWidget(player.ComponentMiner.Inventory, inventory);
                return;
            }
            if (id == HomeRules.Bed) { TrySleep(player, p, data, request.Reply); return; }
            if (id == HomeRules.Fridge && !HomeRules.IsOpen(data))
            {
                Point3 forward = new[] { new Point3(0, 0, -1), new Point3(-1, 0, 0), new Point3(0, 0, 1), new Point3(1, 0, 0) }[HomeRules.Rotation(data)];
                for (int y = 0; y < 2; y++) if (SubsystemTerrain.Terrain.GetCellContents(p.X + forward.X, p.Y + y, p.Z + forward.Z) != 0) return;
            }
            int next = Terrain.ReplaceData(value, data ^ HomeRules.StateBit);
            Change(p, next, player.ComponentMiner);
            if (HomeRules.IsTall(id) && Terrain.ExtractData(Value(p)) == Terrain.ExtractData(next))
            {
                Point3 top = new Point3(p.X, p.Y + 1, p.Z);
                int oldTop = Value(top);
                if (Terrain.ExtractContents(oldTop) == HomeRules.Upper && HomeRules.LowerId(Terrain.ExtractData(oldTop)) == id)
                    Change(top, Terrain.MakeBlockValue(HomeRules.Upper, 0, HomeRules.UpperData(id, Terrain.ExtractData(next))), player.ComponentMiner);
            }
            if (lastRequest.Count > 256) lastRequest.Clear();
        }
        /// <inheritdoc/>
        public override void OnHitByProjectile(CellFace face, WorldItem item) { /* Furniture never acquires thrown items implicitly. */ }
        internal static void AdaptGui(ComponentGui gui)
        {
            var chest = gui?.ModalPanelWidget as ChestWidget;
            if (chest != null && !(chest is HomeStorageWidget) && chest.m_componentChest is ComponentHomeStorage)
                gui.ModalPanelWidget = new HomeStorageWidget(gui.m_componentPlayer.ComponentMiner.Inventory, (ComponentHomeStorage)chest.m_componentChest);
        }
        /// <inheritdoc/>
        public override void Dispose() { lock (requests) requests.Clear(); added.Clear(); waiting.Clear(); placers.Clear(); removing.Clear(); lastRequest.Clear(); footprintBusy.Clear(); base.Dispose(); }
    }
}
