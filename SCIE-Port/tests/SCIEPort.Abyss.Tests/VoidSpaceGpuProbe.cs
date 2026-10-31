using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using Engine;
using Engine.Graphics;
using Engine.Media;

// Real target Engine/OpenGL, synthetic geometry. Not gameplay, network or Android footage.
public static class VoidSpaceGpuBootstrap
{
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string n=new AssemblyName(e.Name).Name;foreach(var ext in new[]{".dll",".exe"}){string p=Path.Combine(args[0],n+ext);if(File.Exists(p))return Assembly.LoadFrom(p);}return null;};
        try{typeof(VoidSpaceGpuProbe).GetMethod("Run").Invoke(null,new object[]{args[1],args[2]});return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
public static class VoidSpaceGpuProbe
{
    static Assembly mod;static string output;static object rift,shatter,geometry;static PrimitivesRenderer3D world;
    static RenderTarget2D target;static int checks;
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
    public static void Run(string path,string directory)
    {
        mod=Assembly.LoadFrom(path);output=directory;
        Window.Created+=()=>{var w=typeof(Window).GetField("m_gameWindow",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);w.GetType().GetProperty("Visible").SetValue(w,false,null);};
        Exception failure=null;Window.Frame+=()=>{try{Execute();}catch(Exception e){failure=e;}finally{Window.Close();}};
        Window.Run(960,540,WindowMode.Fixed,"Space V2 synthetic render fixture");if(failure!=null)throw failure;
    }
    static object Scene(string name){return Activator.CreateInstance(mod.GetType("SCIEPort.Features.AbyssReaper."+name,true),true);}
    static PrimitivesRenderer3D Renderer(object scene){return (PrimitivesRenderer3D)scene.GetType().GetField("renderer",F).GetValue(scene);}
    static void Box(FlatBatch3D b,Vector3 p,Vector3 size,Color color)
    {
        Vector3 x=Vector3.UnitX*size.X,y=Vector3.UnitY*size.Y,z=Vector3.UnitZ*size.Z;
        b.QueueQuad(p,p+y,p+x+y,p+x,color);b.QueueQuad(p+x,p+x+y,p+x+y+z,p+x+z,Color.MultiplyColorOnly(color,.8f));
        b.QueueQuad(p+x+z,p+x+y+z,p+y+z,p+z,color);b.QueueQuad(p+z,p+y+z,p+y,p,Color.MultiplyColorOnly(color,.7f));b.QueueQuad(p+y,p+y+z,p+x+y+z,p+x+y,color);
    }
    static Color[] Frame(byte skill,float time,int quality,float yaw,string name)
    {
        Display.RenderTarget=target;Display.Viewport=new Viewport(0,0,960,540);Display.Clear(new Color(136,178,204),1,0);
        var batch=world.FlatBatch(0,DepthStencilState.Default,RasterizerState.CullNone,BlendState.Opaque);
        for(int z=-36;z<100;z+=4)for(int x=-60;x<60;x+=4)batch.QueueQuad(new Vector3(x,-1.6f,z),new Vector3(x+4,-1.6f,z),new Vector3(x+4,-1.6f,z+4),new Vector3(x,-1.6f,z+4),((x+z)/4%2==0)?new Color(100,131,102):new Color(115,146,114));
        Box(batch,new Vector3(-8,-1.6f,21),new Vector3(5,8,5),new Color(164,147,129));
        Box(batch,new Vector3(5,-1.6f,15),new Vector3(3,4,4),new Color(145,132,113));
        for(int i=0;i<7;i++){Box(batch,new Vector3(-13+i*4,-1.6f,36),new Vector3(.5f,5,.5f),new Color(100,75,56));Box(batch,new Vector3(-14+i*4,2,35),new Vector3(2.5f,3,2.5f),new Color(51,98,66));}
        float a=yaw*MathUtils.PI/180;Vector3 eye=new Vector3((float)Math.Sin(a)*15,2,15-(float)Math.Cos(a)*15),focus=new Vector3(0,1,15);
        Matrix view=Matrix.CreateLookAt(eye,focus,Vector3.UnitY)*Matrix.CreatePerspectiveFieldOfView(65*MathUtils.PI/180,960f/540,.1f,256);
        world.Flush(view);var scene=skill==3?shatter:rift;var renderer=Renderer(scene);renderer.Clear();
        if(skill==3)scene.GetType().GetMethod("Queue",F).Invoke(scene,new object[]{geometry,new Vector3(0,1,15),Vector3.UnitZ,time,quality});
        else scene.GetType().GetMethod("Queue",F).Invoke(scene,new object[]{new Vector3(0,0,5),new Vector3(0,0,20),Vector3.UnitZ,skill,time,quality,123u});
        renderer.Flush(view);var pixels=new Color[960*540];target.GetData(pixels,0,new Rectangle(0,0,960,540));
        using(var file=File.Create(Path.Combine(output,name+".png")))RenderTarget2D.Save(target,file,ImageFileFormat.Png,false);renderer.Clear();return pixels;
    }
    static int Difference(Color[] a,Color[] b){int n=0;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])n++;return n;}
    static object Rect(Type type,float x,float y,float w,float h)
    {
        object r=Activator.CreateInstance(type);string[] names={"X","Y","W","H"};float[] values={x,y,w,h};for(int i=0;i<4;i++)type.GetField(names[i],F).SetValue(r,values[i]);return r;
    }
    static void Hud()
    {
        var type=mod.GetType("SCIEPort.Features.Artifacts.ArtifactSkillLayout+Rect",true);
        var layout=mod.GetType("SCIEPort.Features.AbyssReaper.AbyssSpaceHudLayout",true);var rows=new System.Text.StringBuilder("left,index,x,y,size\n");
        foreach(bool left in new[]{false,true})
        {
            object look=Rect(type,left?16:1136,564,128,128),move=Rect(type,left?1136:16,564,128,128);
            var obstacles=(System.Collections.IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(type));obstacles.Add(look);obstacles.Add(move);obstacles.Add(Rect(type,320,662,640,50));
            object[] args={1280f,720f,look,obstacles,left,null};Check((bool)layout.GetMethod("Place",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args),"CPU HUD layout used for labelled schematic");
            int i=0;foreach(var r in (Array)args[5])rows.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4}\n",left?1:0,i++,type.GetField("X",F).GetValue(r),type.GetField("Y",F).GetValue(r),type.GetField("W",F).GetValue(r));
        }
        File.WriteAllText(Path.Combine(output,"hud-layout.csv"),rows.ToString());
    }
    static void Execute()
    {
        if(target!=null)return;rift=Scene("VoidRiftScene");shatter=Scene("VoidShatterScene");
        geometry=shatter.GetType().GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{123u});
        world=new PrimitivesRenderer3D();target=new RenderTarget2D(960,540,1,ColorFormat.Rgba8888,DepthFormat.Depth24Stencil8);
        try
        {
            for(int q=0;q<3;q++)foreach(byte skill in new byte[]{1,2,3})
            {
                string p="q"+q+"-skill"+skill+"-";var before=Frame(skill,0,q,0,p+"before");
                float[] times=skill==3?new[]{.15f,.6f,1.1f,1.3f,1.35f,1.45f,1.6f,1.79f}:skill==1?new[]{.3f,2f,3.7f}:new[]{.3f,1.5f,2.7f};
                foreach(float t in times){var pixels=Frame(skill,t,q,0,p+t.ToString("0.00",CultureInfo.InvariantCulture));Check(Difference(before,pixels)>40,"visible native GPU phase "+p+t);}
                var after=Frame(skill,skill==3?1.8f:skill==1?4:3,q,0,p+"after");Check(Difference(before,after)==0,"exact end clears every V2 fragment "+p);
                foreach(float yaw in new[]{90f,180f,270f}){var baseline=Frame(skill,0,q,yaw,p+"side"+yaw+"-before");var side=Frame(skill,.7f,q,yaw,p+"side"+yaw);Check(Difference(baseline,side)>40,"world-space side/back view "+p+yaw);}
            }
            Hud();
            File.WriteAllText(Path.Combine(output,"result.txt"),(checks-2)+" target Engine/OpenGL V2 checks and 2 CPU HUD exports passed. Synthetic geometry; NOT gameplay or Android.");
            Console.WriteLine(checks+" target Engine/OpenGL V2 checks passed: "+output);
        }
        finally{Display.RenderTarget=null;target.Dispose();((IDisposable)rift).Dispose();((IDisposable)shatter).Dispose();}
    }
}
