#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Crops
{
    internal static class CropsTextures
    {
        private static Texture2D s_atlas;

        // Clean-room 512x512 RGBA atlas, laid out as the confirmed 16x16 grid of
        // 32x32 tiles (see docs/COMPATIBILITY.md). Corn stalk stages occupy slots
        // 0..7, the seed item slot 8 and the harvested ear slot 9.
        public const int StalkSlotBase = 0;
        public const int SeedSlot = 8;
        public const int EarSlot = 9;

        public static Texture2D Atlas
        {
            get
            {
                if (s_atlas == null)
                {
                    s_atlas = ContentManager.Get<Texture2D>("Textures/Corn");
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

    public sealed class CornStalkBlock : CrossBlock
    {
        public const int Index = 929;

        public override int GetFaceTextureSlot(int face, int value)
        {
            return CropsTextures.StalkSlotBase + CropRules.GetSize(Terrain.ExtractData(value));
        }

        public override int GetShadowStrength(int value)
        {
            return CropRules.GetSize(Terrain.ExtractData(value)) + 1;
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
                geometry.GetGeometry(CropsTextures.Atlas).SubsetAlphaTest);
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
                CropsTextures.Atlas,
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
            return CropsTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index, 0, CropRules.SetSize(0, CropRules.MaxCropSize));
        }

        public override BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner,
            int value,
            TerrainRaycastResult raycastResult)
        {
            return CornPlacement.GetPlacementValue(subsystemTerrain, raycastResult);
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
            if (CropRules.IsMature(Terrain.ExtractData(oldValue)))
            {
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(CornEarBlock.Index),
                    Count = CropRules.GetEarYield()
                });
                dropValues.Add(new BlockDropValue
                {
                    Value = Terrain.MakeBlockValue(CornSeedBlock.Index),
                    Count = CropRules.GetSeedYield()
                });
            }
        }
    }

    public sealed class CornSeedBlock : FlatBlock
    {
        public const int Index = 930;

        public override int GetFaceTextureSlot(int face, int value)
        {
            return CropsTextures.SeedSlot;
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
                geometry.GetGeometry(CropsTextures.Atlas).SubsetAlphaTest);
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
                CropsTextures.Atlas,
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
            return CropsTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner,
            int value,
            TerrainRaycastResult raycastResult)
        {
            return CornPlacement.GetPlacementValue(subsystemTerrain, raycastResult);
        }
    }

    public sealed class CornEarBlock : FlatBlock
    {
        public const int Index = 931;

        public override int GetFaceTextureSlot(int face, int value)
        {
            return CropsTextures.EarSlot;
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
                geometry.GetGeometry(CropsTextures.Atlas).SubsetAlphaTest);
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
                CropsTextures.Atlas,
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
            return CropsTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner,
            int value,
            TerrainRaycastResult raycastResult)
        {
            // An ear can be planted directly to restart a corn patch, mirroring how
            // the vanilla soybean fruit replants without a separate seed step.
            return CornPlacement.GetPlacementValue(subsystemTerrain, raycastResult);
        }
    }

    internal static class CornPlacement
    {
        public static BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            TerrainRaycastResult raycastResult)
        {
            CellFace target = raycastResult.CellFace;
            Point3 direction = CellFace.FaceToPoint3(target.Face);
            bool isSoil = Terrain.ExtractContents(
                subsystemTerrain.Terrain.GetCellValue(target.X, target.Y, target.Z)) == SoilBlock.Index;
            if (CropRules.CanPlaceCorn(direction.Y == 1, isSoil))
            {
                return new BlockPlacementData
                {
                    Value = Terrain.MakeBlockValue(CornStalkBlock.Index, 0, CropRules.SetSize(0, 0)),
                    CellFace = target
                };
            }

            return new BlockPlacementData { Value = 0, CellFace = target };
        }
    }
}
