using System;
using System.Collections.Generic;
using Engine;
using Game;

namespace SCIEPort.Features.Bamboo
{
    internal static class BambooBuildingGeometry
    {
        private static readonly Matrix CellCenter = Matrix.CreateTranslation(.5f, 0f, .5f);
        private static readonly Color Binding = new Color(132, 92, 53);

        internal static BlockMesh Fence(int connections, out BoundingBox[] boxes)
        {
            var mesh = new BlockMesh();
            var collision = new List<BoundingBox> { Bounds(-.14f, 0, -.14f, .14f, 1.5f, .14f, CellCenter) };
            Cane(mesh, -.092f, 0, -.092f, .092f, 1, .092f, CellCenter);
            foreach (float height in new[] { .31f, .73f })
                Box(mesh, new Vector3(-.104f, height, -.104f), new Vector3(.104f, height + .041f, .104f), CellCenter, BambooTextures.CaneSlot, Binding);

            for (int connection = 0; connection < 4; connection++)
            {
                if ((connections & (1 << connection)) == 0) continue;
                int quarterTurn = connection == 0 ? 0 : connection == 1 ? 2 : connection == 2 ? 3 : 1;
                Matrix transform = Matrix.CreateRotationY(quarterTurn * MathUtils.PI / 2f) * CellCenter;
                Cane(mesh, .088f, .28f, -.043f, .5f, .37f, .043f, transform, rail: true);
                Cane(mesh, .088f, .70f, -.043f, .5f, .79f, .043f, transform, rail: true);
                collision.Add(Bounds(.088f, 0f, -.07f, .5f, 1.5f, .07f, transform));
            }
            mesh.GenerateSidesData();
            boxes = collision.ToArray();
            return mesh;
        }

        internal static BlockMesh Gate(int data, float pivotDistance, out BoundingBox[] boxes)
        {
            bool rightHanded = FenceGateBlock.GetRightHanded(data);
            float side = rightHanded ? -1f : 1f;
            float pivot = side * (.5f - pivotDistance);
            Matrix transform = Matrix.CreateRotationY(FenceGateBlock.GetRotation(data) * MathUtils.PI / 2f) * CellCenter;
            if (FenceGateBlock.GetOpen(data))
            {
                transform = Matrix.CreateTranslation(-pivot, 0f, 0f)
                    * Matrix.CreateRotationY(side * MathUtils.PI / 2f)
                    * Matrix.CreateTranslation(pivot, 0f, 0f) * transform;
            }

            var mesh = new BlockMesh();
            for (int end = -1; end <= 1; end += 2)
            {
                float x = end * .42f;
                Cane(mesh, x - .049f, .095f, -.049f, x + .049f, .91f, .049f, transform);
            }
            Cane(mesh, -.49f, .28f, -.041f, .49f, .37f, .041f, transform, rail: true);
            Cane(mesh, -.49f, .70f, -.041f, .49f, .79f, .041f, transform, rail: true);
            for (int slat = -2; slat <= 2; slat++)
            {
                float x = slat * .145f;
                Cane(mesh, x - .026f, .20f, -.029f, x + .026f, .846f, .029f, transform);
            }
            Box(mesh, new Vector3(-side * .35f - .037f, .63f, -.078f),
                new Vector3(-side * .35f + .037f, .70f, .078f), transform, BambooTextures.CaneSlot, Binding);

            boxes = new[] { Bounds(-.49f, 0f, -.078f, .49f, 1f, .078f, transform) };
            return mesh;
        }

        internal static BlockMesh Screen(int rotation, out BoundingBox[] boxes)
        {
            Matrix transform = Matrix.CreateRotationY(rotation * MathUtils.PI / 2f) * CellCenter;
            var mesh = new BlockMesh();
            Box(mesh, new Vector3(-.455f, .045f, -.028f), new Vector3(.455f, .955f, .028f),
                transform, BambooTextures.WeaveSlot, Color.White);
            Cane(mesh, -.5f, 0, -.056f, -.432f, 1, .056f, transform);
            Cane(mesh, .432f, 0, -.056f, .5f, 1, .056f, transform);
            Cane(mesh, -.432f, 0, -.047f, .432f, .071f, .047f, transform, rail: true);
            Cane(mesh, -.432f, .929f, -.047f, .432f, 1, .047f, transform, rail: true);
            boxes = new[] { Bounds(-.5f, 0, -.056f, .5f, 1, .056f, transform) };
            mesh.GenerateSidesData();
            return mesh;
        }

