using System.Collections.Generic;
using Engine;
using Game;

namespace SCIEPort.Features.MountainShelter
{
    internal static class ShelterMesh
    {
        private static readonly Color Canvas = new Color(105, 119, 71);
        private static readonly Color Seam = new Color(163, 150, 98);
        private static readonly Color Frame = new Color(62, 52, 43);
        private static readonly Color Lining = new Color(179, 152, 105);
        private static readonly Color[] Bedding = { new Color(123, 137, 87), new Color(83, 103, 59) };
        private static readonly int[] Faces = { 0, 3, 2, 1, 5, 6, 7, 4, 1, 2, 6, 5, 4, 7, 3, 0, 3, 7, 6, 2, 4, 0, 1, 5 };
        private static readonly byte[] NativeFaces = { 2, 0, 1, 3, 4, 5 };
        private static readonly int[] Triangles = { 0, 1, 2, 0, 2, 3 };
        private const float FabricInset = .008f;

        private static BoundingBox Bounds(float x, float y, float z, float endX, float endY, float endZ)
            => new BoundingBox(new Vector3(x, y, z), new Vector3(endX, endY, endZ));

        private static IEnumerable<BoundingBox> Shell(int part, bool closed)
        {
            if (part >= ShelterLayout.StructureCount)
            {
                yield return Bounds(.34f, 0, .34f, .66f, 1, .66f);
                yield break;
            }
            int height = part / 4;
            bool front = (part & 2) == 0;
            bool left = (part & 1) == 0;
            float sideX = left ? 0f : .94f;
            if (height == 2)
            {
                yield return Bounds(0, ShelterLayout.RoofBottom, 0, 1, .20f, 1);
                yield return Bounds(sideX, 0, 0, left ? .06f : 1f, ShelterLayout.RoofBottom, 1);
                if (front) yield return Bounds(0, 0, 0, 1, ShelterLayout.RoofBottom, .09f);
                else yield return Bounds(0, 0, .94f, 1, ShelterLayout.RoofBottom, 1);
                yield break;
            }
            if (height == 0) yield return Bounds(0, 0, 0, 1, ShelterLayout.FloorHeight, 1);
            yield return Bounds(sideX, 0, 0, left ? .06f : 1f, 1, 1);
            if (!front) yield return Bounds(0, 0, .94f, 1, 1, 1);
            if (front && closed) yield return Bounds(left ? .06f : 0f, 0, .015f, left ? 1f : .94f, 1, .09f);
        }

        internal static BoundingBox[] Boxes(int part, int rotation, bool closed)
        {
            var boxes = new List<BoundingBox>();
            foreach (var box in Shell(part, closed))
            {
                Vector3 a = ShelterLayout.RotateCell(box.Min, rotation);
                Vector3 b = ShelterLayout.RotateCell(box.Max, rotation);
                boxes.Add(new BoundingBox(Vector3.Min(a,b), Vector3.Max(a,b)));
            }
            // Keep a touchable handle when the canvas is rolled up.
            if (part == 4 || part == 5)
            {
                bool left = part == 4;
                Vector3 a = ShelterLayout.RotateCell(new Vector3(left ? .06f : .83f, .91f, .02f), rotation);
                Vector3 b = ShelterLayout.RotateCell(new Vector3(left ? .17f : .94f, 1, .12f), rotation);
                boxes.Add(new BoundingBox(Vector3.Min(a,b), Vector3.Max(a,b)));
            }
            return boxes.ToArray();
        }

