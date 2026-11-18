using System;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Plants
{
    // Use the vanilla scanner, soil DATA and ChangeCell path for all four annuals.
    internal static class AnnualCropGrowth
    {
        internal const int Wild = 8;
        private const int Mature = 7;

        private static bool HasSoil(int contents, int data)
            => contents == SoilBlock.Index || ((data & Wild) != 0 && PlantHabitat.IsNaturalSoil(contents));

        internal static void Poll(SubsystemTerrain subsystem, int plant, int x, int y, int z, int pass,
            Func<bool, int, int, int> growthInterval)
        {
            if (CommonLib.WorkType == WorkType.Client || y <= 0 || y >= 255) return;

            Terrain terrain = subsystem.Terrain;
            int value = terrain.GetCellValue(x, y, z);
            if (Terrain.ExtractContents(value) != plant) return;
            if (!PlantGrowthSite.IsLiving(subsystem)) return;
            int data = Terrain.ExtractData(value);
            int stage = data & Mature;
            if (stage == Mature) return;

            int soilValue = terrain.GetCellValue(x, y - 1, z);
            int soil = Terrain.ExtractContents(soilValue);
            if (!HasSoil(soil, data)) return;

            int soilData = Terrain.ExtractData(soilValue);
            bool tilled = soil == SoilBlock.Index;
            bool hydrated = tilled && SoilBlock.GetHydration(soilData);
            int nitrogen = tilled ? SoilBlock.GetNitrogen(soilData) : 0;
            int temperature = PlantHabitat.TemperatureAt(terrain, x, y, z);
            int light = terrain.GetCellLight(x, y + 1, z);
            if (!PlantHabitat.For(plant).AllowsGrowth(temperature, terrain.GetSeasonalHumidity(x, z), light, hydrated))
                return;

            int interval = growthInterval(hydrated, nitrogen, temperature);
            if (!tilled) interval += 2;
            bool fertilizedHarvest = stage + 1 == Mature && nitrogen > 0;
            if (pass % interval != 0 || !PlantGrowthBudget.Take(fertilizedHarvest ? 2 : 1)) return;

            int next = Terrain.ReplaceData(value, (data & ~Mature) | (stage + 1));
            subsystem.ChangeCell(x, y, z, next, true, null);
            if (fertilizedHarvest && terrain.GetCellContents(x, y, z) == plant &&
                Terrain.ExtractData(terrain.GetCellValue(x, y, z)) == Terrain.ExtractData(next) &&
                Terrain.ReplaceLight(terrain.GetCellValue(x, y - 1, z), 0) == Terrain.ReplaceLight(soilValue, 0))
            {
                subsystem.ChangeCell(x, y - 1, z, Terrain.ReplaceData(soilValue, SoilBlock.SetNitrogen(soilData, nitrogen - 1)), true, null);
            }
        }

        internal static void CheckSupport(SubsystemTerrain subsystem, int plant, int x, int y, int z)
        {
            if (CommonLib.WorkType == WorkType.Client || y <= 0 || y >= 255) return;
            Terrain terrain = subsystem.Terrain;
            int value = terrain.GetCellValue(x, y, z);
            if (Terrain.ExtractContents(value) == plant &&
                !HasSoil(terrain.GetCellContents(x, y - 1, z), Terrain.ExtractData(value)))
            {
                subsystem.DestroyCell(0, x, y, z, 0, true, true, null);
            }
        }
    }
}
