using System.Collections.Generic;
using Game;

namespace SCIEPort.Features.FourSaints
{
    /// <summary>
    /// Null-safe replacement for SCNET x25.01.17's sparse clothing enumeration.
    /// Loading, rendering and durability remain implemented by the verified base block.
    /// </summary>
    public sealed class ClothingBlock : Game.ClothingBlock
    {
        /// <summary>Preserves the native clothing block ID and all existing save values.</summary>
        public new const int Index = 203;

        /// <summary>Enumerates native and mod clothing without dereferencing sparse null slots.</summary>
        public override IEnumerable<int> GetCreativeValues()
        {
            foreach (ClothingData clothingData in FourSaintsClothingRules.NonNullInDisplayOrder(
                m_clothingData,
                item => item.DisplayIndex))
            {
                int colorsCount = clothingData.CanBeDyed ? 16 : 1;
                for (int color = 0; color < colorsCount; color++)
                {
                    int data = SetClothingColor(SetClothingIndex(0, clothingData.Index), color);
                    yield return Terrain.MakeBlockValue(Index, 0, data);
                }
            }
        }
    }
}
