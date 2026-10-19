using Engine;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.KellyPlants
{
    internal static class KellyPlantWorldGeneration
    {
        private const int PermanentMushroomData = 32;
        private static SubsystemGameInfo s_gameInfo;
        private static SubsystemSeasons s_seasons;
        private static SubsystemTerrain s_terrain;

        public static void SetProject(Project project)
        {
            if (project == null)
            {
                return;
            }
            s_seasons = project.FindSubsystem<SubsystemSeasons>(false);
            s_terrain = project.FindSubsystem<SubsystemTerrain>(false);
            s_gameInfo = project.FindSubsystem<SubsystemGameInfo>(false);
        }

        public static bool IsReady
        {
            get { return s_terrain != null && s_gameInfo != null; }
        }

        public static void Generate(TerrainChunk chunk)
        {
            if (chunk == null || !IsReady)
            {
                return;
            }

            Game.Random random = new Game.Random(
                KellyPlantRules.GetChunkSeed(s_gameInfo.WorldSeed, chunk.Coords.X, chunk.Coords.Y));

            if (random.Float(0f, 1f) <= 0.25f)
            {
                bool fruiting = s_seasons == null ||
                    s_seasons.Season == Season.Spring ||
                    s_seasons.Season == Season.Summer;
                int value = Terrain.MakeBlockValue(
                    WildBerryPlantBlock.Index,
                    0,
                    WildBerryPlantBlock.SetFruiting(0, fruiting));
                ScatterOnGrass(chunk, random, value, 1, 1);
            }

            if (random.Float(0f, 1f) <= 0.35f)
            {
                ScatterCattails(chunk, random, Terrain.MakeBlockValue(CattailBlock.Index), 3, 4);
            }
            if (random.Float(0f, 1f) <= 0.65f)
            {
                ScatterCactusFruit(chunk, random, Terrain.MakeBlockValue(CactusFruitBlock.Index), 4, 2);
            }
            if (random.Float(0f, 1f) <= 0.18f)
            {
                ScatterCloudberries(chunk, random, Terrain.MakeBlockValue(CloudberryPlantBlock.Index), 1, 2);
            }
            if (random.Float(0f, 1f) <= 0.18f)
            {
                ScatterForestMushrooms(
                    chunk,
                    random,
                    Terrain.MakeBlockValue(MushroomPatchBlock.Index, 0, PermanentMushroomData),
                    1,
                    1);
            }
            if (random.Float(0f, 1f) <= 0.06f)
            {
                ScatterForestMushrooms(
                    chunk,
                    random,
                    Terrain.MakeBlockValue(FlyAgaricPatchBlock.Index, 0, PermanentMushroomData),
                    1,
                    1);
            }
        }

        private static void ScatterOnGrass(
            TerrainChunk chunk,
            Game.Random random,
            int value,
            int patches,
            int perPatch)
        {
            for (int patch = 0; patch < patches; patch++)
            {
                int centerX = random.Int(0, 15);
                int centerZ = random.Int(0, 15);
                for (int item = 0; item < perPatch; item++)
                {
                    int x = Clamp(centerX + random.Int(-2, 2), 0, 15);
                    int z = Clamp(centerZ + random.Int(-2, 2), 0, 15);
                    int y = FindSurface(chunk, x, z);
                    if (y >= 0 && IsBlock<GrassBlock>(chunk, x, y, z) && IsAir(chunk, x, y + 1, z))
                    {
                        chunk.SetCellValueFast(x, y + 1, z, value);
                    }
                }
            }
        }

        private static void ScatterCattails(
            TerrainChunk chunk,
            Game.Random random,
            int value,
            int patches,
            int perPatch)
        {
            for (int patch = 0; patch < patches; patch++)
            {
                int centerX = random.Int(0, 15);
                int centerZ = random.Int(0, 15);
                for (int item = 0; item < perPatch; item++)
                {
                    int x = Clamp(centerX + random.Int(-2, 2), 0, 15);
                    int z = Clamp(centerZ + random.Int(-2, 2), 0, 15);
                    int y = FindSurface(chunk, x, z);
                    int ground = y >= 0 ? Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z)) : 0;
                    if (y >= 0 && IsNaturalGround(ground) && IsAir(chunk, x, y + 1, z) &&
                        HasNearbyInlandWater(chunk, x, y, z))
                    {
                        chunk.SetCellValueFast(x, y + 1, z, value);
                    }
                }
            }
        }

        private static bool HasNearbyInlandWater(TerrainChunk chunk, int x, int y, int z)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    int localX = x + dx;
                    int localZ = z + dz;
                    if (localX < 0 || localX > 15 || localZ < 0 || localZ > 15)
                    {
                        continue;
                    }
                    for (int dy = -2; dy <= 0; dy++)
                    {
                        if (IsBlock<WaterBlock>(chunk, localX, y + dy, localZ))
                        {
                            if (s_terrain == null || s_terrain.TerrainContentsGenerator == null)
                            {
                                return true;
                            }
                            float shoreDistance = s_terrain.TerrainContentsGenerator.CalculateOceanShoreDistance(
                                chunk.Origin.X + localX,
                                chunk.Origin.Y + localZ);
                            if (shoreDistance >= 2f)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        private static void ScatterCactusFruit(
            TerrainChunk chunk,
            Game.Random random,
            int value,
            int patches,
            int perPatch)
        {
            for (int patch = 0; patch < patches; patch++)
            {
                int centerX = random.Int(0, 15);
                int centerZ = random.Int(0, 15);
                for (int item = 0; item < perPatch; item++)
                {
                    int x = Clamp(centerX + random.Int(-3, 3), 0, 15);
                    int z = Clamp(centerZ + random.Int(-3, 3), 0, 15);
                    int y = FindSurface(chunk, x, z);
                    if (y >= 0 && IsBlock<CactusBlock>(chunk, x, y, z) && IsAir(chunk, x, y + 1, z))
                    {
                        chunk.SetCellValueFast(x, y + 1, z, value);
                    }
                }
            }
        }

        private static void ScatterCloudberries(
            TerrainChunk chunk,
            Game.Random random,
            int value,
            int patches,
            int perPatch)
        {
            for (int patch = 0; patch < patches; patch++)
            {
                int centerX = random.Int(0, 15);
                int centerZ = random.Int(0, 15);
                for (int item = 0; item < perPatch; item++)
                {
                    int x = Clamp(centerX + random.Int(-2, 2), 0, 15);
                    int z = Clamp(centerZ + random.Int(-2, 2), 0, 15);
                    int surface = FindSurface(chunk, x, z);
                    if (surface < 0)
                    {
                        continue;
                    }
                    int groundY = IsBlock<SnowBlock>(chunk, x, surface, z) ? surface - 1 : surface;
                    int ground = Terrain.ExtractContents(chunk.GetCellValueFast(x, groundY, z));
                    if (IsNaturalGround(ground) &&
                        IsAir(chunk, x, groundY + 1, z) &&
                        SubsystemWeather.IsPlaceFrozen(chunk.GetTemperatureFast(x, z), groundY))
                    {
                        chunk.SetCellValueFast(x, groundY + 1, z, value);
                    }
                }
            }
        }

        private static void ScatterForestMushrooms(
            TerrainChunk chunk,
            Game.Random random,
            int value,
            int patches,
            int perPatch)
        {
            for (int patch = 0; patch < patches; patch++)
            {
                int centerX = random.Int(0, 15);
                int centerZ = random.Int(0, 15);
                for (int item = 0; item < perPatch; item++)
                {
                    int x = Clamp(centerX + random.Int(-2, 2), 0, 15);
                    int z = Clamp(centerZ + random.Int(-2, 2), 0, 15);
                    int y = FindSurface(chunk, x, z);
                    int ground = y >= 0 ? Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z)) : 0;
                    if (y >= 0 && IsNaturalGround(ground) && IsAir(chunk, x, y + 1, z) &&
                        HasNearbyLeaves(chunk, x, y + 1, z))
                    {
                        chunk.SetCellValueFast(x, y + 1, z, value);
                    }
                }
            }
        }

        private static bool HasNearbyLeaves(TerrainChunk chunk, int x, int y, int z)
        {
            int leaves = 0;
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    int localX = x + dx;
                    int localZ = z + dz;
                    if (localX < 0 || localX > 15 || localZ < 0 || localZ > 15)
                    {
                        continue;
                    }
                    for (int dy = 1; dy <= 9 && y + dy < 256; dy++)
                    {
                        int contents = Terrain.ExtractContents(chunk.GetCellValueFast(localX, y + dy, localZ));
                        if (contents > 0 && BlocksManager.Blocks[contents] is LeavesBlock && ++leaves >= 3)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static int FindSurface(TerrainChunk chunk, int x, int z)
        {
            for (int y = MathUtils.Min(chunk.GetTopHeightFast(x, z), 254); y >= 1; y--)
            {
                if (Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z)) != 0)
                {
                    return y;
                }
            }
            return -1;
        }

        private static bool IsAir(TerrainChunk chunk, int x, int y, int z)
        {
            return y >= 0 && y < 256 && Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z)) == 0;
        }

        private static bool IsNaturalGround(int contents)
        {
            return contents > 0 &&
                (BlocksManager.Blocks[contents] is GrassBlock || BlocksManager.Blocks[contents] is DirtBlock);
        }

        private static bool IsBlock<T>(TerrainChunk chunk, int x, int y, int z) where T : Block
        {
            if (y < 0 || y >= 256)
            {
                return false;
            }
            int contents = Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z));
            return contents > 0 && BlocksManager.Blocks[contents] is T;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
