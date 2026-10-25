using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.AirVehicles
{
    /// <summary>Creative deployment item for the project-created two-seat helicopter.</summary>
    public sealed class HelicopterBlock : Block
    {
        /// <summary>SCIE-Port reserved deployment item ID.</summary>
        public const int Index = 941;

        private readonly BlockMesh m_itemMesh = new BlockMesh();
        private static Texture2D s_texture;

        internal static Texture2D Texture => s_texture ?? (s_texture = ContentManager.Get<Texture2D>("Textures/Helicopter"));

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/Helicopter");
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
            return new Vector3(-0.7f, 0.55f, -1.25f);
        }
    }
}
