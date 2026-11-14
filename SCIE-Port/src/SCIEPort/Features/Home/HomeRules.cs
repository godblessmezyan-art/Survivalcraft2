using System;

namespace SCIEPort.Features.Home
{
    /// <summary>Stable furniture DATA: rotation 0..1, open 2, variant 3..5, upper kind 6/7.</summary>
    internal static class HomeRules
    {
        internal const int First = 957, Last = 962, Upper = 963;
        internal const int Fridge = 957, Cabinet = 958, Sink = 959, Sofa = 960, Table = 961, Curtain = 962;
        internal const int Fireplace = 964, Dining = 965, Bed = 966, Nightstand = 967, WallArt = 968;
        internal const int DiningPart = 969, BedPart = 970;
        internal const int Bookshelf = 973, BookshelfTall = 974, Bookcase = 975, BookPile = 976, BookcasePart = 977;
        internal const int StateBit = 4, UpperBit = 64, ShelfUpperBit = 128, Slots = 16;
        // Keep the published physical/save layout. Only the first half accepts deposits.
        internal const int UsableSlots = (Slots + 1) / 2;
        internal static bool IsHome(int contents) => contents >= First && contents <= Last || contents >= Fireplace && contents <= WallArt || contents >= Bookshelf && contents <= BookPile;
        internal static bool IsTall(int contents) => contents == Fridge || contents == Curtain || contents == BookshelfTall;
        internal static bool IsStorage(int contents) => contents == Fridge || contents == Cabinet || contents == Nightstand;
        internal static bool IsSwitchable(int contents) => contents == Fridge || contents == Sink || contents == Curtain || contents == Fireplace;
        internal static bool HasFootprint(int contents) => contents == Dining || contents == Bed || contents == Bookcase;
        internal static bool IsFootprintPart(int contents) => contents == DiningPart || contents == BedPart || contents == BookcasePart;
        internal static int PartId(int contents) => contents == Bed ? BedPart : contents == Dining ? DiningPart : contents == Bookcase ? BookcasePart : 0;
        internal static int PartRootId(int contents) => contents == BedPart ? Bed : contents == DiningPart ? Dining : contents == BookcasePart ? Bookcase : 0;
        internal static int VariantCount(int contents) => contents == Cabinet ? 4 : contents == Sofa ? 5 : contents == WallArt || contents == Bookshelf ? 3 : contents == BookshelfTall ? 2 : contents == BookPile ? 4 : 1;
        internal static int Variant(int data) => (data >> 3) & 7;
        internal static int Rotation(int data) => data & 3;
        internal static bool IsOpen(int data) => (data & StateBit) != 0;
        internal static int Facing(float x, float z) => Math.Abs(x) > Math.Abs(z) ? (x > 0 ? 1 : 3) : (z > 0 ? 0 : 2);
        internal static int Normalize(int contents, int data)
        {
            int variant = Variant(data);
            if (variant >= VariantCount(contents)) variant = 0;
            int state = IsSwitchable(contents) ? data & StateBit : 0;
            return Rotation(data) | state | (variant << 3);
        }
        internal static int ItemData(int contents, int data) => Normalize(contents, data) & 56;
        internal static int UpperData(int contents, int data) => Normalize(contents, data) | (contents == Curtain ? UpperBit : contents == BookshelfTall ? ShelfUpperBit : 0);
        internal static int LowerId(int data) => (data & ShelfUpperBit) != 0 ? BookshelfTall : (data & UpperBit) != 0 ? Curtain : Fridge;
        internal static bool Pair(int lowerId, int lowerData, int upperId, int upperData)
            => IsTall(lowerId) && upperId == Upper && LowerId(upperData) == lowerId
                && (upperData & 63) == Normalize(lowerId, lowerData);
        internal static string Model(int id, int data)
        {
            int variant = Variant(Normalize(id, data));
            string suffix = IsOpen(data) ? "Open" : "";
            switch (id)
            {
                case Fridge: return "HomeFridge" + suffix;
                case Cabinet: return "HomeCabinet" + variant;
                case Sink: return "HomeSink" + suffix;
                case Sofa: return "HomeSofa" + variant;
                case Table: return "HomeCoffeeTable";
                case Curtain: return "HomeCurtain" + suffix;
                case Fireplace: return "HomeFireplace" + (IsOpen(data) ? "On" : "");
                case Dining: return "HomeDiningSet";
                case Bed: return "HomeDoubleBed";
                case Nightstand: return "HomeNightstand";
                case WallArt: return "HomeWallArt" + variant;
                case Bookshelf: return "Bookshelf" + variant;
                case BookshelfTall: return "BookshelfTall" + variant;
                case Bookcase: return "Bookcase";
                case BookPile: return "BookPile" + variant;
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }
    }
}
