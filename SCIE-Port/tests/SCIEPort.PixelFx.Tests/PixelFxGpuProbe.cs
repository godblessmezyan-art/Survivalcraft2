using System;
using System.IO;
using System.Reflection;
using Engine;
using Engine.Graphics;
using Engine.Media;

// Actual target Engine/OpenGL, isolated synthetic scene. No game world or network is started.
public static class PixelFxGpuBootstrap
{
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
            string name = new AssemblyName(e.Name).Name;
            foreach (string ext in new[] { ".dll", ".exe" }) { string p = Path.Combine(args[0], name + ext); if (File.Exists(p)) return Assembly.LoadFrom(p); }
            return null;
        };
        try { typeof(PixelFxGpuProbe).GetMethod("Run").Invoke(null, new object[] { args[1], args[2], args[3] }); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
public static class PixelFxGpuProbe
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static object scene, player;
    static Type clips;
    static PrimitivesRenderer3D renderer, world;
    static RenderTarget2D target;
    static Texture2D texture;
    static int checks;
    static string output;
    static object Call(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name, F).Invoke(obj, args); }
    static void Check(bool b, string message) { checks++; if (!b) throw new Exception(message); }
    static int Quads { get { return (int)scene.GetType().GetProperty("LastQuadCount", F).GetValue(scene, null); } }
    static void Spawn(string name, Vector3 at, float size, int quality, float life)
    { Call(scene, "Play", clips.GetField(name, F).GetValue(null), at, size, 0d, quality, life); }
    static void Reset() { Call(player, "Clear"); renderer.Clear(); }
    static Color[] Frame(double age, int quality, bool reduced, Vector3 eye, string name, bool wall, float aspect, bool empty)
    {
        Display.RenderTarget = target; Display.Viewport = new Viewport(0, 0, 960, 540);
        Display.Clear(new Color(25, 35, 61), 1, 0); renderer.Clear(); world.Clear();
        Vector3 look = new Vector3(0, 3, 12), dir = Vector3.Normalize(look - eye);
        Matrix view = Matrix.CreateLookAt(eye, look, Vector3.UnitY) * Matrix.CreatePerspectiveFieldOfView(65 * MathUtils.PI / 180, aspect, .1f, 200);
        if (wall)
        {
            var b = world.FlatBatch(0, DepthStencilState.Default, RasterizerState.CullNone, BlendState.Opaque);
            b.QueueQuad(new Vector3(-100,-100,5), new Vector3(100,-100,5), new Vector3(100,100,5), new Vector3(-100,100,5), new Color(72,69,83));
        }
        world.Flush(view);
        if (!empty) Call(scene, "Queue", eye, dir, age, quality, reduced);
        Check(Quads <= (quality == 0 ? 6 : quality == 1 ? 12 : 24), "quad cap");
        var batch = (TexturedBatch3D)scene.GetType().GetField("m_batch", F).GetValue(scene);
        Check(batch.TriangleVertices.Count == (empty ? 0 : Quads * 4) && batch.TriangleIndices.Count == (empty ? 0 : Quads * 6), "exact native vertex/index budget");
        for (int i = 0; i < batch.TriangleVertices.Count; i++)
        {
            var v = batch.TriangleVertices.Array[i];
            Check(!float.IsNaN(v.Position.X) && !float.IsInfinity(v.Position.Y) && v.TexCoord.X > 0 && v.TexCoord.X < 1 && v.TexCoord.Y > 0 && v.TexCoord.Y < 1, "finite geometry and UV");
        }
        renderer.Flush(view);
        Color[] pixels = new Color[960 * 540]; target.GetData(pixels, 0, new Rectangle(0,0,960,540));
        if (name != null) using (var f = File.Create(Path.Combine(output, name + ".png"))) RenderTarget2D.Save(target, f, ImageFileFormat.Png, false);
        return pixels;
    }
    static int Diff(Color[] a, Color[] b) { int n = 0; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) n++; return n; }
    static long Energy(Color[] a) { long n = 0; foreach (var c in a) n += c.R + c.G + c.B; return n; }
    public static void Run(string assembly, string assets, string directory)
    {
        var mod = Assembly.LoadFrom(assembly); output = directory;
        scene = Activator.CreateInstance(mod.GetType("SCIEPort.Features.PixelFx.SpriteFxScene"), true);
        player = scene.GetType().GetField("Player", F).GetValue(scene);
        clips = mod.GetType("SCIEPort.Features.PixelFx.StormhammerSpriteClips");
        Window.Created += () => { var w = typeof(Window).GetField("m_gameWindow", F).GetValue(null); w.GetType().GetProperty("Visible").SetValue(w, false, null); };
        Exception failure = null;
        Window.Frame += () => { try { Execute(assets); } catch (Exception e) { failure = e; } finally { Window.Close(); } };
        Window.Run(960, 540, WindowMode.Fixed, "PixelFX isolated native GPU fixture");
        if (failure != null) throw failure;
    }
    static void Execute(string assets)
    {
        renderer = new PrimitivesRenderer3D(); world = new PrimitivesRenderer3D();
        target = new RenderTarget2D(960,540,1,ColorFormat.Rgba8888,DepthFormat.Depth24Stencil8);
        using (var stream = File.OpenRead(Path.Combine(assets, "Textures/PixelFxStormhammer.png"))) texture = Texture2D.Load(stream, false, 1);
        try
        {
            Call(scene,"Attach",renderer,texture);
            var batch = (TexturedBatch3D)scene.GetType().GetField("m_batch", F).GetValue(scene);
            Check(batch.DepthStencilState == DepthStencilState.DepthRead && batch.BlendState == BlendState.Additive && batch.SamplerState == SamplerState.PointClamp, "native render states");
            Check(BlendState.Additive.ColorSourceBlend == Blend.SourceAlpha && BlendState.Additive.ColorDestinationBlend == Blend.One, "audited straight-alpha additive blend");
            var eye = new Vector3(0,4,-8);
            var empty = Frame(0,2,false,eye,"empty",false,16f/9,true);
            foreach (string clip in new[] { "ChargeUp", "LightningStrike", "Thunderclap" })
            for (int q = 0; q < 3; q++)
            {
                Reset(); Spawn(clip,new Vector3(0,0,12),clip == "LightningStrike" ? 16 : 7,q,0);
                var start = Frame(0,q,false,eye,clip+"-q"+q+"-start",false,16f/9,false);
                var middle = Frame(.35,q,false,eye,clip+"-q"+q+"-middle",false,16f/9,false);
                Check(Diff(empty,start)>20 && Diff(empty,middle)>20,"effect visible in all tiers");
                Check(Diff(start,middle)>20,"animation advances");
                var reduced = Frame(.35,q,true,eye,null,false,16f/9,false);
                Check(Energy(reduced)<Energy(middle) && Diff(reduced,empty)>0,"reduced flashes attenuate without hiding effect");
                Check(Diff(empty,Frame(2,q,false,eye,null,false,16f/9,false))==0 && Quads==0,"expired effect leaves no geometry");
            }
            Reset(); Spawn("LightningStrike",new Vector3(0,0,12),16,2,0);
            var wallEmpty=Frame(0,2,false,eye,null,true,16f/9,true);
            Check(Diff(wallEmpty,Frame(.2,2,false,eye,"wall-occlusion",true,16f/9,false))==0,"opaque foreground occludes sprite");
            foreach (var side in new[] { new Vector3(12,6,-8),new Vector3(-12,6,-8),new Vector3(0,24,-8) })
                Check(Diff(empty,Frame(.2,2,false,side,null,false,16f/9,false))>50,"billboard side/elevated views");
            Check(Diff(empty,Frame(.2,2,false,eye,"wide-20x9",false,20f/9,false))>50,"wide phone aspect");
            Reset(); Spawn("ChargeUp",new Vector3(0,1,12),4,2,.28f);
            Check(Diff(empty,Frame(.27,2,false,eye,null,false,16f/9,false))>0,"charge visible through existing windup");
            Check(Diff(empty,Frame(.28,2,false,eye,null,false,16f/9,false))==0,"no delayed release frame");
            for (int q=0;q<3;q++)
            {
                Reset(); for (int i=0;i<200;i++) Spawn("Thunderclap",new Vector3((i%5-2)*.2f,0,12),7,q,0);
                Frame(.2,q,false,eye,"stress-q"+q,false,16f/9,false);
                Check(Quads==(q==0?6:q==1?12:24),"stress reaches but never exceeds tier draw budget");
                Check((int)player.GetType().GetProperty("ActiveCount",F).GetValue(player,null)==(q==0?8:q==1?16:32),"native live pool cap");
            }
            Reset(); Spawn("LightningStrike",new Vector3(0,0,100),16,0,0);
            Frame(.2,0,false,eye,null,false,16f/9,false); Check(Quads==0,"distance culling");
            Reset(); Spawn("Thunderclap",new Vector3(0,0,-20),7,2,0);
            Frame(.2,2,false,eye,null,false,16f/9,false); Check(Quads==0,"behind-camera culling");
            Call(scene,"Clear"); Check((int)player.GetType().GetProperty("ActiveCount",F).GetValue(player,null)==0,"scene cleanup");
            Call(scene,"Attach",renderer,texture); Reset();
            Frame(.2,2,false,eye,"after-clear",false,16f/9,false); Check(Quads==0,"no old-world effects after reattach");
            string result=checks+" target Engine/OpenGL PixelFX state/geometry/animation/occlusion/quality/cleanup checks passed. Synthetic scene, not Android/game/network evidence.";
            File.WriteAllText(Path.Combine(output,"result.txt"),result); Console.WriteLine(result);
        }
        finally { Display.RenderTarget=null; texture.Dispose(); target.Dispose(); renderer.Clear(); world.Clear(); }
    }
}
