using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Geology;
using SCIEPort.Features.Poseidon;
using SCIEPort.Features.Suspension;
using TemplatesDatabase;

namespace SCIEPort.Features.TidePalace
{
    /// <summary>Authoritative palace reservations, mechanism progress, and one-time reward.</summary>
    public sealed class SubsystemTidePalace : SubsystemBlockBehavior, IUpdateable
    {
        private readonly object sitesLock = new object();
        private readonly Dictionary<Point2, TidePalaceState> sites = new Dictionary<Point2, TidePalaceState>();
        private readonly Dictionary<Point2, PalaceSite> candidates = new Dictionary<Point2, PalaceSite>();
        private readonly Queue<UseRequest> requests = new Queue<UseRequest>();
        private readonly Queue<TidePalaceState> pending = new Queue<TidePalaceState>();
        private readonly HashSet<TidePalaceState> scheduled = new HashSet<TidePalaceState>();
        private SubsystemBlockEntities entities;

        /// <inheritdoc/>
        public override int[] HandledBlocks => new int[0]; // TidePalace.csv provides the named binding.
        /// <inheritdoc/>
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <inheritdoc/>
        public override void Load(ValuesDictionary values)
        {
            base.Load(values);
            entities = Project.FindSubsystem<SubsystemBlockEntities>(true);
            if (CommonLib.WorkType == WorkType.Client) return;
            foreach (object entry in values.GetValue("Palaces", new ValuesDictionary()).Values)
            {
                if (!(entry is ValuesDictionary row)) continue;
                Point2 origin = row.GetValue("Origin", new Point2(0, 0));
                int deck = row.GetValue("Deck", 0), solution = row.GetValue("Solution", 0);
                if (!ValidOrigin(origin) || deck < 24 || deck > 224 || solution < 1 || solution > 26) continue;
                var state = new TidePalaceState(new PalaceSite(origin, deck, solution), row.GetValue("Slices", 0),
                    row.GetValue("Dials", 0), row.GetValue("Flags", 0));
                sites[PalaceSite.Region(origin)] = state;
            }
        }

        /// <inheritdoc/>
        public override void Save(ValuesDictionary values)
        {
            if (CommonLib.WorkType == WorkType.Client) return;
            var records = new ValuesDictionary();
            lock (sitesLock)
            {
                foreach (TidePalaceState state in sites.Values)
                {
                    if (state.Slices == 0) continue;
                    var row = new ValuesDictionary();
                    row.SetValue("Origin", state.Site.Origin);
                    row.SetValue("Deck", state.Site.BaseY);
                    row.SetValue("Solution", state.Site.Solution);
                    row.SetValue("Slices", state.Slices);
                    row.SetValue("Dials", state.Dials);
                    row.SetValue("Flags", state.SaveFlags);
                    records.SetValue("Palace" + records.Count, row);
                }
            }
            values.SetValue("Palaces", records);
        }

        private static bool ValidOrigin(Point2 origin) =>
            PalaceSite.InBounds(origin.X, origin.Y) && PalaceSite.InBounds(origin.X + 63, origin.Y + 47) &&
            (origin.X & 15) == 0 && (origin.Y & 15) == 0 &&
            PalaceSite.Region(origin) == PalaceSite.Region(new Point2(origin.X + 63, origin.Y + 47));

        // Called exclusively from the native generation pass. The world's storage serializes Exists
        // with its own I/O lock. Never modify an already serialized slice or mutate neighbor chunks.
        internal bool Generate(TerrainChunk chunk, ITerrainContentsGenerator generator, int seed, TerrainGenerationMode mode)
        {
            if (CommonLib.WorkType == WorkType.Client || chunk.IsLoaded || !GeologySites.Enabled(mode) ||
                !GeologyCube.Registered || !(BlocksManager.Blocks[TidePalaceBlock.Index] is TidePalaceBlock) ||
                !PalaceSite.InBounds(chunk.Origin.X, chunk.Origin.Y)) return false;
            Point2 region = PalaceSite.Region(chunk.Origin);
            lock (sitesLock)
            {
                if (!sites.TryGetValue(region, out TidePalaceState state))
                {
                    if (!candidates.TryGetValue(region, out PalaceSite candidate))
                    {
                        if (candidates.Count >= 128) candidates.Clear();
                        candidate = PalaceSite.Choose(generator, seed, region);
                        candidates[region] = candidate;
                    }
                    if (candidate == null || candidate.ChunkBit(chunk.Origin) == 0) return false;
                    if (OverlapsSavedChunks(candidate))
                    {
                        candidates[region] = null;
                        return false;
                    }
                    state = new TidePalaceState(candidate);
                    sites.Add(region, state);
                }
                int slice = state.Site.ChunkBit(chunk.Origin);
                if (slice == 0 || !PalaceTerrain.Apply(chunk, state.Site, generator.OceanLevel)) return false;
                state.Slices |= slice;
                return true;
            }
        }

