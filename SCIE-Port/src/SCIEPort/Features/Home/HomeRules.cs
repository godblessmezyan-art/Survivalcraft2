using System;

namespace SCIEPort.Features.Home
{
    /// <summary>Stable furniture DATA: rotation 0..1, open 2, variant 3..5, upper part 6.</summary>
    internal static class HomeRules
    {
        internal const int First = 957, Last = 962, Upper = 963;
        internal const int Fridge = 957, Cabinet = 958, Sink = 959, Sofa = 960, Table = 961, Curtain = 962;
        internal const int StateBit = 4, UpperBit = 64, Slots = 16;
        internal static bool IsHome(int contents) => contents >= First && contents <= Last;
        internal static bool IsTall(int contents) => contents == Fridge || contents == Curtain;
        internal static bool IsStorage(int contents) => contents == Fridge || contents == Cabinet;
        internal static int VariantCount(int contents) => contents == Cabinet ? 4 : contents == Sofa ? 5 : 1;
        internal static int Variant(int data) => (data >> 3) & 7;
        internal static int Rotation(int data) => data & 3;
        internal static bool IsOpen(int data) => (data & StateBit) != 0;
        internal static int Facing(float x, float z) => Math.Abs(x) > Math.Abs(z) ? (x > 0 ? 1 : 3) : (z > 0 ? 0 : 2);
        internal static int Normalize(int contents, int data)
        {
            int variant = Variant(data);
            if (variant >= VariantCount(contents)) variant = 0;
            int state = contents == Fridge || contents == Sink || contents == Curtain ? data & StateBit : 0;
            return Rotation(data) | state | (variant << 3);
        }
        internal static int ItemData(int contents, int data) => Normalize(contents, data) & 56;
        internal static int UpperData(int contents, int data) => Normalize(contents, data) | (contents == Curtain ? UpperBit : 0);
        internal static int LowerId(int data) => (data & UpperBit) != 0 ? Curtain : Fridge;
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
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }
    }
}
