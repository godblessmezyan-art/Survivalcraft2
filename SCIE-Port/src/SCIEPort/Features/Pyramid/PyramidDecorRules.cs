using System;

namespace SCIEPort.Features.Pyramid
{
    /// <summary>
    /// Egyptian tomb decor shares block index 886. Kind/variant/state/rotation live in DATA:
    /// kind bits 0-4 (0-8), variant bits 5-6, state bit 7, rotation bits 8-9. DATA has 18 bits.
    /// </summary>
    internal static class PyramidDecorRules
    {
        internal const int Index = 886;
        internal const int LegacyIndex = 947;
        internal const int Sarcophagus = 0, CanopicJar = 1, StorageJar = 2, Shabti = 3, AnubisStatue = 4;
        internal const int OfferingTable = 5, Brazier = 6, BookOfDead = 7, Obelisk = 8, KindCount = 9;
        internal const int KindMask = 31, VariantShift = 5, VariantMask = 3, StateBit = 128, RotationShift = 8, RotationMask = 3;

        internal static int Kind(int data) => data & KindMask;
        internal static int Variant(int data) => (data >> VariantShift) & VariantMask;
        internal static int Rotation(int data) => (data >> RotationShift) & RotationMask;
        internal static bool IsOpen(int data) => (data & StateBit) != 0;
        internal static bool IsSwitchable(int kind) => kind == Sarcophagus || kind == Brazier;
        internal static int VariantCount(int kind) => kind == CanopicJar ? 4 : 1;
        internal static int Encode(int kind, int variant, bool open, int rotation)
            => (kind & KindMask) | ((variant & VariantMask) << VariantShift) | (open ? StateBit : 0) | ((rotation & RotationMask) << RotationShift);
        internal static int Normalize(int data)
        {
            int kind = Kind(data);
            if (kind >= KindCount) kind = Sarcophagus;
            int variant = Variant(data);
            if (variant >= VariantCount(kind)) variant = 0;
            int rotation = Rotation(data);
            return Encode(kind, variant, IsSwitchable(kind) && IsOpen(data), rotation);
        }
        internal static int ItemData(int data) => Normalize(data) & ~(RotationMask << RotationShift);
        internal static string Model(int data)
        {
            int kind = Kind(Normalize(data)), variant = Variant(data);
            switch (kind)
            {
                case Sarcophagus: return "PyramidSarcophagus" + (IsOpen(data) ? "Open" : "");
                case CanopicJar: return new[] { "PyramidCanopicHuman", "PyramidCanopicBaboon", "PyramidCanopicJackal", "PyramidCanopicFalcon" }[variant];
                case StorageJar: return "PyramidStorageJar";
                case Shabti: return "PyramidShabti";
                case AnubisStatue: return "PyramidAnubisStatue";
                case OfferingTable: return "PyramidOfferingTable";
                case Brazier: return "PyramidBrazier" + (IsOpen(data) ? "Lit" : "");
                case BookOfDead: return "PyramidBookOfDead";
                case Obelisk: return "PyramidObelisk";
                default: throw new ArgumentOutOfRangeException(nameof(data));
            }
        }
        internal static string Name(int data)
        {
            int kind = Kind(Normalize(data));
            switch (kind)
            {
                case Sarcophagus: return IsOpen(data) ? "开启的石棺" : "石棺";
                case CanopicJar: return new[] { "卡诺匹斯罐·人首", "卡诺匹斯罐·狒狒首", "卡诺匹斯罐·豺狼首", "卡诺匹斯罐·隼首" }[Variant(data)];
                case StorageJar: return "陶罐";
                case Shabti: return "沙伯提俑";
                case AnubisStatue: return "阿努比斯像";
                case OfferingTable: return "供桌";
                case Brazier: return IsOpen(data) ? "燃烧的火盆" : "火盆";
                case BookOfDead: return "亡灵书";
                case Obelisk: return "方尖碑";
                default: return "埃及饰物";
            }
        }
    }
}
