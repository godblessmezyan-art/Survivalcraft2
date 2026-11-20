using Game;
using SCIEPort.Features.Geology;

namespace SCIEPort.Features.TidePalace
{
    internal static class PalaceTerrain
    {
        internal static bool Apply(TerrainChunk chunk, PalaceSite site, int sea)
        {
            var layout = new PalaceLayout(site, chunk.Origin);
            // Validate the full slice before writing. Existing saved chunks bypass generation
            // altogether, and unexpected mod blocks reject the slice without partial writes.
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            for (int y = PalaceLayout.LowY; y <= PalaceLayout.HighY; y++)
            {
                if (layout.At(x, y, z) == PalaceLayout.Unchanged) continue;
                int actual = Terrain.ExtractContents(chunk.GetCellValueFast(x, site.BaseY + y, z));
                if (y < -12 && actual != 0 && actual != WaterBlock.Index) continue;
                if (!Natural(actual)) return false;
            }

            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                bool changed = false;
                for (int y = PalaceLayout.LowY; y <= PalaceLayout.HighY; y++)
                {
                    int value = layout.At(x, y, z);
                    if (value == PalaceLayout.Unchanged) continue;
                    int actual = Terrain.ExtractContents(chunk.GetCellValueFast(x, site.BaseY + y, z));
                    // Piers extend through water to the ocean bed; preserve solid substrate.
                    if (y < -12 && actual != 0 && actual != WaterBlock.Index) continue;
                    chunk.SetCellValueFast(x, site.BaseY + y, z, value);
                    changed = true;
                }
                if (changed) RepairWaterColumn(chunk, x, z, site.BaseY + PalaceLayout.LowY - 1, sea);
            }
            chunk.ModificationCounter++;
            return true;
        }

        private static void RepairWaterColumn(TerrainChunk chunk, int x, int z, int low, int high)
        {
            bool above = false;
            for (int y = high + 1; y >= low; y--)
            {
                int value = chunk.GetCellValueFast(x, y, z);
                bool water = Terrain.ExtractContents(value) == WaterBlock.Index;
                if (water)
                {
                    int data = FluidBlock.SetIsTop(Terrain.ExtractData(value), !above);
                    chunk.SetCellValueFast(x, y, z, Terrain.ReplaceData(value, data));
                }
                above = water;
            }
        }

        private static bool Natural(int contents)
        {
            switch (contents)
            {
                case 0:
                case WaterBlock.Index:
                case MagmaBlock.Index:
                case IceBlock.Index:
                case SandBlock.Index:
                case GravelBlock.Index:
                case DirtBlock.Index:
                case ClayBlock.Index:
                case GraniteBlock.Index:
                case BasaltBlock.Index:
                case SandstoneBlock.Index:
                case LimestoneBlock.Index:
                case CoalOreBlock.Index:
                case CoalBlock.Index:
                case CopperOreBlock.Index:
                case IronOreBlock.Index:
                case DiamondOreBlock.Index:
                case GermaniumOreBlock.Index:
                case SulphurOreBlock.Index:
                case SaltpeterOreBlock.Index:
                case BasaltPillarBlock.Index:
                case VolcanicSandBlock.Index:
                    return true;
                default:
                    // Native aquatic vegetation/crustaceans are part of the generated sea floor.
                    return BlocksManager.Blocks[contents] is WaterPlantBlock || BlocksManager.Blocks[contents] is BottomSuckerBlock;
            }
        }
    }
}
