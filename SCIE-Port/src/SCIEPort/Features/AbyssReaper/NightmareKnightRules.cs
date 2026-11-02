using System;
namespace SCIEPort.Features.AbyssReaper
{
    internal static class NightmareKnightRules
    {
        internal const string Template="SCIEPortNightmareKnight",Name="夜魇骑士";
        internal const ushort SummonKind=4;
        internal const float Health=70,Damage=15,GroundSpeed=8,ChargeSpeed=10,Range=2.65f,StopRange=2.2f;
        internal const double Cooldown=1.2,ImpactDelay=.22,SwingDuration=.64;
        internal static bool ChargeDistance(float distance)=>WorldCutRules.Finite(distance)&&distance>=8&&distance<=16;
        internal static float SwordAngle(double age)
        {
            if(age<0||age>=SwingDuration)return -.12f;
            if(age<.12)return -.12f+.62f*(float)(age/.12);
            if(age<.26)return .5f-1.3f*(float)((age-.12)/.14);
            return -.8f+.68f*(float)((age-.26)/(SwingDuration-.26));
        }
    }
}
