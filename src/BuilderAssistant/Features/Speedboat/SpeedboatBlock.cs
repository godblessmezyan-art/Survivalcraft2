using Engine;
using Engine.Graphics;
using Game;

namespace BuilderAssistant.Features.Speedboat
{
    /// <summary>
    /// Inventory item used to deploy the Builder Assistant speedboat.
    /// Index 900 is outside the native x25.01.17 range (0-263).
    /// </summary>
    public sealed class SpeedboatBlock : Block
    {
        /// <summary>SCNET block content index reserved by this Mod.</summary>
        public const int Index = 900;

        private readonly BlockMesh m_standaloneBlockMesh = new BlockMesh();

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/Speedboat");
            ModelMesh mesh = model.FindMesh("Speedboat");
            Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone)
                * Matrix.CreateTranslation(0f, -0.25f, 0f);

            m_standaloneBlockMesh.AppendModelMeshPart(
                mesh.MeshParts[0], transform, false, false, false, false, Color.White);
            m_standaloneBlockMesh.AppendModelMeshPart(
                mesh.MeshParts[0], transform, false, true, false, false, Color.White);
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
            Texture2D texture = ContentManager.Get<Texture2D>("Textures/Speedboat");
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                m_standaloneBlockMesh,
                texture,
                color,
                size,
                ref matrix,
                environmentData);
        }
    }
}
