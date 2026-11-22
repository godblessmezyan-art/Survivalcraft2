using System.Collections.Generic;
using Engine;
using Game;

namespace SCIEPort.Features.MountainShelter
{
    internal static class ShelterMesh
    {
        private static readonly Color Canvas = new Color(53, 88, 122);
        private static readonly Color Seam = new Color(105, 146, 174);
        private static readonly Color Frame = new Color(30, 42, 59);
        private static readonly Color Lining = new Color(147, 176, 196);
        private static readonly Color Status = new Color(61, 198, 229);
        private static readonly Color Glazing = new Color(108, 178, 211, 38);
        private static readonly Color[] Bedding = { new Color(75, 116, 149), new Color(64, 99, 135) };
        private static readonly int[] Faces = { 0, 3, 2, 1, 5, 6, 7, 4, 1, 2, 6, 5, 4, 7, 3, 0, 3, 7, 6, 2, 4, 0, 1, 5 };
        private static readonly byte[] NativeFaces = { 2, 0, 1, 3, 4, 5 };
        // Native terrain uses clockwise front faces.
        private static readonly int[] Triangles = { 0, 2, 1, 0, 3, 2 };
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

        internal static BlockMesh Part(int part, int rotation, bool closed, bool lit, bool viewOpen, out BlockMesh glass)
        {
            var mesh = new BlockMesh();
            glass = new BlockMesh();
            int height = part / 4;
            bool front = (part & 2) == 0, left = (part & 1) == 0;
            foreach (var box in Shell(part, closed))
            {
                if (height == 2 && box.Min.Y == ShelterLayout.RoofBottom)
                {
                    if (ShelterLayout.SkylightPart(part)) Skylight(mesh, glass, left, viewOpen);
                    else Box(mesh, box, Canvas, slope: left ? 1 : -1);
                }
                else if (ShelterLayout.LampPart(part) && box.Max.Z - box.Min.Z == 1f)
                    SideWindow(mesh, glass, left, viewOpen);
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
                float edge = left ? .086f : .888f;
                Box(mesh, Bounds(edge, .179f, .12f, edge + .026f, .188f, .91f), Seam);
                for (int stitch = 0; stitch < 3; stitch++)
                {
                    float z = (front ? .55f : .23f) + stitch * .09f;
                    Box(mesh, Bounds(.22f, .182f, z, .78f, .189f, z + .009f), Seam);
                }
            }
            else if (height == 1)
            {
                float x = left ? 0 : .94f;
                Box(mesh, Bounds(x, 0, .08f, x + .06f, 1, .13f), Seam);
                if (front)
                    Box(mesh, Bounds(left ? .02f : .925f, .035f, .02f, left ? .075f : .98f, .95f, .075f), Frame);
                if (front)
                {
                    float inner = left ? .058f : .922f;
                    Box(mesh, Bounds(inner, .23f, .36f, inner + .020f, .64f, .82f), Frame);
                    for (int vent = 0; vent < 4; vent++)
                    {
                        float y = .34f + vent * .06f;
                        Box(mesh, Bounds(left ? .075f : .911f, y, .44f, left ? .089f : .925f, y + .018f, .73f), Seam);
                    }
                    if (closed)
                    {
                        Box(mesh, Bounds(.48f, 0, .01f, .52f, 1, .097f), Seam);
                        Box(mesh, Bounds(.50f, .40f, .002f, .61f, .47f, .125f), Frame);
                    }
                    else Box(mesh, Bounds(.08f, .94f, .015f, .92f, .99f, .11f), Seam);
                }
                else
                {
                    Console(mesh, lit);
                }
            }
            else
            {
                float x = left ? .95f : .005f;
                Box(mesh, Bounds(x, .433f, 0, x + .045f, .485f, 1), Seam);
            }
            Orient(mesh, height, left, front, rotation);
            Orient(glass, height, left, front, rotation);
            return mesh;
        }

        private static void Orient(BlockMesh mesh, int height, bool left, bool front, int rotation)
        {
            // Arbitrary thin meshes retain their outer faces next to opaque blocks.
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
        }

        private static void Console(BlockMesh mesh, bool lit)
        {
            Box(mesh, Bounds(.24f, .56f, .88f, .76f, .93f, .96f), Frame);
            Box(mesh, Bounds(.31f, .69f, .864f, .69f, .84f, .89f), lit ? new Color(240, 218, 177) : Seam, lit);
            Box(mesh, Bounds(.31f, .61f, .86f, .54f, .65f, .89f), Canvas);
            for (int i = 0; i < 3; i++)
            {
                float x = .58f + i * .042f;
                Box(mesh, Bounds(x, .61f, .856f, x + .024f, .643f, .883f), lit ? Status : Seam, lit);
            }
            Box(mesh, Bounds(.23f, .47f, .82f, .77f, .505f, .96f), Seam);
            Box(mesh, Bounds(.30f, .34f, .895f, .70f, .47f, .96f), Canvas);
        }

