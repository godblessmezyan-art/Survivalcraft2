using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.OffroadVehicles
{
    /// <summary>Creative item that deploys the project-owned two-seat off-road car.</summary>
    public sealed class OffroadCarBlock : Block
    {
        /// <summary>SCIE-Port reserved content ID.</summary>
        public const int Index = 925;

        private readonly BlockMesh m_itemMesh = new BlockMesh();
        private static Texture2D s_texture;

        internal static Texture2D Texture => s_texture ?? (s_texture = ContentManager.Get<Texture2D>("Textures/OffroadCar"));

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/OffroadCar");
            foreach (ModelMesh mesh in model.Meshes)
            {
                Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone) * Matrix.CreateScale(0.32f);
                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    m_itemMesh.AppendModelMeshPart(part, transform, false, false, false, false, Color.White);
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
            BlocksManager.DrawMeshBlock(primitivesRenderer, m_itemMesh, Texture, color, size, ref matrix, environmentData);
        }

        /// <inheritdoc />
        public override Vector3 GetIconViewOffset(int value, DrawBlockEnvironmentData environmentData)
        {
            return new Vector3(-0.65f, 0.45f, -1.2f);
        }
    }
}
