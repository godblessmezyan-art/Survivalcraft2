#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.KellyPlants
{
    public sealed class SubsystemSoybeanBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { SoybeanBlock.Index };

        public override int[] HandledBlocks { get { return s_handledBlocks; } }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            KellyPlantWorldGeneration.SetProject(Project);
        }

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            Terrain terrain = SubsystemTerrain.Terrain;
            if (Terrain.ExtractLight(terrain.GetCellValueFast(x, y + 1, z)) < 9)
            {
                return;
            }

            int data = Terrain.ExtractData(value);
            int size = SoybeanBlock.GetSize(data);
            if (size >= 7)
            {
                return;
            }

            int soilValue = terrain.GetCellValueFast(x, y - 1, z);
            if (Terrain.ExtractContents(soilValue) != SoilBlock.Index)
            {
                return;
            }

            int interval = 6;
            int soilData = Terrain.ExtractData(soilValue);
            if (SoilBlock.GetHydration(soilData))
            {
                interval -= 2;
            }
            if (SoilBlock.GetNitrogen(soilData) > 0)
            {
                interval -= 2;
            }

            int temperature = terrain.GetSeasonalTemperature(x, z)
                + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
            if (temperature <= 4)
            {
                interval += 6;
            }

            if (pollPass % MathUtils.Max(interval, 1) != 0)
            {
                return;
            }

            SubsystemTerrain.ChangeCell(
                x,
                y,
                z,
                Terrain.ReplaceData(value, SoybeanBlock.SetSize(data, size + 1)),
                true,
                null);

            if (size + 1 == 7)
            {
                int nitrogen = MathUtils.Max(SoilBlock.GetNitrogen(soilData) - 1, 0);
                SubsystemTerrain.ChangeCell(
                    x,
                    y - 1,
                    z,
                    Terrain.ReplaceData(soilValue, SoilBlock.SetNitrogen(soilData, nitrogen)),
                    true,
                    null);
            }
        }
    }

    public sealed class SubsystemWildBerryBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { WildBerryPlantBlock.Index };
        private SubsystemSeasons m_seasons;

        public override int[] HandledBlocks { get { return s_handledBlocks; } }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_seasons = Project.FindSubsystem<SubsystemSeasons>(false);
            KellyPlantWorldGeneration.SetProject(Project);
        }

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            bool fruiting = m_seasons == null ||
                m_seasons.Season == Season.Spring ||
                m_seasons.Season == Season.Summer;
            int data = Terrain.ExtractData(value);
            if (WildBerryPlantBlock.GetFruiting(data) != fruiting)
            {
                SubsystemTerrain.ChangeCell(
                    x,
                    y,
                    z,
                    Terrain.ReplaceData(value, WildBerryPlantBlock.SetFruiting(data, fruiting)),
                    true,
                    null);
            }
        }
    }

    public sealed class SubsystemKellyFruitBlockBehavior : SubsystemBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { OakLeavesBlock.Index, CactusFruitBlock.Index };
        private readonly Game.Random m_random = new Game.Random();
        private SubsystemPickables m_pickables;
        private SubsystemSeasons m_seasons;

        public override int[] HandledBlocks { get { return s_handledBlocks; } }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_pickables = Project.FindSubsystem<SubsystemPickables>(true);
            m_seasons = Project.FindSubsystem<SubsystemSeasons>(false);
            KellyPlantWorldGeneration.SetProject(Project);
        }

        public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
        {
            if (Terrain.ExtractContents(value) != OakLeavesBlock.Index)
            {
                return;
            }

            float chance = m_seasons != null && m_seasons.Season == Season.Autumn ? 0.15f : 0.04f;
            if (m_random.Float(0f, 1f) <= chance)
            {
                m_pickables.AddPickable(
                    Terrain.MakeBlockValue(AcornBlock.Index),
                    1,
                    new Vector3(x + 0.5f, y + 0.3f, z + 0.5f),
                    Vector3.Zero,
                    null);
            }
        }

        public override void OnNeighborBlockChanged(
            int x,
            int y,
            int z,
            int neighborX,
            int neighborY,
            int neighborZ)
        {
            Terrain terrain = SubsystemTerrain.Terrain;
            if (Terrain.ExtractContents(terrain.GetCellValue(x, y, z)) == CactusFruitBlock.Index &&
                Terrain.ExtractContents(terrain.GetCellValue(x, y - 1, z)) != CactusBlock.Index)
            {
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
            }
        }
    }

    public sealed class SubsystemKellyMushroomBlockBehavior : SubsystemPollableBlockBehavior, IUpdateable
    {
        private const int PermanentData = 32;
        private const int DryAgeMask = 31;
        private const int MaxDryAge = 30;
        private static readonly int[] s_handledBlocks =
        {
            MushroomPatchBlock.Index,
            FlyAgaricPatchBlock.Index
        };

        private readonly Queue<Point2> m_spawnQueue = new Queue<Point2>();
        private readonly Game.Random m_random = new Game.Random();
        private SubsystemWeather m_weather;
        private float m_previousRain;
        private float m_accumulator;
        private float m_postRainDelay;
        private bool m_qualifiedRain;

        public override int[] HandledBlocks { get { return s_handledBlocks; } }
        public UpdateOrder UpdateOrder { get { return UpdateOrder.Default; } }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_weather = Project.FindSubsystem<SubsystemWeather>(true);
            m_previousRain = m_weather.PrecipitationIntensity;
            KellyPlantWorldGeneration.SetProject(Project);
        }

        public void Update(float dt)
        {
            m_accumulator += dt;
            if (m_accumulator < 0.25f)
            {
                return;
            }
            float elapsed = m_accumulator;
            m_accumulator = 0f;

            float rain = m_weather.PrecipitationIntensity;
            if (rain >= 0.28f)
            {
                m_qualifiedRain = true;
            }
            if (m_qualifiedRain && m_previousRain > 0.05f && rain <= 0.05f)
            {
                QueueLoadedChunks();
                m_postRainDelay = 15f;
                m_qualifiedRain = false;
            }
            m_previousRain = rain;

            if (m_spawnQueue.Count == 0)
            {
                return;
            }
            if (m_postRainDelay > 0f)
            {
                m_postRainDelay -= elapsed;
                return;
            }

            Point2 coords = m_spawnQueue.Dequeue();
            TerrainChunk chunk = SubsystemTerrain.Terrain.GetChunkAtCell(coords.X * 16, coords.Y * 16);
            if (chunk != null && chunk.State == TerrainChunkState.Valid)
            {
                TrySpawnInChunk(chunk);
            }
        }

        private void QueueLoadedChunks()
        {
            m_spawnQueue.Clear();
            foreach (TerrainChunk chunk in SubsystemTerrain.Terrain.AllocatedChunks)
            {
                if (chunk != null && chunk.State == TerrainChunkState.Valid)
                {
                    m_spawnQueue.Enqueue(chunk.Coords);
                }
            }
        }

        private void TrySpawnInChunk(TerrainChunk chunk)
        {
            if (CountPatches(chunk, 3) >= 3)
            {
                return;
            }

            if (m_random.Float(0f, 1f) < 0.64f)
            {
                TrySpawnPatch(chunk, MushroomPatchBlock.Index);
            }
            if (CountPatches(chunk, 3) < 3 && m_random.Float(0f, 1f) < 0.20f)
            {
                TrySpawnPatch(chunk, FlyAgaricPatchBlock.Index);
            }
        }

        private bool TrySpawnPatch(TerrainChunk chunk, int contents)
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                int localX = m_random.Int(0, 15);
                int localZ = m_random.Int(0, 15);
                int worldX = chunk.Origin.X + localX;
                int worldZ = chunk.Origin.Y + localZ;
                int y = FindNaturalGround(chunk, localX, localZ);
                if (y >= 2 && IsForestFloor(worldX, y + 1, worldZ))
                {
                    SubsystemTerrain.ChangeCell(
                        worldX,
                        y + 1,
                        worldZ,
                        Terrain.MakeBlockValue(contents),
                        true,
                        null);
                    return true;
                }
            }
            return false;
        }

        private int FindNaturalGround(TerrainChunk chunk, int localX, int localZ)
        {
            for (int y = chunk.GetTopHeightFast(localX, localZ); y >= 2; y--)
            {
                int contents = Terrain.ExtractContents(chunk.GetCellValueFast(localX, y, localZ));
                if (IsNaturalGround(contents) &&
                    Terrain.ExtractContents(chunk.GetCellValueFast(localX, y + 1, localZ)) == 0)
                {
                    return y;
                }
            }
            return -1;
        }

        private bool IsForestFloor(int x, int y, int z)
        {
            Terrain terrain = SubsystemTerrain.Terrain;
            if (terrain.GetSeasonalHumidity(x, z) < 6)
            {
                return false;
            }
            int leaves = 0;
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    for (int dy = 2; dy <= 8; dy++)
                    {
                        int contents = Terrain.ExtractContents(terrain.GetCellValue(x + dx, y + dy, z + dz));
                        if (contents > 0 && BlocksManager.Blocks[contents] is LeavesBlock && ++leaves >= 3)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static int CountPatches(TerrainChunk chunk, int stopAt)
        {
            int count = 0;
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    int top = chunk.GetTopHeightFast(x, z);
                    for (int y = 1; y <= top; y++)
                    {
                        int contents = Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z));
                        if ((contents == MushroomPatchBlock.Index || contents == FlyAgaricPatchBlock.Index) &&
                            ++count >= stopAt)
                        {
                            return count;
                        }
                    }
                }
            }
            return count;
        }

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            if (!IsNaturalGround(Terrain.ExtractContents(SubsystemTerrain.Terrain.GetCellValue(x, y - 1, z))))
            {
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, false, null);
                return;
            }

            int data = Terrain.ExtractData(value);
            if ((data & PermanentData) != 0)
            {
                return;
            }

            int age = data & DryAgeMask;
            if (m_weather.GetPrecipitationShaftInfo(x, z).Intensity > 0.05f)
            {
                if (age != 0)
                {
                    SubsystemTerrain.ChangeCell(x, y, z, Terrain.ReplaceData(value, data & ~DryAgeMask), true, null);
                }
                return;
            }

            age++;
            if (age >= MaxDryAge)
            {
                SubsystemTerrain.DestroyCell(0, x, y, z, 0, true, true, null);
            }
            else
            {
                SubsystemTerrain.ChangeCell(
                    x,
                    y,
                    z,
                    Terrain.ReplaceData(value, (data & ~DryAgeMask) | (age & DryAgeMask)),
                    true,
                    null);
            }
        }

        private static bool IsNaturalGround(int contents)
        {
            return contents > 0 &&
                (BlocksManager.Blocks[contents] is GrassBlock || BlocksManager.Blocks[contents] is DirtBlock);
        }
    }
}
