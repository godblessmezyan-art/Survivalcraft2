#pragma warning disable CS1591

using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Geology
{
    public sealed class SaltCrystalItemBlock : FlatBlock
    {
        public const int Index = 898;

        public override void DrawBlock(PrimitivesRenderer3D renderer, int value, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
            => BlocksManager.DrawFlatBlock(renderer, value, size, ref matrix, GeologyCube.Atlas, color, false, environment);

        public override BlockPlacementData GetPlacementValue(SubsystemTerrain terrain, ComponentMiner miner,
            int value, TerrainRaycastResult hit) => default;

        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            if (!(BlocksManager.Blocks[SaltCrustBlock.Index] is SaltCrustBlock crust) ||
                string.IsNullOrEmpty(crust.CraftingId)) yield break;

            yield return new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(Index),
                ResultCount = 2,
                Ingredients = new[] { crust.CraftingId, null, null, null, null, null, null, null, null },
                Description = "从白盐壳中取出盐晶。"
            };
        }
    }

    public sealed class SaltCrystalLampBlock : GeologyCube
    {
        public const int Index = 899;

        public override int GetFaceTextureSlot(int face, int value)
        {
            if (face == 4) return 14;
            if (face == 5) return 2;
            return base.GetFaceTextureSlot(face, value);
        }

        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            if (!(BlocksManager.Blocks[SaltCrystalItemBlock.Index] is SaltCrystalItemBlock crystal) ||
                !(BlocksManager.Blocks[BasaltPillarBlock.Index] is BasaltPillarBlock basalt) ||
                !(BlocksManager.Blocks[TorchBlock.Index] is TorchBlock torch) ||
                string.IsNullOrEmpty(crystal.CraftingId) || string.IsNullOrEmpty(basalt.CraftingId) ||
                string.IsNullOrEmpty(torch.CraftingId)) yield break;

            yield return new CraftingRecipe
            {
                ResultValue = Terrain.MakeBlockValue(Index),
                ResultCount = 1,
                Ingredients = new[] { crystal.CraftingId, torch.CraftingId, crystal.CraftingId,
                    null, basalt.CraftingId, null, null, null, null },
                Description = "嵌在玄武岩底座上的盐晶灯。"
            };
        }
    }
}
