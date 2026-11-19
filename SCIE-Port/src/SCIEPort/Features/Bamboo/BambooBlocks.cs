#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Bamboo
{
    internal static class BambooTextures
    {
        private static Texture2D s_atlas;

        // Clean-room 512x512 RGBA atlas laid out as the confirmed 16x16 grid of
        // 32x32 tiles (see docs/COMPATIBILITY.md). Slot 0 is the planted base
        // cross face, slot 1 the stacked upper culm cross face and slot 2 the
        // solid building block cube face. Slot 3 holds the shoot sprite for debris.
        public const int BasePlantSlot = 0;
        public const int UpperPlantSlot = 1;
        public const int BuildingSlot = 2;
        public const int ShootSlot = 3;

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
            return TreeBlockRendering.Debris(block, Atlas, terrain, position, value, strength);
        }
    }

    public sealed class BambooPlantBlock : CrossBlock
    {
        private readonly PlantModel m_plantModel = new PlantModel();

        public override void Initialize()
        {
            m_plantModel.Initialize(BlockIndex);
            _ = BambooTextures.Atlas;
            base.Initialize();
        }

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
            // Read only the immediate neighbor so the native slice mesh cache
            // invalidates correctly even across a vertical slice boundary.
            int above = generator.Terrain.GetCellContents(x, y + 1, z);
            int visual = above != Index ? PlantGeometry.BambooCrown : y % 3 == 1 ? PlantGeometry.BambooBranch : PlantGeometry.BambooStem;
            m_plantModel.Generate(this, generator, geometry, Terrain.ReplaceData(value, visual), x, y, z);
        }

        public override void DrawBlock(
            PrimitivesRenderer3D primitivesRenderer,
            int value,
            Color color,
            float size,
            ref Matrix matrix,
            DrawBlockEnvironmentData environmentData)
        {
            m_plantModel.Draw(this, primitivesRenderer, Terrain.ReplaceData(value, PlantGeometry.BambooCrown), color, size, ref matrix, environmentData);
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

    public sealed class BambooShootBlock : FlatBlock
    {
        public const int Index = 1001;

        private readonly PlantModel model = new PlantModel();

        public override void Initialize()
        {
            model.Initialize(BlockIndex);
            base.Initialize();
        }

        public override int GetFaceTextureSlot(int face, int value) => BambooTextures.ShootSlot;

        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
            => model.Boxes(this, value);

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry,
            int value, int x, int y, int z) => model.Generate(this, generator, geometry, value, x, y, z);

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
            => model.Draw(this, renderer, Terrain.MakeBlockValue(Index), color, size, ref matrix, environment);

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner,
            int value, TerrainRaycastResult hit) => BambooPlacement.GetPlacementValue(terrain, hit, Index);

        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel,
            List<BlockDropValue> drops, out bool debris)
        {
            debris = true;
            drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(Index), Count = 1 });
        }

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain terrain,
            Vector3 position, int value, float strength)
            => BambooTextures.CreateDebris(this, terrain, position, value, strength);
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

        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            if (!(BlocksManager.Blocks[BambooPlantBlock.Index] is BambooPlantBlock plant)) yield break;
            string culm = plant.GetCraftingId(Terrain.MakeBlockValue(BambooPlantBlock.Index));
            if (string.IsNullOrEmpty(culm)) yield break;

            yield return new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(Index),
                ResultCount = 1,
                Ingredients = new[] { culm, culm, null, culm, culm, null, null, null, null },
                Description = "把竹竿扎成一方竹材。"
            };
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
        internal static bool HasSubstrate(int contents)
            => PlantHabitat.IsRootingSoil(contents) || contents == SandBlock.Index;

        public static BlockPlacementData GetPlacementValue(
            SubsystemTerrain subsystemTerrain,
            TerrainRaycastResult raycastResult,
            int contents = BambooPlantBlock.Index)
        {
            CellFace target = raycastResult.CellFace;
            Point3 direction = CellFace.FaceToPoint3(target.Face);
            int below = subsystemTerrain.Terrain.GetCellValue(target.X, target.Y, target.Z);
            int substrate = Terrain.ExtractContents(below);
            if (target.Y >= 0 && target.Y <= 252 && BambooRules.CanPlaceBase(direction.Y == 1, HasSubstrate(substrate)))
            {
                return new BlockPlacementData
                {
                    Value = Terrain.MakeBlockValue(contents, 0, contents == BambooPlantBlock.Index ? BambooRules.MarkBase(0) : 0),
                    CellFace = target
                };
            }

            return new BlockPlacementData { Value = 0, CellFace = target };
        }
    }
}
