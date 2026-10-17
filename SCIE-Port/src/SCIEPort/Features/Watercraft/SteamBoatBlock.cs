using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Watercraft
{
    /// <summary>
    /// Creative item that deploys the authorized SCIE steam-boat preview.
    /// </summary>
    public sealed class SteamBoatBlock : Block
    {
        /// <summary>SCNET content ID reserved by this project.</summary>
        public const int Index = 999;

        private readonly BlockMesh m_standaloneBlockMesh = new BlockMesh();

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/SteamBoat");
            ModelMesh mesh = model.FindMesh("Cylinder");
            Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone)
                * Matrix.CreateTranslation(0f, -0.4f, 0f);

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
            Texture2D texture = ContentManager.Get<Texture2D>("Textures/SteamBoat");
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                m_standaloneBlockMesh,
                texture,
                color,
                size,
                ref matrix,
                environmentData);
        }

        /// <inheritdoc />
        public override Vector3 GetIconViewOffset(int value, DrawBlockEnvironmentData environmentData)
        {
            return new Vector3(-0.7f, 0.8f, -1f);
        }

        /// <inheritdoc />
        public override float GetIconViewScale(int value, DrawBlockEnvironmentData environmentData)
        {
            return 0.45f;
        }
    }
}
