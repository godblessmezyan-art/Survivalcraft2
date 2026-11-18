using Game;
using SCIEPort.Features.Apple;
using SCIEPort.Features.Bamboo;
using SCIEPort.Features.Crops;
using SCIEPort.Features.Flax;
using SCIEPort.Features.KellyPlants;
using SCIEPort.Features.Sunflower;

namespace SCIEPort.Features.Plants
{
    internal readonly struct PlantHabitat
    {
        private readonly int minTemperature;
        private readonly int maxTemperature;
        private readonly int minHumidity;
        private readonly int maxHumidity;
        private readonly int maxHeightAboveSea;
        private readonly int minLight;

        private PlantHabitat(int minTemperature, int maxTemperature, int minHumidity, int maxHumidity,
            int maxHeightAboveSea, int minLight)
        {
            this.minTemperature = minTemperature;
            this.maxTemperature = maxTemperature;
            this.minHumidity = minHumidity;
            this.maxHumidity = maxHumidity;
            this.maxHeightAboveSea = maxHeightAboveSea;
            this.minLight = minLight;
        }

        internal static PlantHabitat For(int plant)
        {
            switch (plant)
            {
                case ApplePlantBlock.Index: return new PlantHabitat(5, 14, 6, 15, 32, AppleRules.MinLightToGrow);
                case BambooPlantBlock.Index: return new PlantHabitat(9, 18, 9, 15, 21, BambooRules.MinLightToGrow);
                case CornStalkBlock.Index: return new PlantHabitat(8, 18, 5, 13, 26, CropRules.MinLightToGrow);
                case FlaxPlantBlock.Index: return new PlantHabitat(4, 12, 4, 12, 44, FlaxRules.MinLightToGrow);
                case SunflowerPlantBlock.Index: return new PlantHabitat(6, 18, 2, 11, 41, SunflowerRules.MinLightToGrow);
                case SoybeanBlock.Index: return new PlantHabitat(8, 17, 7, 15, 24, 9);
                case WildBerryPlantBlock.Index: return new PlantHabitat(4, 15, 5, 15, 48, 8);
                case CloudberryPlantBlock.Index: return new PlantHabitat(0, 8, 8, 15, 69, 8);
                case CattailBlock.Index: return new PlantHabitat(4, 18, 4, 15, 38, 8);
                case CactusFruitBlock.Index: return new PlantHabitat(8, 19, 0, 7, 34, 10);
                case MushroomPatchBlock.Index: return new PlantHabitat(4, 15, 8, 15, 48, 0);
                case FlyAgaricPatchBlock.Index: return new PlantHabitat(1, 11, 8, 15, 56, 0);
                default: throw new System.ArgumentOutOfRangeException(nameof(plant));
            }
        }

        internal bool AllowsWildSite(int temperature, int humidity, int heightAboveSea)
            => temperature >= minTemperature && temperature <= maxTemperature &&
                humidity >= minHumidity && humidity <= maxHumidity &&
                heightAboveSea >= -1 && heightAboveSea <= maxHeightAboveSea;

        internal bool AllowsGrowth(int temperature, int humidity, int light, bool watered)
        {
            // Seasonal shafts can differ from the base climate. Watered plants tolerate drier air.
            return temperature >= System.Math.Max(-1, minTemperature - 3) && temperature <= maxTemperature + 5 &&
                (watered || humidity >= minHumidity - 2) && humidity <= maxHumidity + 4 &&
                (minLight > 0 ? light >= minLight : light < 15);
        }

        internal static bool IsNaturalSoil(int contents) => contents == GrassBlock.Index || contents == DirtBlock.Index;

        internal static bool IsRootingSoil(int contents) => contents == SoilBlock.Index || IsNaturalSoil(contents);

        internal static int TemperatureAt(Terrain terrain, int x, int y, int z)
            => terrain.GetSeasonalTemperature(x, z) + SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
    }
}
