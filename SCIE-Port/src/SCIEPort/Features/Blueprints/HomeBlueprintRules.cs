using SCIEPort.Features.Home;

namespace SCIEPort.Features.Blueprints
{
    /// <summary>Single-cell, non-storage furniture only. Paired and entity-backed items stay excluded.</summary>
    internal static class HomeBlueprintRules
    {
        internal static readonly string[] Types = { "BookshelfBlock", "BookPileBlock", "HomeSofaBlock", "HomeCoffeeTableBlock" };
        internal static int Id(string type) => type == "BookshelfBlock" ? HomeRules.Bookshelf : type == "BookPileBlock" ? HomeRules.BookPile : type == "HomeSofaBlock" ? HomeRules.Sofa : type == "HomeCoffeeTableBlock" ? HomeRules.Table : -1;
        internal static bool Valid(string type, int data)
        {
            int id = Id(type);
            return id >= 0 && data >= 0 && data < 64 && (data & 4) == 0 && HomeRules.Variant(data) < HomeRules.VariantCount(id)
                && (id != HomeRules.Sofa || HomeRules.Variant(data) == 0 || HomeRules.Variant(data) == 2);
        }
        // Canonical facing/module reflection. Intrinsic book patterns and atlas graphics remain unchanged.
        internal static int Mirror(int data)
        {
            return (data & ~3) | ((-data) & 3);
        }
    }
}
