using System;
using System.Collections.Generic;

namespace SCIEPort.Features.IndustrialFurniture
{
    /// <summary>
    /// An axis-aligned collision box expressed in cell-local coordinates. Every
    /// component is in the range <c>[0, 1]</c> where the origin is the block's min
    /// corner, <c>+Y</c> points up one block, and <c>+X</c>/<c>+Z</c> span the block
    /// footprint. The engine treats these values as fractions of a single block, so
    /// a box whose maximum <c>Y</c> is <c>0.35</c> presents a 0.35-block-tall solid.
    /// </summary>
    public struct CollisionBox
    {
        /// <summary>Local minimum corner, inclusive.</summary>
        public double MinX;
        /// <summary>Local minimum vertical corner, inclusive.</summary>
        public double MinY;
        /// <summary>Local minimum depth corner, inclusive.</summary>
        public double MinZ;
        /// <summary>Local maximum corner, exclusive of the next cell.</summary>
        public double MaxX;
        /// <summary>Local maximum vertical corner; the walkable top for this box.</summary>
        public double MaxY;
        /// <summary>Local maximum depth corner.</summary>
        public double MaxZ;

        /// <summary>Creates a box from two opposing local corners.</summary>
        /// <param name="minX">Local minimum X.</param>
        /// <param name="minY">Local minimum Y.</param>
        /// <param name="minZ">Local minimum Z.</param>
        /// <param name="maxX">Local maximum X.</param>
        /// <param name="maxY">Local maximum Y.</param>
        /// <param name="maxZ">Local maximum Z.</param>
        public CollisionBox(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
        }

        /// <summary>Gets the vertical height of the box, one block being <c>1.0</c>.</summary>
        public double Height => MaxY - MinY;
    }

    /// <summary>
    /// Pure, engine-independent mesh-to-collision-box conversion. It is compiled both
    /// into the running mod (through an adapter that feeds real baked <c>BlockMesh</c>
    /// data) and into the offline <c>net8.0</c> test suite, so the geometry logic can be
    /// validated without the game assembly. The algorithm rasterizes the actual triangle
    /// set into a cell-local occupancy grid and then merges occupied cells into a small
    /// set of axis-aligned boxes, which keeps open cavities (rings, moulds, frames, legs)
    /// passable instead of filling them with one solid block-sized brick.
    /// </summary>
    public static class FurnitureCollisionModel
    {
        /// <summary>
        /// Derives cell-local collision boxes that follow the model silhouette.
        /// </summary>
        /// <param name="positions">Flat triangle-strip of local-space vertex coordinates as <c>x,y,z</c> triples, already scaled, rotated and translated into the <c>[0, 1]</c> block footprint.</param>
        /// <param name="triangles">Flat list of vertex indices in <c>i0,i1,i2</c> triples referencing <paramref name="positions"/>.</param>
        /// <param name="gridX">Voxel subdivisions across the local X axis; must be positive.</param>
        /// <param name="gridY">Voxel subdivisions across the local Y (height) axis; controls vertical resolution and therefore the smallest representable step height.</param>
        /// <param name="gridZ">Voxel subdivisions across the local Z axis; must be positive.</param>
        /// <returns>The merged collision boxes, clamped to <c>[0, 1]</c> and ordered deterministically; an empty array when the mesh has no usable triangles.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any grid dimension is smaller than one.</exception>
        public static CollisionBox[] BuildBoxes(
            IList<float> positions,
            IList<int> triangles,
            int gridX,
            int gridY,
            int gridZ)
        {
            return BuildBoxes(positions, triangles, gridX, gridY, gridZ, out _);
        }

        /// <summary>
        /// Derives collision boxes and reports the highest occupied local <c>Y</c> so the
        /// caller can log and verify that the physical top matches the visible top.
        /// </summary>
        /// <param name="positions">Flat local-space vertex coordinate triples.</param>
        /// <param name="triangles">Flat triangle vertex-index triples.</param>
        /// <param name="gridX">Voxel subdivisions across local X.</param>
        /// <param name="gridY">Voxel subdivisions across local Y.</param>
        /// <param name="gridZ">Voxel subdivisions across local Z.</param>
        /// <param name="topY">Receives the largest local Y covered by any triangle, clamped to <c>[0, 1]</c>.</param>
        /// <returns>The merged collision boxes.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when any grid dimension is smaller than one.</exception>
        public static CollisionBox[] BuildBoxes(
            IList<float> positions,
            IList<int> triangles,
            int gridX,
            int gridY,
            int gridZ,
            out double topY)
        {
            if (gridX < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(gridX), "Grid X must be at least one.");
            }

