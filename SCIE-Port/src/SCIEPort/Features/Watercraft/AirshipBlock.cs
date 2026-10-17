using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Watercraft
{
    /// <summary>Creative item that deploys the authorized SCIE airship preview.</summary>
    public sealed class AirshipBlock : Block
    {
        /// <summary>SCNET content ID reserved by this project.</summary>
        public const int Index = 998;

        private readonly BlockMesh m_standaloneBlockMesh = new BlockMesh();
        private static Texture2D s_whiteTexture;

        /// <summary>Returns the shared white texture expected by the original airship model.</summary>
        public static Texture2D GetWhiteTexture()
        {
            if (s_whiteTexture == null)
            {
                s_whiteTexture = new Texture2D(1, 1, 1, ColorFormat.Rgba8888);
                s_whiteTexture.SetData(0, new byte[] { 255, 255, 255, 255 });
            }

            return s_whiteTexture;
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/Airship");
            ModelMesh mesh = model.FindMesh("Airship");
            Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone)
                * Matrix.CreateTranslation(0f, -0.4f, 0f)
                * Matrix.CreateScale(0.08f);

            m_standaloneBlockMesh.AppendModelMeshPart(
                mesh.MeshParts[0], transform, false, false, false, false, Color.White);
            m_standaloneBlockMesh.AppendModelMeshPart(
                mesh.MeshParts[0], transform, false, true, false, false, Color.White);
            GetWhiteTexture();
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
            BlocksManager.DrawMeshBlock(
                primitivesRenderer,
                m_standaloneBlockMesh,
                GetWhiteTexture(),
                color,
                size,
                ref matrix,
                environmentData);
        }

        /// <inheritdoc />
        public override Vector3 GetIconViewOffset(int value, DrawBlockEnvironmentData environmentData)
        {
            return new Vector3(-0.6f, 0.6f, -0.8f);
        }

        /// <inheritdoc />
        public override Vector3 GetIconBlockOffset(int value, DrawBlockEnvironmentData environmentData)
        {
            return new Vector3(0f, -0.2f, 0f);
        }
    }
}
