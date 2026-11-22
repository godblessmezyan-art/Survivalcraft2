#pragma warning disable CS1591

using System;
using System.Collections.Generic;

namespace SCIEPort.Features.Scenery
{
    /// <summary>Type of a generated fantasy landmark.</summary>
    public enum SceneryKind
    {
        None = 0,
        Shipwreck = 1,
        Airship = 2,
        SkyIsland = 3
    }

    /// <summary>
    /// A deterministic landmark site. All coordinates are world cells; local shape
    /// coordinates are offset from X/Y/Z and rotated by Rotation quarter turns.
    /// </summary>
    public struct ScenerySite
    {
        public SceneryKind Kind;
        public int Seed;
        public int X;
        public int Y;
        public int Z;
        public int Length;
        public int Width;
        public int Height;
        public int Rotation;
        public int HullHeight;
        public int SlopePercent;
        public int MastHeight;
        public int EnvelopeRadius;
        public int Variant;
        public int TopHeight;
        public int BottomDepth;
        public int TreeX;
        public int TreeZ;
        public int TreeHeight;
        public int RewardX;
        public int RewardZ;
        public int DisplayX;
        public int DisplayY;
        public int DisplayZ;
        public int MinX;
        public int MinY;
        public int MinZ;
        public int MaxX;
        public int MaxY;
        public int MaxZ;

        public bool Intersects(int minX, int minZ, int maxX, int maxZ)
        {
            return MaxX >= minX && MinX <= maxX && MaxZ >= minZ && MinZ <= maxZ;
        }
    }

    /// <summary>Receives the solid cells of a landmark in world coordinates.</summary>
    public interface IScenerySink
    {
        /// <summary>Writes one cell. Implementations must ignore out-of-chunk cells.</summary>
        void Set(int x, int y, int z, int blockIndex, int data, bool replaceFluid);
    }

    /// <summary>Content ids used by a landmark. Keeps the rule layer engine-free.</summary>
    public struct SceneryBlockSet
    {
        public int Hull;
        public int Envelope;
        public int Cloudstone;
        public int Crystal;
        public int Brass;
        public int Sail;
        public int Wood;
        public int Planks;
        public int Glass;
        public int Treasure;
        public int Leaves;
        public int Dirt;
        public int Grass;
        public int Display;
    }

    /// <summary>
    /// Deterministic site selection and voxel layout for fantasy landmarks.
    /// The same world seed, cell and terrain height always produce the same site
    /// and the same cells, independent of chunk generation order.
    /// </summary>
    public static class SceneryRules
    {
        public const int CellSize = 112;
        public const int CellInset = 24;
        public const int RegionSize = 512;
        public const int RegionInset = 96;
        public const int SkySiteChancePercent = 55;
        public const int ChunkSize = 16;
        public const int MaxSitesPerChunk = 4;
        public const int MinOceanDepth = 8;
        public const int MinAirClearance = 26;
        public const int MaxWorldHeight = 246;

