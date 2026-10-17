using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Rail
{
    /// <summary>SCIE rail with straight, corner and one-block slope variants.</summary>
    public sealed class RailBlock : Block
    {
        /// <summary>Block ID reserved for the SCIE rail.</summary>
        public const int Index = 997;

        private readonly BoundingBox[][] m_collisionBoxes = new BoundingBox[10][];
        private readonly BlockMesh[] m_meshes = new BlockMesh[10];

        /// <inheritdoc />
        public override void Initialize()
        {
            BlockMesh flat = CreateQuad(false);
            flat.TransformTextureCoordinates(Matrix.CreateTranslation(238 % 16 / 16f, 238 / 16 / 16f, 0f));
            Matrix center = Matrix.CreateTranslation(0.5f, 0f, 0.5f);
            Matrix reverseCenter = Matrix.CreateTranslation(-0.5f, 0f, -0.5f);
            for (int i = 0; i < 4; i++)
            {
                m_meshes[i] = new BlockMesh();
                m_meshes[i].AppendBlockMesh(flat);
                m_meshes[i].TransformPositions(reverseCenter * Matrix.CreateRotationY(MathUtils.PI * 0.5f * i) * center, -1);
                m_meshes[i].GenerateSidesData();
            }

            flat = CreateQuad(false);
            flat.TransformTextureCoordinates(Matrix.CreateTranslation(237 % 16 / 16f, 237 / 16 / 16f, 0f));
            m_meshes[4] = new BlockMesh();
            m_meshes[4].AppendBlockMesh(flat);
            m_meshes[4].GenerateSidesData();
            m_meshes[5] = new BlockMesh();
            m_meshes[5].AppendBlockMesh(flat);
            m_meshes[5].TransformPositions(reverseCenter * Matrix.CreateRotationY(MathUtils.PI * 0.5f) * center, -1);
            m_meshes[5].GenerateSidesData();

            BlockMesh slope = CreateQuad(true);
            slope.TransformTextureCoordinates(Matrix.CreateTranslation(237 % 16 / 16f, 237 / 16 / 16f, 0f));
            for (int i = 6; i < 10; i++)
            {
                m_meshes[i] = new BlockMesh();
                m_meshes[i].AppendBlockMesh(slope);
                m_meshes[i].TransformPositions(reverseCenter * Matrix.CreateRotationY(MathUtils.PI * 0.5f * i) * center, -1);
            }

            BoundingBox[] low = { new BoundingBox(0f, 0f, 0f, 1f, 0.01f, 1f) };
            for (int i = 0; i < 6; i++) m_collisionBoxes[i] = low;
            m_collisionBoxes[6] = StepsZ(false);
            m_collisionBoxes[8] = StepsZ(true);
            m_collisionBoxes[7] = StepsX(false);
            m_collisionBoxes[9] = StepsX(true);
            base.Initialize();
        }

        /// <inheritdoc />
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
        {
            return m_collisionBoxes[GetRailType(Terrain.ExtractData(value))];
        }

        /// <inheritdoc />
        public override void DrawBlock(PrimitivesRenderer3D primitivesRenderer, int value, Color color, float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawFlatBlock(primitivesRenderer, value, size, ref matrix, null, color, false, environmentData);
        }

        /// <inheritdoc />
        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
        {
            int type = MathUtils.Clamp(GetRailType(Terrain.ExtractData(value)), 0, 9);
            generator.GenerateMeshVertices(this, x, y, z, m_meshes[type], Color.White, null, geometry.SubsetAlphaTest);
        }

        /// <inheritdoc />
        public override BlockPlacementData GetPlacementValue(SubsystemTerrain subsystemTerrain, ComponentMiner componentMiner, int value, TerrainRaycastResult raycastResult)
        {
            Vector3 forward = Matrix.CreateFromQuaternion(componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            int type = MathUtils.Abs(forward.X) < MathUtils.Abs(forward.Z) ? 4 : 5;
            return new BlockPlacementData { CellFace = raycastResult.CellFace, Value = Terrain.MakeBlockValue(Index, 0, SetRailType(0, type)) };
        }

        /// <inheritdoc />
        public override void GetDropValues(SubsystemTerrain subsystemTerrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> dropValues, out bool showDebris)
        {
            showDebris = true;
            dropValues.Add(new BlockDropValue { Value = Index, Count = 1 });
        }

        /// <summary>Reads the shape from the low four data bits.</summary>
        public static int GetRailType(int data) => data & 15;
        /// <summary>Writes the shape to the low four data bits.</summary>
        public static int SetRailType(int data, int type) => (data & -16) | (type & 15);
        /// <summary>Returns whether a shape is one of the four corners.</summary>
        public static bool IsCorner(int type) => (type >> 2) == 0;
        /// <summary>Returns whether the shape accepts north/south travel.</summary>
        public static bool IsDirectionX(int type) => (type & 1) == 0;

        /// <summary>Checks whether a rail shape has an edge in the specified direction.</summary>
        public static bool CanConnectTo(int railType, int direction)
        {
            return IsCorner(railType)
                ? direction == railType || direction == ((railType + 1) & 3)
                : IsDirectionX(railType) ^ !IsDirectionX(direction);
        }

        private static BlockMesh CreateQuad(bool slope)
        {
            BlockMesh mesh = new BlockMesh();
            mesh.Vertices.AddRange(new[]
            {
                new BlockMeshVertex { Color = Color.White, TextureCoordinates = new Vector2(0f, .0625f), Position = new Vector3(0f, .01f, 1f) },
                new BlockMeshVertex { Color = Color.White, TextureCoordinates = new Vector2(.0625f, .0625f), Position = new Vector3(1f, .01f, 1f) },
                new BlockMeshVertex { Color = Color.White, TextureCoordinates = new Vector2(.0625f, 0f), Position = new Vector3(1f, slope ? 1.01f : .01f, 0f) },
                new BlockMeshVertex { Color = Color.White, TextureCoordinates = new Vector2(0f, 0f), Position = new Vector3(0f, slope ? 1.01f : .01f, 0f) }
            });
            mesh.Indices.AddRange(slope ? new ushort[] { 2, 1, 0, 0, 1, 2, 0, 3, 2, 2, 3, 0 } : new ushort[] { 2, 1, 0, 0, 3, 2 });
            return mesh;
        }

        private static BoundingBox[] StepsZ(bool reverse) => new[]
        {
            new BoundingBox(0f, 0f, 0f, 1f, .01f, 1f),
            new BoundingBox(0f, 0f, reverse ? 0f : .25f, 1f, .25f, reverse ? .75f : 1f),
            new BoundingBox(0f, 0f, reverse ? 0f : .5f, 1f, .5f, reverse ? .5f : 1f),
            new BoundingBox(0f, 0f, reverse ? 0f : .75f, 1f, .75f, reverse ? .25f : 1f)
        };

        private static BoundingBox[] StepsX(bool reverse) => new[]
        {
            new BoundingBox(0f, 0f, 0f, 1f, .01f, 1f),
            new BoundingBox(reverse ? 0f : .25f, 0f, 0f, reverse ? .75f : 1f, .25f, 1f),
            new BoundingBox(reverse ? 0f : .5f, 0f, 0f, reverse ? .5f : 1f, .5f, 1f),
            new BoundingBox(reverse ? 0f : .75f, 0f, 0f, reverse ? .25f : 1f, .75f, 1f)
        };
    }
}
