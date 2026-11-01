using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Suspension
{
    /// <summary>An intangible, selectable storage cell. Its marker vanishes while an item is displayed.</summary>
    public sealed class SuspendedItemBlock : Block
    {
        /// <summary>New identity, audited independently from the artifact range.</summary>
        public const int Index = 954;
        private readonly BlockMesh marker = new BlockMesh();
        private Texture2D texture;
        private static readonly BoundingBox[] Empty = new BoundingBox[0];
        private static readonly BoundingBox[] Selection = { new BoundingBox(Vector3.Zero, Vector3.One) };

        /// <inheritdoc/>
        public override void Initialize()
        {
            BuildMarker(marker);
            texture = new Texture2D(1, 1, 1, ColorFormat.Rgba8888);
            texture.SetData(0, new byte[] { 255, 255, 255, 255 });
            base.Initialize();
        }

        internal static void BuildMarker(BlockMesh mesh)
        {
            var top = new Vector3(0, .19f, 0); var bottom = -top;
            for (int i = 0; i < 4; i++)
            {
                float a = i * MathUtils.PI / 2, b = (i + 1) * MathUtils.PI / 2;
                var p = new Vector3(MathUtils.Cos(a) * .11f, 0, MathUtils.Sin(a) * .11f);
                var q = new Vector3(MathUtils.Cos(b) * .11f, 0, MathUtils.Sin(b) * .11f);
                Triangle(mesh, top, p, q, new Color(186, 227, 255));
                Triangle(mesh, bottom, q, p, new Color(122, 173, 216));
            }
            for (int i = 0; i < 32; i++)
            {
                float a = i * MathUtils.PI / 16, b = (i + 1) * MathUtils.PI / 16;
                var p = new Vector3(MathUtils.Cos(a), MathUtils.Sin(a) * .18f, MathUtils.Sin(a));
                var q = new Vector3(MathUtils.Cos(b), MathUtils.Sin(b) * .18f, MathUtils.Sin(b));
                Triangle(mesh, p * .29f, q * .29f, q * .31f, new Color(227, 192, 112));
                Triangle(mesh, p * .29f, q * .31f, p * .31f, new Color(227, 192, 112));
            }
        }
        private static void Triangle(BlockMesh mesh, Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            ushort start = (ushort)mesh.Vertices.Count;
            foreach (var p in new[] { a, b, c }) mesh.Vertices.Add(new BlockMeshVertex { Position = p, TextureCoordinates = new Vector2(.5f), Color = color, IsEmissive = true });
            mesh.Indices.Add(start); mesh.Indices.Add((ushort)(start + 1)); mesh.Indices.Add((ushort)(start + 2));
        }
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => Empty;
        /// <inheritdoc/>
        public override BoundingBox[] GetCustomInteractionBoxes(SubsystemTerrain terrain, int value) => Selection;
        /// <inheritdoc/>
        public override bool IsFaceTransparent(SubsystemTerrain terrain, int face, int value) => true;
        /// <inheritdoc/>
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }
        /// <inheritdoc/>
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawMeshBlock(renderer, marker, texture, color, size, ref matrix, environment);
    }
}
