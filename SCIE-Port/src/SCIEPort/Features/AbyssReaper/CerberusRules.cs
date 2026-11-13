namespace SCIEPort.Features.AbyssReaper
{
    internal static class CerberusRules
    {
        internal const string Template="SCIEPortCerberus",Name="地狱三头犬·刻尔柏洛斯";
        internal const ushort SummonKind=2;
        /// <summary>Model bone-matrix capacity. Raised through the loader hook, not a draw-instance count.</summary>
        internal const int BoneCapacity=24;
        internal const float Damage=18,TailDamage=30,Range=3,TailRange=3.2f;
        internal const double Cooldown=1.6,TailCooldown=6,ImpactDelay=.24,AnimationDuration=1.35;
        internal const double TailImpactDelay=.45,TailDuration=1.1,RoarDuration=2.4,IntroDuration=6.5;
        internal const float RageFraction=.35f;
        /// <summary>Left, right, centre head impact times, relative to one bite cycle.</summary>
        internal static readonly double[] BiteDelays={.24,.52,.80};
        /// <summary>Left, right, centre roar onsets.</summary>
        internal static readonly double[] RoarDelays={0,.55,1.15};
    }
}
