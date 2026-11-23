using Game;
using SCIEPort.Features.MountainShelter;

namespace SCIEPort.Features.Pyramid
{
    internal static class PyramidTerrain
    {
        internal static PyramidLayout Prepare(TerrainChunk chunk, PyramidSite site)
        {
            var plan = new PyramidLayout(site, chunk.Origin);
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            for (int y = PyramidLayout.LowY; y <= PyramidLayout.HighY; y++)
            {
                int value = plan.At(x, y, z);
                if (value < 0) continue;
                int old = Terrain.ExtractContents(chunk.GetCellValueFast(x, site.BaseY + y, z));
                if (!Natural(old))
                {
                    Engine.Log.Warning("[SCIEPort] Pyramid slice " + chunk.Coords + " contains protected block " + old +
                        " at " + (chunk.Origin.X + x) + "," + (site.BaseY + y) + "," + (chunk.Origin.Y + z) + ".");
                    return null;
                }
            }
            return plan;
        }

        internal static void Write(TerrainChunk chunk, PyramidSite site, PyramidLayout plan)
        {
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
        }

        internal static void RestoreDecor(SubsystemTerrain terrain, TerrainChunk chunk, PyramidSite site)
        {
            var layout = new PyramidLayout(site, chunk.Origin);
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            for (int y = PyramidLayout.LowY; y <= PyramidLayout.HighY; y++)
            {
                int expected = layout.At(x, y, z);
                if (expected < 0 || Terrain.ExtractContents(expected) != PyramidDecorRules.Index) continue;
                int height = site.BaseY + y;
                int value = chunk.GetCellValueFast(x, height, z);
                if (Terrain.ExtractContents(value) != PyramidDecorRules.LegacyIndex) continue;
                int data = Terrain.ExtractData(value);
                int expectedData = Terrain.ExtractData(expected);
                int difference = data ^ expectedData;
                if (difference != 0 && (difference != PyramidDecorRules.StateBit ||
                    !PyramidDecorRules.IsSwitchable(PyramidDecorRules.Kind(expectedData)))) continue;
                if (IsShelterPart(chunk, x, height, z, data)) continue;
                terrain.ChangeCell(chunk.Origin.X + x, height, chunk.Origin.Y + z, Terrain.MakeBlockValue(PyramidDecorRules.Index, 0, data));
            }
        }

        private static bool IsShelterPart(TerrainChunk chunk, int x, int y, int z, int data)
        {
            if (!ShelterLayout.Decode(data, out int part, out int rotation, out _, out _)) return false;
            int rootY = y - ShelterLayout.Offset(part, rotation).Y;
            // Each intact shelter column has another part with the same rotation in this chunk.
            for (int other = part & 3; other < ShelterLayout.CellCount; other += 4)
            {
                int height = rootY + ShelterLayout.Offset(other, rotation).Y;
                if (other == part || height < 0 || height >= 256) continue;
                int value = chunk.GetCellValueFast(x, height, z);
                if (Terrain.ExtractContents(value) == MountainShelterBlock.Index &&
                    ShelterLayout.Decode(Terrain.ExtractData(value), out int role, out int facing, out _, out _) &&
                    role == other && facing == rotation) return true;
            }
            return false;
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
                // The native fourth pass can decorate the fresh footprint with a grave or cairn.
                case GravestoneBlock.Index:
                case TreasureGeneratorBlock.Index:
                case JackOLanternBlock.Index:
                case TorchBlock.Index:
                case CairnBlock.Index:
                case SandstoneStairsBlock.Index:
                case SandstoneSlabBlock.Index:
                case StoneStairsBlock.Index:
                case StoneSlabBlock.Index:
                case BasaltStairsBlock.Index:
                case BasaltSlabBlock.Index:
                    return true;
                default:
                    Block block = BlocksManager.Blocks[contents];
                    return block?.GetType().Assembly == typeof(Block).Assembly &&
                        (block is CrossBlock || block is LeavesBlock || block is LeavesBlock24 || block is WoodBlock);
            }
        }
    }
}
