using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.PortableFortress
{
    /// <summary>
    /// Creative-only item that unfolds the Portable Fortress. Mirrors the project vehicle
    /// items: it renders a small device as its icon and never enters the terrain.
    /// </summary>
    public sealed class PortableFortressCoreItemBlock : Block
    {
        /// <summary>SCIE-Port reserved content id (944; 903-943 and 995-1000 are taken).</summary>
        public const int Index = 944;

        private readonly BlockMesh m_itemMesh = new BlockMesh();
        private static Texture2D s_texture;

        internal static Texture2D Texture => s_texture ?? (s_texture = ContentManager.Get<Texture2D>("Textures/FortressCore"));

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/FortressCore");
            foreach (ModelMesh mesh in model.Meshes)
            {
                Matrix transform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone) * Matrix.CreateScale(0.62f);
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
            return new Vector3(-0.55f, 0.35f, -1.0f);
        }
    }

    /// <summary>
    /// The deployed control core. It marks the fortress instance in the terrain, is walked
    /// through (no collision) and is the future recovery entry point.
    /// </summary>
    public sealed class PortableFortressCoreBlock : Block
    {
        /// <summary>SCIE-Port reserved content id.</summary>
        public const int Index = 945;

        private readonly BlockMesh[] m_worldMeshes = new BlockMesh[4];
        private BlockMesh m_iconMesh;
        private static Texture2D s_texture;

        private static Texture2D Texture => s_texture ?? (s_texture = ContentManager.Get<Texture2D>("Textures/FortressCore"));

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/FortressCore");
            for (int rotation = 0; rotation < 4; rotation++)
            {
                var target = new BlockMesh();
                Matrix transform = Matrix.CreateScale(0.70f)
                    * Matrix.CreateTranslation(0f, 0.05f, 0f)
                    * Matrix.CreateRotationY(rotation * MathUtils.PI / 2f)
                    * Matrix.CreateTranslation(0.5f, 0f, 0.5f);
                foreach (ModelMesh mesh in model.Meshes)
                {
                    Matrix bone = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone);
                    foreach (ModelMeshPart part in mesh.MeshParts)
                    {
                        target.AppendModelMeshPart(part, bone * transform, false, false, false, false, Color.White);
                    }
                }

                target.GenerateSidesData();
                m_worldMeshes[rotation] = target;
            }

            m_iconMesh = m_worldMeshes[0];
            _ = Texture;
            base.Initialize();
        }

        /// <summary>Horizontal rotation stored in the low two data bits.</summary>
        public static int GetRotation(int data)
        {
            return data & 3;
        }

        /// <summary>Writes the horizontal rotation.</summary>
        public static int SetRotation(int data, int rotation)
        {
            return (data & -4) | (rotation & 3);
        }

        /// <inheritdoc />
        public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
        {
            return true;
        }

        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
            int rotation = GetRotation(Terrain.ExtractData(value));
            generator.GenerateShadedMeshVertices(
                this,
                x,
                y,
                z,
                m_worldMeshes[rotation],
                Color.White,
                null,
                null,
                geometry.GetGeometry(Texture).SubsetAlphaTest);
        }

        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawMeshBlock(primitivesRenderer, m_iconMesh, Texture, color, size, ref matrix, environmentData);
        }

        /// <inheritdoc />
        public override IEnumerable<int> GetCreativeValues()
        {
            yield break;
        }
    }

    /// <summary>
    /// Hidden 0.36-high threshold under the rear door. It exists because only terrain cells
    /// feed <c>ComponentBody.HandleSmoothRise</c>: a body collider can never be stepped onto,
    /// so the entrance needs one real terrain column at the foundation height. It renders
    /// nothing, cannot be placed by hand, drops nothing and is removed by the recovery
    /// journal when that path is enabled.
    /// </summary>
    public sealed class PortableFortressThresholdBlock : Block
    {
        /// <summary>SCIE-Port reserved content id.</summary>
        public const int Index = 946;

        private static readonly BoundingBox[] s_collisionBoxes =
        {
            new BoundingBox(new Vector3(0f, 0f, 0f), new Vector3(1f, PortableFortressGeometry.FloorTop, 1f))
        };

        /// <inheritdoc />
        public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
        {
            return true;
        }

        /// <inheritdoc />
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
        {
            return s_collisionBoxes;
        }

        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
        }

        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
        }

        /// <inheritdoc />
        public override IEnumerable<int> GetCreativeValues()
        {
            yield break;
        }

        /// <inheritdoc />
        public override void GetDropValues(SubsystemTerrain subsystemTerrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> dropValues, out bool showDebris)
        {
            showDebris = false;
        }
    }
}
