#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.KellyPlants
{
    internal static class KellyPlantTextures
    {
        private static Texture2D s_atlas;

        public static Texture2D Atlas
        {
            get
            {
                if (s_atlas == null)
                {
                    s_atlas = ContentManager.Get<Texture2D>("Textures/KellyExpansion");
                }
                return s_atlas;
            }
        }

        public static BlockDebrisParticleSystem CreateDebris(
            Block block,
            SubsystemTerrain terrain,
            Vector3 position,
            int value,
            float strength)
        {
            return new BlockDebrisParticleSystem(
                terrain,
                position,
                strength,
                block.DestructionDebrisScale,
                Color.White,
                block.GetFaceTextureSlot(4, value));
        }
    }

    public abstract class KellyCrossBlock : CrossBlock
    {
        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            generator.GenerateCrossfaceVertices(
                this,
                value,
                x,
                y,
                z,
                Color.White,
                GetFaceTextureSlot(4, value),
                geometry.GetGeometry(KellyPlantTextures.Atlas).SubsetAlphaTest);
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer,
            int value,
            Color color,
            float size,
            ref Matrix matrix,
            DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawFlatBlock(
                primitivesRenderer,
                value,
                size,
                ref matrix,
                KellyPlantTextures.Atlas,
                color,
                false,
                environmentData);
        }

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
            SubsystemTerrain subsystemTerrain,
            Vector3 position,
            int value,
            float strength)
        {
            return KellyPlantTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }
    }

    public abstract class KellyFruitBlock : Block
    {
        private static readonly BoundingBox[] s_boxes =
        {
            new BoundingBox(new Vector3(0.33f, 0f, 0.33f), new Vector3(0.67f, 0.42f, 0.67f))
        };

        private readonly BlockMesh m_worldMesh = new BlockMesh();
        private readonly BlockMesh m_iconMesh = new BlockMesh();

        protected abstract string MeshName { get; }

        protected virtual Matrix IconTransform
        {
            get { return Matrix.CreateScale(1.5f) * Matrix.CreateTranslation(0f, -0.3f, 0f); }
        }

        public override void Initialize()
        {
            Model model = ContentManager.Get<Model>("Models/KellyFruits");
            AppendMesh(m_worldMesh, model, MeshName, Matrix.CreateTranslation(0.5f, 0f, 0.5f));
            AppendMesh(m_iconMesh, model, MeshName, IconTransform);
            base.Initialize();
        }

        private static void AppendMesh(BlockMesh target, Model model, string meshName, Matrix transform)
        {
            ModelMesh mesh = model.FindMesh(meshName, true);
            Matrix boneTransform = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone);
            target.AppendModelMeshPart(
                mesh.MeshParts[0], boneTransform * transform, false, false, false, false, Color.White);
        }

        public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
        {
            return true;
        }

        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
        {
            return s_boxes;
        }

        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            TerrainGeometrySubset subset = geometry.GetGeometry(KellyPlantTextures.Atlas).SubsetAlphaTest;
            generator.GenerateShadedMeshVertices(
                this, x, y, z, m_worldMesh, Color.White, null, null, subset);
        }

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
                KellyPlantTextures.Atlas,
                color,
                size,
                ref matrix,
                environmentData);
        }

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
            SubsystemTerrain subsystemTerrain,
            Vector3 position,
            int value,
            float strength)
        {
            return KellyPlantTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

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
    }

    public abstract class KellyMushroomPatchBlock : KellyFruitBlock
    {
        private static readonly BoundingBox[] s_boxes =
        {
            new BoundingBox(new Vector3(0.22f, 0f, 0.22f), new Vector3(0.78f, 0.34f, 0.78f))
        };

        private readonly Game.Random m_random = new Game.Random();

        protected abstract int DropContents { get; }

        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
        {
            return s_boxes;
        }

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
                Value = Terrain.MakeBlockValue(DropContents),
                Count = m_random.Int(1, 2)
            });
        }
    }

    public sealed class WildBerryPlantBlock : KellyCrossBlock
    {
        public const int Index = 903;

        public static bool GetFruiting(int data) { return (data & 1) != 0; }

        public static int SetFruiting(int data, bool fruiting)
        {
            return fruiting ? data | 1 : data & ~1;
        }

        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            if (GetFruiting(Terrain.ExtractData(value)))
            {
                base.GenerateTerrainVertices(generator, geometry, value, x, y, z);
            }
        }

        public override void GetDropValues(
            SubsystemTerrain subsystemTerrain,
            int oldValue,
            int newValue,
            int toolLevel,
            List<BlockDropValue> dropValues,
            out bool showDebris)
        {
            showDebris = true;
            if (GetFruiting(Terrain.ExtractData(oldValue)))
            {
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(WildBerryBlock.Index),
                    Count = 1
                });
            }
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index, 0, SetFruiting(0, true));
        }
    }

    public sealed class WildBerryBlock : KellyFruitBlock
    {
        public const int Index = 904;
        protected override string MeshName { get { return "WildBerry"; } }
    }

    public sealed class CattailBlock : KellyCrossBlock
    {
        public const int Index = 905;

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
                Value = Terrain.MakeBlockValue(CattailShootBlock.Index),
                Count = 1
            });
        }
    }

    public sealed class CattailShootBlock : KellyFruitBlock
    {
        public const int Index = 906;
        protected override string MeshName { get { return "CattailShoot"; } }
    }

    public sealed class CactusFruitBlock : KellyFruitBlock
    {
        public const int Index = 907;
        protected override string MeshName { get { return "CactusFruit"; } }
    }

    public sealed class AcornBlock : KellyFruitBlock
    {
        public const int Index = 908;
        protected override string MeshName { get { return "Acorn"; } }
    }

    public sealed class MushroomPatchBlock : KellyMushroomPatchBlock
    {
        public const int Index = 909;
        protected override string MeshName { get { return "MushroomPatch"; } }
        protected override int DropContents { get { return WildMushroomBlock.Index; } }
    }

    public sealed class WildMushroomBlock : KellyFruitBlock
    {
        public const int Index = 910;
        protected override string MeshName { get { return "WildMushroom"; } }
    }

    public sealed class SoybeanBlock : KellyCrossBlock
    {
        public const int Index = 911;
        private static readonly Game.Random s_random = new Game.Random();

        public static int GetSize(int data) { return data & 7; }

        public static int SetSize(int data, int size)
        {
            return (data & ~7) | (MathUtils.Clamp(size, 0, 7) & 7);
        }

        public override int GetFaceTextureSlot(int face, int value)
        {
            return 85 + GetSize(Terrain.ExtractData(value));
        }

        public override int GetShadowStrength(int value)
        {
            return GetSize(Terrain.ExtractData(value)) + 1;
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index, 0, SetSize(0, 7));
        }

        public override BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner,
            int value,
            TerrainRaycastResult raycastResult)
        {
            return SoybeanPlacement.GetPlacementValue(subsystemTerrain, raycastResult);
        }

        public override void GetDropValues(
            SubsystemTerrain subsystemTerrain,
            int oldValue,
            int newValue,
            int toolLevel,
            List<BlockDropValue> dropValues,
            out bool showDebris)
        {
            showDebris = true;
            if (GetSize(Terrain.ExtractData(oldValue)) >= 7)
            {
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(SoybeanFruitBlock.Index),
                    Count = s_random.Int(2, 3)
                });
            }
        }
    }

    public sealed class SoybeanFruitBlock : FlatBlock
    {
        public const int Index = 912;

        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            generator.GenerateCrossfaceVertices(
                this,
                value,
                x,
                y,
                z,
                Color.White,
                GetFaceTextureSlot(4, value),
                geometry.GetGeometry(KellyPlantTextures.Atlas).SubsetAlphaTest);
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer,
            int value,
            Color color,
            float size,
            ref Matrix matrix,
            DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawFlatBlock(
                primitivesRenderer,
                value,
                size,
                ref matrix,
                KellyPlantTextures.Atlas,
                color,
                false,
                environmentData);
        }

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
            SubsystemTerrain subsystemTerrain,
            Vector3 position,
            int value,
            float strength)
        {
            return KellyPlantTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner,
            int value,
            TerrainRaycastResult raycastResult)
        {
            return SoybeanPlacement.GetPlacementValue(subsystemTerrain, raycastResult);
        }
    }

    internal static class SoybeanPlacement
    {
        public static BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            TerrainRaycastResult raycastResult)
        {
            CellFace target = raycastResult.CellFace;
            Point3 direction = CellFace.FaceToPoint3(target.Face);
            bool isSoil = Terrain.ExtractContents(
                subsystemTerrain.Terrain.GetCellValue(target.X, target.Y, target.Z)) == SoilBlock.Index;
            if (KellyPlantRules.CanPlaceSoybean(direction.Y == 1, isSoil))
            {
                return new BlockPlacementData
                {
                    Value = Terrain.MakeBlockValue(SoybeanBlock.Index, 0, SoybeanBlock.SetSize(0, 0)),
                    CellFace = target
                };
            }

            return new BlockPlacementData { Value = 0, CellFace = target };
        }
    }

    public sealed class FlyAgaricPatchBlock : KellyMushroomPatchBlock
    {
        public const int Index = 913;
        protected override string MeshName { get { return "FlyAgaricPatch"; } }
        protected override int DropContents { get { return FlyAgaricBlock.Index; } }
    }

    public sealed class FlyAgaricBlock : KellyFruitBlock
    {
        public const int Index = 914;
        protected override string MeshName { get { return "FlyAgaric"; } }
    }

    public sealed class CloudberryPlantBlock : KellyCrossBlock
    {
        public const int Index = 915;

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
                Value = Terrain.MakeBlockValue(CloudberryBlock.Index),
                Count = 1
            });
        }
    }

    public sealed class CloudberryBlock : KellyFruitBlock
    {
        public const int Index = 916;
        protected override string MeshName { get { return "Cloudberry"; } }
    }
}
