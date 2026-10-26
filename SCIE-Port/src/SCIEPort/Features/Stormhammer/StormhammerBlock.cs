using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>Legendary Stormhammer item and its shared model renderer.</summary>
    public sealed class StormhammerBlock : Block
    {
        /// <summary>SCIE-Port content id reserved after the apple slice.</summary>
        public const int Index = 941;

        private readonly BlockMesh m_mesh = new BlockMesh();
        private static Texture2D s_texture;

        internal static Texture2D Texture
        {
            get
            {
                if (s_texture == null)
                {
                    s_texture = ContentManager.Get<Texture2D>("Textures/Stormhammer");
                }

                return s_texture;
            }
        }

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

            _ = Texture;
            // Load both authored auxiliary maps through the real mod content path.
            // DrawMeshBlock has one diffuse sampler, so runtime emission is supplied
            // by GlowPoint and additive particles rather than a fictitious material slot.
            _ = ContentManager.Get<Texture2D>("Textures/StormhammerGlow");
            _ = ContentManager.Get<Texture2D>("Textures/StormhammerEffects");
            base.Initialize();
        }

        /// <inheritdoc />
        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            // The weapon is not placeable. World drops are drawn by SubsystemPickables.
        }

        /// <inheritdoc />
        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer,
            int value,
            Color color,
            float size,
            ref Matrix matrix,
            DrawBlockEnvironmentData environmentData)
        {
            StormhammerVisuals.TouchModelGlow(environmentData, matrix, size);
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                m_mesh,
                Texture,
                color,
                size * 0.265f,
                ref matrix,
                environmentData);
        }

        /// <inheritdoc />
        public override Vector3 GetIconViewOffset(int value, DrawBlockEnvironmentData environmentData)
        {
            return new Vector3(-0.62f, 0.34f, -1.05f);
        }

        /// <inheritdoc />
        public override Vector3 GetIconBlockOffset(int value, DrawBlockEnvironmentData environmentData)
        {
            return new Vector3(0f, -0.12f, 0f);
        }
    }
}
