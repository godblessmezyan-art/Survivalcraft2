using System;
using System.Collections.Generic;
using Game;

namespace SCIEPort.Features.Scenery
{
    /// <summary>
    /// Writes fantasy landmarks into the current chunk. The rule layer decides the
    /// permanent world-space layout; this adapter only clips it to the chunk and
    /// refuses to disturb existing solid terrain.
    /// </summary>
    internal static class SceneryGenerator
    {
        [ThreadStatic]
        private static List<ScenerySite> s_sites;

        public static int Generate(TerrainChunk chunk, ITerrainContentsGenerator source, int worldSeed)
        {
            if (chunk == null || source == null) return 0;
            if (!(BlocksManager.Blocks[SceneryHullBlock.Index] is SceneryHullBlock)) return 0;
            if (s_sites == null) s_sites = new List<ScenerySite>(SceneryRules.MaxSitesPerChunk);

            SceneryRules.VisitSites(
                worldSeed,
                chunk.Coords.X,
                chunk.Coords.Y,
                source.OceanLevel,
                (x, z) => source.CalculateHeight(x, z),
                s_sites);
            if (s_sites.Count == 0) return 0;

            var sink = new ChunkScenerySink(chunk);
            SceneryBlockSet blocks = CreateBlockSet();
            for (int i = 0; i < s_sites.Count; i++)
            {
                SceneryRules.Paint(s_sites[i], sink, blocks);
            }
            return sink.Written;
        }

        private static SceneryBlockSet CreateBlockSet()
        {
            return new SceneryBlockSet
            {
                Hull = SceneryHullBlock.Index,
                Envelope = SceneryEnvelopeBlock.Index,
                Cloudstone = SceneryCloudstoneBlock.Index,
                Crystal = SceneryCrystalBlock.Index,
                Brass = SceneryBrassBlock.Index,
                Sail = ScenerySailBlock.Index,
                Wood = OakWoodBlock.Index,
                Planks = PlanksBlock.Index,
                Glass = GlassBlock.Index,
                Treasure = TreasureGeneratorBlock.Index,
                Leaves = OakLeavesBlock.Index,
                Dirt = DirtBlock.Index,
                Grass = GrassBlock.Index
            };
        }

        private sealed class ChunkScenerySink : IScenerySink
        {
            private readonly TerrainChunk m_chunk;
            private readonly int m_originX;
            private readonly int m_originZ;

            public int Written { get; private set; }

            public ChunkScenerySink(TerrainChunk chunk)
            {
                m_chunk = chunk;
                m_originX = chunk.Origin.X;
                m_originZ = chunk.Origin.Y;
            }

            public void Set(int x, int y, int z, int blockIndex, int data, bool replaceFluid)
            {
                int localX = x - m_originX;
                int localZ = z - m_originZ;
                if ((uint)localX > 15u || (uint)localZ > 15u || y < 1 || y > 254) return;
                int existing = Terrain.ExtractContents(m_chunk.GetCellValueFast(localX, y, localZ));
                if (existing != 0 && !(replaceFluid && existing == WaterBlock.Index)) return;
                m_chunk.SetCellValueFast(localX, y, localZ, Terrain.MakeBlockValue(blockIndex, 0, data));
                Written++;
            }
        }
    }
}
