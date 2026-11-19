#pragma warning disable CS1591

using System.Collections.Generic;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Plants;

namespace SCIEPort.Features.Apple
{
    public sealed class AppleWoodBlock : TexturedTreeWoodBlock
    {
        public const int Index = 978;

        public AppleWoodBlock() : base(AppleTextures.EndGrainSlot, AppleTextures.BarkSlot) { }

        protected override Texture2D Atlas => AppleTextures.Atlas;
        protected override int SaplingIndex => ApplePlantBlock.Index;
    }

    public abstract class AppleCanopyBlock : TexturedTreeLeavesBlock
    {
        protected override Texture2D Atlas => AppleTextures.Atlas;
    }

    public sealed class AppleLeavesBlock : AppleCanopyBlock
    {
        public const int Index = 979;

        public override int GetFaceTextureSlot(int face, int value)
        {
            int stage = AppleRules.GetSize(Terrain.ExtractData(value));
            if (face >= 4) return AppleTextures.LeavesSlot;
            if (stage == 1) return AppleTextures.BlossomSlot;
            return stage >= 3 ? AppleTextures.GreenFruitSlot : AppleTextures.LeavesSlot;
        }
    }

    public sealed class AppleFruitingLeavesBlock : AppleCanopyBlock
    {
        public const int Index = 980;

        public override int GetFaceTextureSlot(int face, int value)
            => face < 4 ? AppleTextures.RipeFruitSlot : AppleTextures.LeavesSlot;

        public override void GetDropValues(SubsystemTerrain terrain, int oldValue, int newValue, int toolLevel,
            List<BlockDropValue> drops, out bool debris)
        {
            drops.Add(new BlockDropValue { Value = Terrain.MakeBlockValue(AppleBlock.Index), Count = AppleRules.GetAppleYield() });
            debris = true;
        }
    }
}
