using System;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using SCIEPort.Features.Shrines;

namespace SCIEPort.Features.Pyramid
{
    /// <summary>Creates the pyramid's resident creatures on the authoritative update thread.</summary>
    internal sealed class PyramidGuardians
    {
        private const string Mummy = "SCIEPortMummy", Pharaoh = "SCIEPortPharaoh";
        private const float ArrivalRange = 14f;
        private static readonly Encounter[] LegacyEncounters =
        {
            new Encounter(Mummy, new Point3(40, 1, 25), BasaltBlock.Index,
                new Point3(28, 1, 14), new Point3(51, 9, 44)),
            new Encounter(Mummy, new Point3(66, 1, 27), LimestoneBlock.Index,
                new Point3(51, 1, 18), new Point3(68, 7, 42)),
            new Encounter(Pharaoh, new Point3(43, -10, 64), LimestoneBlock.Index,
                new Point3(28, -11, 56), new Point3(51, -4, 71))
        };
        private static readonly Encounter[] MazeEncounters =
        {
            MazeRoom(0, 17), MazeRoom(0, 36), MazeRoom(0, 53),
            MazeRoom(1, 11), MazeRoom(1, 29), MazeRoom(1, 47),
            MazeRoom(2, 22), MazeRoom(2, 40), MazeRoom(2, PyramidMaze.Count - 1, Pharaoh)
        };
        private static readonly Encounter TombEntry = MazeRoom(0, 3);

        private readonly SubsystemTerrain terrain;
        private readonly SubsystemTime time;
        private readonly SubsystemPlayers players;
        private readonly SubsystemBodies bodies;
        private readonly Func<Point2, PyramidState> find;
        private readonly DynamicArray<ComponentBody> nearbyBodies = new DynamicArray<ComponentBody>();
        private double nextCheck;
        private int nextPlayer;

        internal PyramidGuardians(SubsystemTerrain terrain, Func<Point2, PyramidState> find)
        {
            this.terrain = terrain;
            this.find = find;
            time = terrain.Project.FindSubsystem<SubsystemTime>(true);
            players = terrain.Project.FindSubsystem<SubsystemPlayers>(true);
            bodies = terrain.Project.FindSubsystem<SubsystemBodies>(true);
        }

        internal void Update()
        {
            if (CommonLib.WorkType == WorkType.Client || time.GameTime < nextCheck) return;
            nextCheck = time.GameTime + 0.5;
            var activePlayers = players.ComponentPlayers;
            for (int checkedPlayers = 0; checkedPlayers < Math.Min(4, activePlayers.Count); checkedPlayers++)
            {
                if (nextPlayer >= activePlayers.Count) nextPlayer = 0;
                ComponentPlayer player = activePlayers[nextPlayer++];
                if (!player.IsAddedToProject || player.ComponentHealth.Health <= 0f) continue;
                Vector3 position = player.ComponentBody.Position;
                PyramidState state = find(new Point2(Terrain.ToCell(position.X), Terrain.ToCell(position.Z)));
                if (state == null || state.GuardiansSpawned == Mask(state.Site)) continue;
                int index = EncounterFor(state, position);
                if (index < 0 || !IsSpawnClear(terrain, state.Site, index)) continue;
                Vector3 spawn = SpawnPosition(state.Site, index);
                if (IsOccupied(spawn)) continue;
                TrySpawn(state, index, spawn);
                return;
            }
        }

        internal static int EncounterFor(PyramidState state, Vector3 position)
        {
            if (state.Site.Maze == null && state.Slices != PyramidSite.Complete) return -1;
            var column = new Point2(Terrain.ToCell(position.X), Terrain.ToCell(position.Z));
            if ((state.Slices & state.Site.ChunkBit(column)) == 0) return -1;
            var local = new Point3(column.X - state.Site.Origin.X,
                Terrain.ToCell(position.Y) - state.Site.BaseY, column.Y - state.Site.Origin.Y);
            Encounter[] encounters = ForSite(state.Site);
            for (int i = 0; i < encounters.Length; i++)
            {
                Encounter encounter = EncounterAt(state.Site, i);
                if ((state.GuardiansSpawned & (1 << i)) != 0 || (encounter.Template == Pharaoh && !state.Solved)) continue;
                Point3 spawn = state.Site.World(encounter.Point);
                if ((state.Slices & state.Site.ChunkBit(new Point2(spawn.X, spawn.Z))) == 0) continue;
                if (local.X >= encounter.RoomMin.X && local.X <= encounter.RoomMax.X &&
                    local.Y >= encounter.RoomMin.Y && local.Y <= encounter.RoomMax.Y &&
                    local.Z >= encounter.RoomMin.Z && local.Z <= encounter.RoomMax.Z &&
                    Vector3.DistanceSquared(position, SpawnPosition(state.Site, i)) <= ArrivalRange * ArrivalRange)
                    return i;
            }
            return -1;
        }

