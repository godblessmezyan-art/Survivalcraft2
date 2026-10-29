using System;
using System.Collections.Generic;
namespace SCIEPort.Features.AbyssReaper
{
    // Logical UI coordinates, mirrored around the actual native Look control.
    internal static class WorldCutHudLayout
    {
        internal const float Width=176,Height=176,Slot=72,Ultimate=88;
        internal struct Rect
        {
            internal float X,Y,W,H;
            internal Rect(float x,float y,float w,float h) { X=x;Y=y;W=w;H=h; }
            internal bool Valid => WorldCutRules.Finite(X)&&WorldCutRules.Finite(Y)&&WorldCutRules.Finite(W)&&WorldCutRules.Finite(H)&&W>1&&H>1;
            internal bool Overlaps(Rect b) => b.Valid && X<b.X+b.W+4 && X+W+4>b.X && Y<b.Y+b.H+4 && Y+H+4>b.Y;
        }
        internal struct Placement
        {
            internal Rect Portal,Cut,Blink,Horizon;
            internal Rect At(int index) => index==0?Portal:index==1?Cut:index==2?Blink:Horizon;
        }
        internal static bool Circle(float x,float y,float w,float h)
        {
            if(!WorldCutRules.Finite(x)||!WorldCutRules.Finite(y)||!WorldCutRules.Finite(w)||!WorldCutRules.Finite(h)||w<=0||h<=0)return false;
            float dx=(x-w*.5f)/(w*.48f),dy=(y-h*.5f)/(h*.48f);
            return dx*dx+dy*dy<=1;
        }
        private static Rect Local(int index,bool mirror)
        {
            float size=index==3?Ultimate:Slot;
            float x=index%2==0?0:88,y=index<2?104:index==2?16:0;
            if(mirror)x=Width-x-size;
            return new Rect(x,y,size,size);
        }
        internal static bool Place(float width,float height,Rect look,IList<Rect> obstacles,bool left,out Placement result)
        {
            result=default(Placement);
            if(!WorldCutRules.Finite(width)||!WorldCutRules.Finite(height)||width<Width+16||height<Height+16||obstacles==null||obstacles.Count>256)return false;
            bool validLook=look.Valid&&look.X>=0&&look.Y>=0&&look.X+look.W<=width+1&&look.Y+look.H<=height+1;
            bool mirror=left || validLook&&look.X+look.W*.5f<width*.5f;
            float anchorX=validLook?(mirror?look.X+look.W+12:look.X-12-Width):(mirror?24:width-Width-24);
            float anchorY=validLook?look.Y+look.H*.55f-140:height-Height-96;
            // Bounded search: prefer Look-adjacent and lift above native controls.
            for(int inward=0;inward<6;inward++)for(int rise=0;rise<18;rise++)
            {
                float x=Math.Max(8,Math.Min(width-Width-8,anchorX+(mirror?1:-1)*inward*40));
                float y=Math.Max(8,Math.Min(height-Height-8,anchorY-rise*16));
                var candidate=new Placement();bool safe=true;
                for(int i=0;i<4;i++)
                {
                    Rect r=Local(i,mirror);r.X+=x;r.Y+=y;
                    if(validLook&&r.Overlaps(look)){safe=false;break;}
                    foreach(var obstacle in obstacles)if(r.Overlaps(obstacle)){safe=false;break;}
                    if(!safe)break;
                    if(i==0)candidate.Portal=r;else if(i==1)candidate.Cut=r;else if(i==2)candidate.Blink=r;else candidate.Horizon=r;
                }
                if(safe){result=candidate;return true;}
            }
            return false;
        }
    }
}
