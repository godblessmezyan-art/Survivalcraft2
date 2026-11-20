using System;
using System.Collections.Generic;
using Game;

namespace SCIEPort.Features.Geology
{
    internal static class GeologyTerrain
    {
        private const int MaxHeightRange = 58;
        private const int SkinDepth = 5;
        private const int MagmaBasinRadius = 7;

        internal static void Generate(TerrainChunk chunk, ITerrainContentsGenerator generator, int seed, TerrainGenerationMode mode)
        {
            if (chunk.IsLoaded || !GeologySites.Enabled(mode) || !GeologyCube.Registered) return;
            GeologySite site = GeologySites.Choose(generator, seed, chunk.Origin);
            if (site == null || !site.Touches(chunk)) return;

            var writes = new List<CellEdit>();
            bool hasBasin = site.Kind == GeologyKind.MagmaCrater && (site.X & ~15) == chunk.Origin.X &&
                (site.Z & ~15) == chunk.Origin.Y;
            if (hasBasin && !PlanMagmaBasin(chunk, site, writes)) writes.Clear();
            for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                if (hasBasin && Math.Abs(x - (site.X & 15)) <= MagmaBasinRadius &&
                    Math.Abs(z - (site.Z & 15)) <= MagmaBasinRadius) continue;
                GeologyColumn profile = GeologyProfiles.At(site, chunk.Origin.X + x, chunk.Origin.Y + z);
                if (profile.Weight > .02f) PlanColumn(chunk, x, z, profile, site.Kind, writes);
            }

            if (writes.Count == 0) return;
            foreach (CellEdit edit in writes) chunk.SetCellValueFast(edit.Index, edit.Value);
            // The native serializer normally skips untouched generated chunks. Persist geology
            // once encountered so a later world generator revision cannot reshape it on reload.
            chunk.ModificationCounter++;
        }

        private static bool PlanMagmaBasin(TerrainChunk chunk, GeologySite site, List<CellEdit> writes)
        {
            // Preflight the whole pool, its bed and at least one solid neighbor on all sides.
            // If a column is occupied, leave this entire part of the center chunk alone.
            int centerX = site.X & 15, centerZ = site.Z & 15;
            for (int z = centerZ - MagmaBasinRadius; z <= centerZ + MagmaBasinRadius; z++)
            for (int x = centerX - MagmaBasinRadius; x <= centerX + MagmaBasinRadius; x++)
            {
                GeologyColumn profile = GeologyProfiles.At(site, chunk.Origin.X + x, chunk.Origin.Y + z);
                if (!PlanColumn(chunk, x, z, profile, site.Kind, writes)) return false;
            }
            return true;
        }

        private static bool PlanColumn(TerrainChunk chunk, int x, int z, GeologyColumn shape, GeologyKind kind, List<CellEdit> writes)
        {
            int top = chunk.CalculateTopmostCellHeight(x, z);
            int ground = top;
            bool dry = kind == GeologyKind.DryCanyon || kind == GeologyKind.ColumnarBasalt ||
                kind == GeologyKind.GraniteDomes || kind == GeologyKind.ErodedBadlands || kind == GeologyKind.GlacialCirque;
            while (ground > 4)
            {
                int id = Terrain.ExtractContents(chunk.GetCellValueFast(x, ground, z));
                if (dry && (id == WaterBlock.Index || id == MagmaBlock.Index || id == IceBlock.Index)) return false;
                if (id != WaterBlock.Index && id != MagmaBlock.Index && id != IceBlock.Index && id != SnowBlock.Index) break;
                ground--;
            }
            if (ground < 5 || top >= 250) return false;

            int surface = GeologyProfiles.Mix(ground, shape.Surface, shape.Weight);
            bool tunnel = shape.CaveFloor != 0;
            if (tunnel) surface = Math.Max(surface, shape.CaveFloor);
            if (shape.Waterline != 0 && shape.Surface < shape.Waterline && shape.Weight >= .9f && !tunnel)
                surface = Math.Min(surface, shape.Waterline - 1);
            if (shape.Weight < .35f && surface == ground && !tunnel) return true;

            int fluidTop = shape.Waterline;
            int fluidValue = shape.Liquid;
            int sourceLine = shape.SourceLine;
            if (fluidTop == 0 && shape.Weight < .7f && Terrain.ExtractContents(chunk.GetCellValueFast(x, top, z)) == WaterBlock.Index)
            {
                fluidTop = sourceLine = top;
                fluidValue = Terrain.MakeBlockValue(WaterBlock.Index);
            }

            int low = Math.Min(ground, surface) - SkinDepth;
            if (tunnel) low = Math.Min(low, shape.CaveFloor - 2);
            low = Math.Max(5, low);
            int high = Math.Max(top, Math.Max(surface, fluidTop));
            if (high > 248 || high - low >= MaxHeightRange) return false;

            int offset = TerrainChunk.CalculateCellIndex(x, 0, z);
            bool volcano = GeologySites.IsVolcano(kind);
            for (int y = low; y <= high; y++)
            {
                int contents = Terrain.ExtractContents(chunk.GetCellValueFast(offset + y));
                if (!Natural(contents) && !(volcano && (contents == ClayBlock.Index || contents == CoalBlock.Index)))
                    return false;
            }

            int nativeCap = chunk.GetCellValueFast(offset + ground);
            int above = 0;
            for (int y = high; y >= low; y--)
            {
                int old = chunk.GetCellValueFast(offset + y);
                int oldContents = Terrain.ExtractContents(old);
                int value;
                bool hollow = tunnel && y > shape.CaveFloor && y <= shape.CaveRoof;
                if (!hollow && y <= surface)
                {
                    if (tunnel && (y == shape.CaveFloor || y == shape.CaveRoof + 1)) value = shape.CaveLining;
                    else if (y < surface - SkinDepth && y < ground && oldContents != 0 &&
                        oldContents != WaterBlock.Index && oldContents != MagmaBlock.Index) value = old;
                    else if (shape.Weight < .35f && y == surface && BlocksManager.Blocks[Terrain.ExtractContents(nativeCap)].IsCollidable) value = nativeCap;
                    else value = shape.MaterialAt(y, surface);
                }
                else if (y <= fluidTop)
                {
                    int contents = Terrain.ExtractContents(fluidValue);
                    bool sameAbove = above == contents || (contents == MineralWaterBlock.Index || contents == WaterBlock.Index) &&
                        (above == MineralWaterBlock.Index || above == WaterBlock.Index);
                    int data = FluidBlock.SetLevel(Terrain.ExtractData(fluidValue), y > sourceLine ? 1 : 0);
                    value = Terrain.MakeBlockValue(contents, 0, FluidBlock.SetIsTop(data, !sameAbove));
                }
                else value = 0;

                if (value != old) writes.Add(new CellEdit(offset + y, value));
                above = Terrain.ExtractContents(value);
            }
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
                case GraniteBlock.Index:
                case BasaltBlock.Index:
                case SandstoneBlock.Index:
                case LimestoneBlock.Index:
                case WaterBlock.Index:
                case MagmaBlock.Index:
                case SnowBlock.Index:
                case IceBlock.Index:
                case TallGrassBlock.Index:
                case CoalOreBlock.Index:
                case IronOreBlock.Index:
                case CopperOreBlock.Index:
                case DiamondOreBlock.Index:
                case GermaniumOreBlock.Index:
                case SaltpeterOreBlock.Index:
                case SulphurOreBlock.Index: return true;
                default: return false;
            }
        }

        private readonly struct CellEdit
        {
            internal readonly int Index, Value;
            internal CellEdit(int index, int value) { Index = index; Value = value; }
        }
    }
}