        private bool OverlapsSavedChunks(PalaceSite site)
        {
            TerrainSerializer23.IStorage storage = SubsystemTerrain.TerrainSerializer?.m_storage;
            if (storage == null) return true;
            int originX = site.Origin.X >> 4, originZ = site.Origin.Y >> 4;
            for (int z = 0; z < 3; z++)
            for (int x = 0; x < 4; x++)
                if (storage.Exists(new Point2(originX + x, originZ + z))) return true;
            return false;
        }

        /// <inheritdoc/>
        public override void OnChunkInitialized(TerrainChunk chunk)
        {
            if (CommonLib.WorkType == WorkType.Client) return;
            TidePalaceState state = Find(chunk.Origin);
            if (state != null && (state.SaveFlags != 0 || state.Dials != 0)) Schedule(state);
        }

        /// <inheritdoc/>
        public override bool OnInteract(TerrainRaycastResult hit, ComponentMiner miner)
        {
            if (miner?.ComponentPlayer == null || !TidePalaceBlock.Matches(hit.Value, 0, TidePalaceBlock.Weight)) return false;
            Point3 point = hit.CellFace.Point;
            byte data = (byte)Terrain.ExtractData(hit.Value);
            if (CommonLib.WorkType == WorkType.Client)
            {
                if (miner.ComponentPlayer == CommonLib.MainPlayer)
                    CommonLib.Net?.QueuePackage(new TidePalacePackage { Point = point, ExpectedData = data, To = CommonLib.Net.Server });
            }
            else Enqueue(miner.ComponentPlayer, point, data);
            return true;
        }

        internal void Enqueue(ComponentPlayer player, Point3 point, byte expectedData)
        {
            lock (requests)
            {
                if (requests.Count < 24) requests.Enqueue(new UseRequest(player, point, expectedData));
            }
        }

        /// <inheritdoc/>
        public void Update(float dt)
        {
            if (CommonLib.WorkType == WorkType.Client) return;
            for (int count = 0; count < 4; count++)
            {
                UseRequest request;
                lock (requests)
                {
                    if (requests.Count == 0) break;
                    request = requests.Dequeue();
                }
                ApplyUse(request);
            }
            for (int count = 0; count < 2 && pending.Count != 0; count++)
            {
                TidePalaceState state = pending.Dequeue();
                scheduled.Remove(state);
                Synchronize(state);
            }
        }

        private TidePalaceState Find(Point2 point)
        {
            lock (sitesLock)
            {
                return sites.TryGetValue(PalaceSite.Region(point), out TidePalaceState state) &&
                    state.Site.ChunkBit(point) != 0 ? state : null;
            }
        }

        private void Schedule(TidePalaceState state)
        {
            if (pending.Count < 32 && scheduled.Add(state)) pending.Enqueue(state);
        }

        private void ApplyUse(UseRequest request)
        {
            ComponentPlayer player = request.Player;
            Point3 point = request.Point;
            if (!player.IsAddedToProject || player.Project != Project || player.ComponentHealth.Health <= 0f ||
                !Ready(point) || !AllowsChange(point)) return;
            int value = Cell(point);
            if (!TidePalaceBlock.Matches(value, request.Expected, request.Expected)) return;
            TidePalaceState state = Find(new Point2(point.X, point.Z));
            if (state == null || state.Slices != PalaceSite.Complete || !CanReach(player, point)) return;

            PalaceSite site = state.Site;
            if (point == site.World(PalaceSite.West))
            {
                if (request.Expected == TidePalaceBlock.Weight && state.Lift()) Schedule(state);
                return;
            }
            for (int dial = 0; dial < PalaceSite.Dials.Length; dial++)
            {
                if (point != site.World(PalaceSite.Dials[dial]) ||
                    request.Expected != dial * 3 + PalaceSite.Digit(state.Dials, dial) || !state.Turn(dial)) continue;
                Change(point, TidePalaceBlock.Value(dial * 3 + PalaceSite.Digit(state.Dials, dial)));
                Schedule(state);
                break;
            }
        }

        private bool CanReach(ComponentPlayer player, Point3 point)
        {
            Vector3 eye = player.ComponentCreatureModel.EyePosition;
            Vector3 target = new Vector3(point) + new Vector3(.5f);
            if (!(Vector3.DistanceSquared(eye, target) <= 25f)) return false;
            TerrainRaycastResult? hit = SubsystemTerrain.Raycast(eye, target, true, true,
                (value, distance) => !BlocksManager.Blocks[Terrain.ExtractContents(value)].IsPlacementTransparent);
            return hit.HasValue && hit.Value.CellFace.Point == point;
        }

