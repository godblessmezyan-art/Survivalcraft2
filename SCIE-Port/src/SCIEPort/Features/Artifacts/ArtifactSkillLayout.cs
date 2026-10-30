using System;
using System.Collections.Generic;
namespace SCIEPort.Features.Artifacts
{
    // All coordinates are logical UI units. Last slot is the ultimate.
    internal static class ArtifactSkillLayout
    {
        internal struct Rect
        {
            internal float X,Y,W,H;
            internal bool Valid => W>1 && H>1;
            internal bool Overlaps(Rect b) => b.Valid && X<b.X+b.W+4 && X+W+4>b.X && Y<b.Y+b.H+4 && Y+H+4>b.Y;
        }
        internal static bool Place(float w,float h,Rect look,IList<Rect> obstacles,float[] sizes,bool left,out Rect[] result)
        {
            result=null;
            if(float.IsNaN(w)||float.IsInfinity(w)||float.IsNaN(h)||float.IsInfinity(h)||w<240||h<240||!look.Valid||obstacles==null||obstacles.Count>256||sizes==null||sizes.Length<1||sizes.Length>4)return false;
            bool mirror=left||look.X+look.W*.5f<w*.5f;
            for(int inward=0;inward<6;inward++)for(int rise=0;rise<18;rise++)
            {
                var candidate=new Rect[sizes.Length];bool safe=true;
                for(int i=0;i<sizes.Length;i++)
                {
                    float dx=52,dy=sizes.Length==1?0:i==0?15:-85;
                    if(sizes.Length==3){dx=i==1?102:52;dy=i==0?30:i==1?-55:-146;}
                    if(sizes.Length==4){dx=i==1||i==2?152:60;dy=i==0?30:i==1?-10:i==2?-94:-150;}
                    float size=sizes[i];
                    var b=new Rect{X=(mirror?look.X+look.W:look.X)+(mirror?1:-1)*(dx+inward*16)-size*.5f,Y=look.Y+look.H*.5f+dy-rise*12-size*.5f,W=size,H=size};
                    if(b.X<8||b.Y<8||b.X+b.W>w-8||b.Y+b.H>h-8||b.Overlaps(look)){safe=false;break;}
                    foreach(var o in obstacles)if(b.Overlaps(o)){safe=false;break;}
                    for(int j=0;j<i;j++)if(b.Overlaps(candidate[j]))safe=false;
                    if(!safe)break;candidate[i]=b;
                }
                if(safe){result=candidate;return true;}
            }
            return false;
        }
    }
}