        /// <summary>Collects shipwrecks from 112-cell cells and sky landmarks from 512-cell regions.</summary>
        public static void VisitSites(
            int worldSeed,
            int chunkX,
            int chunkZ,
            int oceanLevel,
            Func<int, int, float> height,
            List<ScenerySite> sites)
        {
            if (sites == null) throw new ArgumentNullException("sites");
            if (height == null) throw new ArgumentNullException("height");
            sites.Clear();
            int minX = chunkX * ChunkSize;
            int minZ = chunkZ * ChunkSize;
            int maxX = minX + ChunkSize - 1;
            int maxZ = minZ + ChunkSize - 1;

            // Shipwrecks keep the old 112-cell rhythm: they hide under water, so density is unobtrusive.
            int margin = CellInset + 20;
            int fromCellX = FloorDiv(minX - margin, CellSize);
            int toCellX = FloorDiv(maxX + margin, CellSize);
            int fromCellZ = FloorDiv(minZ - margin, CellSize);
            int toCellZ = FloorDiv(maxZ + margin, CellSize);
            for (int cellZ = fromCellZ; cellZ <= toCellZ; cellZ++)
            {
                for (int cellX = fromCellX; cellX <= toCellX; cellX++)
                {
                    ScenerySite site;
                    if (!TryCreateSite(worldSeed, cellX, cellZ, oceanLevel, height, out site)) continue;
                    if (!site.Intersects(minX, minZ, maxX, maxZ)) continue;
                    sites.Add(site);
                    if (sites.Count >= MaxSitesPerChunk) return;
                }
            }

            // Airships and sky islands are large and readable from far away, so each
            // 512-cell region holds at most one, and only a little over half of the
            // regions hold one at all: roughly one landmark per two regions.
            int skyMargin = RegionInset + 20;
            int fromRegionX = FloorDiv(minX - skyMargin, RegionSize);
            int toRegionX = FloorDiv(maxX + skyMargin, RegionSize);
            int fromRegionZ = FloorDiv(minZ - skyMargin, RegionSize);
            int toRegionZ = FloorDiv(maxZ + skyMargin, RegionSize);
            for (int regionZ = fromRegionZ; regionZ <= toRegionZ; regionZ++)
            {
                for (int regionX = fromRegionX; regionX <= toRegionX; regionX++)
                {
                    ScenerySite site;
                    if (!TryCreateSkySite(worldSeed, regionX, regionZ, oceanLevel, height, out site)) continue;
                    if (!site.Intersects(minX, minZ, maxX, maxZ)) continue;
                    sites.Add(site);
                    if (sites.Count >= MaxSitesPerChunk) return;
                }
            }
        }

        /// <summary>Evaluates one shipwreck cell. Pure except for the supplied height function.</summary>
        public static bool TryCreateSite(
            int worldSeed,
            int cellX,
            int cellZ,
            int oceanLevel,
            Func<int, int, float> height,
            out ScenerySite site)
        {
            site = new ScenerySite();
            var random = new SceneryRandom(Hash(worldSeed, cellX, cellZ, 0x2C1B3C6Du));
            int anchorX = cellX * CellSize + CellSize / 2 + random.Next(-CellInset, CellInset);
            int anchorZ = cellZ * CellSize + CellSize / 2 + random.Next(-CellInset, CellInset);
            float surface = height(anchorX, anchorZ);
            if (float.IsNaN(surface) || float.IsInfinity(surface)) return false;
            int terrain = (int)Math.Floor(surface);
            int depth = oceanLevel - terrain;
            if (surface < oceanLevel - 1f && depth >= MinOceanDepth)
            {
                return TryCreateShipwreck(random, anchorX, anchorZ, terrain, oceanLevel, out site);
            }
            return false;
        }

        /// <summary>Evaluates the single sky landmark candidate of one 512-cell region.</summary>
        public static bool TryCreateSkySite(
            int worldSeed,
            int regionX,
            int regionZ,
            int oceanLevel,
            Func<int, int, float> height,
            out ScenerySite site)
        {
            site = new ScenerySite();
            var random = new SceneryRandom(Hash(worldSeed, regionX, regionZ, 0x6B1F42A7u));
            if (!random.Chance(SkySiteChancePercent)) return false;
            int anchorX = regionX * RegionSize + RegionSize / 2 + random.Next(-RegionInset, RegionInset);
            int anchorZ = regionZ * RegionSize + RegionSize / 2 + random.Next(-RegionInset, RegionInset);
            float surface = height(anchorX, anchorZ);
            if (float.IsNaN(surface) || float.IsInfinity(surface)) return false;
            int terrain = (int)Math.Floor(surface);
            if (terrain > 150) return false;
            if (random.Chance(50))
            {
                return TryCreateAirship(random, anchorX, anchorZ, terrain, out site);
            }
            return TryCreateSkyIsland(random, anchorX, anchorZ, terrain, out site);
        }

