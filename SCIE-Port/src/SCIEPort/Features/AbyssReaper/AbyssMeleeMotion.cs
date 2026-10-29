using System;
namespace SCIEPort.Features.AbyssReaper
{
    // The original combo owns cadence and impact edges. This layer owns reach and pose only.
    internal static class AbyssMeleeMotion
    {
        internal const float NormalReach=4.5f,ChargedReach=5.5f;
        internal struct Pose
        {
            internal float Pitch,Yaw,Roll,X,Y,Z;
            internal Pose(float pitch,float yaw,float roll,float x,float y,float z)
            { Pitch=pitch;Yaw=yaw;Roll=roll;X=x;Y=y;Z=z; }
        }
        private static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        private static Pose Blend(Pose a,Pose b,float t)
        {
            t=Smooth(t);
            return new Pose(a.Pitch+(b.Pitch-a.Pitch)*t,a.Yaw+(b.Yaw-a.Yaw)*t,a.Roll+(b.Roll-a.Roll)*t,
                a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);
        }
        internal static Pose Sample(int stage,float phase,float charge=0)
        {
            if(!WorldCutRules.Finite(phase)||!WorldCutRules.Finite(charge)||stage<0||stage>3)return default(Pose);
            if(charge>0)return Blend(default(Pose),new Pose(.65f,-.55f,-.85f,.18f,.12f,-.08f),charge);
            if(phase<0||phase>=1)return default(Pose);
            Pose ready,end;
            if(stage==0){ready=new Pose(.25f,-.9f,-1.1f,.22f,.10f,-.06f);end=new Pose(-.4f,1.2f,1.4f,-.42f,-.14f,-.14f);}
            else if(stage==1){ready=new Pose(-.2f,1f,1.1f,-.24f,.04f,-.06f);end=new Pose(.5f,-1.35f,-1.45f,.38f,-.12f,-.16f);}
            else if(stage==2){ready=new Pose(-.65f,-1.1f,-1.35f,.24f,-.10f,-.08f);end=new Pose(.8f,1.45f,1.55f,-.42f,.16f,-.18f);}
            else{ready=new Pose(1.05f,-.65f,-.8f,.18f,.20f,-.10f);end=new Pose(-1.2f,.8f,1.45f,-.34f,-.24f,-.22f);}
            if(phase<.22f)return Blend(default(Pose),ready,phase/.22f);
            if(phase<.60f)return Blend(ready,end,(phase-.22f)/.38f);
            return Blend(end,default(Pose),(phase-.60f)/.40f);
        }
        internal static bool Sector(float distance,float dot,float vertical,int stage)
        {
            return stage>=0&&stage<=3&&WorldCutRules.Finite(distance)&&WorldCutRules.Finite(dot)&&WorldCutRules.Finite(vertical)
                &&distance>=0&&distance<=(stage==3?ChargedReach:NormalReach)
                &&dot>=(stage==2?-.2f:stage==3?.1f:0f)&&Math.Abs(vertical)<2.2f;
        }
        internal static float TrailAngle(int stage,float phase)
        {
            float t=Smooth((phase-.22f)/.38f);
            return stage==1?1.6f-3.2f*t:-1.6f+3.2f*t;
        }
    }
}