        private void Synchronize(TidePalaceState state)
        {
            if (state.Slices != PalaceSite.Complete) return;
            PalaceSite site = state.Site;
            for (int dial = 0; dial < PalaceSite.Dials.Length; dial++)
                Refresh(site.World(PalaceSite.Dials[dial]), dial * 3, dial * 3 + 2, dial * 3 + PalaceSite.Digit(state.Dials, dial));
            if (state.West)
            {
                Refresh(site.World(PalaceSite.West), TidePalaceBlock.Weight, TidePalaceBlock.Weight, TidePalaceBlock.LiftedWeight);
                Refresh(site.World(PalaceSite.WestLight), TidePalaceBlock.Signal, TidePalaceBlock.Signal, TidePalaceBlock.LitSignal);
            }
            if (state.East)
                Refresh(site.World(PalaceSite.EastLight), TidePalaceBlock.Signal, TidePalaceBlock.Signal, TidePalaceBlock.LitSignal);
            if (!state.Unsealed) return;
            for (int x = 27; x <= 31; x++)
            for (int y = 6; y <= 8; y++)
            {
                Point3 point = site.World(new Point3(x, y, 30));
                if (Ready(point) && TidePalaceBlock.Matches(Cell(point), TidePalaceBlock.Gate, TidePalaceBlock.Gate))
                    Change(point, 0);
            }
            if (state.CanStock) Stock(state);
        }

        private void Stock(TidePalaceState state)
        {
            Point3 point = state.Site.World(PalaceSite.Display);
            if (!Ready(point) || !(BlocksManager.Blocks[PoseidonBlock.Index] is PoseidonBlock) ||
                !(BlocksManager.Blocks[SuspendedItemBlock.Index] is SuspendedItemBlock)) return;
            int old = Cell(point), id = Terrain.ExtractContents(old);
            if (id != 0 && id != SuspendedItemBlock.Index && !TidePalaceBlock.Matches(old, TidePalaceBlock.Seal, TidePalaceBlock.Seal))
                return;
            if (entities.GetBlockEntity(point.X, point.Y, point.Z) != null) return;
            var database = Project.GameDatabase;
            var template = database.Database.FindDatabaseObject("SCIEPortSuspendedItem", database.EntityTemplateType, true);
            var values = new ValuesDictionary();
            values.PopulateFromDatabaseObject(template);
            var blockEntity = values.GetValue<ValuesDictionary>("BlockEntity");
            blockEntity.SetValue("Coordinates", point);
            blockEntity.SetValue("Owner", Guid.Empty);
            Entity entity = Project.CreateEntity(values);
            var inventory = entity.FindComponent<ComponentSuspendedItem>(true);
            // Prepare the real inventory before the entity is added, so its creation packet
            // and persisted registry see the same single artifact as the local display.
            if (!inventory.AddNetSlotItems(0, Terrain.MakeBlockValue(PoseidonBlock.Index), 1))
            {
                entity.Dispose();
                return;
            }
            Change(point, Terrain.MakeBlockValue(SuspendedItemBlock.Index));
            if (Terrain.ExtractContents(Cell(point)) != SuspendedItemBlock.Index)
            {
                entity.Dispose();
                return;
            }
            Project.AddEntity(entity);
            state.MarkStocked();
        }

        private void Refresh(Point3 point, int first, int last, int data)
        {
            if (!Ready(point)) return;
            int value = Cell(point);
            if (TidePalaceBlock.Matches(value, first, last) && Terrain.ExtractData(value) != data)
                Change(point, TidePalaceBlock.Value(data));
        }

        private bool Ready(Point3 point)
        {
            TerrainChunk chunk = SubsystemTerrain.Terrain.GetChunkAtCell(point.X, point.Z);
            return chunk != null && chunk.State >= TerrainChunkState.InvalidLight;
        }

        private int Cell(Point3 point) => SubsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
        private void Change(Point3 point, int value) => SubsystemTerrain.ChangeCell(point.X, point.Y, point.Z, value);

        private static bool AllowsChange(Point3 point)
        {
            if (!SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(point.X, point.Z, out Territoriy territory)) return true;
            return territory.AllowBlockBehavior &&
                (!territory.IsVisible || !SubsystemBedrockBlockBehavior.IsInTerritoriyBorder(territory, point.X, point.Z));
        }

        private readonly struct UseRequest
        {
            internal readonly ComponentPlayer Player;
            internal readonly Point3 Point;
            internal readonly byte Expected;
            internal UseRequest(ComponentPlayer player, Point3 point, byte expected)
            {
                Player = player;
                Point = point;
                Expected = expected;
            }
        }
    }
}