        /// <summary>Writes every cell of a site through the sink. Returns the number of emitted cells.</summary>
        public static int Paint(ScenerySite site, IScenerySink sink, SceneryBlockSet blocks)
        {
            if (sink == null) throw new ArgumentNullException("sink");
            if (site.Kind == SceneryKind.None) return 0;
            int halfLength = Math.Max(site.Length, site.Width) / 2 + 3;
            int halfWidth = halfLength;
            int emitted = 0;
            for (int ly = 0; ly <= site.Height; ly++)
            {
                for (int lz = -halfWidth; lz <= halfWidth; lz++)
                {
                    for (int lx = -halfLength; lx <= halfLength; lx++)
                    {
                        int block;
                        int data;
                        if (!Voxel(site, lx, ly, lz, blocks, out block, out data)) continue;
                        int x;
                        int y;
                        int z;
                        LocalToWorld(site, lx, ly, lz, out x, out y, out z);
                        bool fluid = site.Kind == SceneryKind.Shipwreck;
                        sink.Set(x, y, z, block, data, fluid);
                        emitted++;
                    }
                }
            }
            return emitted;
        }

        /// <summary>Horizontal cell with a positive modulo, including negative world coordinates.</summary>
        public static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            if (value % divisor != 0 && (value < 0) != (divisor < 0)) quotient--;
            return quotient;
        }

        private static bool TryCreateShipwreck(
            SceneryRandom random,
            int anchorX,
            int anchorZ,
            int terrain,
            int oceanLevel,
            out ScenerySite site)
        {
            site = new ScenerySite();
            if (!random.Chance(76)) return false;
            int length = random.Next(17, 25);
            int width = random.Next(7, 11);
            int hullHeight = random.Next(7, 10);
            int mastHeight = random.Next(5, 9);
            int slopePercent = random.Next(-14, 14);
            int tiltRange = (int)Math.Ceiling(Math.Abs(slopePercent) * (length / 2) / 100f);
            int localHeight = hullHeight + mastHeight + 4;
            int top = oceanLevel - 1;
            int baseY = terrain - 2;
            if (baseY < 2) baseY = 2;
            int maxHeight = top - baseY;
            if (maxHeight - tiltRange < hullHeight + 3) return false;
            if (localHeight > maxHeight - tiltRange) localHeight = maxHeight - tiltRange;
            int height = localHeight;

            site.Kind = SceneryKind.Shipwreck;
            site.Seed = unchecked((int)random.NextUInt());
            site.X = anchorX;
            site.Y = baseY;
            site.Z = anchorZ;
            site.Length = length;
            site.Width = width;
            site.Height = height;
            site.Rotation = random.Next(0, 3);
            site.HullHeight = hullHeight;
            site.SlopePercent = slopePercent;
            site.MastHeight = mastHeight;
            ApplyBounds(ref site);
            return true;
        }

        private static bool TryCreateAirship(
            SceneryRandom random,
            int anchorX,
            int anchorZ,
            int terrain,
            out ScenerySite site)
        {
            site = new ScenerySite();
            int length = random.Next(17, 25);
            int width = random.Next(7, 11);
            int envelopeRadius = random.Next(4, 5);
            int height = envelopeRadius + 22;
            int baseY = Math.Max(terrain + MinAirClearance + 10, 136 + random.Next(0, 16));
            if (baseY + height > MaxWorldHeight) baseY = MaxWorldHeight - height;
            if (baseY <= terrain + MinAirClearance) return false;
            if (baseY + height > MaxWorldHeight) return false;

            site.Kind = SceneryKind.Airship;
            site.Seed = unchecked((int)random.NextUInt());
            site.X = anchorX;
            site.Y = baseY;
            site.Z = anchorZ;
            site.Length = length;
            site.Width = width;
            site.Height = height;
            site.Rotation = random.Next(0, 3);
            site.EnvelopeRadius = envelopeRadius;
            site.Variant = random.Next(0, 2);
            site.RewardX = 0;
            site.RewardZ = 0;
            ApplyBounds(ref site);
            LocalToWorld(site, 0, 2, 0, out site.DisplayX, out site.DisplayY, out site.DisplayZ);
            return true;
        }

