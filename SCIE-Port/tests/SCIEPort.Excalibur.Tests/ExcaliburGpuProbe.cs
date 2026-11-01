using System;
using System.IO;
using System.Reflection;
using Engine;
using Engine.Graphics;
using Engine.Media;
// Target Engine/OpenGL synthetic scene using the real shipped ExcaliburScene geometry.
// No game worlds, game network, user saves or Android device are opened.
public static class ExcaliburGpuBootstrap
{
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{var n=new AssemblyName(e.Name).Name;foreach(var ext in new[]{".dll",".exe"}){var p=Path.Combine(args[0],n+ext);if(File.Exists(p))return Assembly.LoadFrom(p);}return null;};
        try{ExcaliburGpuProbe.Run(args[1],args[2]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
public static class ExcaliburGpuProbe
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static Assembly mod;static object scene,cast;static PrimitivesRenderer3D renderer,world;static RenderTarget2D target;static string output;static int checks;
    static void Put(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    public static void Run(string path,string directory)
    {
        mod=Assembly.LoadFrom(path);output=directory;
        Window.Created+=()=>{var w=typeof(Window).GetField("m_gameWindow",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);w.GetType().GetProperty("Visible").SetValue(w,false,null);};
        Exception failure=null;Window.Frame+=()=>{try{Execute();}catch(Exception e){failure=e;}finally{Window.Close();}};
        Window.Run(960,540,WindowMode.Fixed,"Excalibur synthetic GPU fixture");if(failure!=null)throw failure;
    }
    static Color[] Frame(float age,int quality,Vector3 eye,string name,bool foreground=false,bool empty=false,float aspect=960f/540,byte skill=2)
    {
        Display.RenderTarget=target;Display.Viewport=new Viewport(0,0,960,540);Display.Clear(new Color(25,35,61),1,0);
        var view=Matrix.CreateLookAt(eye,new Vector3(0,4,15),Vector3.UnitY)*Matrix.CreatePerspectiveFieldOfView(65*MathUtils.PI/180,aspect,.1f,200);
        var b=world.FlatBatch(0,DepthStencilState.Default,RasterizerState.CullNone,BlendState.Opaque);
        for(int z=-10;z<60;z+=4)for(int x=-40;x<40;x+=4)b.QueueQuad(new Vector3(x,-.1f,z),new Vector3(x+4,-.1f,z),new Vector3(x+4,-.1f,z+4),new Vector3(x,-.1f,z+4),((x+z)/4%2==0)?new Color(56,72,67):new Color(61,80,72));
        if(foreground)b.QueueQuad(new Vector3(-100,-100,5),new Vector3(100,-100,5),new Vector3(100,100,5),new Vector3(-100,100,5),new Color(72,69,83));
        world.Flush(view);renderer.Clear();Put(scene,"eye",eye);Put(scene,"triangles",0);Put(scene,"segments",quality==0?16:quality==1?24:32);Put(scene,"limit",quality==0?3072:6144);
        if(!empty){
            Put(cast,"Skill",skill);
            if(skill==1)scene.GetType().GetMethod("Enchant",F).Invoke(scene,new object[]{new Vector3(0,1.5f,15),Vector3.UnitZ,age});
            else scene.GetType().GetMethod(skill==3?"Avalon":"Ultimate",F).Invoke(scene,new object[]{cast,age});
        }
        int triangles=(int)scene.GetType().GetField("triangles",F).GetValue(scene);Check(triangles<=(quality==0?3072:6144),"triangle cap");
        renderer.Flush(view);var pixels=new Color[960*540];target.GetData(pixels,0,new Rectangle(0,0,960,540));
        using(var f=File.Create(Path.Combine(output,name+".png")))RenderTarget2D.Save(target,f,ImageFileFormat.Png,false);return pixels;
    }
    static int Diff(Color[] a,Color[] b){int n=0;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])n++;return n;}
    static void Execute()
    {
        if(target!=null)return;
        scene=Activator.CreateInstance(mod.GetType("SCIEPort.Features.Excalibur.ExcaliburScene",true),F,null,new object[]{null},null);renderer=(PrimitivesRenderer3D)scene.GetType().GetField("renderer",F).GetValue(scene);
        cast=Activator.CreateInstance(mod.GetType("SCIEPort.Features.Excalibur.SubsystemExcalibur+Cast",true),true);
        var game=Assembly.Load("Survivalcraft");var player=Activator.CreateInstance(game.GetType("Game.ComponentPlayer",true));var body=Activator.CreateInstance(game.GetType("Game.ComponentBody",true));
        body.GetType().GetProperty("Position").SetValue(body,new Vector3(0,0,15),null);player.GetType().GetProperty("ComponentBody").SetValue(player,body,null);Put(cast,"Player",player);Put(cast,"Origin",new Vector3(0,1.5f,15));Put(cast,"Direction",Vector3.UnitZ);Put(cast,"Skill",(byte)3);
        world=new PrimitivesRenderer3D();target=new RenderTarget2D(960,540,1,ColorFormat.Rgba8888,DepthFormat.Depth24Stencil8);
        try
        {
            var eye=new Vector3(13,8,-3);
            for(int q=0;q<3;q++)
            {
                var baseline=Frame(0,q,eye,"q"+q+"-baseline",false,true);
                foreach(float age in new[]{.275f,.88f,1.4575f,1.6225f,1.8425f,2.145f})Check(Diff(baseline,Frame(age,q,eye,"q"+q+"-t"+age.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)))>150,"visible phase q"+q+" age"+age);
                Check(Diff(baseline,Frame(2.2f,q,eye,"q"+q+"-end"))==0,"exact end clears geometry");
                foreach(var sideEye in new[]{new Vector3(-16,7,23),new Vector3(12,7,45)})Check(Diff(Frame(0,q,sideEye,"side-baseline",false,true),Frame(1.76f,q,sideEye,"q"+q+"-side"+sideEye.Z))>150,"side/back flood visibility");
            }
            // Opaque foreground must occlude the whole sword/torrent, with no through-wall overlay.
            var frontEye=new Vector3(0,4,-5);Check(Diff(Frame(0,2,frontEye,"wall-before",true,true),Frame(1.76f,2,frontEye,"wall-cast",true))==0,"depth-tested foreground occlusion");
            Check(Diff(Frame(0,2,eye,"wide-before",false,true,20f/9),Frame(1.705f,2,eye,"wide-cast",false,false,20f/9))>150,"20:9 aspect render");
            for(int q=0;q<3;q++)
            {
                var before=Frame(0,q,eye,"avalon-q"+q+"-before",false,true,960f/540,3);
                foreach(float age in new[]{.2f,.6f,1.6f,3.59f,4.05f})Check(Diff(before,Frame(age,q,eye,"avalon-q"+q+"-t"+age.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture),false,false,960f/540,3))>100,"Avalon unfolding/sanctuary/fade phase");
                Check(Diff(before,Frame(4.2f,q,eye,"avalon-q"+q+"-end",false,false,960f/540,3))==0,"Avalon leaves no persistent geometry");
                Check(Diff(before,Frame(2,q,eye,"enchant-q"+q,false,false,960f/540,1))>8,"enchanted sword aura visible");
            }
            Check(Diff(Frame(0,2,frontEye,"avalon-wall-before",true,true,960f/540,3),Frame(1.6f,2,frontEye,"avalon-wall-cast",true,false,960f/540,3))==0,"Avalon world projection respects opaque foreground");
            File.WriteAllText(Path.Combine(output,"result.txt"),checks+" target Engine/OpenGL geometry/pixel/budget/occlusion checks passed. Synthetic scene, not gameplay/Android.");Console.WriteLine(checks+" target Engine/OpenGL Excalibur checks passed: "+output);
        }
        finally{Display.RenderTarget=null;target.Dispose();scene.GetType().GetMethod("Dispose",F).Invoke(scene,null);}
    }
}
