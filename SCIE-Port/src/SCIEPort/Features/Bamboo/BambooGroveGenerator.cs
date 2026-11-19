using System;
using Game;
using SCIEPort.Features.KellyPlants;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Bamboo
{
    internal static class BambooGroveGenerator
    {
        private const int Radius = 3;
        private const int CandidateAttempts = 24;

        internal static void Generate(TerrainChunk chunk, int worldSeed, int seaLevel)
        {
            if (!(BlocksManager.Blocks[BambooPlantBlock.Index] is BambooPlantBlock) ||
                !(BlocksManager.Blocks[BambooShootBlock.Index] is BambooShootBlock)) return;

            int seed = KellyPlantRules.GetChunkSeed(unchecked(worldSeed ^ BambooPlantBlock.Index * 16777619),
                chunk.Coords.X, chunk.Coords.Y);
            var random = new Game.Random(seed);
            if (!random.Bool(.24f)) return;

            int centerX = 0, centerZ = 0, centerY = 0;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                centerX = random.Int(Radius, 15 - Radius);
                centerZ = random.Int(Radius, 15 - Radius);
                centerY = EvaluateSite(chunk, centerX, centerZ, seaLevel);
                if (centerY != 0 && HasRoom(chunk, centerX, centerY, centerZ, BambooRules.MinGroveHeight)) break;
                centerY = 0;
            }
            if (centerY == 0) return;

            int grown = 0;
            int desired = random.Int(5, BambooRules.MaxGroveCulms);
            for (int attempt = 0; attempt < CandidateAttempts && grown < desired; attempt++)
            {
                int x = attempt == 0 ? centerX : centerX + random.Int(-Radius, Radius);
                int z = attempt == 0 ? centerZ : centerZ + random.Int(-Radius, Radius);
                int y = EvaluateSite(chunk, x, z, seaLevel);
                int height = random.Int(BambooRules.MinGroveHeight, BambooRules.MaxHeight);
                if (y == 0 || Math.Abs(y - centerY) > 2) continue;
                // The first viable center is kept even if the random taller culm does not fit.
                if (attempt == 0 && !HasRoom(chunk, x, y, z, height)) height = BambooRules.MinGroveHeight;
                if (!HasRoom(chunk, x, y, z, height)) continue;

                int root = BambooRules.SetHeight(BambooRules.MarkBase(0), height);
                chunk.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(BambooPlantBlock.Index, 0, root));
                for (int segment = 1; segment < height; segment++)
                    chunk.SetCellValueFast(x, y + segment, z, Terrain.MakeBlockValue(BambooPlantBlock.Index));
                grown++;
            }

            int shoots = 0;
            for (int attempt = 0; attempt < 12 && shoots < BambooRules.MaxGroveShoots; attempt++)
            {
                int x = centerX + random.Int(-Radius, Radius);
                int z = centerZ + random.Int(-Radius, Radius);
                int y = EvaluateSite(chunk, x, z, seaLevel);
                if (y == 0 || Math.Abs(y - centerY) > 2 || !HasRoom(chunk, x, y, z, BambooRules.MinGroveHeight)) continue;
                chunk.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(BambooShootBlock.Index));
                shoots++;
            }
        }

        private static int EvaluateSite(TerrainChunk chunk, int x, int z, int seaLevel)
        {
            int y = chunk.CalculateTopmostCellHeight(x, z) + 1;
            if (y <= 1 || y > 250 || !PlantHabitat.IsNaturalSoil(Terrain.ExtractContents(chunk.GetCellValueFast(x, y - 1, z))))
                return 0;

            int temperature = chunk.GetTemperatureFast(x, z) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
            return PlantHabitat.For(BambooPlantBlock.Index).AllowsWildSite(temperature, chunk.GetHumidityFast(x, z), y - seaLevel) ? y : 0;
        }

        private static bool HasRoom(TerrainChunk chunk, int x, int y, int z, int height)
        {
            if (y + height > 255) return false;
            for (int segment = 0; segment < height; segment++)
                if (Terrain.ExtractContents(chunk.GetCellValueFast(x, y + segment, z)) != 0) return false;
            return true;
        }
    }
}
