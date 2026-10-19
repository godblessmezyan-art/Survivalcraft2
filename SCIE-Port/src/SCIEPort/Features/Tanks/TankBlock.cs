using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Tanks
{
    /// <summary>Creative deployment item for the project-created T-34 style tank.</summary>
    public sealed class TankBlock : Block
    {
        /// <summary>Reserved SCIE-Port content ID.</summary>
        public const int Index = 924;
        private readonly BlockMesh m_mesh = new BlockMesh();
        private static Texture2D s_texture;

        internal static Texture2D Texture => s_texture ?? (s_texture = ContentManager.Get<Texture2D>("Textures/T34Tank"));

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/T34Tank");
            foreach (string name in new[] { "TankHull", "TankTurret", "TankGun" })
            {
                ModelMesh mesh = model.FindMesh(name, true);
                Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone) * Matrix.CreateScale(0.3f);
                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    m_mesh.AppendModelMeshPart(part, transform, false, false, false, false, Color.White);
                    m_mesh.AppendModelMeshPart(part, transform, false, true, false, false, Color.White);
                }
            }
            _ = Texture;
            base.Initialize();
        }

        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, m_mesh, Texture, color, size, ref matrix, environmentData);
        }
    }
}
