using System;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>Game-clock combat state, independent of particles and rendering.</summary>
    public sealed class AbyssCombo
    {
        /// <summary>Current swing: 0..2 normal; 3 charged.</summary>
        public int Stage {get;private set;}
        /// <summary>Normalized animation phase.</summary>
        public float Phase(double now) => Duration>0 ? (float)Math.Max(0,Math.Min(1,(now-start)/Duration)) : 1;
        /// <summary>Current animation duration.</summary>
        public double Duration {get;private set;}
        double start=-100,press=-1,last=-100; int next; bool down,impacted=true;
        /// <summary>Only a released press starts a swing; a held press becomes charged.</summary>
        public void Input(bool held,double now,float elapsedHold=0) {
            if(held&&!down)press=Math.Max(0,now-Math.Max(0,Math.Min(5,elapsedHold)));
            if(!held&&down&&press>=0&&now>=start+Duration){
                Stage=now-press>=.65?3:(now-last>1.05?0:next);
                Duration=Stage==0?.35:Stage==1?.42:Stage==2?.58:.85;
                start=now;last=now+Duration;next=Stage==3?0:(Stage+1)%3;impacted=false;
            }
            down=held;if(!held)press=-1;
        }
        /// <summary>One impact edge per swing even when frames skip the impact instant.</summary>
        public bool Impact(double now){if(!impacted&&now>=start+Duration*.48){impacted=true;return true;}return false;}
        /// <summary>Charging feedback.</summary>
        public float Charge(double now)=>down&&press>=0?(float)Math.Min(1,(now-press)/.65):0;
        /// <summary>Sector check uses 3D range, horizontal arc and vertical reach.</summary>
        public static bool Sector(float distance,float dot,float vertical,int stage)=>distance<= (stage==3?3.5f:3f)&&dot>=(stage==2?.65f:.25f)&&Math.Abs(vertical)<1.7f;
    }
}
