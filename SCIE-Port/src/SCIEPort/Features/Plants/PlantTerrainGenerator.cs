using System;
using Engine;
using Game;
using Game.NetWork;
using SCIEPort.Features.Apple;
using SCIEPort.Features.Bamboo;
using SCIEPort.Features.Crops;
using SCIEPort.Features.Flax;
using SCIEPort.Features.Geology;
using SCIEPort.Features.KellyPlants;
using SCIEPort.Features.Landmarks;
using SCIEPort.Features.Scenery;
using SCIEPort.Features.Sunflower;
using SCIEPort.Features.SpecialTrees;
using SCIEPort.Features.TidePalace;
using SCIEPort.Features.Pyramid;

namespace SCIEPort.Features.Plants
{
    // Decorates the actual generator chosen for the world. The mod's native generation
    // hook fires before pass 4, when neither the vegetation nor the lightmap exists.
    internal sealed class PlantTerrainGenerator : ITerrainContentsGenerator
    {
        private readonly ITerrainContentsGenerator original;
        private readonly SubsystemGameInfo gameInfo;
        private readonly SubsystemTidePalace tidePalace;
        private readonly SubsystemPyramid pyramid;
        private readonly SubsystemScenery scenery;

        internal PlantTerrainGenerator(ITerrainContentsGenerator original, SubsystemGameInfo gameInfo)
        {
            this.original = original;
            this.gameInfo = gameInfo;
            tidePalace = gameInfo.Project?.FindSubsystem<SubsystemTidePalace>(false);
            pyramid = gameInfo.Project?.FindSubsystem<SubsystemPyramid>(false);
            scenery = gameInfo.Project?.FindSubsystem<SubsystemScenery>(false);
        }

        public int OceanLevel => original.OceanLevel;
        public Vector3 FindCoarseSpawnPosition() => original.FindCoarseSpawnPosition();
        public float CalculateOceanShoreDistance(float x, float z) => original.CalculateOceanShoreDistance(x, z);
        public float CalculateHeight(float x, float z) => original.CalculateHeight(x, z);
        public int CalculateTemperature(float x, float z) => original.CalculateTemperature(x, z);
        public int CalculateHumidity(float x, float z) => original.CalculateHumidity(x, z);
        public float CalculateMountainRangeFactor(float x, float z) => original.CalculateMountainRangeFactor(x, z);
        public void GenerateChunkContentsPass1(TerrainChunk chunk) => original.GenerateChunkContentsPass1(chunk);
        public void GenerateChunkContentsPass2(TerrainChunk chunk) => original.GenerateChunkContentsPass2(chunk);
        public void GenerateChunkContentsPass3(TerrainChunk chunk)
        {
            original.GenerateChunkContentsPass3(chunk);
            if (CommonLib.WorkType == WorkType.Client) return;
            try
            {
                GeologyTerrain.Generate(chunk, original, gameInfo.WorldSeed, gameInfo.WorldSettings.TerrainGenerationMode);
            }
            catch (Exception error)
            {
                Log.Error("[SCIEPort] Geology terrain generation failed: " + error);
            }
        }

        public void GenerateChunkContentsPass4(TerrainChunk chunk)
        {
            bool authoritative = CommonLib.WorkType != WorkType.Client &&
                gameInfo.WorldSettings.TerrainGenerationMode != TerrainGenerationMode.Nihility;
            SubsystemPyramid.Placement pyramidPlacement = null;
            if (authoritative)
            {
                try
                {
                    // Hook contributions are already present. Native pass four runs between our preflight and stamp.
                    pyramidPlacement = pyramid?.Prepare(chunk, original, gameInfo.WorldSeed, gameInfo.WorldSettings.TerrainGenerationMode);
                }
                catch (Exception error)
                {
                    Log.Error("[SCIEPort] Pyramid preparation failed: " + error);
                }
            }
            original.GenerateChunkContentsPass4(chunk);
            if (!authoritative) return;
            if (pyramidPlacement != null)
            {
                pyramid.Finish(chunk, pyramidPlacement);
                return;
            }

            try
            {
                GeologyTerrain.Finish(chunk, original, gameInfo.WorldSeed, gameInfo.WorldSettings.TerrainGenerationMode);
            }
            catch (Exception error)
            {
                Log.Error("[SCIEPort] Cloudfall trail cleanup failed: " + error);
            }

            try
            {
                if (tidePalace?.Generate(chunk, original, gameInfo.WorldSeed, gameInfo.WorldSettings.TerrainGenerationMode) == true)
                    return;
            }
            catch (Exception error)
            {
                Log.Error("[SCIEPort] Tide Palace generation failed: " + error);
            }

            try
            {
                GeneratePlants(chunk);
            }
            catch (Exception error)
            {
                Log.Error("[SCIEPort] Natural plant generation failed: " + error);
            }

            try
            {
                NaturalLandmarks.Generate(chunk, original, gameInfo.WorldSeed, gameInfo.WorldSettings.TerrainGenerationMode);
            }
            catch (Exception error)
            {
                Log.Error("[SCIEPort] Natural landmark generation failed: " + error);
            }

            try
            {
                SceneryGenerator.Generate(chunk, this, gameInfo.WorldSeed, scenery);
            }
            catch (Exception error)
            {
                Log.Error("[SCIEPort] Fantasy scenery generation failed: " + error);
            }
        }

