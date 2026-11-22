using System;
using System.Collections.Generic;
using SCIEPort.Features.Scenery;

namespace SCIEPort.Scenery.Tests
{
    internal static class Program
    {
        private const int Seed = 20261122;
        private const int OceanLevel = 64;
        private const int WorldChunkRadius = 14;

        private static int s_assertions;

        private static int Main()
        {
            try
            {
                TestDeterministicSites();
                TestSeaAndSkySitesStayInsideTheirBands();
                TestSitesProduceVoxels();
                TestRotationPreservesVolume();
                TestChunkClippingIsStable();
                TestShipwreckStaysUnderwater();
                TestAirLandmarksKeepClearance();
                TestBoundsContainEveryVoxel();
                TestBlockSetIsComplete();
                TestDensity();
                Console.WriteLine("Scenery rule tests passed: " + s_assertions);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void TestDeterministicSites()
        {
            var first = CollectSites(Seed, OceanHeight);
            var second = CollectSites(Seed, OceanHeight);
            Assert(first.Count > 0, "The sampled ocean band must contain landmarks.");
            Assert(first.Count == second.Count, "The same seed must produce the same site count.");
            for (int i = 0; i < first.Count; i++)
            {
                Assert(SameSite(first[i], second[i]), "Site generation must be byte-for-byte deterministic.");
            }

            var other = CollectSites(Seed + 1, OceanHeight);
            Assert(other.Count > 0, "A different seed must still produce landmarks.");
            Assert(first.Count != other.Count || !SameSite(first[0], other[0]),
                "A different world seed should move landmark placement.");
        }

        private static void TestSeaAndSkySitesStayInsideTheirBands()
        {
            var seaSites = CollectSites(Seed, OceanHeight);
            var landSites = CollectSites(Seed, LandHeight);
            foreach (var site in seaSites)
            {
                if (site.Kind == SceneryKind.Shipwreck)
                {
                    Assert(site.Y >= 2, "Shipwrecks must stay above the world floor.");
                    Assert(site.MaxY <= OceanLevel - 1, "Shipwrecks must remain below the ocean surface.");
                }
            }
            foreach (var site in landSites)
            {
                if (site.Kind == SceneryKind.Airship || site.Kind == SceneryKind.SkyIsland)
                {
                    Assert(site.Y >= 1, "Air landmarks must not touch the world floor.");
                    Assert(site.MaxY <= SceneryRules.MaxWorldHeight, "Air landmarks must stay below the build ceiling.");
                }
            }
            Assert(CountKind(seaSites, SceneryKind.Shipwreck) > 0, "A deep ocean band must contain shipwrecks.");
            Assert(CountKind(landSites, SceneryKind.Airship) > 0, "A land band must contain airships.");
            Assert(CountKind(landSites, SceneryKind.SkyIsland) > 0, "A land band must contain sky islands.");
        }

        private static void TestSitesProduceVoxels()
        {
            var sites = CollectSites(Seed, LandHeight);
            foreach (var site in sites)
            {
                var sink = new RecordingSink();
                int emitted = SceneryRules.Paint(site, sink, BlockSet());
                Assert(emitted > 0, "Every selected site must emit cells.");
                Assert(sink.Cells.Count > 0, "Every selected site must reach the sink.");
            }
        }

        private static void TestRotationPreservesVolume()
        {
            var sites = CollectSites(Seed, LandHeight);
            foreach (var site in sites)
            {
                var volumes = new int[4];
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    var rotated = site;
                    rotated.Rotation = rotation;
                    var sink = new RecordingSink();
                    volumes[rotation] = SceneryRules.Paint(rotated, sink, BlockSet());
                }
                for (int rotation = 1; rotation < 4; rotation++)
                {
                    Assert(volumes[rotation] == volumes[0], "Quarter-turn rotation must preserve the cell count.");
                }
            }
        }

        private static void TestChunkClippingIsStable()
        {
            var sites = CollectSites(Seed, LandHeight);
            foreach (var site in sites)
            {
                var whole = new RecordingSink();
                SceneryRules.Paint(site, whole, BlockSet());

                var clipped = new Dictionary<long, int>();
                int minChunkX = SceneryRules.FloorDiv(site.MinX, 16);
                int maxChunkX = SceneryRules.FloorDiv(site.MaxX, 16);
                int minChunkZ = SceneryRules.FloorDiv(site.MinZ, 16);
                int maxChunkZ = SceneryRules.FloorDiv(site.MaxZ, 16);
                for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
                {
                    for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                    {
                        var chunkSink = new ChunkSink(chunkX, chunkZ);
                        SceneryRules.Paint(site, chunkSink, BlockSet());
                        foreach (var pair in chunkSink.Cells)
                        {
                            Assert(!clipped.ContainsKey(pair.Key), "A cell must never be written twice across chunk borders.");
                            clipped.Add(pair.Key, pair.Value);
                        }
                    }
                }

                Assert(clipped.Count == whole.Cells.Count, "Chunk clipping must not add or drop cells.");
                foreach (var pair in whole.Cells)
                {
                    Assert(clipped.TryGetValue(pair.Key, out int value) && value == pair.Value,
                        "Chunk clipping must preserve every cell value.");
                }
            }
        }

        private static void TestShipwreckStaysUnderwater()
        {
            var sites = CollectSites(Seed, OceanHeight);
            foreach (var site in sites)
            {
                if (site.Kind != SceneryKind.Shipwreck) continue;
                var sink = new RecordingSink();
                SceneryRules.Paint(site, sink, BlockSet());
                foreach (var cell in sink.Coordinates)
                {
                    Assert(cell.Item2 <= OceanLevel - 1, "No shipwreck cell may pierce the ocean surface.");
                    Assert(cell.Item2 >= 1, "No shipwreck cell may fall below the world floor.");
                }
            }
        }

        private static void TestAirLandmarksKeepClearance()
        {
            var sites = CollectSites(Seed, LandHeight);
            foreach (var site in sites)
            {
                if (site.Kind != SceneryKind.Airship && site.Kind != SceneryKind.SkyIsland) continue;
                Assert(site.Y >= LandHeight(0, 0) + SceneryRules.MinAirClearance,
                    "Air landmarks must keep clearance above the terrain.");
                Assert(site.MaxY <= SceneryRules.MaxWorldHeight,
                    "Air landmarks must stay under the build ceiling.");
            }
        }

        private static void TestBoundsContainEveryVoxel()
        {
            foreach (var terrain in new Func<int, int, float>[] { OceanHeight, LandHeight })
            {
                foreach (var site in CollectSites(Seed, terrain))
                {
                    var sink = new RecordingSink();
                    SceneryRules.Paint(site, sink, BlockSet());
                    foreach (var cell in sink.Coordinates)
                    {
                        Assert(cell.Item1 >= site.MinX && cell.Item1 <= site.MaxX, "Voxel x must stay inside the site bounds.");
                        Assert(cell.Item3 >= site.MinZ && cell.Item3 <= site.MaxZ, "Voxel z must stay inside the site bounds.");
                        Assert(cell.Item2 >= site.MinY && cell.Item2 <= site.MaxY, "Voxel y must stay inside the site bounds.");
                    }
                }
            }
        }

        private static void TestBlockSetIsComplete()
        {
            var blocks = BlockSet();
            var ids = new[]
            {
                blocks.Hull, blocks.Envelope, blocks.Cloudstone, blocks.Crystal, blocks.Brass,
                blocks.Sail, blocks.Wood, blocks.Planks, blocks.Glass, blocks.Treasure,
                blocks.Leaves, blocks.Dirt, blocks.Grass
            };
            foreach (int id in ids)
            {
                Assert(id > 0, "Every block id used by a landmark must be positive.");
            }
        }

        private static void TestDensity()
        {
            var sites = CollectSites(Seed, LandHeight);
            int cells = (WorldChunkRadius * 2 + 1) * (WorldChunkRadius * 2 + 1);
            Assert(sites.Count > 0, "The sampled area must contain landmarks.");
            Assert(sites.Count < cells, "Landmarks must not be placed in every cell.");
            var unique = new HashSet<long>();
            foreach (var site in sites)
            {
                long key = ((long)site.X << 32) ^ (uint)site.Z;
                Assert(unique.Add(key), "Two landmarks must not share one anchor.");
            }
        }

        private static List<ScenerySite> CollectSites(int worldSeed, Func<int, int, float> height)
        {
            var result = new List<ScenerySite>();
            for (int chunkZ = -WorldChunkRadius; chunkZ <= WorldChunkRadius; chunkZ++)
            {
                for (int chunkX = -WorldChunkRadius; chunkX <= WorldChunkRadius; chunkX++)
                {
                    var sites = new List<ScenerySite>();
                    SceneryRules.VisitSites(worldSeed, chunkX, chunkZ, OceanLevel, height, sites);
                    foreach (var site in sites)
                    {
                        if (!result.Exists(existing => SameSite(existing, site))) result.Add(site);
                    }
                }
            }
            return result;
        }

        private static int CountKind(List<ScenerySite> sites, SceneryKind kind)
        {
            int count = 0;
            foreach (var site in sites)
            {
                if (site.Kind == kind) count++;
            }
            return count;
        }

        private static bool SameSite(ScenerySite a, ScenerySite b)
        {
            return a.Kind == b.Kind && a.Seed == b.Seed && a.X == b.X && a.Y == b.Y && a.Z == b.Z &&
                   a.Length == b.Length && a.Width == b.Width && a.Height == b.Height &&
                   a.Rotation == b.Rotation && a.HullHeight == b.HullHeight &&
                   a.SlopePercent == b.SlopePercent && a.MastHeight == b.MastHeight &&
                   a.EnvelopeRadius == b.EnvelopeRadius && a.Variant == b.Variant &&
                   a.TopHeight == b.TopHeight && a.BottomDepth == b.BottomDepth &&
                   a.TreeX == b.TreeX && a.TreeZ == b.TreeZ && a.TreeHeight == b.TreeHeight;
        }

        private static float OceanHeight(int x, int z)
        {
            return 50f + (float)Math.Sin(x * 0.031) * 1.5f;
        }

        private static float LandHeight(int x, int z)
        {
            return 72f + (float)Math.Sin(x * 0.017) * 4f + (float)Math.Cos(z * 0.021) * 3f;
        }

        private static SceneryBlockSet BlockSet()
        {
            return new SceneryBlockSet
            {
                Hull = 900,
                Envelope = 901,
                Cloudstone = 902,
                Crystal = 903,
                Brass = 904,
                Sail = 905,
                Wood = 1,
                Planks = 2,
                Glass = 3,
                Treasure = 4,
                Leaves = 5,
                Dirt = 6,
                Grass = 7
            };
        }

        private static void Assert(bool condition, string message)
        {
            s_assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private class RecordingSink : IScenerySink
        {
            public readonly Dictionary<long, int> Cells = new Dictionary<long, int>();
            public readonly List<Tuple<int, int, int>> Coordinates = new List<Tuple<int, int, int>>();

            public virtual void Set(int x, int y, int z, int blockIndex, int data, bool replaceFluid)
            {
                long key = Key(x, y, z);
                Cells[key] = blockIndex;
                Coordinates.Add(Tuple.Create(x, y, z));
            }

            protected static long Key(int x, int y, int z)
            {
                return ((long)(x + 1048576) << 42) | ((long)(y + 1024) << 21) | (uint)(z + 1048576);
            }
        }

        private sealed class ChunkSink : RecordingSink
        {
            private readonly int m_minX;
            private readonly int m_minZ;

            public ChunkSink(int chunkX, int chunkZ)
            {
                m_minX = chunkX * 16;
                m_minZ = chunkZ * 16;
            }

            public override void Set(int x, int y, int z, int blockIndex, int data, bool replaceFluid)
            {
                if (x < m_minX || x > m_minX + 15 || z < m_minZ || z > m_minZ + 15) return;
                base.Set(x, y, z, blockIndex, data, replaceFluid);
            }
        }
    }
}