        internal static int Mask(PyramidSite site) => (1 << ForSite(site).Length) - 1;

        private static Encounter[] ForSite(PyramidSite site) => site.Maze == null ? LegacyEncounters : MazeEncounters;
        private static Encounter EncounterAt(PyramidSite site, int index) =>
            site.Tomb && index == 0 ? TombEntry : ForSite(site)[index];

        private static Encounter MazeRoom(int level, int cell, string template = Mummy)
        {
            Point3 floor = PyramidMaze.Room(level, cell);
            return new Encounter(template, floor + new Point3(0, 1, 0), LimestoneBlock.Index,
                floor + new Point3(-5, 1, -5), floor + new Point3(5, 3, 5));
        }

        internal static Vector3 SpawnPosition(PyramidSite site, int index) =>
            new Vector3(site.World(EncounterAt(site, index).Point)) + new Vector3(0.5f, 0.01f, 0.5f);

        internal static bool IsSpawnClear(SubsystemTerrain terrain, PyramidSite site, int index)
        {
            Encounter encounter = EncounterAt(site, index);
            Point3 point = site.World(encounter.Point);
            if (!ShrineWorld.Ready(terrain, point) || !ShrineWorld.AllowsChange(point)) return false;
            Terrain cells = terrain.Terrain;
            return cells.GetCellContents(point.X, point.Y - 1, point.Z) == encounter.Floor &&
                cells.GetCellContents(point.X, point.Y, point.Z) == AirBlock.Index &&
                cells.GetCellContents(point.X, point.Y + 1, point.Z) == AirBlock.Index &&
                cells.GetCellContents(point.X, point.Y + 2, point.Z) == AirBlock.Index;
        }

        private bool IsOccupied(Vector3 position)
        {
            // Reserve the whole spawn cell (both creatures have sub-block width) and three blocks of height.
            var box = new BoundingBox(position - new Vector3(.5f, 0f, .5f), position + new Vector3(.5f, 3f, .5f));
            nearbyBodies.Clear();
            bodies.FindBodiesInArea(box.Min.XZ, box.Max.XZ, nearbyBodies);
            foreach (ComponentBody body in nearbyBodies)
                if (box.Intersection(body.BoundingBox)) return true;
            return false;
        }

        private void TrySpawn(PyramidState state, int index, Vector3 position)
        {
            Project project = terrain.Project;
            string name = EncounterAt(state.Site, index).Template;
            Entity entity = null;
            try
            {
                entity = DatabaseManager.CreateEntity(project, name, throwIfNotFound: true);
                var creature = entity.FindComponent<ComponentCreature>(true);
                creature.ComponentBody.Position = position;
                creature.ComponentSpawn.SpawnDuration = 0f;
                project.AddEntity(entity);
                state.MarkGuardianSpawned(index);
            }
            catch (Exception error)
            {
                if (entity != null && entity.IsAddedToProject) state.MarkGuardianSpawned(index);
                else entity?.Dispose();
                nextCheck = time.GameTime + 10;
                Log.Error("[SCIEPort] Pyramid guardian spawn failed for " + name + ": " + error);
            }
        }

        private readonly struct Encounter
        {
            internal readonly string Template;
            internal readonly Point3 Point, RoomMin, RoomMax;
            internal readonly int Floor;

            internal Encounter(string template, Point3 point, int floor, Point3 roomMin, Point3 roomMax)
            {
                Template = template;
                Point = point;
                Floor = floor;
                RoomMin = roomMin;
                RoomMax = roomMax;
            }
        }
    }
}
