using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.DevouringEye
{
    /// <summary>Eye of Oblivion: a non-combat, non-placeable world recovery artifact.</summary>
    public sealed class DevouringEyeBlock : Block
    {
        /// <summary>Audited against native x25.01.17 and this branch (949 remains the All-Seeing Eye).</summary>
        public const int Index = 950;
        private readonly BlockMesh m_mesh = new BlockMesh();
        private Texture2D m_texture;
        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/DevouringEye");
            foreach (ModelMesh mesh in model.Meshes)
                foreach (ModelMeshPart part in mesh.MeshParts)
                    m_mesh.AppendModelMeshPart(part, BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone), false, false, false, false, Color.White);
            m_texture = ContentManager.Get<Texture2D>("Textures/DevouringEye");
            base.Initialize();
        }
        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z) { }
        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        { BlocksManager.DrawMeshBlock(renderer, m_mesh, m_texture, color, size, ref matrix, environmentData); }
    }
}
