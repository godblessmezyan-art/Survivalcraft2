using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>
    /// Mjolnir, the true form of the Stormhammer. It deliberately reuses the base hammer's mesh and
    /// texture: the player asked for the original silhouette, so the awakened identity comes from
    /// the live corona, orbit glow and overloaded arcs (see StormhammerVisuals and
    /// AwakenedStormhammerVisuals), never from a different model.
    /// </summary>
    public sealed class StormhammerAwakenedBlock : Block
    {
        /// <summary>SCIE-Port content id immediately following the base form.</summary>
        public const int Index = 942;

        private readonly BlockMesh m_mesh = new BlockMesh();
        private static Texture2D s_texture;

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/Stormhammer");
            foreach (ModelMesh mesh in model.Meshes)
            {
                Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone);
                foreach (ModelMeshPart part in mesh.MeshParts)
                {
                    m_mesh.AppendModelMeshPart(part, transform, false, false, false, false, Color.White);
                }
            }

            s_texture = ContentManager.Get<Texture2D>("Textures/Stormhammer");
            _ = ContentManager.Get<Model>("Models/ThunderSpear");
            _ = ContentManager.Get<Texture2D>("Textures/StormhammerAwakenedGlow");
            _ = ContentManager.Get<Texture2D>("Textures/StormhammerAwakenedEffects");
            _ = ContentManager.Get<Texture2D>("Textures/StormhammerAwakenedSkills");
            base.Initialize();
        }

        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
        }

        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            StormhammerVisuals.TouchModelGlow(environmentData, matrix, size, true);
            BlocksManager.DrawMeshBlock(primitivesRenderer, m_mesh, s_texture, color, size * 0.265f, ref matrix, environmentData);
        }

        /// <inheritdoc />
        public override Vector3 GetIconViewOffset(int value, DrawBlockEnvironmentData environmentData) => new Vector3(-0.66f, 0.40f, -1.12f);

        /// <inheritdoc />
        public override Vector3 GetIconBlockOffset(int value, DrawBlockEnvironmentData environmentData) => new Vector3(0f, -0.16f, 0f);
    }
}
