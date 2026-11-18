#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Plants;

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
        private readonly PlantModel m_plantModel = new PlantModel();

        public override void Initialize()
        {
            m_plantModel.Initialize(BlockIndex);
            base.Initialize();
        }

        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            m_plantModel.Generate(this, generator, geometry, value, x, y, z);
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer,
            int value,
            Color color,
            float size,
            ref Matrix matrix,
            DrawBlockEnvironmentData environmentData)
        {
            m_plantModel.Draw(this, primitivesRenderer, value, color, size, ref matrix, environmentData);
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
        private readonly PlantModel m_plantModel = new PlantModel();

        public override void Initialize()
        {
            m_plantModel.Initialize(BlockIndex);
            base.Initialize();
        }

        public override bool IsFaceTransparent(SubsystemTerrain subsystemTerrain, int face, int value)
        {
            return true;
        }

        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
        {
            return m_plantModel.Boxes(this,value);
        }

        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            m_plantModel.Generate(this, generator, geometry, value, x, y, z);
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer,
            int value,
            Color color,
            float size,
            ref Matrix matrix,
            DrawBlockEnvironmentData environmentData)
        {
            m_plantModel.Draw(this, primitivesRenderer, value, color, size, ref matrix, environmentData);
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
        private readonly Game.Random m_random = new Game.Random();

        protected abstract int DropContents { get; }

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => KellyPlantPlacement.Place(terrain, hit.CellFace, BlockIndex,
                SubsystemKellyMushroomBlockBehavior.Established | SubsystemKellyMushroomBlockBehavior.Immature);

        public override void GetDropValues(
            SubsystemTerrain subsystemTerrain,
            int oldValue,
            int newValue,
            int toolLevel,
            List<BlockDropValue> dropValues,
            out bool showDebris)
        {
            showDebris = true;
            if ((Terrain.ExtractData(oldValue) & SubsystemKellyMushroomBlockBehavior.StageMask) != 0) return;
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

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => KellyPlantPlacement.Place(terrain, hit.CellFace, Index, 0);

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
            base.GenerateTerrainVertices(generator, geometry, value, x, y, z);
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
            yield return Terrain.MakeBlockValue(Index);
        }
    }

    public sealed class WildBerryBlock : KellyFruitBlock
    {
        public const int Index = 904;

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => KellyPlantPlacement.Place(terrain, hit.CellFace, WildBerryPlantBlock.Index, 0);
    }

    public sealed class CattailBlock : KellyCrossBlock
    {
        public const int Index = 905;

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => KellyPlantPlacement.Place(terrain, hit.CellFace, Index, 1);

        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> drops, out bool debris)
        {
            debris = true;
            if ((Terrain.ExtractData(oldValue) & 3) == 0)
                drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(CattailShootBlock.Index), Count = 1 });
        }
    }

    public sealed class CattailShootBlock : KellyFruitBlock
    {
        public const int Index = 906;

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => KellyPlantPlacement.Place(terrain, hit.CellFace, CattailBlock.Index, 1);
    }

    public sealed class CactusFruitBlock : KellyFruitBlock
    {
        public const int Index = 907;

        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel, List<BlockDropValue> drops, out bool debris)
        {
            debris = true;
            if ((Terrain.ExtractData(oldValue) & 3) == 0)
                drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(Index), Count = 1 });
        }

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => KellyPlantPlacement.Place(terrain, hit.CellFace, Index, 1);
    }

    public sealed class AcornBlock : KellyFruitBlock
    {
        public const int Index = 908;

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => KellyPlantPlacement.Place(terrain, hit.CellFace, SaplingBlock.Index, 0);
    }

    public sealed class MushroomPatchBlock : KellyMushroomPatchBlock
    {
        public const int Index = 909;
        protected override int DropContents { get { return WildMushroomBlock.Index; } }
    }

    public sealed class WildMushroomBlock : KellyFruitBlock
    {
        public const int Index = 910;

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => KellyPlantPlacement.Place(terrain, hit.CellFace, MushroomPatchBlock.Index, SubsystemKellyMushroomBlockBehavior.Established | SubsystemKellyMushroomBlockBehavior.Immature);
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
        private readonly PlantModel m_plantModel = new PlantModel();

        public override void Initialize()
        {
            m_plantModel.Initialize(BlockIndex);
            base.Initialize();
        }

        public const int Index = 912;

        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            m_plantModel.Generate(this, generator, geometry, value, x, y, z);
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer,
            int value,
            Color color,
            float size,
            ref Matrix matrix,
            DrawBlockEnvironmentData environmentData)
        {
            m_plantModel.Draw(this, primitivesRenderer, value, color, size, ref matrix, environmentData);
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
        protected override int DropContents { get { return FlyAgaricBlock.Index; } }
    }

    public sealed class FlyAgaricBlock : KellyFruitBlock
    {
        public const int Index = 914;

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => KellyPlantPlacement.Place(terrain, hit.CellFace, FlyAgaricPatchBlock.Index, SubsystemKellyMushroomBlockBehavior.Established | SubsystemKellyMushroomBlockBehavior.Immature);
    }

    public sealed class CloudberryPlantBlock : KellyCrossBlock
    {
        public const int Index = 915;

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index, 0, 1);
        }

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => KellyPlantPlacement.Place(terrain, hit.CellFace, Index, 1);

        public override void GetDropValues(
            SubsystemTerrain subsystemTerrain,
            int oldValue,
            int newValue,
            int toolLevel,
            List<BlockDropValue> dropValues,
            out bool showDebris)
        {
            showDebris = true;
            if((Terrain.ExtractData(oldValue)&3)!=0) return;
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

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => KellyPlantPlacement.Place(terrain, hit.CellFace, CloudberryPlantBlock.Index, 1);
    }

    internal static class KellyPlantPlacement
    {
        internal static BlockPlacementData Place(SubsystemTerrain subsystem, CellFace target, int plant, int data)
        {
            var placement = new BlockPlacementData { CellFace = target };
            if (CellFace.FaceToPoint3(target.Face).Y != 1 || target.Y < 1 || target.Y >= 254) return placement;

            Terrain terrain = subsystem.Terrain;
            int ground = terrain.GetCellContents(target.X, target.Y, target.Z);
            bool supported;
            if (plant == CactusFruitBlock.Index || plant == CattailBlock.Index)
            {
                supported = SubsystemKellyFruitBlockBehavior.HasSupport(plant, ground);
                if (supported && plant == CattailBlock.Index)
                {
                    supported = PlantGrowthSite.HasWater(target.X, target.Y + 1, target.Z, terrain.GetCellContents);
                }
            }
            else
            {
                supported = plant == SaplingBlock.Index ? PlantHabitat.IsNaturalSoil(ground) : PlantHabitat.IsRootingSoil(ground);
            }
            if (supported) placement.Value = Terrain.MakeBlockValue(plant, 0, data);
            return placement;
        }
    }
}
