using Game;
using BuilderAssistant.Core.Blueprints;

namespace BuilderAssistant.Features.WorldEdit
{
    /// <summary>Audited support predicates, shared by the live gateway and native ABI probes.</summary>
    internal static class VanillaSupportRules
    {
        public static bool Supports(Block block,SubsystemTerrain terrain,int value,BlockFamily family,int face)
        {
            if(block==null)return false;
            switch (family)
            {
                case BlockFamily.Ladder: return !block.IsFaceTransparent(terrain, face, value);
                case BlockFamily.Torch: return block.IsCollidable;
                case BlockFamily.Falling:
                    // Conservative native support subset: full upper face, or native farmland.
                    return !(block is FluidBlock) && (!(block is TrapdoorBlock) &&
                        (!block.IsFaceTransparent(terrain, 4, value) || block is SoilBlock));
                case BlockFamily.Plant: return block is DirtBlock || block is GrassBlock || block is SandBlock || block is SoilBlock;
                case BlockFamily.Snow: return SubsystemSnowBlockBehavior.CanSupportSnow(value);
                case BlockFamily.Carpet:
                case BlockFamily.GroundCover:
                case BlockFamily.Gravestone:
                case BlockFamily.Cairn: return !block.IsTransparent;
                case BlockFamily.Lantern: return !block.IsFaceTransparent(terrain, 4, value) || block is FenceBlock;
                default: return false;
            }
        }
    }
}