        private static bool TryCreateSkyIsland(
            SceneryRandom random,
            int anchorX,
            int anchorZ,
            int terrain,
            out ScenerySite site)
        {
            site = new ScenerySite();
            int radius = random.Next(7, 10);
            int topHeight = random.Next(3, 6);
            int bottomDepth = random.Next(6, 10);
            int treeHeight = random.Next(4, 6);
            int height = topHeight + bottomDepth + treeHeight + 2;
            int baseY = Math.Max(terrain + MinAirClearance + bottomDepth + 4, 140 + random.Next(0, 20));
            if (baseY + height > MaxWorldHeight) baseY = MaxWorldHeight - height;
            if (baseY <= terrain + MinAirClearance + bottomDepth) return false;
            if (baseY + height > MaxWorldHeight) return false;

            int treeX = random.Next(-(radius - 3), radius - 3);
            int treeZ = random.Next(-(radius - 3), radius - 3);
            int limit = Math.Max(2, radius - 4);
            int rewardX = treeX > 0 ? treeX - 4 : treeX + 4;
            int rewardZ = treeZ;
            if (rewardX > limit) rewardX = limit;
            if (rewardX < -limit) rewardX = -limit;
            if (rewardZ > limit) rewardZ = limit;
            if (rewardZ < -limit) rewardZ = -limit;
            site.Kind = SceneryKind.SkyIsland;
            site.Seed = unchecked((int)random.NextUInt());
            site.X = anchorX;
            site.Y = baseY;
            site.Z = anchorZ;
            site.Length = radius * 2 + 1;
            site.Width = site.Length;
            site.Height = height;
            site.Rotation = random.Next(0, 3);
            site.TopHeight = topHeight;
            site.BottomDepth = bottomDepth;
            site.TreeX = treeX;
            site.TreeZ = treeZ;
            site.TreeHeight = treeHeight;
            site.RewardX = rewardX;
            site.RewardZ = rewardZ;
            ApplyBounds(ref site);
            LocalToWorld(site, rewardX, topHeight + bottomDepth + 2, rewardZ,
                out site.DisplayX, out site.DisplayY, out site.DisplayZ);
            return true;
        }

        private static void ApplyBounds(ref ScenerySite site)
        {
            int half = Math.Max(site.Length, site.Width) / 2 + 3;
            site.MinX = site.X - half;
            site.MaxX = site.X + half;
            site.MinZ = site.Z - half;
            site.MaxZ = site.Z + half;
            site.MinY = site.Y - 1;
            site.MaxY = site.Y + site.Height;
            if (site.Kind == SceneryKind.Shipwreck)
            {
                int tiltRange = (int)Math.Ceiling(Math.Abs(site.SlopePercent) * (site.Length / 2) / 100f);
                site.MaxY += tiltRange;
            }
        }

        private static bool Voxel(
            ScenerySite site,
            int lx,
            int ly,
            int lz,
            SceneryBlockSet blocks,
            out int block,
            out int data)
        {
            switch (site.Kind)
            {
                case SceneryKind.Shipwreck: return ShipwreckVoxel(site, lx, ly, lz, blocks, out block, out data);
                case SceneryKind.Airship: return AirshipVoxel(site, lx, ly, lz, blocks, out block, out data);
                case SceneryKind.SkyIsland: return SkyIslandVoxel(site, lx, ly, lz, blocks, out block, out data);
                default: block = 0; data = 0; return false;
            }
        }

        private static void LocalToWorld(
            ScenerySite site,
            int lx,
            int ly,
            int lz,
            out int x,
            out int y,
            out int z)
        {
            y = site.Y + ly + (site.Kind == SceneryKind.Shipwreck ? site.SlopePercent * lx / 100 : 0);
            switch (site.Rotation & 3)
            {
                case 1:
                    x = site.X + lz;
                    z = site.Z - lx;
                    break;
                case 2:
                    x = site.X - lx;
                    z = site.Z - lz;
                    break;
                case 3:
                    x = site.X - lz;
                    z = site.Z + lx;
                    break;
                default:
                    x = site.X + lx;
                    z = site.Z + lz;
                    break;
            }
        }

