using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using SCIEPort.Features.Geology;
using SCIEPort.Features.Shrines;
using TemplatesDatabase;

namespace SCIEPort.Features.Pyramid
{
    /// <summary>Owns pyramid generation reservations, seal progress, and single-use prizes.</summary>
    public sealed class SubsystemPyramid : SubsystemBlockBehavior, IUpdateable
    {
        private readonly object gate = new object();
        private readonly Dictionary<Point2, PyramidState> sites = new Dictionary<Point2, PyramidState>();
        private readonly Dictionary<Point2, PyramidSite> candidates = new Dictionary<Point2, PyramidSite>();
        private readonly Queue<(ComponentPlayer Player, Point3 Point)> requests = new Queue<(ComponentPlayer, Point3)>();
        private readonly Queue<PyramidState> pending = new Queue<PyramidState>();
        private readonly HashSet<PyramidState> scheduled = new HashSet<PyramidState>();
        private SubsystemBlockEntities entities;
        private PyramidGuardians guardians;

        /// <inheritdoc/>
        public override int[] HandledBlocks => new int[0]; // Pyramid.csv supplies the native named binding.
        /// <inheritdoc/>
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <inheritdoc/>
        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            entities = Project.FindSubsystem<SubsystemBlockEntities>(true);
            if (CommonLib.WorkType == WorkType.Client) return;
            guardians = new PyramidGuardians(SubsystemTerrain, Find);
            foreach (object entry in values.GetValue("Pyramids", new ValuesDictionary()).Values)
            {
                if (!(entry is ValuesDictionary row)) continue;
                Point2 origin = row.GetValue("Origin", new Point2());
                int baseY = row.GetValue("Base", 0), order = row.GetValue("Order", -1), reward = row.GetValue("Reward", 0);
                if (!PyramidSite.ValidOrigin(origin) || baseY < 32 || baseY + PyramidLayout.HighY >= 248 ||
                    order < 0 || order > 5 || !PyramidLoot.IsEligible(reward)) continue;
                var site = new PyramidSite(origin, baseY, order, reward);
                sites[PyramidSite.Region(origin)] = new PyramidState(site, row.GetValue("Slices", 0),
                    row.GetValue("Progress", 0), row.GetValue("Stocked", false), row.GetValue("GuardiansSpawned", 0));
            }
        }

        /// <inheritdoc/>
        public override void Save(ValuesDictionary values)
        {
            if (CommonLib.WorkType == WorkType.Client) return;
            var records = new ValuesDictionary();
            lock (gate)
            {
                foreach (PyramidState state in sites.Values)
                {
                    if (state.Slices == 0) continue;
                    var row = new ValuesDictionary();
                    row.SetValue("Origin", state.Site.Origin);
                    row.SetValue("Base", state.Site.BaseY);
                    row.SetValue("Order", state.Site.Order);
                    row.SetValue("Reward", state.Site.RewardId);
                    row.SetValue("Slices", state.Slices);
                    row.SetValue("Progress", state.Progress);
                    row.SetValue("Stocked", state.Stocked);
                    row.SetValue("GuardiansSpawned", state.GuardiansSpawned);
                    records.SetValue("Pyramid" + records.Count, row);
                }
            }
            values.SetValue("Pyramids", records);
        }

        internal bool Generate(TerrainChunk chunk, ITerrainContentsGenerator generator, int seed, TerrainGenerationMode mode)
        {
            if (CommonLib.WorkType == WorkType.Client || chunk.IsLoaded || !GeologySites.Enabled(mode) ||
                !(BlocksManager.Blocks[PyramidMechanismBlock.Index] is PyramidMechanismBlock) ||
                !PyramidSite.InBounds(chunk.Origin.X, chunk.Origin.Y)) return false;

            Point2 region = PyramidSite.Region(chunk.Origin);
            lock (gate)
            {
                if (!sites.TryGetValue(region, out PyramidState state))
                {
                    if (!candidates.TryGetValue(region, out PyramidSite candidate))
                    {
                        if (candidates.Count >= 128) candidates.Clear();
                        candidate = PyramidSite.Choose(generator, seed, region);
                        candidates[region] = candidate;
                    }
                    if (candidate == null || candidate.ChunkBit(chunk.Origin) == 0) return false;
                    if (ShrineWorld.OverlapsSavedChunks(SubsystemTerrain, candidate.Origin, PyramidSite.ChunkSide, PyramidSite.ChunkSide))
                    {
                        candidates[region] = null;
                        return false;
                    }
                    state = new PyramidState(candidate);
                    sites.Add(region, state);
                }
                int slice = state.Site.ChunkBit(chunk.Origin);
                if (slice == 0 || !PyramidTerrain.Apply(chunk, state.Site)) return false;
                state.Slices |= slice;
                return true;
            }
        }