        internal static BlockMesh Part(int part, int rotation, bool closed, bool lit)
        {
            var mesh = new BlockMesh();
            int height = part / 4;
            bool front = (part & 2) == 0, left = (part & 1) == 0;
            foreach (var box in Shell(part, closed))
            {
                if (height == 2 && box.Min.Y == ShelterLayout.RoofBottom)
                    Box(mesh, box, Canvas, slope: left ? 1 : -1);
                else
                    Box(mesh, box, part >= ShelterLayout.StructureCount ? Frame : Canvas);
            }
            if (part >= ShelterLayout.StructureCount)
            {
                Box(mesh, Bounds(.27f, .01f, .27f, .73f, .09f, .73f), Frame);
                Box(mesh, Bounds(.31f, .71f, .31f, .69f, .78f, .69f), Seam);
            }
            else if (height == 0)
            {
                Box(mesh, Bounds(.15f, .173f, front ? .33f : .01f, .85f, .182f, front ? .98f : .78f), Bedding[part & 1]);
                Box(mesh, Bounds(.19f, .183f, front ? .38f : .03f, .81f, .188f, front ? .42f : .08f), Lining);
                if (!front) Box(mesh, Bounds(.24f, .179f, .72f, .76f, .226f, .87f), Lining);
                Box(mesh, Bounds(0, .03f, 0, 1, .075f, .037f), Seam);
            }
            else if (height == 1)
            {
                float x = left ? 0 : .94f;
                Box(mesh, Bounds(x, 0, .08f, x + .06f, 1, .13f), Seam);
                if (front)
                    Box(mesh, Bounds(left ? .02f : .925f, .035f, .02f, left ? .075f : .98f, .95f, .075f), Frame);
                if (front)
                {
                    if (closed)
                    {
                        Box(mesh, Bounds(.48f, 0, .01f, .52f, 1, .097f), Seam);
                        Box(mesh, Bounds(.50f, .40f, .002f, .61f, .47f, .125f), Frame);
                    }
                    else Box(mesh, Bounds(.08f, .94f, .015f, .92f, .99f, .11f), Seam);
                }
                else
                {
                    Box(mesh, Bounds(.37f, .61f, .88f, .63f, .89f, .95f), Frame);
                    Box(mesh, Bounds(.40f, .65f, .86f, .60f, .85f, .89f), lit ? new Color(255, 215, 144) : Seam, lit);
                    Box(mesh, Bounds(.47f, .58f, .84f, .53f, .68f, .94f), Lining);
                }
            }
            else
            {
                float x = left ? .95f : .005f;
                Box(mesh, Bounds(x, .433f, 0, x + .045f, .485f, 1), Seam);
                if (!front) Box(mesh, Bounds(.41f, .01f, .44f, .59f, .14f, .68f), Frame);
            }
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                var vertex = mesh.Vertices.Array[i];
                // Keep the outside skin away from coplanar rock faces, while joining adjacent shelter parts.
                if (left && vertex.Position.X == 0f) vertex.Position.X = FabricInset;
                if (!left && vertex.Position.X == 1f) vertex.Position.X = 1f - FabricInset;
                if (!front && vertex.Position.Z == 1f) vertex.Position.Z = 1f - FabricInset;
                if (height == 0 && vertex.Position.Y == 0f) vertex.Position.Y = FabricInset;
                vertex.Position = ShelterLayout.RotateCell(vertex.Position, rotation);
                if (vertex.Face < 4) vertex.Face = (byte)((vertex.Face - rotation) & 3);
                mesh.Vertices.Array[i] = vertex;
            }
            // Arbitrary thin meshes must retain their outer faces next to opaque blocks.
            return mesh;
        }

        internal static BlockMesh Folded()
        {
            var mesh = new BlockMesh();
            Box(mesh, Bounds(-.32f, -.13f, -.21f, .32f, .12f, .21f), Frame);
            Box(mesh, Bounds(-.30f, -.095f, -.225f, .30f, .165f, .225f), Canvas);
            Box(mesh, Bounds(-.27f, .165f, -.195f, .27f, .187f, .195f), Canvas);
            foreach (float x in new[] { -.245f, .195f })
            {
                Box(mesh, Bounds(x, -.14f, -.236f, x + .05f, .197f, .236f), Seam);
                Box(mesh, Bounds(x - .018f, -.015f, -.27f, x + .07f, .06f, -.23f), Frame);
            }
            Box(mesh, Bounds(-.105f, .183f, -.025f, .105f, .25f, .025f), Frame);
            return mesh;
        }

        private static void Box(BlockMesh mesh, BoundingBox box, Color color, bool emissive = false, int slope = 0)
        {
            Vector3 a = box.Min, b = box.Max;
            float left = b.Y + (slope > 0 ? .035f : slope < 0 ? .28f : 0);
            float right = b.Y + (slope < 0 ? .035f : slope > 0 ? .28f : 0);
            Vector3[] corners =
            {
                new Vector3(a.X,a.Y,a.Z), new Vector3(b.X,a.Y,a.Z), new Vector3(b.X,right,a.Z), new Vector3(a.X,left,a.Z),
                new Vector3(a.X,a.Y,b.Z), new Vector3(b.X,a.Y,b.Z), new Vector3(b.X,right,b.Z), new Vector3(a.X,left,b.Z)
            };
            for (int face = 0; face < 6; face++)
            {
                ushort start = (ushort)mesh.Vertices.Count;
                Color shade = Color.MultiplyColorOnly(color, emissive || face == 4 ? 1f : face == 5 ? .62f : .84f);
                for (int i = 0; i < 4; i++)
                    mesh.Vertices.Add(new BlockMeshVertex { Position = corners[Faces[face * 4 + i]], Color = shade,
                        TextureCoordinates = new Vector2(.5f), Face = NativeFaces[face], IsEmissive = emissive });
                foreach (int index in Triangles) mesh.Indices.Add((ushort)(start + index));
            }
        }
    }
}
