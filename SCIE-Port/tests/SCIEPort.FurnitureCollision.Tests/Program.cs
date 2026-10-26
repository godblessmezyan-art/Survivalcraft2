using System;
using System.Collections.Generic;
using SCIEPort.Features.IndustrialFurniture;

namespace SCIEPort.FurnitureCollision.Tests
{
    /// <summary>
    /// Offline validation for the geometry-derived furniture collision. These exercise
    /// the pure <see cref="FurnitureCollisionModel"/> only, so no game assembly is needed.
    /// The suite covers the acceptance heights (0.25 / 0.35 / 0.5 / 0.75 / 1.0), cavity
    /// preservation, single-cell clamping and rotation sync.
    /// </summary>
    internal static class Program
    {
        private const int Grid = 20;
        private static int s_assertions;

        private static int Main()
        {
            try
            {
                TestFlatSheetAt035MatchesVisibleTop();
                TestAcceptanceHeights();
                TestOverScaleModelNeverExceedsOneBlock();
                TestFrameCavityStaysPassable();
                TestFullCubeFillsTheCell();
                TestEmptyMeshProducesNoBoxes();
                TestVoxelizationRotatesWithModel();
                TestInvalidGridThrows();
                Console.WriteLine("Furniture collision tests passed: " + s_assertions);
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void TestFlatSheetAt035MatchesVisibleTop()
        {
            List<float> positions = new List<float>();
            List<int> triangles = new List<int>();
            AddBox(positions, triangles, 0.1f, 0.35f, 0.1f, 0.9f, 0.35f, 0.9f);
            CollisionBox[] boxes = Build(positions, triangles, out double topY);
            Assert(boxes.Length >= 1, "A flat sheet must still produce at least one collider.");
            Assert(WithinOneQuantum(topY, 0.35), "Reported geometric top must sit at the visible 0.35 surface.");
            foreach (CollisionBox box in boxes)
            {
                Assert(box.MinY >= 0.30 && box.MinY <= 0.40, "Sheet collider must hug the 0.35 plane, not drop to the floor.");
                Assert(box.MaxY <= 0.35 + Quantum() + 1e-6, "Sheet collider must not rise a full block above the visible top.");
                Assert(box.MinX >= 0.0 && box.MaxX <= 1.0 && box.MinZ >= 0.0 && box.MaxZ <= 1.0, "Sheet collider must stay inside the cell footprint.");
            }
        }

        private static void TestAcceptanceHeights()
        {
            double[] heights = { 0.25, 0.35, 0.5, 0.75, 1.0 };
            foreach (double h in heights)
            {
                List<float> positions = new List<float>();
                List<int> triangles = new List<int>();
                AddBox(positions, triangles, 0.0f, 0.0f, 0.0f, 1.0f, (float)h, 1.0f);
                CollisionBox[] boxes = Build(positions, triangles, out double topY);
                double maxTop = 0d;
                double minBottom = 1d;
                foreach (CollisionBox box in boxes)
                {
                    if (box.MaxY > maxTop)
                    {
                        maxTop = box.MaxY;
                    }

                    if (box.MinY < minBottom)
                    {
                        minBottom = box.MinY;
                    }
                }

                Assert(Math.Abs(minBottom) < 1e-9, "A floor-standing prop must start at Y=0 so nothing hovers under it.");
                Assert(maxTop >= h - Quantum() - 1e-9, "Collider top must reach the acceptance height " + h + ".");
                Assert(maxTop <= h + Quantum() + 1e-9, "Collider top must not exceed the acceptance height " + h + " by more than one quantum.");
                Assert(WithinOneQuantum(topY, h), "Reported top must match the requested height " + h + ".");
            }
        }

        private static void TestOverScaleModelNeverExceedsOneBlock()
        {
            List<float> positions = new List<float>();
            List<int> triangles = new List<int>();
            AddBox(positions, triangles, -5f, -3f, -8f, 6f, 7f, 9f);
            CollisionBox[] boxes = Build(positions, triangles, out _);
            foreach (CollisionBox box in boxes)
            {
                Assert(box.MinX >= 0.0 && box.MaxX <= 1.0, "Over-scaled X must clamp into the cell.");
                Assert(box.MinY >= 0.0 && box.MaxY <= 1.0, "Over-scaled height must clamp to a single block so it never reads taller than 1.");
                Assert(box.MinZ >= 0.0 && box.MaxZ <= 1.0, "Over-scaled Z must clamp into the cell.");
            }
        }

        private static void TestFrameCavityStaysPassable()
        {
            List<float> positions = new List<float>();
            List<int> triangles = new List<int>();

            // A four-bar rectangular frame with an open middle, 0.5 blocks tall.
            AddBox(positions, triangles, 0.00f, 0.0f, 0.00f, 0.20f, 0.5f, 1.00f);
            AddBox(positions, triangles, 0.80f, 0.0f, 0.00f, 1.00f, 0.5f, 1.00f);
            AddBox(positions, triangles, 0.20f, 0.0f, 0.00f, 0.80f, 0.5f, 0.20f);
            AddBox(positions, triangles, 0.20f, 0.0f, 0.80f, 0.80f, 0.5f, 1.00f);
            CollisionBox[] boxes = Build(positions, triangles, out _);



            // The whole-model bounding box would have sealed this; the geometry model must not.
            Assert(!IsCovered(boxes, 0.5, 0.25, 0.5), "The open centre of a frame must stay passable, not a solid brick.");
            Assert(IsCovered(boxes, 0.1, 0.25, 0.5), "The frame's side bars must remain solid.");
            Assert(IsCovered(boxes, 0.5, 0.25, 0.1), "The frame's front bar must remain solid.");
        }

        private static void TestFullCubeFillsTheCell()
        {
            List<float> positions = new List<float>();
            List<int> triangles = new List<int>();
            AddBox(positions, triangles, 0f, 0f, 0f, 1f, 1f, 1f);
            CollisionBox[] boxes = Build(positions, triangles, out double topY);
            Assert(boxes.Length >= 1, "A full cube must produce colliders.");
            Assert(topY > 0.95, "A full cube must report a top close to one block.");
            Assert(IsCovered(boxes, 0.5, 0.9, 0.5), "A full cube must be solid through its interior.");
        }

        private static void TestEmptyMeshProducesNoBoxes()
        {
            CollisionBox[] boxes = Build(new List<float>(), new List<int>(), out double topY);
            Assert(boxes.Length == 0, "An empty mesh must yield zero boxes so the caller can fall back.");
            Assert(Math.Abs(topY) < 1e-9, "An empty mesh must report a zero top.");
        }

        private static void TestVoxelizationRotatesWithModel()
        {
            List<float> positions = new List<float>();
            List<int> triangles = new List<int>();

            // An asymmetric L plate so a 90 degree turn is observable.
            AddBox(positions, triangles, 0.0f, 0.0f, 0.0f, 0.5f, 0.5f, 0.5f);
            AddBox(positions, triangles, 0.5f, 0.0f, 0.0f, 1.0f, 0.5f, 0.25f);

            CollisionBox[] baseBoxes = Build(positions, triangles, out _);
            float[] rotated = FurnitureCollisionModel.RotateVertices90(positions);
            CollisionBox[] rotatedBoxes = Build(rotated, triangles, out _);

            CollisionBox[] expected = new CollisionBox[baseBoxes.Length];
            for (int i = 0; i < baseBoxes.Length; i++)
            {
                expected[i] = FurnitureCollisionModel.RotateBox90(baseBoxes[i]);
            }

            AABB baseAabb = UnionAabb(baseBoxes);
            AABB rotAabb = UnionAabb(rotatedBoxes);
            AABB expAabb = UnionAabb(expected);
            Assert(AabbClose(rotAabb, expAabb), "Voxelizing a rotated mesh must match rotating the base boxes: " + rotAabb + " vs " + expAabb);

            double baseVolume = SumVolume(baseBoxes);
            double rotVolume = SumVolume(rotatedBoxes);
            Assert(Math.Abs(baseVolume - rotVolume) < 0.05, "Rotation must preserve occupied volume, proving the collider turns with the model.");

            // A point inside the short arm must map to the rotated frame under the same turn.
            Assert(IsCovered(rotatedBoxes, 0.9, 0.25, 0.6), "After rotation the short arm must occupy the turned footprint.");
        }

        private static void TestInvalidGridThrows()
        {
            bool threw = false;
            try
            {
                List<float> positions = new List<float>();
                List<int> triangles = new List<int>();
                AddBox(positions, triangles, 0f, 0f, 0f, 1f, 1f, 1f);
                Build(positions, triangles, 0, 20, 20, out _);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }

            Assert(threw, "A zero grid subdivision must be rejected.");
        }

        private static CollisionBox[] Build(IList<float> positions, IList<int> triangles, out double topY)
        {
            return Build(positions, triangles, Grid, Grid, Grid, out topY);
        }

        private static CollisionBox[] Build(IList<float> positions, IList<int> triangles, int gx, int gy, int gz, out double topY)
        {
            return FurnitureCollisionModel.BuildBoxes(positions, triangles, gx, gy, gz, out topY);
        }

        private static void AddBox(List<float> positions, List<int> triangles, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            int b = positions.Count / 3;
            positions.AddRange(new[]
            {
                x0, y0, z0,
                x1, y0, z0,
                x1, y0, z1,
                x0, y0, z1,
                x0, y1, z0,
                x1, y1, z0,
                x1, y1, z1,
                x0, y1, z1
            });
            int[] faces =
            {
                0, 1, 2, 0, 2, 3,
                4, 6, 5, 4, 7, 6,
                0, 5, 1, 0, 4, 5,
                1, 6, 2, 1, 5, 6,
                2, 7, 3, 2, 6, 7,
                3, 4, 0, 3, 7, 4
            };
            foreach (int f in faces)
            {
                triangles.Add(b + f);
            }
        }

        private static bool IsCovered(CollisionBox[] boxes, double x, double y, double z)
        {
            foreach (CollisionBox box in boxes)
            {
                if (x > box.MinX + 1e-6 && x < box.MaxX - 1e-6 &&
                    y > box.MinY + 1e-6 && y < box.MaxY - 1e-6 &&
                    z > box.MinZ + 1e-6 && z < box.MaxZ - 1e-6)
                {
                    return true;
                }
            }

            return false;
        }

        private static double SumVolume(CollisionBox[] boxes)
        {
            double total = 0d;
            foreach (CollisionBox box in boxes)
            {
                total += (box.MaxX - box.MinX) * (box.MaxY - box.MinY) * (box.MaxZ - box.MinZ);
            }

            return total;
        }

        private static AABB UnionAabb(CollisionBox[] boxes)
        {
            AABB aabb = new AABB
            {
                MinX = 1,
                MinY = 1,
                MinZ = 1,
                MaxX = 0,
                MaxY = 0,
                MaxZ = 0
            };
            foreach (CollisionBox box in boxes)
            {
                if (box.MinX < aabb.MinX)
                {
                    aabb.MinX = box.MinX;
                }

                if (box.MinY < aabb.MinY)
                {
                    aabb.MinY = box.MinY;
                }

                if (box.MinZ < aabb.MinZ)
                {
                    aabb.MinZ = box.MinZ;
                }

                if (box.MaxX > aabb.MaxX)
                {
                    aabb.MaxX = box.MaxX;
                }

                if (box.MaxY > aabb.MaxY)
                {
                    aabb.MaxY = box.MaxY;
                }

                if (box.MaxZ > aabb.MaxZ)
                {
                    aabb.MaxZ = box.MaxZ;
                }
            }

            return aabb;
        }

        private static bool AabbClose(AABB a, AABB b)
        {
            double tol = 1.5 * Quantum();
            return Math.Abs(a.MinX - b.MinX) < tol && Math.Abs(a.MinY - b.MinY) < tol &&
                Math.Abs(a.MinZ - b.MinZ) < tol && Math.Abs(a.MaxX - b.MaxX) < tol &&
                Math.Abs(a.MaxY - b.MaxY) < tol && Math.Abs(a.MaxZ - b.MaxZ) < tol;
        }

        private static double Quantum()
        {
            return 1d / Grid;
        }

        private static bool WithinOneQuantum(double value, double target)
        {
            return value >= target - Quantum() - 1e-9 && value <= target + Quantum() + 1e-9;
        }

        private static void Assert(bool condition, string message)
        {
            s_assertions++;
            if (!condition)
            {
                throw new InvalidOperationException("Assertion failed: " + message);
            }
        }

        private struct AABB
        {
            public double MinX;
            public double MinY;
            public double MinZ;
            public double MaxX;
            public double MaxY;
            public double MaxZ;

            public override string ToString()
            {
                return string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "min=({0:0.###},{1:0.###},{2:0.###}) max=({3:0.###},{4:0.###},{5:0.###})",
                    MinX,
                    MinY,
                    MinZ,
                    MaxX,
                    MaxY,
                    MaxZ);
            }
        }
    }
}
