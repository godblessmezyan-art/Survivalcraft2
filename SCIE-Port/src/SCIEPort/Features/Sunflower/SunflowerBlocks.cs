#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Sunflower
{
    internal static class SunflowerTextures
    {
        private static Texture2D s_atlas;

        // Clean-room 512x512 RGBA atlas laid out as the confirmed 16x16 grid of
        // 32x32 tiles (see docs/COMPATIBILITY.md). Sunflower plant stages occupy slots
        // 0..7 and the harvested seed pile slot 8.
        public const int PlantSlotBase = 0;
        public const int SeedSlot = 8;

        public static Texture2D Atlas
        {
            get
            {
                if (s_atlas == null)
                {
                    s_atlas = ContentManager.Get<Texture2D>("Textures/Sunflower");
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

    public sealed class SunflowerPlantBlock : CrossBlock
    {
        public const int Index = 936;

        public override int GetFaceTextureSlot(int face, int value)
        {
            return SunflowerTextures.PlantSlotBase + SunflowerRules.GetSize(Terrain.ExtractData(value));
        }

        public override int GetShadowStrength(int value)
        {
            return SunflowerRules.GetSize(Terrain.ExtractData(value)) + 1;
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
                geometry.GetGeometry(SunflowerTextures.Atlas).SubsetAlphaTest);
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
                SunflowerTextures.Atlas,
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
            return SunflowerTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index, 0, SunflowerRules.SetSize(0, SunflowerRules.MaxSunflowerSize));
        }

        public override BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner,
            int value,
            TerrainRaycastResult raycastResult)
        {
            return SunflowerPlacement.GetPlacementValue(subsystemTerrain, raycastResult);
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
            if (SunflowerRules.IsMature(Terrain.ExtractData(oldValue)))
            {
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(SunflowerSeedBlock.Index),
                    Count = SunflowerRules.GetSeedYield()
                });
                // A mature head also yields one replantable seed (the plant item at
                // stage zero) so harvesting does not strip the field.
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(SunflowerPlantBlock.Index, 0, SunflowerRules.SetSize(0, 0)),
                    Count = SunflowerRules.GetReplantSeedYield()
                });
            }
        }
    }

    public sealed class SunflowerSeedBlock : FlatBlock
    {
        public const int Index = 937;

        public override int GetFaceTextureSlot(int face, int value)
        {
            return SunflowerTextures.SeedSlot;
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
                geometry.GetGeometry(SunflowerTextures.Atlas).SubsetAlphaTest);
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
                SunflowerTextures.Atlas,
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
            return SunflowerTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index);
        }
    }

    internal static class SunflowerPlacement
    {
        public static BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            TerrainRaycastResult raycastResult)
        {
            CellFace target = raycastResult.CellFace;
            Point3 direction = CellFace.FaceToPoint3(target.Face);
            bool isSoil = Terrain.ExtractContents(
                subsystemTerrain.Terrain.GetCellValue(target.X, target.Y, target.Z)) == SoilBlock.Index;
            if (SunflowerRules.CanPlaceSunflower(direction.Y == 1, isSoil))
            {
                return new BlockPlacementData
                {
                    Value = Terrain.MakeBlockValue(SunflowerPlantBlock.Index, 0, SunflowerRules.SetSize(0, 0)),
                    CellFace = target
                };
            }

            return new BlockPlacementData { Value = 0, CellFace = target };
        }
    }
}
