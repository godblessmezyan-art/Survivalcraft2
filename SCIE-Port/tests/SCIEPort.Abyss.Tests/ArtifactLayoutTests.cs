using System;
using System.Collections.Generic;
using SCIEPort.Features.Artifacts;
internal static class ArtifactLayoutTests
{
 internal static void Run()
 {
  int count=0;
  foreach(float w in new[]{640f,960f,1280f,1920f})foreach(float h in new[]{480f,720f,1080f})foreach(bool left in new[]{false,true})for(int n=1;n<=4;n++)
  {
   var look=new ArtifactSkillLayout.Rect{X=left?8:w-168,Y=h-180,W=160,H=160};
   var obstacles=new List<ArtifactSkillLayout.Rect>{new ArtifactSkillLayout.Rect{X=w*.3f,Y=h-70,W=w*.4f,H=60}};
   float[] sizes=new float[n];for(int i=0;i<n;i++)sizes[i]=i==n-1?88:72;
   ArtifactSkillLayout.Rect[] result;
   if(!ArtifactSkillLayout.Place(w,h,look,obstacles,sizes,left,out result))throw new Exception("No skill area "+w+"/"+h+"/"+n);
   for(int i=0;i<n;i++)
   {
    var r=result[i];if(r.Overlaps(look)||r.Overlaps(obstacles[0])||(!left&&r.X+r.W>look.X)||(left&&r.X<look.X+look.W))throw new Exception("Unsafe skill position");
    for(int j=0;j<i;j++)if(r.Overlaps(result[j]))throw new Exception("Skills overlap");
   }
   count++;
  }
  ArtifactSkillLayout.Rect[] rejected;
  if(ArtifactSkillLayout.Place(100,100,default(ArtifactSkillLayout.Rect),new List<ArtifactSkillLayout.Rect>(),new[]{72f},false,out rejected))throw new Exception("Unsafe tiny viewport");
  Console.WriteLine("Unified artifact skill arc: "+count+" viewport/count/hand/obstacle cases passed.");
 }
}