        /// <inheritdoc/>
        public override void OnChunkInitialized(TerrainChunk chunk)
        {
            if (CommonLib.WorkType == WorkType.Client) return;
            PyramidState state = Find(chunk.Origin);
            if (state != null && state.Slices == PyramidSite.Complete) Schedule(state);
        }

        /// <inheritdoc/>
        public override bool OnInteract(TerrainRaycastResult hit, ComponentMiner miner)
        {
            ComponentPlayer player = miner?.ComponentPlayer;
            if (player == null || !BlocksManager.Blocks[PyramidMechanismBlock.Index].IsInteractive(SubsystemTerrain, hit.Value))
                return false;
            Point3 point = hit.CellFace.Point;
            if (CommonLib.WorkType == WorkType.Client)
            {
                if (player == CommonLib.MainPlayer)
                    CommonLib.Net?.QueuePackage(new PyramidPackage { Point = point, To = CommonLib.Net.Server });
            }
            else Enqueue(player, point);
            return true;
        }

        internal void Enqueue(ComponentPlayer player, Point3 point)
        {
            lock (requests)
            {
                if (requests.Count < 24) requests.Enqueue((player, point));
            }
        }

        /// <inheritdoc/>
        public void Update(float dt)
        {
            if (CommonLib.WorkType == WorkType.Client) return;
            for (int count = 0; count < 4; count++)
            {
                (ComponentPlayer Player, Point3 Point) request;
                lock (requests)
                {
                    if (requests.Count == 0) break;
                    request = requests.Dequeue();
                }
                ApplyUse(request.Player, request.Point);
            }
            for (int count = 0; count < 2 && pending.Count != 0; count++)
            {
                PyramidState state = pending.Dequeue();
                scheduled.Remove(state);
                Synchronize(state);
            }
            guardians?.Update();
        }

        private PyramidState Find(Point2 point)
        {
            lock (gate)
            {
                return sites.TryGetValue(PyramidSite.Region(point), out PyramidState state) &&
                    state.Site.ChunkBit(point) != 0 ? state : null;
            }
        }

        private void Schedule(PyramidState state)
        {
            if (pending.Count < 32 && scheduled.Add(state)) pending.Enqueue(state);
        }

        private void ApplyUse(ComponentPlayer player, Point3 point)
        {
            if (!player.IsAddedToProject || player.Project != Project || player.ComponentHealth.Health <= 0f ||
                !ShrineWorld.Ready(SubsystemTerrain, point) || !ShrineWorld.AllowsChange(point))
                return;
            PyramidState state = Find(new Point2(point.X, point.Z));
            if (state == null || state.Slices != PyramidSite.Complete || state.Solved)
                return;
            for (int index = 0; index < PyramidSite.Seals.Length; index++)
            {
                if (point != state.Site.World(PyramidSite.Seals[index]) ||
                    state.IsLit(index) || Cell(point) != PyramidMechanismBlock.Value(index) ||
                    !ShrineWorld.CanReach(SubsystemTerrain, player, point)) continue;
                if (state.Press(index)) Schedule(state);
                break;
            }
        }

        private void Synchronize(PyramidState state)
        {
            if (state.Slices != PyramidSite.Complete) return;
            PyramidSite site = state.Site;
            for (int index = 0; index < PyramidSite.Seals.Length; index++)
            {
                Point3 point = site.World(PyramidSite.Seals[index]);
                if (!ShrineWorld.Ready(SubsystemTerrain, point)) continue;
                int dark = PyramidMechanismBlock.Value(index), lit = PyramidMechanismBlock.Value(index + PyramidMechanismBlock.Lit);
                int current = Cell(point), desired = state.IsLit(index) ? lit : dark;
                if (current != desired && (current == dark || current == lit)) Change(point, desired);
            }
            if (!state.Solved) return;
            for (int x = 37; x <= 42; x++)
            for (int y = 1; y <= 4; y++)
            {
                Point3 point = site.World(new Point3(x, y, PyramidSite.GateZ));
                if (ShrineWorld.Ready(SubsystemTerrain, point) && Cell(point) == PyramidMechanismBlock.Value(PyramidMechanismBlock.Gate))
                    Change(point, 0);
            }
            if (!state.Stocked && PyramidLoot.IsRegistered(site.RewardId) &&
                ShrineWorld.TryStock(SubsystemTerrain, entities, site.World(PyramidSite.Display),
                    PyramidMechanismBlock.Value(PyramidMechanismBlock.Seal), Terrain.MakeBlockValue(site.RewardId)))
                state.MarkStocked();
        }

        private int Cell(Point3 point) =>
            Terrain.ReplaceLight(SubsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z), 0);

        private void Change(Point3 point, int value) => SubsystemTerrain.ChangeCell(point.X, point.Y, point.Z, value);
    }
}
