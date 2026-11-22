#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.SpecialTrees
{
    internal static class SpecialTreeTextures
    {
        private static Texture2D atlas;
        internal static Texture2D Atlas => atlas ?? (atlas = ContentManager.Get<Texture2D>("Textures/SpecialTrees"));
    }

    public abstract class SpecialTreeSapling : CrossBlock
    {
        internal TreeSpecies Species { get; }
        private readonly BoundingBox[] seedling = { new BoundingBox(new Vector3(.28f, 0f, .28f), new Vector3(.72f, .66f, .72f)) };

        internal SpecialTreeSapling(TreeSpecies species) { Species = species; }

        public override int GetFaceTextureSlot(int face, int value)
            => Species.AtlasStart + 3 + MathUtils.Clamp(Terrain.ExtractData(value) / 2, 0, 2);

        public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value) => seedling;

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
        {
            CellFace face = hit.CellFace;
            bool place = face.Face == 4 && face.Y >= 1 && face.Y <= 254 - Species.MaxHeight &&
                Species.Supports(terrain.Terrain.GetCellContents(face.X, face.Y, face.Z));
            return new BlockPlacementData { CellFace = face, Value = place ? Terrain.MakeBlockValue(BlockIndex) : 0 };
        }

        public override void GenerateTerrainVertices(BlockGeometryGenerator generator, TerrainGeometry geometry, int value, int x, int y, int z)
            => base.GenerateTerrainVertices(generator, geometry.GetGeometry(SpecialTreeTextures.Atlas), value, x, y, z);

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawFlatBlock(renderer, value, size, ref matrix, SpecialTreeTextures.Atlas, color, false, environment);

        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel,
            List<BlockDropValue> drops, out bool debris)
        {
            drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(BlockIndex), Count = 1 });
            debris = true;
        }

        public override BlockDebrisParticleSystem CreateDebrisParticleSystem(SubsystemTerrain terrain, Vector3 position, int value, float strength)
            => TreeBlockRendering.Debris(this, SpecialTreeTextures.Atlas, terrain, position, value, strength);
    }

    public abstract class SpecialTreeWood : TexturedTreeWoodBlock
    {
        internal const int BarkAtEnds = 8;
        private readonly TreeSpecies species;
        internal SpecialTreeWood(TreeSpecies species) : base(species.AtlasStart + 1, species.AtlasStart)
        {
            this.species = species;
        }
        protected override Texture2D Atlas => SpecialTreeTextures.Atlas;
        protected override int SaplingIndex => species.Sapling;

        public override int GetFaceTextureSlot(int face, int value)
            => (Terrain.ExtractData(value) & BarkAtEnds) != 0 ? species.AtlasStart : base.GetFaceTextureSlot(face, value);
    }

    public abstract class SpecialTreeLeaves : TexturedTreeLeavesBlock
    {
        internal TreeSpecies Species { get; }
        internal SpecialTreeLeaves(TreeSpecies species) { Species = species; }
        protected override Texture2D Atlas => SpecialTreeTextures.Atlas;

        public override int GetFaceTextureSlot(int face, int value)
        {
            int data = Terrain.ExtractData(value);
            if (Species.Fruit != 0 && face < 4 && (data & FruitStage.Owned) != 0)
            {
                int count = FruitStage.Count(data);
                if (count > 0) return Species.AtlasStart + TreeSpecies.FruitLeafSlot + count - 1;
            }

            return Species.AtlasStart + 2;
        }

        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel,
            List<BlockDropValue> drops, out bool debris)
        {
            int data = Terrain.ExtractData(oldValue);
            int fruits = (data & FruitStage.Owned) != 0 ? FruitStage.Count(data) : 0;
            if (Species.Fruit != 0 && fruits > 0)
            {
                drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(Species.Fruit), Count = fruits });
                debris = true;
                return;
            }
            base.GetDropValues(terrain, oldValue, newValue, toolLevel, drops, out debris);
        }
    }

    public abstract class SpecialTreeFruit : FlatBlock
    {
        private readonly PlantModel model = new PlantModel();
        private readonly TreeSpecies species;
        internal SpecialTreeFruit(TreeSpecies species) { this.species = species; }

        public override void Initialize()
        {
            model.Initialize(BlockIndex);
            base.Initialize();
        }

        public override int GetFaceTextureSlot(int face, int value) => species.AtlasStart + TreeSpecies.FruitSlot;

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner, int value, TerrainRaycastResult hit)
            => new BlockPlacementData { CellFace = hit.CellFace };

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
            => model.Draw(this, renderer, value, color, size, ref matrix, environment);
    }

    public sealed class TallPalmSaplingBlock : SpecialTreeSapling { public const int Index = 981; public TallPalmSaplingBlock() : base(TreeSpecies.Palm) { } }
    public sealed class TallPalmWoodBlock : SpecialTreeWood { public const int Index = 982; public TallPalmWoodBlock() : base(TreeSpecies.Palm) { } }
    public sealed class TallPalmLeavesBlock : SpecialTreeLeaves { public const int Index = 983; public TallPalmLeavesBlock() : base(TreeSpecies.Palm) { } }
    public sealed class CoconutSaplingBlock : SpecialTreeSapling { public const int Index = 984; public CoconutSaplingBlock() : base(TreeSpecies.Coconut) { } }
    public sealed class CoconutWoodBlock : SpecialTreeWood { public const int Index = 985; public CoconutWoodBlock() : base(TreeSpecies.Coconut) { } }
    public sealed class CoconutLeavesBlock : SpecialTreeLeaves { public const int Index = 986; public CoconutLeavesBlock() : base(TreeSpecies.Coconut) { } }
    public sealed class CoconutFruitBlock : SpecialTreeFruit { public const int Index = 987; public CoconutFruitBlock() : base(TreeSpecies.Coconut) { } }
    public sealed class BaobabSaplingBlock : SpecialTreeSapling { public const int Index = 988; public BaobabSaplingBlock() : base(TreeSpecies.Baobab) { } }
    public sealed class BaobabWoodBlock : SpecialTreeWood { public const int Index = 989; public BaobabWoodBlock() : base(TreeSpecies.Baobab) { } }
    public sealed class BaobabLeavesBlock : SpecialTreeLeaves { public const int Index = 990; public BaobabLeavesBlock() : base(TreeSpecies.Baobab) { } }
    public sealed class BaobabFruitBlock : SpecialTreeFruit { public const int Index = 991; public BaobabFruitBlock() : base(TreeSpecies.Baobab) { } }
    public sealed class SakuraSaplingBlock : SpecialTreeSapling { public const int Index = 992; public SakuraSaplingBlock() : base(TreeSpecies.Sakura) { } }
    public sealed class SakuraWoodBlock : SpecialTreeWood { public const int Index = 993; public SakuraWoodBlock() : base(TreeSpecies.Sakura) { } }
    public sealed class SakuraLeavesBlock : SpecialTreeLeaves { public const int Index = 994; public SakuraLeavesBlock() : base(TreeSpecies.Sakura) { } }
}
