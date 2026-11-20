#pragma warning disable CS1591

using Engine;
using Game;
using Game.NetWork;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Bamboo
{
    public sealed class SubsystemBambooBlockBehavior : SubsystemPollableBlockBehavior
    {
        private static readonly int[] s_handledBlocks = { BambooPlantBlock.Index, BambooShootBlock.Index };

        public override int[] HandledBlocks => s_handledBlocks;

        public override void OnPoll(int value, int x, int y, int z, int pollPass)
        {
            if (CommonLib.WorkType == WorkType.Client || y <= 0 || y > 254 ||
                !PlantGrowthSite.IsLiving(SubsystemTerrain)) return;

            Terrain terrain = SubsystemTerrain.Terrain;
            value = terrain.GetCellValue(x, y, z);
            int contents = Terrain.ExtractContents(value);
            if (contents != BambooPlantBlock.Index && contents != BambooShootBlock.Index) return;
            int data = Terrain.ExtractData(value);
            int soilValue = terrain.GetCellValueFast(x, y - 1, z);
            int soil = Terrain.ExtractContents(soilValue);
            bool shoot = contents == BambooShootBlock.Index;
            if (!shoot && !BambooRules.IsBase(data))
            {
                // A neighbor update usually handles this. The scanner also catches
                // detached culms left behind by a save or a full frame budget.
                if (soil != BambooPlantBlock.Index) DropUnsupported(x, y, z);
                return;
            }

            if (!BambooPlacement.HasSubstrate(soil))
            {
                DropUnsupported(x, y, z);
                return;
            }

            bool hydrated = soil == SoilBlock.Index && SoilBlock.GetHydration(Terrain.ExtractData(soilValue));
            int temperature = PlantHabitat.TemperatureAt(terrain, x, y, z);
            int interval = BambooRules.GetGrowthInterval(hydrated, temperature);
            if (pollPass % interval != 0) return;

            int humidity = terrain.GetSeasonalHumidity(x, z);
            if (shoot)
            {
                if (!PlantHabitat.For(BambooPlantBlock.Index).AllowsGrowth(
                    temperature, humidity, terrain.GetCellLight(x, y + 1, z), hydrated)) return;

                int stage = BambooRules.GetShootStage(data);
                int next = stage < BambooRules.LastShootStage
                    ? Terrain.ReplaceData(value, stage + 1)
                    : Terrain.MakeBlockValue(BambooPlantBlock.Index, 0,
                        BambooRules.SetHeight(BambooRules.MarkBase(0), BambooRules.HeightFromRoll(
                            MathUtils.Hash(unchecked((uint)(x * 73428767 ^ z * 912931))))));
                Change(x, y, z, next);
                return;
            }

            int height = 1;
            int limit = BambooRules.GetHeight(data);
            while (height < limit && y + height < 255)
            {
                int segment = terrain.GetCellValueFast(x, y + height, z);
                if (Terrain.ExtractContents(segment) != BambooPlantBlock.Index ||
                    BambooRules.IsBase(Terrain.ExtractData(segment))) break;
                height++;
            }

            int top = y + height;
            if (height >= limit || top >= 255 || terrain.GetCellContents(x, top, z) != 0) return;
            if (!PlantHabitat.For(BambooPlantBlock.Index).AllowsGrowth(
                PlantHabitat.TemperatureAt(terrain, x, top, z), humidity,
                terrain.GetCellLight(x, top + 1, z), hydrated)) return;

            Change(x, top, z, Terrain.MakeBlockValue(BambooPlantBlock.Index));
        }

        private void Change(int x, int y, int z, int value)
        {
            if (PlantGrowthBudget.Take(1)) SubsystemTerrain.ChangeCell(x, y, z, value, true, null);
        }

        private void DropUnsupported(int x, int y, int z)
        {
            if (PlantGrowthBudget.Take(1)) SubsystemTerrain.DestroyCell(0, x, y, z, 0, false, true, null);
        }

        public override void OnNeighborBlockChanged(int x, int y, int z, int neighborX, int neighborY, int neighborZ)
        {
            if (CommonLib.WorkType == WorkType.Client || y <= 0 || neighborX != x || neighborY != y - 1 || neighborZ != z)
                return;

            Terrain terrain = SubsystemTerrain.Terrain;
            int value = terrain.GetCellValue(x, y, z);
            int contents = Terrain.ExtractContents(value);
            int below = terrain.GetCellContents(x, y - 1, z);
            if (contents == BambooShootBlock.Index && !BambooPlacement.HasSubstrate(below))
                DropUnsupported(x, y, z);
            else if (contents == BambooPlantBlock.Index)
            {
                bool supported = BambooRules.IsBase(Terrain.ExtractData(value))
                    ? BambooPlacement.HasSubstrate(below) : below == BambooPlantBlock.Index;
                if (!supported) DropUnsupported(x, y, z);
            }
        }
    }
}