        private void GeneratePlants(TerrainChunk chunk)
        {
            // Independent deterministic streams keep one species' density from moving all the others.
            Scatter<ApplePlantBlock>(chunk, ApplePlantBlock.Index, .21f, 10, 1);
            Point3? bambooGrove = BambooGroveGenerator.Generate(chunk, gameInfo.WorldSeed, OceanLevel);
            Scatter<CornStalkBlock>(chunk, CornStalkBlock.Index, .14f, 7, 2);
            Scatter<FlaxPlantBlock>(chunk, FlaxPlantBlock.Index, .19f, 7, 3);
            Scatter<SunflowerPlantBlock>(chunk, SunflowerPlantBlock.Index, .17f, 7, 3);
            Scatter<SoybeanBlock>(chunk, SoybeanBlock.Index, .13f, 7, 2);
            Scatter<WildBerryPlantBlock>(chunk, WildBerryPlantBlock.Index, .26f, 8, 3);
            Scatter<CloudberryPlantBlock>(chunk, CloudberryPlantBlock.Index, .25f, 9, 3);
            Scatter<CattailBlock>(chunk, CattailBlock.Index, .50f, 14, 4);
            Scatter<CactusFruitBlock>(chunk, CactusFruitBlock.Index, .52f, 14, 2);
            Scatter<MushroomPatchBlock>(chunk, MushroomPatchBlock.Index, .29f, 12, 2);
            Scatter<FlyAgaricPatchBlock>(chunk, FlyAgaricPatchBlock.Index, .10f, 10, 1);
            foreach (TreeSpecies species in TreeSpecies.All)
                ScatterSpecialTree(chunk, species);
            if (bambooGrove.HasValue)
                BambooLandmarkGenerator.Generate(chunk, gameInfo.WorldSeed, bambooGrove.Value, original);
        }

        private void Scatter<T>(TerrainChunk chunk, int id, float chance, int attempts, int limit) where T : Block
        {
            if (!(BlocksManager.Blocks[id] is T)) return;
            int seed = KellyPlantRules.GetChunkSeed(unchecked(gameInfo.WorldSeed ^ (id * 16777619)), chunk.Coords.X, chunk.Coords.Y);
            var random = new Game.Random(seed);
            if (!random.Bool(chance)) return;
            int centerX = random.Int(2, 13);
            int centerZ = random.Int(2, 13);

            for (int i = 0; i < attempts && limit > 0; i++)
            {
                int x = i == 0 ? centerX : centerX + random.Int(-2, 2);
                int z = i == 0 ? centerZ : centerZ + random.Int(-2, 2);
                bool fungus = id == MushroomPatchBlock.Index || id == FlyAgaricPatchBlock.Index;
                int top = chunk.CalculateTopmostCellHeight(x, z);
                int y = top + 1;
                if (fungus)
                {
                    // Look for the forest floor underneath the actual vanilla canopy.
                    int depth = 0;
                    for (int scan = top; scan > 1 && scan >= top - 12; scan--)
                    {
                        int contents = Contents(chunk, x, scan, z);
                        if (BlocksManager.Blocks[contents] is LeavesBlock || BlocksManager.Blocks[contents] is LeavesBlock24) depth++;
                        if (!PlantHabitat.IsNaturalSoil(contents)) continue;
                        y = scan + 1;
                        break;
                    }
                    if (depth < 2) continue;
                }

                if (y <= 1 || y >= 252 || Contents(chunk, x, y, z) != 0) continue;
                int temperature = chunk.GetTemperatureFast(x, z) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
                int humidity = chunk.GetHumidityFast(x, z);
                if (!PlantHabitat.For(id).AllowsWildSite(temperature, humidity, y - OceanLevel)) continue;

                int soil = Contents(chunk, x, y - 1, z);
                if (id == CactusFruitBlock.Index)
                {
                    if (soil != CactusBlock.Index) continue;
                }
                else if (!PlantHabitat.IsNaturalSoil(soil) && !(soil == SandBlock.Index && id == CattailBlock.Index))
                {
                    continue;
                }

                if (id == CattailBlock.Index && !HasInlandWater(chunk, x, y, z)) continue;
                if (id == ApplePlantBlock.Index)
                {
                    if (!TryPlantApple(chunk, x, y, z)) continue;
                }
                else
                {
                    chunk.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(id, 0, InitialData(id, random)));
                }
                limit--;
            }
        }

