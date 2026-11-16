using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Home
{
    /// <summary>Texture-free book furniture built at runtime; faces and winding mirror the native cube generator.</summary>
    internal static class BookshelfGeometry
    {
        private static readonly Color[] Covers =
        {
            new Color(142, 46, 50), new Color(48, 66, 112), new Color(56, 96, 72), new Color(176, 128, 54),
            new Color(100, 58, 92), new Color(54, 102, 104), new Color(156, 84, 48), new Color(60, 60, 68),
            new Color(148, 58, 76), new Color(72, 88, 54)
        };
        private static readonly Color Pages = new Color(226, 214, 184);
        private static readonly int[] PatternFront = { 0, 2, 1, 2, 0, 3 };
        private static readonly int[] PatternBack = { 0, 1, 2, 2, 3, 0 };
        private static readonly BoundingBox[] CaseCollision =
        {
            new BoundingBox(new Vector3(.03f, 0f, .03f), new Vector3(.97f, 1.90f, .97f))
        };

        internal static BlockMesh Build(int id, int data, int rotation)
        {
            var mesh = new BlockMesh();
            int variant = HomeRules.Variant(HomeRules.Normalize(id, data));
            Matrix transform = Matrix.CreateRotationY((rotation & 3) * MathUtils.PI / 2f)
                * (id == HomeRules.Bookcase ? Matrix.CreateTranslation(CaseCenter(rotation & 3)) : Matrix.CreateTranslation(.5f, 0f, .5f));
            if (id == HomeRules.Bookshelf) Shelf(mesh, transform, rotation & 3, variant);
            else if (id == HomeRules.BookshelfTall) TallShelf(mesh, transform, rotation & 3, variant);
            else if (id == HomeRules.Bookcase) Case(mesh, transform, rotation & 3);
            else if (id == HomeRules.BookPile) Pile(mesh, transform, rotation & 3, variant);
            return mesh;
        }

        private static void Shelf(BlockMesh mesh, Matrix t, int rotation, int variant)
        {
            bool dark = variant == 1;
            Color frame = dark ? new Color(96, 68, 48) : new Color(168, 126, 82);
            Color board = dark ? new Color(76, 52, 36) : new Color(146, 108, 72);
            Color back = dark ? new Color(54, 38, 28) : new Color(108, 78, 52);
            Engine.Random random = new Engine.Random(973 * 131 + variant * 17);
            float fill = variant == 2 ? .95f : .64f;
            Box(mesh, t, rotation, -.46f, 0, -.22f, -.40f, 1, .46f, frame);
            Box(mesh, t, rotation, .40f, 0, -.22f, .46f, 1, .46f, frame);
            Box(mesh, t, rotation, -.46f, 0, -.22f, .46f, .07f, .46f, frame);
            Box(mesh, t, rotation, -.46f, .93f, -.22f, .46f, 1, .46f, frame);
            Box(mesh, t, rotation, -.40f, .07f, .39f, .40f, .93f, .46f, back);
            Box(mesh, t, rotation, -.40f, .36f, -.16f, .40f, .40f, .42f, board);
            Box(mesh, t, rotation, -.40f, .64f, -.16f, .40f, .68f, .42f, board);
            Fill(mesh, t, rotation, random, -.38f, .38f, .07f, .36f, 0f, .28f, fill);
            Fill(mesh, t, rotation, random, -.38f, .38f, .40f, .64f, 0f, .28f, fill);
            Fill(mesh, t, rotation, random, -.38f, .38f, .68f, .93f, 0f, .28f, fill);
        }

        private static void TallShelf(BlockMesh mesh, Matrix t, int rotation, int variant)
        {
            bool dark = variant == 1;
            Color frame = dark ? new Color(96, 68, 48) : new Color(168, 126, 82);
            Color board = dark ? new Color(76, 52, 36) : new Color(146, 108, 72);
            Color back = dark ? new Color(54, 38, 28) : new Color(108, 78, 52);
            Engine.Random random = new Engine.Random(974 * 149 + variant * 19);
            float fill = .82f;
            Box(mesh, t, rotation, -.46f, 0, -.22f, -.40f, 1.90f, .46f, frame);
            Box(mesh, t, rotation, .40f, 0, -.22f, .46f, 1.90f, .46f, frame);
            Box(mesh, t, rotation, -.46f, 0, -.22f, .46f, .07f, .46f, frame);
            Box(mesh, t, rotation, -.46f, 1.83f, -.22f, .46f, 1.90f, .46f, frame);
            Box(mesh, t, rotation, -.40f, .07f, .39f, .40f, 1.83f, .46f, back);
            foreach (float y in new[] { .40f, .76f, 1.12f, 1.48f })
                Box(mesh, t, rotation, -.40f, y, -.16f, .40f, y + .04f, .42f, board);
            Fill(mesh, t, rotation, random, -.38f, .38f, .07f, .40f, 0f, .28f, fill);
            Fill(mesh, t, rotation, random, -.38f, .38f, .44f, .76f, 0f, .28f, fill);
            Fill(mesh, t, rotation, random, -.38f, .38f, .80f, 1.12f, 0f, .28f, fill);
            Fill(mesh, t, rotation, random, -.38f, .38f, 1.16f, 1.48f, 0f, .28f, fill);
            Fill(mesh, t, rotation, random, -.38f, .38f, 1.52f, 1.83f, 0f, .28f, fill);
        }

        private static void Case(BlockMesh mesh, Matrix t, int rotation)
        {
            Color frame = new Color(150, 112, 74);
            Color board = new Color(132, 96, 64);
            Color back = new Color(102, 74, 50);
            Engine.Random random = new Engine.Random(975 * 163);
            // Centred two-by-two island: shelves face out on both long sides so every
            // quarter turn stays inside the four reserved cells.
            Box(mesh, t, rotation, -.96f, 0, -.98f, -.90f, 1.90f, .98f, frame);
            Box(mesh, t, rotation, .90f, 0, -.98f, .96f, 1.90f, .98f, frame);
            Box(mesh, t, rotation, -.96f, 0, -.98f, .96f, .10f, .98f, frame);
            Box(mesh, t, rotation, -.96f, 1.84f, -.98f, .96f, 1.90f, .98f, frame);
            Box(mesh, t, rotation, -.90f, .10f, -.06f, .90f, 1.84f, .06f, back);
            foreach (float y in new[] { .48f, .92f, 1.36f })
            {
                Box(mesh, t, rotation, -.90f, y, -.96f, .90f, y + .05f, -.06f, board);
                Box(mesh, t, rotation, -.90f, y, .06f, .90f, y + .05f, .96f, board);
            }
            Box(mesh, t, rotation, -.03f, .10f, -.96f, .03f, 1.84f, -.06f, frame);
            Box(mesh, t, rotation, -.03f, .10f, .06f, .03f, 1.84f, .96f, frame);
            foreach (float bottom in new[] { .10f, .53f, .97f, 1.41f })
            {
                float top = bottom == .10f ? .48f : bottom == .53f ? .92f : bottom == .97f ? 1.36f : 1.84f;
                Fill(mesh, t, rotation, random, -.88f, -.05f, bottom, top, -.92f, .30f, .76f, false, 12);
                Fill(mesh, t, rotation, random, .05f, .88f, bottom, top, -.92f, .30f, .76f, false, 12);
                Fill(mesh, t, rotation, random, -.88f, -.05f, bottom, top, .92f, .30f, .76f, true, 12);
                Fill(mesh, t, rotation, random, .05f, .88f, bottom, top, .92f, .30f, .76f, true, 12);
            }
        }

        private static Vector3 CaseCenter(int rotation)
            => rotation == 1 ? new Vector3(1f, 0f, 0f) : rotation == 2 ? Vector3.Zero : rotation == 3 ? new Vector3(0f, 0f, 1f) : new Vector3(1f, 0f, 1f);

        internal static BoundingBox[] CaseBoxes() => CaseCollision;

        private static void Pile(BlockMesh mesh, Matrix t, int rotation, int variant)
        {
            Engine.Random random = new Engine.Random(976 * 179 + variant * 23);
            if (variant == 3)
            {
                Color cover = new Color(126, 42, 48);
                Box(mesh, t, rotation, -.38f, 0, -.26f, -.012f, .022f, .26f, cover);
                Box(mesh, t, rotation, .012f, 0, -.26f, .38f, .022f, .26f, cover);
                Box(mesh, t, rotation, -.36f, .022f, -.24f, -.02f, .052f, .24f, Pages);
                Box(mesh, t, rotation, .02f, .022f, -.24f, .36f, .052f, .24f, Pages);
                Box(mesh, t, rotation, -.016f, 0, -.26f, .016f, .030f, .26f, Color.MultiplyColorOnly(cover, .7f));
                Box(mesh, t, rotation, .07f, .052f, -.06f, .11f, .058f, .06f, new Color(206, 170, 72));
                return;
            }
            if (variant == 1)
            {
                for (int i = 0; i < 3; i++)
                {
                    float x = new[] { -.14f, .12f, -.02f }[i], z = new[] { -.06f, .10f, .24f }[i];
                    Color cover = Covers[(int)random.Float(0f, Covers.Length - .001f)];
                    FlatBook(mesh, t, rotation, x, z, 0f, random.Float(.24f, .32f), random.Float(.20f, .26f), random.Float(.040f, .055f), cover);
                }
                return;
            }
            int count = variant == 2 ? 7 : 5;
            float bottom = 0f;
            for (int i = 0; i < count; i++)
            {
                float width = random.Float(.24f, .36f), depth = random.Float(.20f, .27f), thickness = random.Float(.038f, .052f);
                float x = random.Float(-.05f, .05f), z = random.Float(-.06f, .06f);
                Color cover = Covers[(int)random.Float(0f, Covers.Length - .001f)];
                FlatBook(mesh, t, rotation, x, z, bottom, width, depth, thickness, cover);
                bottom += thickness + .002f;
            }
        }

        private static void Fill(BlockMesh mesh, Matrix t, int rotation, Engine.Random random, float left, float right, float bottom, float top, float zFront, float depth, float fill, bool back = false, int max = 24)
        {
            float cursor = left, limit = left + (right - left) * fill;
            for (int count = 0; count < max && cursor < limit - .025f; count++)
            {
                float width = random.Float(.034f, .064f);
                if (cursor + width > right - .005f) break;
                float height = random.Float((top - bottom) * .52f, (top - bottom) * .93f);
                float d = random.Float(depth * .72f, depth);
                Color cover = Covers[(int)random.Float(0f, Covers.Length - .001f)];
                Book(mesh, t, rotation, cursor + width * .5f, bottom, zFront, width, height, d, cover, back);
                cursor += width + random.Float(.001f, .012f);
            }
        }

        private static void Book(BlockMesh mesh, Matrix t, int rotation, float centerX, float bottom, float zFront, float width, float height, float depth, Color cover, bool back = false)
        {
            float half = width * .5f;
            if (!back)
            {
                Box(mesh, t, rotation, centerX - half, bottom, zFront, centerX + half, bottom + height, zFront + .018f, cover);
                Box(mesh, t, rotation, centerX - half + .004f, bottom + .008f, zFront + .016f, centerX + half - .004f, bottom + height - .008f, zFront + depth, Pages);
            }
            else
            {
                Box(mesh, t, rotation, centerX - half, bottom, zFront - .018f, centerX + half, bottom + height, zFront, cover);
                Box(mesh, t, rotation, centerX - half + .004f, bottom + .008f, zFront - depth, centerX + half - .004f, bottom + height - .008f, zFront - .016f, Pages);
            }
        }

        private static void FlatBook(BlockMesh mesh, Matrix t, int rotation, float centerX, float centerZ, float bottom, float width, float depth, float thickness, Color cover)
        {
            float hx = width * .5f, hz = depth * .5f, top = bottom + thickness;
            Box(mesh, t, rotation, centerX - hx, bottom, centerZ - hz, centerX + hx, bottom + .008f, centerZ + hz, cover);
            Box(mesh, t, rotation, centerX - hx + .004f, bottom + .008f, centerZ - hz + .004f, centerX + hx - .004f, top - .008f, centerZ + hz - .004f, Pages);
            Box(mesh, t, rotation, centerX - hx, top - .008f, centerZ - hz, centerX + hx, top, centerZ + hz, Color.MultiplyColorOnly(cover, .9f));
        }

        private static void Box(BlockMesh mesh, Matrix transform, int rotation, float x0, float y0, float z0, float x1, float y1, float z1, Color color)
        {
            Face(mesh, transform, rotation, 0,
                new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), color, .92f);
            Face(mesh, transform, rotation, 1,
                new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1), color, .84f);
            Face(mesh, transform, rotation, 2,
                new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), color, 1f);
            Face(mesh, transform, rotation, 3,
                new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), color, .84f);
            Face(mesh, transform, rotation, 4,
                new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), color, 1f);
            Face(mesh, transform, rotation, 5,
                new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), color, .55f);
        }

        private static void Face(BlockMesh mesh, Matrix transform, int rotation, int face, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float shade)
        {
            int world = face < 4 ? (face + rotation) & 3 : face;
            Color lit = Color.MultiplyColorOnly(color, shade);
            ushort start = (ushort)mesh.Vertices.Count;
            Add(mesh, transform, world, a, lit);
            Add(mesh, transform, world, b, lit);
            Add(mesh, transform, world, c, lit);
            Add(mesh, transform, world, d, lit);
            int[] pattern = face == 0 || face == 1 || face == 5 ? PatternFront : PatternBack;
            for (int i = 0; i < pattern.Length; i++) mesh.Indices.Add((ushort)(start + pattern[i]));
        }

        private static void Add(BlockMesh mesh, Matrix transform, int face, Vector3 position, Color color)
            => mesh.Vertices.Add(new BlockMeshVertex
            {
                Position = Vector3.Transform(position, transform),
                TextureCoordinates = new Vector2(.5f),
                Color = color,
                Face = (byte)face
            });
    }
}
