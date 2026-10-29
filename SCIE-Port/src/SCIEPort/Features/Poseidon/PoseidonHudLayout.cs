using System;
using System.Collections.Generic;
using Rect=SCIEPort.Features.Gungnir.GungnirHudLayout.Rect;
namespace SCIEPort.Features.Poseidon
{
    internal static class PoseidonHudLayout
    {
        internal struct Placement
        {
            internal Rect Shield,Wave,Domain;
            internal Rect At(int i)=>i==0?Shield:i==1?Wave:Domain;
        }
        internal static bool Circle(float x,float y,float w,float h)
        {
            if(!PoseidonRules.Finite(x)||!PoseidonRules.Finite(y)||w<=0||h<=0)return false;
            float a=(x-w*.5f)/(w*.46f),b=(y-h*.5f)/(h*.46f);return a*a+b*b<=1;
        }
        internal static bool Place(float w,float h,Rect look,IList<Rect> obstacles,bool left,out Placement result)
        {
            result=default(Placement);
            if(!PoseidonRules.Finite(w)||!PoseidonRules.Finite(h)||w<320||h<240||!look.IsValid||obstacles==null||obstacles.Count>256)return false;
            bool mirror=left||look.X+look.Width*.5f<w*.5f;
            // An arc on the inward side of native Look: left, upper-left, above.
            for(int inward=0;inward<5;inward++)for(int rise=0;rise<7;rise++)
            {
                float cx=look.X+look.Width*.5f,cy=look.Y+look.Height*.5f;
                var p=new Placement();bool safe=true;
                for(int i=0;i<3;i++)
                {
                    float size=i==2?84:68,dx=i==0?look.Width*.5f+48:i==1?look.Width*.5f+44:8;
                    float dy=i==0?0:i==1?-92:-look.Height*.5f-58;
                    var r=new Rect{X=cx+(mirror?1:-1)*(dx+inward*16)-size*.5f,Y=cy+dy-rise*12-size*.5f,Width=size,Height=size};
                    if(r.X<8||r.Y<8||r.Right>w-8||r.Bottom>h-8||r.Intersects(look)){safe=false;break;}
                    foreach(var o in obstacles)if(o.IsValid&&r.Intersects(o)){safe=false;break;}
                    if(!safe)break;
                    for(int j=0;j<i;j++)if(r.Intersects(p.At(j))){safe=false;break;}
                    if(!safe)break;
                    if(i==0)p.Shield=r;else if(i==1)p.Wave=r;else p.Domain=r;
                }
                if(safe){result=p;return true;}
            }
            return false;
        }
    }
}
