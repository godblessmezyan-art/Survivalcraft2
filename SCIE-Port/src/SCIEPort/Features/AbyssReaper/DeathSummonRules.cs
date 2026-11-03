using System;
namespace SCIEPort.Features.AbyssReaper
{
    internal static class DeathSummonRules
    {
        internal const ushort Failure=ushort.MaxValue;
        // Exactly one uniform host roll in [0,100); unique-unit collisions never reroll.
        internal static ushort Kind(int roll)
        {
            if(roll<0||roll>=100)throw new ArgumentOutOfRangeException(nameof(roll));
            if(roll<5)return CerberusRules.SummonKind;
            if(roll<15)return BoneDragonRules.SummonKind;
            if(roll<30)return NightmareKnightRules.SummonKind;
            if(roll<50)return 1;
            if(roll<80)return 0;
            return Failure;
        }
    }
}