        private static void SideWindow(BlockMesh mesh, BlockMesh glass, bool left, bool open)
        {
            float x = left ? 0f : .94f;
            // Metal edge surrounds an actual opening in the opaque side skin.
            Box(mesh, Bounds(x, 0, 0, x + .06f, .29f, 1), Canvas);
            Box(mesh, Bounds(x, .94f, 0, x + .06f, 1, 1), Canvas);
            Box(mesh, Bounds(x, .29f, 0, x + .06f, .94f, .12f), Canvas);
            Box(mesh, Bounds(x, .29f, .88f, x + .06f, .94f, 1), Canvas);
            Box(mesh, Bounds(x, .27f, .095f, x + .06f, .304f, .905f), Seam);
            Box(mesh, Bounds(x, .92f, .095f, x + .06f, .954f, .905f), Seam);
            Box(mesh, Bounds(x, .30f, .103f, x + .06f, .925f, .133f), Frame);
            Box(mesh, Bounds(x, .30f, .867f, x + .06f, .925f, .897f), Frame);
            float track = left ? .046f : .924f;
            Box(mesh, Bounds(track, .901f, .121f, track + .03f, .925f, .881f), Lining);
            float fabric = left ? .042f : .926f;
            if (open)
            {
                Box(mesh, Bounds(fabric, .318f, .122f, fabric + .032f, .901f, .213f), Bedding[0]);
                Box(mesh, Bounds(fabric, .318f, .787f, fabric + .032f, .901f, .878f), Bedding[1]);
            }
            else Box(mesh, Bounds(fabric, .296f, .12f, fabric + .032f, .904f, .88f), Bedding[0]);
            int folds = open ? 2 : 7;
            for (int i = 0; i < folds; i++)
            {
                float z = open ? (i == 0 ? .169f : .829f) : .17f + i * .108f;
                Box(mesh, Bounds(fabric - .004f, .321f, z, fabric + .038f, .9f, z + .016f), Bedding[1]);
            }
            float pane = left ? .024f : .976f;
            Pane(glass, new Vector3(pane, .294f, .118f), new Vector3(pane, .934f, .118f),
                new Vector3(pane, .934f, .882f), new Vector3(pane, .294f, .882f), 1);
        }

        private static void Skylight(BlockMesh mesh, BlockMesh glass, bool left, bool open)
        {
            int slope = left ? 1 : -1;
            float a = left ? .19f : .10f, b = left ? .90f : .81f;
            const float z = .12f, back = .87f;
            const float bottom = ShelterLayout.RoofBottom;
            const float top = .20f;
            Box(mesh, Bounds(0, bottom, 0, a, top, 1), Canvas, slope: slope);
            Box(mesh, Bounds(b, bottom, 0, 1, top, 1), Canvas, slope: slope);
            Box(mesh, Bounds(a, bottom, 0, b, top, z), Seam, slope: slope);
            Box(mesh, Bounds(a, bottom, back, b, top, 1), Seam, slope: slope);
            if (open)
            {
                float low = RoofHeight(.192f, a, slope), high = RoofHeight(.192f, b, slope);
                Pane(glass, new Vector3(a, low, z), new Vector3(a, low, back),
                    new Vector3(b, high, back), new Vector3(b, high, z), 4);
            }
            else Box(mesh, Bounds(a, bottom, z, b, top, back), new Color(73, 120, 157), slope: slope);
            Box(mesh, Bounds(a + .015f, bottom - .019f, z - .017f, b - .015f, bottom + .010f, z + .019f), Frame);
            Box(mesh, Bounds(a + .025f, bottom - .022f, back - .035f, b - .025f, bottom + .010f, back + .018f), Frame);
        }

        private static void Pane(BlockMesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, byte face)
        {
            ushort start = (ushort)mesh.Vertices.Count;
            foreach (Vector3 p in new[] { a, b, c, d })
                mesh.Vertices.Add(new BlockMeshVertex { Position = p, Color = Glazing, Face = face, TextureCoordinates = new Vector2(.5f) });
            // Both sides must be visible through the native transparent pass.
            foreach (int i in new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 })
                mesh.Indices.Add((ushort)(start + i));
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

        private static float RoofHeight(float y, float x, int slope)
            => slope == 0 ? y : y + .035f + (slope > 0 ? x : 1f - x) * .245f;

        private static void Box(BlockMesh mesh, BoundingBox box, Color color, bool emissive = false, int slope = 0)
        {
            Vector3 a = box.Min, b = box.Max;
            float left = RoofHeight(b.Y, a.X, slope);
            float right = RoofHeight(b.Y, b.X, slope);
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
