using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Creatures
{
    /// <summary>Creative-only deployment item for the SCIE-Port rabbit validation.</summary>
    public sealed class RabbitSpawnBlock : Block
    {
        /// <summary>SCIE-Port reserved content ID for the manual rabbit spawner.</summary>
        public const int Index = 928;

        private readonly BlockMesh m_mesh = new BlockMesh();
        private static Texture2D s_texture;

        internal static Texture2D Texture => s_texture ?? (s_texture = ContentManager.Get<Texture2D>("Textures/Rabbit"));

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/Rabbit");
            foreach (ModelMesh mesh in model.Meshes)
            {
                Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone) * Matrix.CreateScale(0.7f);
                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    m_mesh.AppendModelMeshPart(part, transform, false, false, false, false, Color.White);
                }
            }

            _ = Texture;
            base.Initialize();
        }

        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
        }

        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, m_mesh, Texture, color, size, ref matrix, environmentData);
        }

        /// <inheritdoc />
        public override Vector3 GetIconViewOffset(int value, DrawBlockEnvironmentData environmentData)
        {
            return new Vector3(-0.55f, 0.2f, -0.9f);
        }
    }
}