        private static void Cane(BlockMesh mesh, float x0, float y0, float z0, float x1, float y1, float z1, Matrix transform, bool rail = false)
            => Box(mesh, new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), transform, BambooTextures.CaneSlot, Color.White, rail);

        private static BoundingBox Bounds(float x0, float y0, float z0, float x1, float y1, float z1, Matrix transform)
        {
            Vector3 a = Vector3.Transform(new Vector3(x0, y0, z0), transform);
            Vector3 b = Vector3.Transform(new Vector3(x1, y1, z1), transform);
            return new BoundingBox(new Vector3(MathUtils.Saturate(Math.Min(a.X, b.X)), Math.Min(a.Y, b.Y), MathUtils.Saturate(Math.Min(a.Z, b.Z))),
                new Vector3(MathUtils.Saturate(Math.Max(a.X, b.X)), Math.Max(a.Y, b.Y), MathUtils.Saturate(Math.Max(a.Z, b.Z))));
        }

        private static void Box(BlockMesh mesh, Vector3 low, Vector3 high, Matrix transform, int slot, Color color, bool rail = false)
        {
            var a = new Vector3(low.X, low.Y, low.Z);
            var b = new Vector3(high.X, low.Y, low.Z);
            var c = new Vector3(high.X, low.Y, high.Z);
            var d = new Vector3(low.X, low.Y, high.Z);
            var e = new Vector3(low.X, high.Y, low.Z);
            var f = new Vector3(high.X, high.Y, low.Z);
            var g = new Vector3(high.X, high.Y, high.Z);
            var h = new Vector3(low.X, high.Y, high.Z);
            Face(mesh, d, h, g, c, 0, transform, slot, color, rail);
            Face(mesh, c, g, f, b, 1, transform, slot, color, false);
            Face(mesh, b, f, e, a, 2, transform, slot, color, rail);
            Face(mesh, a, e, h, d, 3, transform, slot, color, false);
            Face(mesh, h, e, f, g, 4, transform, slot, color, rail);
            Face(mesh, a, d, c, b, 5, transform, slot, color, rail);
        }

        private static void Face(BlockMesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, int localFace,
            Matrix transform, int slot, Color color, bool rail)
        {
            Vector3 normal = Vector3.TransformNormal(CellFace.FaceToVector3(localFace), transform);
            int face = CellFace.Vector3ToFace(normal);
            float shade = face == 4 ? 1f : face == 5 ? .59f : face % 2 == 0 ? .91f : .78f;
            Color litColor = Color.MultiplyColorOnly(color, shade);
            ushort start = (ushort)mesh.Vertices.Count;
            AddVertex(mesh, a, 0, 1, face, transform, slot, litColor, rail);
            AddVertex(mesh, b, 0, 0, face, transform, slot, litColor, rail);
            AddVertex(mesh, c, 1, 0, face, transform, slot, litColor, rail);
            AddVertex(mesh, d, 1, 1, face, transform, slot, litColor, rail);
            mesh.Indices.Add(start);
            mesh.Indices.Add((ushort)(start + 1));
            mesh.Indices.Add((ushort)(start + 2));
            mesh.Indices.Add(start);
            mesh.Indices.Add((ushort)(start + 2));
            mesh.Indices.Add((ushort)(start + 3));
        }

        private static void AddVertex(BlockMesh mesh, Vector3 position, int u, int v, int face, Matrix transform, int slot, Color color, bool rail)
        {
            const float inset = .5f / 32f;
            const float span = 1f - 2f * inset;
            mesh.Vertices.Add(new BlockMeshVertex
            {
                Position = Vector3.Transform(position, transform),
                TextureCoordinates = new Vector2((slot % 16 + inset + span * (rail ? v : u)) / 16f,
                    (slot / 16 + inset + span * (rail ? u : v)) / 16f),
                Color = color,
                Face = (byte)face
            });
        }
    }
}
