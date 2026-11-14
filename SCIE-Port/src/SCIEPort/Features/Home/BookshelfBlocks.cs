using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Home
{
    /// <summary>Decorative bookshelf family rendered from procedural meshes with a white one-pixel texture.</summary>
    public abstract class BookshelfBlockBase : HomeBlock
    {
        private Texture2D texture;
        /// <inheritdoc/>
        protected override Texture2D CreateTexture()
        {
            if (texture == null)
            {
                texture = new Texture2D(1, 1, 1, ColorFormat.Rgba8888);
                texture.SetData(0, new byte[] { 255, 255, 255, 255 });
            }
            return texture;
        }
        /// <inheritdoc/>
        protected override BlockMesh CreateMesh(int data, int rotation) => BookshelfGeometry.Build(BlockIndex, data, rotation);
        /// <summary>Resolves the four 2x2 slots, or null when a native material is unavailable.</summary>
        protected static string[] Materials(int planks, int sticks, int cotton)
        {
            string plank = Ingredient("PlanksBlock"), stick = Ingredient("StickBlock"), cottonId = Ingredient("CottonWadBlock");
            if (planks > 0 && string.IsNullOrEmpty(plank) || sticks > 0 && string.IsNullOrEmpty(stick) || cotton > 0 && string.IsNullOrEmpty(cottonId)) return null;
            var ingredients = new List<string>(4);
            for (int i = 0; i < planks; i++) ingredients.Add(plank);
            for (int i = 0; i < sticks; i++) ingredients.Add(stick);
            for (int i = 0; i < cotton; i++) ingredients.Add(cottonId);
            while (ingredients.Count < 4) ingredients.Add(string.Empty);
            return ingredients.ToArray();
        }
        private static string Ingredient(string typeName)
        {
            Block block = BlocksManager.FindBlockByTypeName(typeName, false);
            return block == null ? string.Empty : block.GetCraftingId(0);
        }
        /// <summary>Single-output 2x2 recipe with no heat or level requirement.</summary>
        protected static CraftingRecipe Recipe(int index, string[] ingredients, string description) => new CraftingRecipe
        {
            ResultValue = Terrain.MakeBlockValue(index),
            ResultCount = 1,
            RemainsValue = 0,
            RemainsCount = 0,
            RequiredHeatLevel = 0f,
            RequiredPlayerLevel = 0f,
            Ingredients = ingredients,
            Description = description,
            Message = string.Empty,
            IsAdHocCraftingRecipe = false
        };
    }
    /// <summary>Single-cell decorative bookshelf with three book densities.</summary>
    public sealed class BookshelfBlock : BookshelfBlockBase
    {
        /// <summary>Content index.</summary>
        public const int Index = HomeRules.Bookshelf;
        /// <inheritdoc/>
        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            string[] ingredients = Materials(3, 1, 0);
            if (ingredients != null) yield return Recipe(Index, ingredients, GetDisplayName(null, 0));
        }
    }
    /// <summary>Two-cell decorative bookshelf; the upper cell uses the shared hidden occupancy block.</summary>
    public sealed class BookshelfTallBlock : BookshelfBlockBase
    {
        /// <summary>Content index.</summary>
        public const int Index = HomeRules.BookshelfTall;
        /// <inheritdoc/>
        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            string[] ingredients = Materials(2, 2, 0);
            if (ingredients != null) yield return Recipe(Index, ingredients, GetDisplayName(null, 0));
        }
    }
    /// <summary>Two-by-two decorative bookcase; three hidden cells reserve its footprint.</summary>
    public sealed class BookcaseBlock : BookshelfBlockBase
    {
        /// <summary>Content index.</summary>
        public const int Index = HomeRules.Bookcase;
        /// <inheritdoc/>
        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            string[] ingredients = Materials(3, 0, 1);
            if (ingredients != null) yield return Recipe(Index, ingredients, GetDisplayName(null, 0));
        }
    }
    /// <summary>Single-cell pile of decorative books in four arrangements.</summary>
    public sealed class BookPileBlock : BookshelfBlockBase
    {
        /// <summary>Content index.</summary>
        public const int Index = HomeRules.BookPile;
        /// <inheritdoc/>
        public override IEnumerable<CraftingRecipe> GetProceduralCraftingRecipes()
        {
            string[] ingredients = Materials(1, 0, 1);
            if (ingredients != null) yield return Recipe(Index, ingredients, GetDisplayName(null, 0));
        }
    }
    /// <summary>Invisible occupancy cell of the two-by-two bookcase; never an independent item.</summary>
    public sealed class BookcasePartBlock : HomeFootprintPartBlock
    {
        /// <summary>Content index.</summary>
        public const int Index = HomeRules.BookcasePart;
    }
}
