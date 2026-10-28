using System;
using SCIEPort.Features.OmniscientEye;

internal static class Program
{
    private static int Main()
    {
        int count = 0;
        foreach (float scale in new[] { 1f, 1.25f, 1.5f })
        foreach (var size in new[] { (854f,480f), (1280f,720f), (1920f,1080f), (2560f,1080f) })
        foreach (bool leftHand in new[] { false, true })
        {
            float w=size.Item1/scale, h=size.Item2/scale;
            var obstacles = new[] {
                new EyeLayout.Rect { X=leftHand?w-150:0, Y=0, Width=150, Height=200 },
                new EyeLayout.Rect { X=leftHand?0:w-150, Y=0, Width=150, Height=200 },
                new EyeLayout.Rect { X=(w-400)/2,Y=h-72,Width=400,Height=72 },
                new EyeLayout.Rect { X=w-165,Y=h-165,Width=165,Height=165 }
            };
            var p=EyeLayout.Place(w,h,obstacles);
            if (!p.HasValue) throw new Exception("Expected space at " + w + "x" + h);
            foreach(var o in obstacles) if(p.Value.Overlaps(o)) throw new Exception("HUD overlaps controls");
            if(p.Value.X<0 || p.Value.Y<0 || p.Value.X+p.Value.Width>w || p.Value.Y+p.Value.Height>h) throw new Exception("Offscreen HUD");
            count++;
        }
        if(EyeLayout.Place(300,200,new EyeLayout.Rect[0]).HasValue) throw new Exception("Too-small viewport must hide HUD");
        if(EyeLayout.Place(800,480,new[]{new EyeLayout.Rect{Width=800,Height=480}}).HasValue) throw new Exception("Full controls must win");
        var fallback=EyeLayout.Place(800,480,new[]{new EyeLayout.Rect{X=230,Y=0,Width=340,Height=40}});
        if(!fallback.HasValue || fallback.Value.Y!=64) throw new Exception("HUD must avoid upper controls");
        Console.WriteLine("Omniscient Eye: " + (count+3) + " layout scenarios passed.");
        return 0;
    }
}