        private int InitialData(int id, Game.Random random)
        {
            float year = gameInfo.WorldSettings.TimeOfYear;
            bool cycling = gameInfo.WorldSettings.AreSeasonsChanging;
            switch (id)
            {
                case CornStalkBlock.Index:
                case FlaxPlantBlock.Index:
                case SunflowerPlantBlock.Index:
                case SoybeanBlock.Index: return AnnualCropGrowth.Wild | random.Int(4, 7);
                case WildBerryPlantBlock.Index: return !cycling || year < .25f || year >= .75f ? 1 : 0;
                case CloudberryPlantBlock.Index: return !cycling || year < .25f ? 0 : 1;
                case MushroomPatchBlock.Index:
                case FlyAgaricPatchBlock.Index: return SubsystemKellyMushroomBlockBehavior.Established;
                default: return 0;
            }
        }

        private bool TryPlantApple(TerrainChunk chunk, int x, int y, int z)
        {
            if (!AppleTreeLayout.IsRegistered || x < 1 || x > 14 || z < 1 || z > 14 || y > 250) return false;
            float year = gameInfo.WorldSettings.TimeOfYear;
            int stage = !gameInfo.WorldSettings.AreSeasonsChanging || (year >= .25f && year < .5f)
                ? 15 : year < .25f ? 4 : year >= .75f ? 1 : 0;
            return TryPaintTree(chunk, x, y, z, AppleTreeLayout.ForStage(stage));
        }

        private void ScatterSpecialTree(TerrainChunk chunk, TreeSpecies species)
        {
            if (!species.Registered) return;
            int seed = KellyPlantRules.GetChunkSeed(unchecked(gameInfo.WorldSeed ^ (species.Sapling * 16777619)),
                chunk.Coords.X, chunk.Coords.Y);
            var random = new Game.Random(seed);
            if (!random.Bool(species.GenerationChance)) return;

            for (int attempt = 0; attempt < 14; attempt++)
            {
                int x = random.Int(3, 12);
                int z = random.Int(3, 12);
                int y = chunk.CalculateTopmostCellHeight(x, z) + 1;
                if (y <= 1 || y > 254 - species.MaxHeight) continue;
                int temperature = chunk.GetTemperatureFast(x, z) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
                int humidity = chunk.GetHumidityFast(x, z);
                float shore = CalculateOceanShoreDistance(chunk.Origin.X + x, chunk.Origin.Y + z);
                if (!species.WildSite(Contents(chunk, x, y - 1, z), temperature, humidity, y - OceanLevel, shore)) continue;
                int variant = species.VariantAt(gameInfo.WorldSeed, chunk.Origin.X + x, chunk.Origin.Y + z);
                TerrainBrush brush = species.GetBrush(variant, true);
                if (TryPaintTree(chunk, x, y, z, brush, species)) return;
            }
        }

        private static bool TryPaintTree(TerrainChunk chunk, int x, int y, int z, TerrainBrush brush, TreeSpecies species = null)
        {
            foreach (TerrainBrush.Cell cell in brush.Cells)
            {
                int cx = x + cell.X, cy = y + cell.Y, cz = z + cell.Z;
                if (cx < 0 || cx >= 16 || cz < 0 || cz >= 16 || cy < 1 || cy > 254 || Contents(chunk, cx, cy, cz) != 0)
                    return false;
                if (species != null && cell.Y == 0 && !species.Supports(Contents(chunk, cx, cy - 1, cz)))
                    return false;
            }
            brush.PaintFast(chunk, chunk.Origin.X + x, y, chunk.Origin.Y + z);
            return true;
        }

        private bool HasInlandWater(TerrainChunk chunk, int x, int y, int z)
        {
            if (CalculateOceanShoreDistance(chunk.Origin.X + x, chunk.Origin.Y + z) < 2f) return false;
            return PlantGrowthSite.HasWater(x, y, z, (cx, cy, cz) =>
                cx >= 0 && cx < 16 && cz >= 0 && cz < 16 ? Contents(chunk, cx, cy, cz) : 0);
        }

        private static int Contents(TerrainChunk chunk, int x, int y, int z)
            => Terrain.ExtractContents(chunk.GetCellValueFast(x, y, z));
    }
}
