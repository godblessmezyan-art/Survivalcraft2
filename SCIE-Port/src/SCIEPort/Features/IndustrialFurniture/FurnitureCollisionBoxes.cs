using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.IndustrialFurniture
{
    /// <summary>
    /// Bridges a real baked <see cref="BlockMesh"/> to the pure
    /// <see cref="FurnitureCollisionModel"/>. The mesh vertices have already had the
    /// model scale, per-rotation Y turn and placement translation baked into them by
    /// <c>AppendModelMeshPart</c>, so they are the authoritative cell-local geometry.
    /// Feeding those vertices (rather than the whole-mesh bounding box) into the model
    /// produces boxes that follow the silhouette and rotate with the model for free.
    /// </summary>
    internal static class FurnitureCollisionBoxes
    {
        /// <summary>
        /// Builds engine collision boxes from the mesh geometry in cell-local space.
        /// </summary>
        /// <param name="mesh">A baked, rotation-specific block mesh.</param>
        /// <param name="topY">Receives the clamped local height of the tallest box.</param>
        /// <returns>One or more local <see cref="BoundingBox"/> colliders clamped to <c>[0, 1]</c>.</returns>
        public static BoundingBox[] Build(BlockMesh mesh, out double topY)
        {
            List<float> positions = new List<float>();
            foreach (BlockMeshVertex vertex in mesh.Vertices)
            {
                positions.Add(vertex.Position.X);
                positions.Add(vertex.Position.Y);
                positions.Add(vertex.Position.Z);
            }

            List<int> triangles = new List<int>();
            foreach (ushort index in mesh.Indices)
            {
                triangles.Add(index);
            }

            CollisionBox[] boxes = FurnitureCollisionModel.BuildBoxes(
                positions,
                triangles,
                FurnitureCollisionOptions.GridX,
                FurnitureCollisionOptions.GridY,
                FurnitureCollisionOptions.GridZ,
                out topY);

            if (boxes.Length == 0)
            {
                BoundingBox fallback = ClampToCell(mesh.CalculateBoundingBox());
                topY = fallback.Max.Y;
                return new[] { fallback };
            }

            BoundingBox[] result = new BoundingBox[boxes.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                CollisionBox box = boxes[i];
                Vector3 min = new Vector3((float)box.MinX, (float)box.MinY, (float)box.MinZ);
                Vector3 max = new Vector3((float)box.MaxX, (float)box.MaxY, (float)box.MaxZ);
                if (max.Y < min.Y)
                {
                    max.Y = min.Y;
                }

                if (max.X < min.X)
                {
                    max.X = min.X;
                }

                if (max.Z < min.Z)
                {
                    max.Z = min.Z;
                }

                result[i] = new BoundingBox(min, max);
            }

            return result;
        }

        /// <summary>
        /// Clamps a bounding box into the single-cell footprint so a model that pokes
        /// outside <c>[0, 1]</c> can never present a collider taller or wider than one
        /// block, which is what makes a short prop behave like a full solid cube.
        /// </summary>
        /// <param name="box">The raw local bounding box.</param>
        /// <returns>The clamped bounding box.</returns>
        public static BoundingBox ClampToCell(BoundingBox box)
        {
            Vector3 min = box.Min;
            Vector3 max = box.Max;
            min.X = ClampUnit(min.X);
            min.Y = ClampUnit(min.Y);
            min.Z = ClampUnit(min.Z);
            max.X = ClampUnit(max.X);
            max.Y = ClampUnit(max.Y);
            max.Z = ClampUnit(max.Z);
            if (max.X < min.X)
            {
                max.X = min.X;
            }

            if (max.Y < min.Y)
            {
                max.Y = min.Y;
            }

            if (max.Z < min.Z)
            {
                max.Z = min.Z;
            }

            return new BoundingBox(min, max);
        }

        private static float ClampUnit(float value)
        {
            if (float.IsNaN(value))
            {
                return 0f;
            }

            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }
    }
}
