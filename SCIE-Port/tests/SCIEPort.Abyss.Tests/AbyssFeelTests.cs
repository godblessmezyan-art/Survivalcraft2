using System;
using System.Collections.Generic;
using SCIEPort.Features.AbyssReaper;
internal static class AbyssFeelTests
{
    private static int checks;
    private static void Check(bool value,string name){checks++;if(!value)throw new Exception("Abyss feel: "+name);}
    internal static void WritePreview(string path)
    {
        var poses=new List<object>();
        for(int stage=0;stage<4;stage++)for(int frame=0;frame<=100;frame++)
        {
            float phase=frame/100f;var p=AbyssMeleeMotion.Sample(stage,phase);
            poses.Add(new {stage,phase,rotation=new[]{p.Pitch,p.Yaw,p.Roll},offset=new[]{p.X,p.Y,p.Z}});
        }
        var layouts=new List<object>();
        foreach(bool left in new[]{false,true})
        {
            var look=new WorldCutHudLayout.Rect(left?22:1098,526,160,168);
            var bar=new WorldCutHudLayout.Rect(420,656,440,56);
            var move=new WorldCutHudLayout.Rect(left?1098:22,526,160,168);
            WorldCutHudLayout.Placement place;
            if(!WorldCutHudLayout.Place(1280,720,look,new List<WorldCutHudLayout.Rect>{look,move,bar},left,out place))throw new Exception("Preview layout failed");
            var icons=new List<object>();
            for(int i=0;i<4;i++){var r=place.At(i);icons.Add(new {slot=i,x=r.X,y=r.Y,size=r.W});}
            layouts.Add(new {left,look=new[]{look.X,look.Y,look.W,look.H},move=new[]{move.X,move.Y,move.W,move.H},icons});
        }
        System.IO.File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new {poses,layouts}));
    }
    internal static void Run()
    {
        foreach(float width in new[]{640f,853f,1024f,1280f,1920f})foreach(float height in new[]{360f,480f,720f,1080f})foreach(bool left in new[]{false,true})
        {
            float lookSize=height<400?96:128;
            var look=new WorldCutHudLayout.Rect(left?16:width-16-lookSize,height-lookSize-28,lookSize,lookSize);
            var move=new WorldCutHudLayout.Rect(left?width-lookSize-16:16,height-lookSize-28,lookSize,lookSize);
            var bar=new WorldCutHudLayout.Rect(width*.25f,height-58,width*.5f,50);
            var obstacles=new List<WorldCutHudLayout.Rect>{look,move,bar};
            WorldCutHudLayout.Placement p;
            Check(WorldCutHudLayout.Place(width,height,look,obstacles,left,out p),"standard viewport offers all four skills");
            for(int i=0;i<4;i++)
            {
                var r=p.At(i);
                Check(r.X>=8&&r.Y>=8&&r.X+r.W<=width-8&&r.Y+r.H<=height-8,"circle within viewport");
                foreach(var blocker in obstacles)Check(!r.Overlaps(blocker),"native control not covered");
                for(int j=0;j<i;j++)Check(!r.Overlaps(p.At(j)),"buttons do not overlap one another");
                Check(r.W>=72&&r.H>=72,"large touch target");
            }
            Check(p.Horizon.W==88&&p.Horizon.Y<p.Cut.Y,"ultimate larger and above normal skills");
            Check(left?p.Horizon.X<p.Blink.X:p.Horizon.X>p.Blink.X,"layout mirrors on native look side");
        }
        WorldCutHudLayout.Placement hidden;
        Check(!WorldCutHudLayout.Place(160,160,default(WorldCutHudLayout.Rect),new List<WorldCutHudLayout.Rect>(),false,out hidden),"small unsupported viewport hides safely");
        Check(!WorldCutHudLayout.Place(640,480,default(WorldCutHudLayout.Rect),new List<WorldCutHudLayout.Rect>{new WorldCutHudLayout.Rect(0,0,640,480)},false,out hidden),"no overlay when native controls occupy whole surface");
        Check(WorldCutHudLayout.Circle(36,36,72,72)&&WorldCutHudLayout.Circle(36,3,72,72),"center and rim respond");
        Check(!WorldCutHudLayout.Circle(1,1,72,72)&&!WorldCutHudLayout.Circle(71,71,72,72)&&!WorldCutHudLayout.Circle(float.NaN,36,72,72),"transparent corners and invalid input pass through");
        for(int stage=0;stage<4;stage++)
        {
            float reach=stage==3?5.5f:4.5f;
            Check(AbyssMeleeMotion.Sector(reach,1,0,stage)&&!AbyssMeleeMotion.Sector(reach+.01f,1,0,stage),"reach boundary");
            Check(!AbyssMeleeMotion.Sector(2,-1,0,stage)&&!AbyssMeleeMotion.Sector(2,1,2.21f,stage)&&!AbyssMeleeMotion.Sector(float.NaN,1,0,stage),"rear, height and invalid distance rejected");
            var ready=AbyssMeleeMotion.Sample(stage,.22f);var end=AbyssMeleeMotion.Sample(stage,.60f);
            Check(Math.Abs(end.Roll-ready.Roll)>2&&Math.Abs(end.X-ready.X)>.5f,"large roll sweep and lateral grip travel");
            // The target build smooths pose commands with 10*dt. Measure rendered response,
            // not just the large requested angles, including the original swing duration.
            float rotation=0,offset=0,minR=0,maxR=0,minX=0,maxX=0;
            float[] duration={.35f,.42f,.58f,.85f};
            float dt=1f/60f;int frames=(int)Math.Ceiling(duration[stage]/dt);
            for(int frame=0;frame<=frames;frame++)
            {
                var target=AbyssMeleeMotion.Sample(stage,Math.Min(1,frame*dt/duration[stage]));
                rotation+=(target.Roll-rotation)*Math.Min(1,10*dt);
                offset+=(target.X-offset)*Math.Min(1,10*dt);
                minR=Math.Min(minR,rotation);maxR=Math.Max(maxR,rotation);
                minX=Math.Min(minX,offset);maxX=Math.Max(maxX,offset);
            }
            Check(maxR-minR>1f&&maxX-minX>.25f,"native-smoothed motion still crosses a substantial arc");
            var idle=AbyssMeleeMotion.Sample(stage,1);
            Check(idle.Pitch==0&&idle.Yaw==0&&idle.Roll==0&&idle.X==0&&idle.Y==0&&idle.Z==0,"complete pose recovery");
            for(int frame=0;frame<=100;frame++)
            {
                var pose=AbyssMeleeMotion.Sample(stage,frame/100f);
                Check(WorldCutRules.Finite(pose.Pitch)&&Math.Abs(pose.X)<=.45f&&pose.Z<=0&&Math.Abs(pose.Roll)<1.6f,"finite bounded pose through whole action");
            }
        }
        Check(AbyssMeleeMotion.Sample(0,float.NaN).X==0&&AbyssMeleeMotion.Sample(0,1,1).Pitch>0,"invalid phase safe and charged stance visible");
        // The frozen original combo still decides one impact and the original 3-hit order.
        var combo=new AbyssCombo();combo.Input(true,1);combo.Input(false,1.02);
        Check(!combo.Impact(1.1)&&combo.Impact(1.3)&&!combo.Impact(2),"new motion cannot add combat impact edges");
        Console.WriteLine("Abyss circular HUD/pose/reach: "+checks+" checks passed (no device rendering).");
    }
}
