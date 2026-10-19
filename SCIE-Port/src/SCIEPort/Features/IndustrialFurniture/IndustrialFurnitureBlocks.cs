using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.IndustrialFurniture
{
    /// <summary>
    /// Static, placeable SCIE industrial models. These blocks intentionally do not
    /// implement the upstream machine, crafting, inventory or circuit behavior.
    /// </summary>
    public abstract class IndustrialFurnitureBlock : Block
    {
        private readonly BlockMesh[] m_worldMeshes = new BlockMesh[4];
        private readonly BoundingBox[][] m_collisionBoxes = new BoundingBox[4][];
        private BlockMesh m_iconMesh;
        private static Texture2D s_whiteTexture;

        /// <summary>Gets the packaged model content name.</summary>
        protected abstract string ModelName { get; }
        /// <summary>Gets the required mesh node name.</summary>
        protected abstract string MeshName { get; }
        /// <summary>Gets the scale used for world and inventory rendering.</summary>
        protected abstract float ModelScale { get; }
        /// <summary>Gets the model tint used with the shared white texture.</summary>
        protected abstract Color ModelColor { get; }

        /// <summary>Gets the translation applied after scaling and rotation.</summary>
        protected virtual Vector3 ModelOffset
        {
            get { return new Vector3(0.5f, 0f, 0.5f); }
        }

        private static Texture2D WhiteTexture
        {
            get
            {
                if (s_whiteTexture == null)
                {
                    s_whiteTexture = new Texture2D(1, 1, 1, ColorFormat.Rgba8888);
                    s_whiteTexture.SetData(0, new byte[] { 255, 255, 255, 255 });
                }

                return s_whiteTexture;
            }
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>(ModelName);
            ModelMesh sourceMesh = model.FindMesh(MeshName, true);
            Matrix boneTransform = BlockMesh.GetBoneAbsoluteTransform(sourceMesh.ParentBone);

            for (int rotation = 0; rotation < 4; rotation++)
            {
                BlockMesh target = new BlockMesh();
                Matrix transform = boneTransform
                    * Matrix.CreateScale(ModelScale)
                    * Matrix.CreateRotationY(rotation * MathUtils.PI / 2f)
                    * Matrix.CreateTranslation(ModelOffset);

                foreach (ModelMeshPart part in sourceMesh.MeshParts)
                {
                    target.AppendModelMeshPart(part, transform, false, false, false, false, ModelColor);
                }

                target.GenerateSidesData();
                m_worldMeshes[rotation] = target;
                m_collisionBoxes[rotation] = new[] { target.CalculateBoundingBox() };
            }

            m_iconMesh = m_worldMeshes[0];
            _ = WhiteTexture;
            base.Initialize();
        }

        /// <inheritdoc />
        public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
        {
            return true;
        }

        /// <inheritdoc />
        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
        {
            return m_collisionBoxes[GetRotation(Terrain.ExtractData(value))];
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
            TerrainGeometrySubset subset = geometry.GetGeometry(WhiteTexture).SubsetOpaque;
            generator.GenerateShadedMeshVertices(
                this,
                x,
                y,
                z,
                m_worldMeshes[GetRotation(Terrain.ExtractData(value))],
                Color.White,
                null,
                null,
                subset);
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
                m_iconMesh,
                WhiteTexture,
                color,
                size,
                ref matrix,
                environmentData);
        }

        /// <inheritdoc />
        public override BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner,
            int value,
            TerrainRaycastResult raycastResult)
        {
            Vector3 forward = Matrix.CreateFromQuaternion(
                componentMiner.ComponentCreature.ComponentCreatureModel.EyeRotation).Forward;
            int rotation;
            if (MathUtils.Abs(forward.X) > MathUtils.Abs(forward.Z))
            {
                rotation = forward.X > 0f ? 1 : 3;
            }
            else
            {
                rotation = forward.Z > 0f ? 2 : 0;
            }

            return new BlockPlacementData
            {
                CellFace = raycastResult.CellFace,
                Value = Terrain.MakeBlockValue(BlockIndex, 0, SetRotation(0, rotation))
            };
        }

        /// <inheritdoc />
        public override void GetDropValues(
            SubsystemTerrain subsystemTerrain,
            int oldValue,
            int newValue,
            int toolLevel,
            List<BlockDropValue> dropValues,
            out bool showDebris)
        {
            showDebris = true;
            dropValues.Add(new BlockDropValue
            {
                Value = Terrain.MakeBlockValue(BlockIndex),
                Count = 1
            });
        }

        /// <inheritdoc />
        public override Vector3 GetIconBlockOffset(int value, DrawBlockEnvironmentData environmentData)
        {
            return new Vector3(-0.5f, 0f, -0.5f);
        }

        /// <summary>Reads the horizontal orientation from the low two data bits.</summary>
        public static int GetRotation(int data)
        {
            return data & 3;
        }

        /// <summary>Writes the horizontal orientation to the low two data bits.</summary>
        public static int SetRotation(int data, int rotation)
        {
            return (data & -4) | (rotation & 3);
        }
    }

    /// <summary>Static placeable SCIE steel gear.</summary>
    public sealed class SteelGearFurnitureBlock : IndustrialFurnitureBlock
    {
        /// <summary>SCNET content ID reserved for the steel gear.</summary>
        public const int Index = 917;
        /// <inheritdoc />
        protected override string ModelName => "Models/Gear";
        /// <inheritdoc />
        protected override string MeshName => "Gear";
        /// <inheritdoc />
        protected override float ModelScale => 0.9f;
        /// <inheritdoc />
        protected override Color ModelColor => new Color(185, 190, 196);
    }

    /// <summary>Static placeable SCIE steel wheel.</summary>
    public sealed class SteelWheelFurnitureBlock : IndustrialFurnitureBlock
    {
        /// <summary>SCNET content ID reserved for the steel wheel.</summary>
        public const int Index = 918;
        /// <inheritdoc />
        protected override string ModelName => "Models/Wheel";
        /// <inheritdoc />
        protected override string MeshName => "Wheel";
        /// <inheritdoc />
        protected override float ModelScale => 0.9f;
        /// <inheritdoc />
        protected override Color ModelColor => new Color(165, 172, 180);
    }

    /// <summary>Static placeable SCIE gear mould.</summary>
    public sealed class GearMouldFurnitureBlock : IndustrialFurnitureBlock
    {
        /// <summary>SCNET content ID reserved for the gear mould.</summary>
        public const int Index = 919;
        /// <inheritdoc />
        protected override string ModelName => "Models/GearMould";
        /// <inheritdoc />
        protected override string MeshName => "GearMould";
        /// <inheritdoc />
        protected override float ModelScale => 0.9f;
        /// <inheritdoc />
        protected override Color ModelColor => new Color(179, 132, 78);
    }

    /// <summary>Static placeable SCIE wheel mould.</summary>
    public sealed class WheelMouldFurnitureBlock : IndustrialFurnitureBlock
    {
        /// <summary>SCNET content ID reserved for the wheel mould.</summary>
        public const int Index = 920;
        /// <inheritdoc />
        protected override string ModelName => "Models/WheelMould";
        /// <inheritdoc />
        protected override string MeshName => "WheelMould";
        /// <inheritdoc />
        protected override float ModelScale => 0.9f;
        /// <inheritdoc />
        protected override Color ModelColor => new Color(179, 132, 78);
    }

    /// <summary>Static placeable SCIE industrial piston.</summary>
    public sealed class IndustrialPistonFurnitureBlock : IndustrialFurnitureBlock
    {
        /// <summary>SCNET content ID reserved for the industrial piston.</summary>
        public const int Index = 921;
        /// <inheritdoc />
        protected override string ModelName => "Models/Piston";
        /// <inheritdoc />
        protected override string MeshName => "Piston";
        /// <inheritdoc />
        protected override float ModelScale => 0.75f;
        /// <inheritdoc />
        protected override Color ModelColor => new Color(188, 193, 198);
        /// <inheritdoc />
        protected override Vector3 ModelOffset => new Vector3(0.55f, 0f, 0.55f);
    }

    /// <summary>Static placeable SCIE industrial cylinder.</summary>
    public sealed class IndustrialCylinderFurnitureBlock : IndustrialFurnitureBlock
    {
        /// <summary>SCNET content ID reserved for the industrial cylinder.</summary>
        public const int Index = 922;
        /// <inheritdoc />
        protected override string ModelName => "Models/Cylinder";
        /// <inheritdoc />
        protected override string MeshName => "obj1";
        /// <inheritdoc />
        protected override float ModelScale => 55f;
        /// <inheritdoc />
        protected override Color ModelColor => new Color(105, 116, 128);
    }
}
