using System;
using System.IO;
using System.Reflection;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;
using GameEntitySystem;
using System.Runtime.Serialization;

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
    static float ScreenArea { get { return (float)scene.GetType().GetProperty("LastScreenArea", F).GetValue(scene, null); } }
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
        if (!empty) Call(scene, "Queue", eye, dir, view, age, quality, reduced);
        Check(Quads <= (quality == 0 ? 6 : quality == 1 ? 12 : 24), "quad cap");
        Check(ScreenArea <= (quality == 0 ? 1f : quality == 1 ? 1.5f : 2f), "screen coverage cap");
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
        clips = mod.GetType("SCIEPort.Features.PixelFx.ArtifactSpriteClips");
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
            foreach (string clip in new[] { "ChargeUp", "LightningStrike", "Thunderclap", "HolyBlessing", "HolySunburst", "HolySmite", "WaterSplash", "WaterWave", "DarkExplosion" })
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
            Reset(); Spawn("Thunderclap",new Vector3(40,0,12),7,2,0);
            Frame(.2,2,false,eye,null,false,16f/9,false); Check(Quads==0 && ScreenArea==0,"offscreen quad consumes no budget");
            Reset(); Spawn("Thunderclap",new Vector3(23,0,12),7,2,0);
            Check(Diff(empty,Frame(.2,2,false,eye,"screen-edge",false,16f/9,false))>0 && Quads==1,"partially visible quad retained at screen edge");
            for (int q=0;q<3;q++)
            {
                Reset(); for(int i=0;i<32;i++) Spawn("LightningStrike",new Vector3(0,0,0),24,q,0);
                Frame(.2,q,false,eye,"coverage-stress-q"+q,false,16f/9,false);
                Check(Quads>0 && Quads<(q==0?6:q==1?12:24),"overlap coverage budget reduces near-camera draw count");
                Check(ScreenArea>0,"coverage diagnostics populated");
            }
            var lightning=Activator.CreateInstance(scene.GetType().Assembly.GetType("SCIEPort.Features.Stormhammer.LightningScene"),true);
            var hasEffects=lightning.GetType().GetProperty("HasEffects",F);
            Check(!(bool)hasEffects.GetValue(lightning,null),"idle lightning scene draw gate");
            Call(lightning,"SpawnRune",Vector3.Zero,1f,2f,0d,1d,Color.White);
            Check((bool)hasEffects.GetValue(lightning,null),"rune-only scene remains drawable");
            Call(lightning,"Update",2d); Check(!(bool)hasEffects.GetValue(lightning,null),"expired rune releases idle gate");
            var sprite=lightning.GetType().GetProperty("SpriteFx",F).GetValue(lightning,null);
            Call(sprite,"Attach",renderer,texture);
            Call(sprite,"Play",clips.GetField("ChargeUp",F).GetValue(null),Vector3.Zero,2f,2d,2,.42f);
            Check((bool)hasEffects.GetValue(lightning,null),"sprite-only scene remains drawable");
            Call(lightning,"Update",3d); Check(!(bool)hasEffects.GetValue(lightning,null),"sprite expiry releases idle gate");
            Call(lightning,"Clear"); Check(!(bool)hasEffects.GetValue(lightning,null),"clear releases idle gate");
            Call(scene,"Clear"); Check((int)player.GetType().GetProperty("ActiveCount",F).GetValue(player,null)==0,"scene cleanup");
            Call(scene,"Attach",renderer,texture); Reset();
            Frame(.2,2,false,eye,"after-clear",false,16f/9,false); Check(Quads==0,"no old-world effects after reattach");
            SharedArtifacts(lightning, eye);
            string result=checks+" target Engine/OpenGL PixelFX state/geometry/animation/occlusion/quality/cleanup checks passed. Synthetic scene, not Android/game/network evidence.";
            File.WriteAllText(Path.Combine(output,"result.txt"),result); Console.WriteLine(result);
        }
        finally { Display.RenderTarget=null; texture.Dispose(); target.Dispose(); renderer.Clear(); world.Clear(); }
    }

    static void SharedReset()
    { Call(scene,"Clear"); Call(scene,"Attach",renderer,texture); renderer.Clear(); }
    static int Active { get { return (int)player.GetType().GetProperty("ActiveCount",F).GetValue(player,null); } }
    static object At(int index) { return Call(player,"At",index); }
    static void SharedArtifacts(object lightning, Vector3 eye)
    {
        var mod=clips.Assembly;
        var project=(Project)FormatterServices.GetUninitializedObject(typeof(Project));
        var owner=new ComponentPlayer(); owner.m_entity=(Entity)FormatterServices.GetUninitializedObject(typeof(Entity));
        var register=mod.GetType("SCIEPort.Features.Stormhammer.AwakenedStormhammerVisuals").GetMethod("RegisterScene",F);
        var presenter=mod.GetType("SCIEPort.Features.PixelFx.ArtifactSpriteFx");
        scene=lightning.GetType().GetProperty("SpriteFx",F).GetValue(lightning,null); player=scene.GetType().GetField("Player",F).GetValue(scene);
        register.Invoke(null,new object[]{project,lightning});
        Vector3 origin=new Vector3(0,1.5f,12),direction=Vector3.UnitZ;
        try
        {
            SharedReset();
            var start=presenter.GetMethod("Excalibur",F);
            start.Invoke(null,new object[]{project,owner,1u,(byte)1,origin,direction,0d,0f});
            Check(Active==1,"accepted Excalibur starts shared holy clip");
            start.Invoke(null,new object[]{project,owner,1u,(byte)1,origin,direction,.1d,.1f});
            Check(Active==1,"Excalibur snapshot never duplicates event");
            Frame(.2,2,false,eye,"shared-excalibur-start",false,16f/9,false);
            Frame(.2,2,false,new Vector3(10,5,-8),null,false,16f/9,false); Check(Active==1,"second camera does not duplicate cast");
            SharedReset(); start.Invoke(null,new object[]{project,owner,2u,(byte)2,origin,direction,0d,1.95f});
            Check(Active==1 && (double)At(0).GetType().GetField("Birth",F).GetValue(At(0))<0,"late Excalibur resumes release without old charge");
            Frame(0,2,false,eye,"shared-excalibur-late",false,16f/9,false);
            SharedReset(); start.Invoke(null,new object[]{project,owner,3u,(byte)2,origin,direction,0d,0f});
            Call(player,"Update",1.75d,2); Check(Active==0,"cancelled Excalibur has no queued future burst");
            SharedReset(); start.Invoke(null,new object[]{project,owner,4u,(byte)2,origin,direction,0d,3f});
            Check(Active==0,"expired Excalibur snapshot never replays");
            var water=presenter.GetMethod("Poseidon",F); SharedReset();
            water.Invoke(null,new object[]{project,owner,5u,(byte)2,new Vector3(0,0,12),direction,0d,.29f}); Check(Active==0,"Poseidon wave waits for original windup");
            water.Invoke(null,new object[]{project,owner,5u,(byte)2,new Vector3(0,0,12),direction,0d,.3f}); Check(Active==1,"Poseidon wave starts at original windup");
            water.Invoke(null,new object[]{project,owner,5u,(byte)2,new Vector3(0,0,12),direction,.1d,.4f}); Check(Active==1,"Poseidon updates deduplicated");
            Frame(.2,2,false,eye,"shared-poseidon",false,16f/9,false);
            var dark=presenter.GetMethod("Abyss",F); SharedReset();
            dark.Invoke(null,new object[]{project,owner,6u,new Vector3(0,0,12),0d,1.24f}); Check(Active==0,"Abyss does not burst before existing impact");
            dark.Invoke(null,new object[]{project,owner,6u,new Vector3(0,0,12),0d,1.25f}); Check(Active==1,"Abyss bursts at original impact");
            dark.Invoke(null,new object[]{project,owner,6u,new Vector3(0,0,12),.1d,1.35f}); Check(Active==1,"Abyss wave duplicates suppressed");
            Frame(.2,2,false,eye,"shared-abyss",false,16f/9,false);
            var spear=presenter.GetMethod("Gungnir",F); SharedReset();
            spear.Invoke(null,new object[]{project,"Invoke",origin,0d}); spear.Invoke(null,new object[]{project,"Invoke",origin,.05d});
            Check(Active==1,"Gungnir same-place cue merge"); spear.Invoke(null,new object[]{project,"Impact",origin,.05d}); Check(Active==2,"Gungnir impact distinct from invoke");
            Frame(.2,2,false,eye,"shared-gungnir",false,16f/9,false);
            foreach(int q in new[]{0,1,2})
            {
                SharedReset(); string[] names={"ChargeUp","HolyBlessing","HolySunburst","HolySmite","WaterSplash","WaterWave","DarkExplosion"};
                for(int i=0;i<200;i++)Call(scene,"PlayUnique",clips.GetField(names[i%names.Length],F).GetValue(null),new Vector3(0,0,12),7f,0d,q,(ulong)(1000+i),0f,4d);
                Check(Active==(q==0?8:q==1?16:32),"all artifacts share one world pool cap");
                Frame(.2,q,false,eye,"shared-all-q"+q,false,16f/9,false);
                Check(Quads<=(q==0?6:q==1?12:24),"all artifact quad budgets are shared");
            }
            SharedReset(); dark.Invoke(null,new object[]{project,owner,7u,new Vector3(0,0,12),0d,1.25f}); Check(Active==1,"world cleanup releases dedup references");
            SharedReset(); for(int i=0;i<32;i++)Spawn("LightningStrike",new Vector3(0,0,12),7,2,0);
            object low=clips.GetField("HolyBlessing",F).GetValue(null);
            Check(!(bool)Call(scene,"PlayUnique",low,origin,4f,0d,2,9000UL,0f,4d)&&Active==32,"low-priority effect dropped under shared saturation");
            Call(player,"Clear"); Check(!(bool)Call(scene,"PlayUnique",low,origin,4f,.1d,2,9000UL,.1f,4d)&&Active==0,"dropped event never becomes delayed replay");
        }
        finally { Call(scene,"Clear"); register.Invoke(null,new object[]{project,null}); }
    }
}