        private static bool ShipwreckVoxel(
            ScenerySite site,
            int lx,
            int ly,
            int lz,
            SceneryBlockSet blocks,
            out int block,
            out int data)
        {
            block = 0;
            data = 0;
            int halfLength = site.Length / 2;
            int halfWidth = site.Width / 2;
            if (lx < -halfLength - 2 || lx > halfLength + 2) return false;
            if (Math.Abs(lz) > halfWidth + 3) return false;

            float taper = 1f - 0.55f * Power(Math.Abs(lx) / (float)halfLength, 2.4f);
            if (taper < 0.18f) return false;
            float width = halfWidth * taper;
            float hullHeight = site.HullHeight * (0.72f + 0.28f * taper);
            int deck = site.HullHeight;

            // Mast, yard and torn sail first so the deck does not swallow them.
            if (lx == -2 || lx == 3)
            {
                int mastHeight = lx == -2 ? site.MastHeight : site.MastHeight - 2;
                int mastTop = deck + mastHeight;
                if (lz == 0 && ly >= deck && ly <= mastTop)
                {
                    block = blocks.Wood;
                    return true;
                }
                if (lx == -2 && ly == mastTop - 1 && Math.Abs(lz) <= 3)
                {
                    block = blocks.Wood;
                    return true;
                }
                if (lx == -2 && ly >= mastTop - 4 && ly <= mastTop - 2 && Math.Abs(lz) <= 2)
                {
                    if (Noise(site.Seed, lx, ly, lz, 0x51) < 26) return false;
                    block = blocks.Sail;
                    data = 0;
                    return true;
                }
            }

            // Deck and hatch.
            if ((ly == deck || ly == deck - 1) && Math.Abs(lz) <= width * 0.82f)
            {
                if (Math.Abs(lx) <= 2 && Math.Abs(lz) <= 1) return false;
                if (Noise(site.Seed, lx, ly, lz, 0x52) < 5) return false;
                block = blocks.Planks;
                return true;
            }

            // Hull shell: a hollow elliptical tube narrowed towards bow and stern.
            if (ly <= Math.Ceiling(hullHeight) + 1)
            {
                float zNorm = lz / Math.Max(0.75f, width);
                float yNorm = (ly - hullHeight * 0.5f) / Math.Max(0.75f, hullHeight * 0.5f);
                float q = zNorm * zNorm + yNorm * yNorm;
                if (q >= 0.55f && q <= 1.05f)
                {
                    if (Noise(site.Seed, FloorDiv(lx, 3), FloorDiv(ly, 2), FloorDiv(lz, 3), 0x53) < 9) return false;
                    block = blocks.Hull;
                    return true;
                }
            }

            // Cargo inside the hold.
            if (ly >= 1 && ly <= 2 && Math.Abs(lx) <= 2 && Math.Abs(lz) <= 1)
            {
                if (Noise(site.Seed, lx, ly, lz, 0x54) < 38)
                {
                    block = blocks.Treasure;
                    return true;
                }
            }

            // Flotsam around the hull on the sea floor.
            if (ly == 0 && Math.Abs(lx) <= halfLength + 2 && Math.Abs(lz) <= halfWidth + 3)
            {
                if (Noise(site.Seed, lx, ly, lz, 0x55) < 4)
                {
                    block = blocks.Planks;
                    return true;
                }
            }
            return false;
        }

