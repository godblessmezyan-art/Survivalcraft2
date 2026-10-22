#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Flax
{
    internal static class FlaxTextures
    {
        private static Texture2D s_atlas;

        // Clean-room 512x512 RGBA atlas laid out as the confirmed 16x16 grid of
        // 32x32 tiles (see docs/COMPATIBILITY.md). Flax plant stages occupy slots
        // 0..7 and the harvested fiber bundle slot 8.
        public const int PlantSlotBase = 0;
        public const int FiberSlot = 8;

        public static Texture2D Atlas
        {
            get
            {
                if (s_atlas == null)
                {
                    s_atlas = ContentManager.Get<Texture2D>("Textures/Flax");
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

    public sealed class FlaxPlantBlock : CrossBlock
    {
        public const int Index = 934;

        public override int GetFaceTextureSlot(int face, int value)
        {
            return FlaxTextures.PlantSlotBase + FlaxRules.GetSize(Terrain.ExtractData(value));
        }

        public override int GetShadowStrength(int value)
        {
            return FlaxRules.GetSize(Terrain.ExtractData(value)) + 1;
        }

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
                geometry.GetGeometry(FlaxTextures.Atlas).SubsetAlphaTest);
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
                FlaxTextures.Atlas,
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
            return FlaxTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index, 0, FlaxRules.SetSize(0, FlaxRules.MaxFlaxSize));
        }

        public override BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner,
            int value,
            TerrainRaycastResult raycastResult)
        {
            return FlaxPlacement.GetPlacementValue(subsystemTerrain, raycastResult);
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
            if (FlaxRules.IsMature(Terrain.ExtractData(oldValue)))
            {
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(FlaxFiberBlock.Index),
                    Count = FlaxRules.GetFiberYield()
                });
                // A mature plant also yields one replantable seed (the plant item at
                // stage zero) so harvesting does not strip the field.
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(FlaxPlantBlock.Index, 0, FlaxRules.SetSize(0, 0)),
                    Count = FlaxRules.GetSeedYield()
                });
            }
        }
    }

    public sealed class FlaxFiberBlock : FlatBlock
    {
        public const int Index = 935;

        public override int GetFaceTextureSlot(int face, int value)
        {
            return FlaxTextures.FiberSlot;
        }

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
                geometry.GetGeometry(FlaxTextures.Atlas).SubsetAlphaTest);
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
                FlaxTextures.Atlas,
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
            return FlaxTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index);
        }
    }

    internal static class FlaxPlacement
    {
        public static BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            TerrainRaycastResult raycastResult)
        {
            CellFace target = raycastResult.CellFace;
            Point3 direction = CellFace.FaceToPoint3(target.Face);
            bool isSoil = Terrain.ExtractContents(
                subsystemTerrain.Terrain.GetCellValue(target.X, target.Y, target.Z)) == SoilBlock.Index;
            if (FlaxRules.CanPlaceFlax(direction.Y == 1, isSoil))
            {
                return new BlockPlacementData
                {
                    Value = Terrain.MakeBlockValue(FlaxPlantBlock.Index, 0, FlaxRules.SetSize(0, 0)),
                    CellFace = target
                };
            }

            return new BlockPlacementData { Value = 0, CellFace = target };
        }
    }
}
