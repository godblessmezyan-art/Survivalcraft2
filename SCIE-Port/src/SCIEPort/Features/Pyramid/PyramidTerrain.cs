using Game;

namespace SCIEPort.Features.Pyramid
{
    internal static class PyramidTerrain
    {
        internal static bool Apply(TerrainChunk chunk, PyramidSite site)
        {
            var plan = new PyramidLayout(site, chunk.Origin);
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            for (int y = PyramidLayout.LowY; y <= PyramidLayout.HighY; y++)
            {
                int value = plan.At(x, y, z);
                if (value < 0) continue;
                int old = Terrain.ExtractContents(chunk.GetCellValueFast(x, site.BaseY + y, z));
                if (!Natural(old)) return false;
            }
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            for (int y = PyramidLayout.LowY; y <= PyramidLayout.HighY; y++)
            {
                int value = plan.At(x, y, z);
                if (value == PyramidLayout.Unchanged) continue;
                int height = site.BaseY + y;
                if (value == PyramidLayout.Underpinning)
                {
                    int contents = Terrain.ExtractContents(chunk.GetCellValueFast(x, height, z));
                    if (contents != 0 && contents != WaterBlock.Index && contents != MagmaBlock.Index) continue;
                    value = Terrain.MakeBlockValue(SandstoneBlock.Index);
                }
                chunk.SetCellValueFast(x, height, z, value);
            }
            chunk.ModificationCounter++;
            return true;
        }

        private static bool Natural(int contents)
        {
            switch (contents)
            {
                case 0:
                case DirtBlock.Index:
                case GrassBlock.Index:
                case SandBlock.Index:
                case GravelBlock.Index:
                case ClayBlock.Index:
                case GraniteBlock.Index:
                case BasaltBlock.Index:
                case SandstoneBlock.Index:
                case LimestoneBlock.Index:
                case WaterBlock.Index:
                case MagmaBlock.Index:
                case SnowBlock.Index:
                case IceBlock.Index:
                case TallGrassBlock.Index:
                case CactusBlock.Index:
                case DryBushBlock.Index:
                case LargeDryBushBlock.Index:
                case IvyBlock.Index:
                case CoalOreBlock.Index:
                case CoalBlock.Index:
                case IronOreBlock.Index:
                case CopperOreBlock.Index:
                case DiamondOreBlock.Index:
                case GermaniumOreBlock.Index:
                case SaltpeterOreBlock.Index:
                case SulphurOreBlock.Index:
                    return true;
                default:
                    Block block = BlocksManager.Blocks[contents];
                    return block?.GetType().Assembly == typeof(Block).Assembly &&
                        (block is CrossBlock || block is LeavesBlock || block is LeavesBlock24 || block is WoodBlock);
            }
        }
    }
}
