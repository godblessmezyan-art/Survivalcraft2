#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Bamboo
{
    internal static class BambooTextures
    {
        private static Texture2D s_atlas;

        // Clean-room 512x512 RGBA atlas laid out as the confirmed 16x16 grid of
        // 32x32 tiles (see docs/COMPATIBILITY.md). Slot 0 is the planted base
        // cross face, slot 1 the stacked upper culm cross face and slot 2 the
        // solid building block cube face.
        public const int BasePlantSlot = 0;
        public const int UpperPlantSlot = 1;
        public const int BuildingSlot = 2;

        public static Texture2D Atlas
        {
            get
            {
                if (s_atlas == null)
                {
                    s_atlas = ContentManager.Get<Texture2D>("Textures/Bamboo");
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

    public sealed class BambooPlantBlock : CrossBlock
    {
        public const int Index = 932;

        public override int GetFaceTextureSlot(int face, int value)
        {
            int data = Terrain.ExtractData(value);
            return BambooRules.IsBase(data) ? BambooTextures.BasePlantSlot : BambooTextures.UpperPlantSlot;
        }

        public override int GetShadowStrength(int value)
        {
            return 8;
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
                geometry.GetGeometry(BambooTextures.Atlas).SubsetAlphaTest);
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
                BambooTextures.Atlas,
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
            return BambooTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index, 0, BambooRules.MarkBase(0));
        }

        public override BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            ComponentMiner componentMiner,
            int value,
            TerrainRaycastResult raycastResult)
        {
            return BambooPlacement.GetPlacementValue(subsystemTerrain, raycastResult);
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
                Value = Terrain.MakeBlockValue(Index, 0, BambooRules.MarkBase(0)),
                Count = BambooRules.GetCulmYield()
            });
        }
    }

    public sealed class BambooBlock : Block
    {
        public const int Index = 933;

        public override int GetFaceTextureSlot(int face, int value)
        {
            return BambooTextures.BuildingSlot;
        }

        public override void GenerateTerrainVertices(
            BlockGeometryGenerator generator,
            TerrainGeometry geometry,
            int value,
            int x,
            int y,
            int z)
        {
            TerrainGeometrySubset opaque = geometry.GetGeometry(BambooTextures.Atlas).SubsetOpaque;
            generator.GenerateCubeVertices(
                this,
                value,
                x,
                y,
                z,
                Color.White,
                new[] { opaque, opaque, opaque, opaque, opaque, opaque });
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer,
            int value,
            Color color,
            float size,
            ref Matrix matrix,
            DrawBlockEnvironmentData environmentData)
        {
            BlocksManager.DrawCubeBlock(
                primitivesRenderer,
                value,
                new Vector3(size, size, size),
                ref matrix,
                color,
                color,
                environmentData,
                BambooTextures.Atlas);
        }

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(
            SubsystemTerrain subsystemTerrain,
            Vector3 position,
            int value,
            float strength)
        {
            return BambooTextures.CreateDebris(this, subsystemTerrain, position, value, strength);
        }

        public override IEnumerable<int> GetCreativeValues()
        {
            yield return Terrain.MakeBlockValue(Index);
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
                Value = Terrain.MakeBlockValue(Index),
                Count = 1
            });
        }
    }

    internal static class BambooPlacement
    {
        public static BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            TerrainRaycastResult raycastResult)
        {
            CellFace target = raycastResult.CellFace;
            Point3 direction = CellFace.FaceToPoint3(target.Face);
            int below = subsystemTerrain.Terrain.GetCellValue(target.X, target.Y, target.Z);
            int substrate = Terrain.ExtractContents(below);
            bool hasSubstrate = substrate == SoilBlock.Index
                || substrate == SandBlock.Index
                || substrate == DirtBlock.Index;
            if (BambooRules.CanPlaceBase(direction.Y == 1, hasSubstrate))
            {
                return new BlockPlacementData
                {
                    Value = Terrain.MakeBlockValue(BambooPlantBlock.Index, 0, BambooRules.MarkBase(0)),
                    CellFace = target
                };
            }

            return new BlockPlacementData { Value = 0, CellFace = target };
        }
    }
}
