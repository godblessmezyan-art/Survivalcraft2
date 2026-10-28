using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.OmniscientEye
{
    /// <summary>Original read-only astrolabe artifact.</summary>
    public sealed class OmniscientEyeBlock : Block
    {
        /// <summary>Audited against x25.01.17 and all blocks on this branch.</summary>
        public const int Index = 949;
        private readonly BlockMesh m_mesh = new BlockMesh();
        private Texture2D m_texture;

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/OmniscientEye");
            foreach (ModelMesh mesh in model.Meshes)
                foreach (ModelMeshPart part in mesh.MeshParts)
                    m_mesh.AppendModelMeshPart(part, BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone), false, false, false, false, Color.White);
            m_texture = ContentManager.Get<Texture2D>("Textures/OmniscientEye");
            base.Initialize();
        }

        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }

        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawMeshBlock(renderer, m_mesh, m_texture, color, size, ref matrix, environmentData);
        }
    }
}