            if (gridY < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(gridY), "Grid Y must be at least one.");
            }

            if (gridZ < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(gridZ), "Grid Z must be at least one.");
            }

            bool[,] occupancy = MarkOccupancy(positions, triangles, gridX, gridY, gridZ, out topY);
            FloodFillEnclosedCavities(occupancy, gridX, gridY, gridZ);
            return MergeOccupancy(occupancy, gridX, gridY, gridZ);
        }

        /// <summary>
        /// Turns triangle-stamped surface shells into solid bodies. A voxel that is empty but
        /// sealed off from the outside by surrounding geometry is the interior of a solid and
        /// must block movement; a voxel that connects to the boundary through other empty
        /// voxels is a real opening (a ring bore, a mould cavity, the gap between legs) and
        /// must stay passable. This is the distinction a single whole-model bounding box
        /// always gets wrong.
        /// </summary>
        private static void FloodFillEnclosedCavities(bool[,] occupancy, int gridX, int gridY, int gridZ)
        {
            int face = gridX * gridY;
            bool[,] outside = new bool[face, gridZ];
            Queue<int> queue = new Queue<int>();

            void Seed(int x, int y, int z)
            {
                int flat = x + (y * gridX);
                if (!occupancy[flat, z] && !outside[flat, z])
                {
                    outside[flat, z] = true;
                    queue.Enqueue(x);
                    queue.Enqueue(y);
                    queue.Enqueue(z);
                }
            }

            for (int z = 0; z < gridZ; z++)
            {
                for (int y = 0; y < gridY; y++)
                {
                    Seed(0, y, z);
                    Seed(gridX - 1, y, z);
                }
            }

            for (int z = 0; z < gridZ; z++)
            {
                for (int x = 0; x < gridX; x++)
                {
                    Seed(x, 0, z);
                    Seed(x, gridY - 1, z);
                }
            }

            for (int y = 0; y < gridY; y++)
            {
                for (int x = 0; x < gridX; x++)
                {
                    Seed(x, y, 0);
                    Seed(x, y, gridZ - 1);
                }
            }

            while (queue.Count > 0)
            {
                int x = queue.Dequeue();
                int y = queue.Dequeue();
                int z = queue.Dequeue();
                VisitNeighbor(x + 1, y, z, occupancy, outside, gridX, gridY, gridZ, queue);
                VisitNeighbor(x - 1, y, z, occupancy, outside, gridX, gridY, gridZ, queue);
                VisitNeighbor(x, y + 1, z, occupancy, outside, gridX, gridY, gridZ, queue);
                VisitNeighbor(x, y - 1, z, occupancy, outside, gridX, gridY, gridZ, queue);
                VisitNeighbor(x, y, z + 1, occupancy, outside, gridX, gridY, gridZ, queue);
                VisitNeighbor(x, y, z - 1, occupancy, outside, gridX, gridY, gridZ, queue);
            }

            for (int z = 0; z < gridZ; z++)
            {
                for (int y = 0; y < gridY; y++)
                {
                    for (int x = 0; x < gridX; x++)
                    {
                        int flat = x + (y * gridX);
                        if (!occupancy[flat, z] && !outside[flat, z])
                        {
                            occupancy[flat, z] = true;
                        }
                    }
                }
            }
        }

        private static void VisitNeighbor(
            int x,
            int y,
            int z,
            bool[,] occupancy,
            bool[,] outside,
            int gridX,
            int gridY,
            int gridZ,
            Queue<int> queue)
        {
            if (x < 0 || y < 0 || z < 0 || x >= gridX || y >= gridY || z >= gridZ)
            {
                return;
            }

            int flat = x + (y * gridX);
            if (occupancy[flat, z] || outside[flat, z])
            {
                return;
            }

            outside[flat, z] = true;
            queue.Enqueue(x);
            queue.Enqueue(y);
            queue.Enqueue(z);
        }

        /// <summary>
        /// Rotates a vertex triple set 90 degrees about the local vertical axis through the
        /// block center. It exists so the offline tests can prove that voxelizing a rotated
        /// mesh yields the same boxes as rotating the boxes of the unrotated mesh.
        /// </summary>
        /// <param name="positions">Flat local-space vertex coordinate triples.</param>
        /// <returns>A new flat array of rotated local-space coordinate triples.</returns>
        public static float[] RotateVertices90(IList<float> positions)
        {
            float[] result = new float[positions.Count];
            for (int i = 0; i + 2 < positions.Count; i += 3)
            {
                float x = positions[i];
                float y = positions[i + 1];
                float z = positions[i + 2];
                result[i] = 1f - z;
                result[i + 1] = y;
                result[i + 2] = x;
            }

            return result;
        }

        /// <summary>
        /// Rotates a single box 90 degrees about the local vertical axis through the block
        /// center, matching <see cref="RotateVertices90"/>.
        /// </summary>
        /// <param name="box">The box to rotate.</param>
        /// <returns>The rotated box in local space.</returns>
        public static CollisionBox RotateBox90(CollisionBox box)
        {
            return new CollisionBox(
                1d - box.MaxZ,
                box.MinY,
                box.MinX,
                1d - box.MinZ,
                box.MaxY,
                box.MaxX);
        }

        private static bool[,] MarkOccupancy(
            IList<float> positions,
            IList<int> triangles,
            int gridX,
            int gridY,
            int gridZ,
            out double topY)
        {
            bool[,] occupancy = new bool[gridX * gridY, gridZ];
            topY = 0d;
            if (positions == null || triangles == null || positions.Count < 3)
            {
                return occupancy;
            }

            int vertexCount = positions.Count / 3;
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                int i0 = triangles[t];
                int i1 = triangles[t + 1];
                int i2 = triangles[t + 2];
                if (!IsValidIndex(i0, vertexCount) || !IsValidIndex(i1, vertexCount) || !IsValidIndex(i2, vertexCount))
                {
                    continue;
                }

                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double minZ = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;
                double maxZ = double.MinValue;
                AccumulateVertex(i0, positions, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                AccumulateVertex(i1, positions, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                AccumulateVertex(i2, positions, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);

                // A triangle whose bounding box collapses to a line or point carries no
                // volume and would otherwise stamp a zero-thickness wall.
                if (!HasVolume(minX, minY, minZ, maxX, maxY, maxZ))
                {
                    // Still record the apex height so a genuinely flat model (a single
                    // horizontal sheet) reports a sensible top instead of zero.
                    if (Clamp01(maxY) > topY)
                    {
                        topY = Clamp01(maxY);
                    }

                    continue;
                }

                if (Clamp01(maxY) > topY)
                {
                    topY = Clamp01(maxY);
                }

                int x0 = CellIndex(Clamp01(minX), gridX, true);
                int x1 = CellIndex(Clamp01(maxX), gridX, false);
                int y0 = CellIndex(Clamp01(minY), gridY, true);
                int y1 = CellIndex(Clamp01(maxY), gridY, false);
                int z0 = CellIndex(Clamp01(minZ), gridZ, true);
                int z1 = CellIndex(Clamp01(maxZ), gridZ, false);

                // A triangle can be degenerate along one axis (a flat sheet or a wall) and,
                // when it lands exactly on a voxel boundary, the ceil-based upper index can
                // fall below the floor-based lower index. Force the range to a single voxel
                // so the geometry is still stamped instead of vanishing.
                if (x1 < x0)
                {
                    x1 = x0;
                }

                if (y1 < y0)
                {
                    y1 = y0;
                }

                if (z1 < z0)
                {
                    z1 = z0;
                }


                for (int z = z0; z <= z1; z++)
                {
                    for (int y = y0; y <= y1; y++)
                    {
                        for (int x = x0; x <= x1; x++)
                        {
                            occupancy[x + (y * gridX), z] = true;
                        }
                    }
                }
            }

            return occupancy;
        }

        private static CollisionBox[] MergeOccupancy(
            bool[,] occupancy,
            int gridX,
            int gridY,
            int gridZ)
        {
            List<CollisionBox> boxes = new List<CollisionBox>();
            bool[,] consumed = new bool[occupancy.GetLength(0), occupancy.GetLength(1)];
            double stepX = 1d / gridX;
            double stepY = 1d / gridY;
            double stepZ = 1d / gridZ;
            for (int z = 0; z < gridZ; z++)
            {
                for (int y = 0; y < gridY; y++)
                {
                    for (int x = 0; x < gridX; x++)
                    {
                        int flat = x + (y * gridX);
                        if (!occupancy[flat, z] || consumed[flat, z])
                        {
                            continue;
                        }

                        int runX = 1;
                        while (x + runX < gridX)
                        {
                            int next = (x + runX) + (y * gridX);
                            if (!occupancy[next, z] || consumed[next, z])
                            {
                                break;
                            }

                            runX++;
                        }

                        int runY = 1;
                        while (y + runY < gridY)
                        {
                            bool rowFull = true;
                            for (int dx = 0; dx < runX; dx++)
                            {
                                int flatRow = (x + dx) + ((y + runY) * gridX);
                                if (!occupancy[flatRow, z] || consumed[flatRow, z])
                                {
                                    rowFull = false;
                                    break;
                                }
                            }

                            if (!rowFull)
                            {
                                break;
                            }

                            runY++;
                        }

                        int runZ = 1;
                        while (z + runZ < gridZ)
                        {
                            bool layerFull = true;
                            for (int dy = 0; dy < runY && layerFull; dy++)
                            {
                                for (int dx = 0; dx < runX; dx++)
                                {
                                    int flatCell = (x + dx) + ((y + dy) * gridX);
                                    if (!occupancy[flatCell, z + runZ] || consumed[flatCell, z + runZ])
                                    {
                                        layerFull = false;
                                        break;
                                    }
                                }
                            }

                            if (!layerFull)
                            {
                                break;
                            }

                            runZ++;
                        }

                        for (int dz = 0; dz < runZ; dz++)
                        {
                            for (int dy = 0; dy < runY; dy++)
                            {
                                for (int dx = 0; dx < runX; dx++)
                                {
                                    consumed[x + dx + ((y + dy) * gridX), z + dz] = true;
                                }
                            }
                        }

                        boxes.Add(new CollisionBox(
                            x * stepX,
                            y * stepY,
                            z * stepZ,
                            (x + runX) * stepX,
                            (y + runY) * stepY,
                            (z + runZ) * stepZ));
                    }
                }
            }

            boxes.Sort((a, b) =>
            {
                int c = a.MinY.CompareTo(b.MinY);
                if (c != 0)
                {
                    return c;
                }

                c = a.MinZ.CompareTo(b.MinZ);
                if (c != 0)
                {
                    return c;
                }

                return a.MinX.CompareTo(b.MinX);
            });
            return boxes.ToArray();
        }

        private static bool IsValidIndex(int index, int vertexCount)
        {
            return index >= 0 && index < vertexCount;
        }

        private static void AccumulateVertex(
            int index,
            IList<float> positions,
            ref double minX,
            ref double minY,
            ref double minZ,
            ref double maxX,
            ref double maxY,
            ref double maxZ)
        {
            double x = positions[index * 3];
            double y = positions[(index * 3) + 1];
            double z = positions[(index * 3) + 2];
            if (x < minX)
            {
                minX = x;
            }

            if (y < minY)
            {
                minY = y;
            }

            if (z < minZ)
            {
                minZ = z;
            }

            if (x > maxX)
            {
                maxX = x;
            }

            if (y > maxY)
            {
                maxY = y;
            }

            if (z > maxZ)
            {
                maxZ = z;
            }
        }

        private static bool HasVolume(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            const double epsilon = 1e-4;
            return (maxX - minX) > epsilon || (maxY - minY) > epsilon || (maxZ - minZ) > epsilon;
        }

        private static double Clamp01(double value)
        {
            if (double.IsNaN(value))
            {
                return 0d;
            }

            if (value < 0d)
            {
                return 0d;
            }

            if (value > 1d)
            {
                return 1d;
            }

            return value;
        }

        private static int CellIndex(double unit, int grid, bool isMin)
        {
            int index = isMin ? (int)(unit * grid) : (int)Math.Ceiling(unit * grid) - 1;
            if (index < 0)
            {
                return 0;
            }

            if (index > grid - 1)
            {
                return grid - 1;
            }

            return index;
        }
    }
}
