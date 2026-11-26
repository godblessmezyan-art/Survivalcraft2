using System;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;

namespace SCIEPort.Features.Creatures
{
    /// <summary>Registers surface wildlife with the audited native update-thread spawner.</summary>
    internal static class CreatureWorldSpawn
    {
        private static Project s_project;
        private static double s_spawnTime = double.NaN;
        private static int s_frameSpawns;

        internal static void Update()
        {
            Project project = GameManager.Project;
            if (project == null) { s_project = null; return; }
            if (ReferenceEquals(project, s_project)) return;
            var spawn = project.FindSubsystem<SubsystemCreatureSpawn>(false);
            if (spawn == null) return;
            for (int species = 0; species < WildlifeRules.Names.Length; species++) Register(spawn, species);
            s_spawnTime = double.NaN;
            s_frameSpawns = 0;
            s_project = project;
        }

        private static void Register(SubsystemCreatureSpawn spawn, int species)
        {
            string name = WildlifeRules.Names[species];
            foreach (SubsystemCreatureSpawn.CreatureType existing in spawn.m_creatureTypes)
                if (existing.Name == name) return;
            spawn.m_creatureTypes.Add(new SubsystemCreatureSpawn.CreatureType(name, SpawnLocationType.Surface, true, false)
            {
                SpawnSuitabilityFunction = (type, point) => Suitability(spawn, species, point),
                SpawnFunction = (type, point) => Spawn(spawn, type, species, point)
            });
        }

        private static float Suitability(SubsystemCreatureSpawn spawn, int species, Point3 point)
        {
            if (CommonLib.WorkType == WorkType.Client) return 0f;
            Terrain terrain = spawn.m_subsystemTerrain.Terrain;
            TerrainChunk chunk = terrain.GetChunkAtCell(point.X, point.Z);
            if (chunk == null || chunk.State < TerrainChunkState.InvalidLight || point.Y < 2 || point.Y > 252) return 0f;
            if (species >= WildlifeRules.SandScorpion)
            {
                if (!ArthropodHabitat.Suitable(terrain, species, point)) return 0f;
                var center = new Vector2(point.X, point.Z);
                int count = 0;
                int local = 0;
                foreach (ComponentCreature creature in spawn.Creatures)
                {
                    if (creature.ComponentHealth.Health <= 0f ||
                        creature.Entity.ValuesDictionary.DatabaseObject.Name != WildlifeRules.Names[species]) continue;
                    count++;
                    if ((creature.ComponentBody.Position.XZ - center).LengthSquared() <= 14f * 14f) local++;
                }
                return count < WildlifeRules.Caps[species] && local < 2 ? WildlifeRules.Weights[species] : 0f;
            }
            int ground = Terrain.ExtractContents(terrain.GetCellValueFast(point.X, point.Y - 1, point.Z));
            bool natural = BlocksManager.Blocks[ground] is GrassBlock || BlocksManager.Blocks[ground] is DirtBlock;
            bool open = terrain.GetTopHeight(point.X, point.Z) < point.Y
                && Terrain.ExtractContents(terrain.GetCellValueFast(point.X, point.Y, point.Z)) == 0
                && Terrain.ExtractContents(terrain.GetCellValueFast(point.X, point.Y + 1, point.Z)) == 0;
            if (!WildlifeRules.Habitat(species, terrain.GetTemperature(point.X, point.Z), terrain.GetHumidity(point.X, point.Z), point.Y, natural, open)) return 0f;
            return Count(spawn, WildlifeRules.Names[species]) < WildlifeRules.Caps[species] ? WildlifeRules.Weights[species] : 0f;
        }

        private static int Count(SubsystemCreatureSpawn spawn, string name)
        {
            int count = 0;
            foreach (ComponentCreature creature in spawn.Creatures)
                if (creature.ComponentHealth.Health > 0f && creature.Entity.ValuesDictionary.DatabaseObject.Name == name) count++;
            return count;
        }

        private static int Spawn(SubsystemCreatureSpawn spawn, SubsystemCreatureSpawn.CreatureType type, int species, Point3 point)
        {
            if (CommonLib.WorkType == WorkType.Client || Suitability(spawn, species, point) <= 0f) return 0;
            double now = spawn.m_subsystemTime.GameTime;
            if (now != s_spawnTime) { s_spawnTime = now; s_frameSpawns = 0; }
            int count = WildlifeRules.SpawnCount(species, Count(spawn, type.Name), s_frameSpawns,
                spawn.m_random.Int(1, WildlifeRules.GroupMax[species]));
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (spawn.CountCreatures(false) >= SubsystemCreatureSpawn.m_totalLimit) break;
                var center = new Vector2(point.X, point.Z);
                if (spawn.CountCreaturesInArea(center - new Vector2(16f), center + new Vector2(16f), false) >= SubsystemCreatureSpawn.m_maxPointLimit) break;
                try
                {
                    int added = species >= WildlifeRules.SandScorpion
                        ? (spawn.SpawnCreature(type.Name, new Vector3(point.X + .5f, point.Y + .06f, point.Z + .5f), type.ConstantSpawn) != null ? 1 : 0)
                        : spawn.SpawnCreatures(type, type.Name, point, 1).Count;
                    spawned += added; s_frameSpawns += added;
                }
                catch (Exception error)
                {
                    Log.Error("[SCIEPort] Wildlife spawn failed for " + type.Name + ": " + error);
                    break;
                }
            }
            return spawned;
        }
    }
}
