using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Poseidon
{
    /// <summary>Original ocean-regalia trident. The opaque model needs no special shader.</summary>
    public sealed class PoseidonBlock : Block
    {
        /// <summary>Audited free in native and SCIE-Port; 950 remains reserved for Devouring Eye.</summary>
        public const int Index = 951;
        private BlockMesh m_mesh;
        private Texture2D m_texture;
        /// <inheritdoc />
        public override void Initialize()
        {
            m_mesh = new BlockMesh();
            foreach (var mesh in ContentManager.Get<Model>("Models/PoseidonTrident").Meshes)
                foreach (var part in mesh.MeshParts)
                    m_mesh.AppendModelMeshPart(part, BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone), false, false, false, false, Color.White);
            m_texture = ContentManager.Get<Texture2D>("Textures/PoseidonTrident");
            base.Initialize();
        }
        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator g, TerrainGeometry t, int v, int x, int y, int z) { }
        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D r, int v, Color c, float size, ref Matrix matrix, DrawBlockEnvironmentData env)
            => BlocksManager.DrawMeshBlock(r, m_mesh, m_texture, c, size, ref matrix, env);
        /// <inheritdoc />
        public override Vector3 GetIconViewOffset(int value, DrawBlockEnvironmentData env) => new Vector3(-0.4f, 0.08f, -1f);
    }
}
