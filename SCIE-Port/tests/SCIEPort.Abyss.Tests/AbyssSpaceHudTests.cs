using System;
using System.Collections.Generic;
using SCIEPort.Features.AbyssReaper;
using Rect=SCIEPort.Features.Artifacts.ArtifactSkillLayout.Rect;
internal static class AbyssSpaceHudTests
{
    internal static void Run()
    {
        int checks=0;
        foreach(float w in new[]{640f,853f,1024f,1280f,1920f})foreach(float h in new[]{360f,480f,720f,1080f})foreach(bool left in new[]{false,true})
        {
            float size=h<400?96:128;
            var look=new Rect{X=left?16:w-16-size,Y=h-size-28,W=size,H=size};
            var move=new Rect{X=left?w-16-size:16,Y=h-size-28,W=size,H=size};
            var bar=new Rect{X=w*.25f,Y=h-58,W=w*.5f,H=50};
            var obstacles=new List<Rect>{look,move,bar};Rect[] result;
            if(!AbyssSpaceHudLayout.Place(w,h,look,obstacles,left,out result))throw new Exception("Space HUD viewport failed: "+w+"x"+h+" left="+left);
            checks++;
            for(int i=0;i<5;i++)
            {
                if(result[i].W!=(i==3?88:i==4?64:72))throw new Exception("Space HUD touch size");checks++;
                foreach(var native in obstacles){if(result[i].Overlaps(native))throw new Exception("Space HUD native overlap");checks++;}
                for(int j=0;j<i;j++){if(result[i].Overlaps(result[j]))throw new Exception("Space HUD skill overlap");checks++;}
            }
            for(int i=1;i<4;i++){if(result[i].Y>=result[i-1].Y)throw new Exception("Space HUD staircase ordering");checks++;}
            if(left?result[4].X<w/2:result[4].X>w/2)throw new Exception("Space HUD portal must live on opposite side");checks++;
        }
        Rect[] hidden;
        if(AbyssSpaceHudLayout.Place(1280,720,new Rect{X=1000,Y=500,W=128,H=128},new List<Rect>{new Rect{W=1280,H=720}},false,out hidden))throw new Exception("Blocked HUD must hide");
        Console.WriteLine("Abyss five-skill HUD: "+(checks+1)+" layout/size/mirror/native-obstacle checks passed (not touch-device proof).");
    }
}