        private static bool AirshipVoxel(
            ScenerySite site,
            int lx,
            int ly,
            int lz,
            SceneryBlockSet blocks,
            out int block,
            out int data)
        {
            block = 0;
            data = 0;
            int halfLength = site.Length / 2;
            int halfWidth = site.Width / 2;
            int radiusY = site.EnvelopeRadius;
            int radiusX = Math.Max(4, halfLength - 1);
            int radiusZ = Math.Max(3, halfWidth - 1);
            int centerY = site.Height - radiusY - 2;

            // Guaranteed prize: the suspended display floats above the cargo crate.
            if (blocks.Display != 0 && lx == 0 && ly == 2 && lz == 0)
            {
                block = blocks.Display;
                return true;
            }

            // Envelope shell.
            float nx = lx / (float)radiusX;
            float ny = (ly - centerY) / (float)radiusY;
            float nz = lz / (float)radiusZ;
            float q = nx * nx + ny * ny + nz * nz;
            if (q >= 0.60f && q <= 1.06f)
            {
                if (Noise(site.Seed, FloorDiv(lx, 3), FloorDiv(ly, 2), FloorDiv(lz, 3), 0x61) < 7) return false;
                block = blocks.Envelope;
                data = site.Variant;
                return true;
            }

            // Ring ribs just inside the envelope.
            if (q >= 0.40f && q < 0.60f && (lx + 1000) % 6 == 0)
            {
                block = blocks.Wood;
                return true;
            }

            // Gondola hull, deck, door and windows.
            if (ly <= 4 && Math.Abs(lx) <= 5 && Math.Abs(lz) <= 3)
            {
                if (lx == -5 && lz == 0 && ly >= 1 && ly <= 3) return false;
                if (ly == 0 || Math.Abs(lx) == 5 || Math.Abs(lz) == 3)
                {
                    if (ly == 2 && Math.Abs(lz) == 3 && (lx == -3 || lx == 0 || lx == 3))
                    {
                        block = blocks.Glass;
                        return true;
                    }
                    block = ly == 4 ? blocks.Planks : blocks.Hull;
                    return true;
                }
                if (ly == 4)
                {
                    block = blocks.Planks;
                    return true;
                }
                if (ly == 1 && lx == 0 && lz == 0)
                {
                    block = blocks.Treasure;
                    return true;
                }
                if (ly == 3 && Math.Abs(lx) == 4 && Math.Abs(lz) == 2)
                {
                    block = blocks.Crystal;
                    data = 0;
                    return true;
                }
            }

            // Struts from the gondola to the envelope.
            if (ly >= 4 && ly <= centerY - radiusY + 1 && lz == 0 && (lx == -4 || lx == 0 || lx == 4))
            {
                block = blocks.Wood;
                return true;
            }

            // Tail fins, propeller and pennant.
            if (lx >= halfLength + 1 && lx <= halfLength + 2 && Math.Abs(lz) <= radiusZ + 3 &&
                ly >= centerY - 1 && ly <= centerY + 1)
            {
                block = blocks.Planks;
                return true;
            }
            if (lx >= halfLength + 1 && lx <= halfLength + 2 && lz == 0 &&
                ly >= centerY + 1 && ly <= centerY + 5)
            {
                block = blocks.Sail;
                data = site.Variant;
                return true;
            }
            if (lx == -6 && ly >= 1 && ly <= 3 && (lz == 0 || (ly == 2 && Math.Abs(lz) <= 2)))
            {
                block = blocks.Brass;
                return true;
            }
            if (lx == 0 && lz == 0 && ly > centerY + radiusY && ly <= centerY + radiusY + 2)
            {
                block = blocks.Wood;
                return true;
            }
            if (lx == 0 && ly > centerY + radiusY + 1 && ly <= centerY + radiusY + 2 && Math.Abs(lz) <= 2)
            {
                block = blocks.Sail;
                data = site.Variant;
                return true;
            }
            return false;
        }

