using System;
using System.Collections.Generic;
namespace SCIEPort.Features.AbyssReaper
{
    internal static class WorldCutHudLayout
    {
        internal const float Width=280,Height=108,Slot=64;
        internal struct Rect
        {
            internal float X,Y,W,H;
            internal Rect(float x,float y,float w,float h) { X=x;Y=y;W=w;H=h; }
            internal bool Overlaps(Rect b) => X<b.X+b.W+4 && X+W+4>b.X && Y<b.Y+b.H+4 && Y+H+4>b.Y;
        }
        internal static bool Place(float width,float height,IList<Rect> obstacles,out Rect result)
        {
            result=default(Rect);
            if(!WorldCutRules.Finite(width)||!WorldCutRules.Finite(height)||width<Width+16||height<Height+16)return false;
            float x=(width-Width)/2;
            for(int row=0;row<3;row++)
            {
                var candidate=new Rect(x,8+row*(Height+8),Width,Height);
                if(candidate.Y+Height>height-8)continue;
                bool free=true;foreach(var obstacle in obstacles)if(candidate.Overlaps(obstacle)){free=false;break;}
                if(free){result=candidate;return true;}
            }
            return false;
        }
    }
}
