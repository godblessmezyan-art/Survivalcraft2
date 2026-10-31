using System;
using System.Collections.Generic;
using Rect=SCIEPort.Features.Artifacts.ArtifactSkillLayout.Rect;
namespace SCIEPort.Features.AbyssReaper
{
    // Abyss-only override. Shared artifact layouts remain unchanged.
    internal static class AbyssSpaceHudLayout
    {
        internal static bool Place(float w,float h,Rect look,IList<Rect> obstacles,bool left,out Rect[] result)
        {
            result=null;
            if(!WorldCutRules.Finite(w)||!WorldCutRules.Finite(h)||w<320||h<280||!look.Valid||obstacles==null||obstacles.Count>256)return false;
            bool mirror=left||look.X+look.W*.5f<w*.5f;float sign=mirror?-1:1;
            for(int rise=0;rise<24;rise++)for(int inward=0;inward<12;inward++)
            {
                var places=new Rect[5];bool safe=true;
                for(int i=0;i<4;i++)
                {
                    float size=i==3?88:72,dx=i==0?-220:i==1?-140:i==2?-60:24,dy=i==0?38:i==1?-18:i==2?-82:-154;
                    var box=new Rect{X=look.X+look.W*.5f+sign*(dx-inward*12)-size*.5f,Y=look.Y+look.H*.5f+dy-rise*12-size*.5f,W=size,H=size};
                    if(!Safe(box,w,h,look,obstacles,places,i)){safe=false;break;}places[i]=box;
                }
                if(!safe)continue;
                for(int lift=0;lift<24;lift++)for(int inset=0;inset<6;inset++)
                {
                    float center=mirror?w-96-inset*24:96+inset*24;
                    var portal=new Rect{X=center-32,Y=h-200-lift*12-32,W=64,H=64};
                    if(Safe(portal,w,h,look,obstacles,places,4)){places[4]=portal;result=places;return true;}
                }
            }
            return false;
        }
        private static bool Safe(Rect box,float w,float h,Rect look,IList<Rect> obstacles,Rect[] placed,int count)
        {
            if(box.X<8||box.Y<8||box.X+box.W>w-8||box.Y+box.H>h-8||box.Overlaps(look))return false;
            foreach(var item in obstacles)if(box.Overlaps(item))return false;
            for(int i=0;i<count;i++)if(box.Overlaps(placed[i]))return false;
            return true;
        }
    }
}