        private static bool SkyIslandVoxel(
            ScenerySite site,
            int lx,
            int ly,
            int lz,
            SceneryBlockSet blocks,
            out int block,
            out int data)
        {
            block = 0;
            data = 0;
            int radius = site.Width / 2;
            int topHeight = site.TopHeight;
            int bottomDepth = site.BottomDepth;
            int topY = bottomDepth + topHeight;

            // Brass altar with the guaranteed suspended prize; kept clear of the tree.
            if (blocks.Display != 0 && lx == site.RewardX && lz == site.RewardZ)
            {
                if (ly == topY + 2)
                {
                    block = blocks.Display;
                    return true;
                }
                if (ly == topY + 1)
                {
                    block = blocks.Brass;
                    return true;
                }
            }
            if (ly == topY + 1 && lx * lx + lz * lz <= 1 &&
                (lx != site.RewardX || lz != site.RewardZ) &&
                (lx != site.TreeX || lz != site.TreeZ))
            {
                block = blocks.Crystal;
                data = 1;
                return true;
            }

            // Tree trunk and canopy, rooted on the grass cap.
            if (site.TreeHeight > 0)
            {
                int trunkBase = topY + 1;
                int trunkTop = topY + site.TreeHeight;
                if (lx == site.TreeX && lz == site.TreeZ && ly >= trunkBase && ly <= trunkTop)
                {
                    block = blocks.Wood;
                    return true;
                }
                int crownY = trunkTop;
                int dx = lx - site.TreeX;
                int dz = lz - site.TreeZ;
                if (dx * dx + dz * dz + (ly - crownY) * (ly - crownY) * 2 <= 6)
                {
                    block = blocks.Leaves;
                    return true;
                }
            }

            if (ly <= topY)
            {
                float r;
                int material;
                if (ly >= bottomDepth)
                {
                    int d = topY - ly;
                    r = radius * (0.84f + 0.16f * (1f - d / (float)Math.Max(1, topHeight)));
                    material = d == 0 ? blocks.Grass : d <= 2 ? blocks.Dirt : blocks.Cloudstone;
                }
                else
                {
                    int depth = bottomDepth - ly;
                    float t = depth / (float)(bottomDepth + 1);
                    r = radius * 0.92f * Power(1f - t, 0.72f);
                    material = blocks.Cloudstone;
                }

                if (lx * lx + lz * lz <= r * r)
                {
                    block = material;
                    return true;
                }

                // Crystal outcrops and dangling shards around the rim and underside.
                if (ly <= bottomDepth && lx * lx + lz * lz <= (r + 1.35f) * (r + 1.35f) &&
                    Noise(site.Seed, lx, ly, lz, 0x71) < 7)
                {
                    block = blocks.Crystal;
                    data = ly < bottomDepth / 2 ? 0 : 1;
                    return true;
                }
                return false;
            }

            // Crystal spires on the grass cap.
            if (ly > topY && ly <= topY + 2 && lx * lx + lz * lz <= (radius - 2) * (radius - 2) &&
                (lx != site.TreeX || lz != site.TreeZ) &&
                Noise(site.Seed, lx, ly, lz, 0x72) < 5)
            {
                block = blocks.Crystal;
                data = 0;
                return true;
            }
            return false;
        }

        private static float Power(float value, float exponent)
        {
            return (float)Math.Pow(value, exponent);
        }

        private static int Noise(int seed, int x, int y, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u + (uint)salt * 0x85EBCA77u;
                h ^= (uint)x * 0xC2B2AE3Du;
                h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0x27D4EB2Fu;
                h = (h << 11) | (h >> 21);
                h ^= (uint)z * 0x165667B1u;
                h ^= h >> 15;
                h *= 0x2545F491u;
                h ^= h >> 15;
                return (int)(h % 100u);
            }
        }

        private static int Hash(int a, int b, int c, uint salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h = (h ^ salt) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return (int)(h & 0x7FFFFFFFu);
            }
        }
    }

    /// <summary>Small deterministic xorshift stream; independent of engine Random.</summary>
    public struct SceneryRandom
    {
        private uint m_state;

        public SceneryRandom(int seed)
        {
            m_state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
        }

        public uint NextUInt()
        {
            uint x = m_state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            m_state = x;
            return x;
        }

        /// <summary>Inclusive range; smaller than the engine's exclusive helper by design.</summary>
        public int Next(int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxInclusive - minInclusive + 1));
        }

        public bool Chance(int percent)
        {
            if (percent <= 0) return false;
            if (percent >= 100) return true;
            return (int)(NextUInt() % 100u) < percent;
        }
    }
}
