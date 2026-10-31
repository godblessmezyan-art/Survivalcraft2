using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using Engine;
using Engine.Graphics;
using Engine.Media;

// Windows native Engine/OpenGL depth fixture. Synthetic scenery, NOT a gameplay or Android capture.
// No Project, terrain, player save, network, damage or proprietary resources are loaded.
public static class VoidHorizonGpuBootstrap
{
    public static int Main(string[] args)
    {
        string game=args[0];
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,e)=>
        {
            string name=new AssemblyName(e.Name).Name;
            foreach(string ext in new[]{".dll",".exe"}){string path=Path.Combine(game,name+ext);if(File.Exists(path))return Assembly.LoadFrom(path);}return null;
        };
        try { Assembly.GetExecutingAssembly().GetType("VoidHorizonGpuProbe").GetMethod("Run").Invoke(null,new object[]{args[1],args[2],args.Length>3});return 0; }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
public static class VoidHorizonGpuProbe
{
    static Assembly mod;
    static string output;
    static object scene;
    static MethodInfo queue;
    static FieldInfo rendererField;
    static PrimitivesRenderer3D world;
    static RenderTarget2D target;
    static int checks;
    static bool animation;
    static readonly Color Near=new Color(249,215,31),Middle=new Color(45,225,112),Far=new Color(232,95,44);
    public static void Run(string path,string directory,bool captureAnimation)
    {
        mod=Assembly.LoadFrom(path);output=directory;animation=captureAnimation;Directory.CreateDirectory(output);
        Window.Created+=()=>
        {
            // Keep the test helper out of the user's desktop; renders to its own depth-equipped FBO.
            var field=typeof(Window).GetField("m_gameWindow",BindingFlags.Static|BindingFlags.NonPublic);
            var window=field.GetValue(null);window.GetType().GetProperty("Visible").SetValue(window,false,null);
        };
        Exception failure=null;
        Window.Frame+=()=>
        {
            try { Execute(); }catch(Exception e){failure=e;}finally{Window.Close();}
        };
        Window.Run(960,540,WindowMode.Fixed,"Void depth regression (synthetic Engine fixture)");
        if(failure!=null)throw failure;
    }
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static Matrix View(float yaw,float pitch)
    {
        yaw*=MathUtils.PI/180;pitch*=MathUtils.PI/180;
        var direction=new Vector3((float)Math.Sin(yaw)*(float)Math.Cos(pitch),(float)Math.Sin(pitch),(float)Math.Cos(yaw)*(float)Math.Cos(pitch));
        var right=new Vector3(-(float)Math.Cos(yaw),0,(float)Math.Sin(yaw));
        return Matrix.CreateLookAt(Vector3.Zero,direction,Vector3.Cross(right,direction))*Matrix.CreatePerspectiveFieldOfView(65*MathUtils.PI/180,960f/540,.1f,2048);
    }
    static void Quad(FlatBatch3D b,Vector3 a,Vector3 c,Vector3 d,Vector3 e,Color color){b.QueueQuad(a,c,d,e,color);}
    static void Box(FlatBatch3D b,Vector3 at,Vector3 size,Color color)
    {
        Vector3 a=at,c=at+new Vector3(size.X,0,0),d=at+new Vector3(size.X,0,size.Z),e=at+new Vector3(0,0,size.Z),up=Vector3.UnitY*size.Y;
        Quad(b,a,c,d,e,color);Quad(b,a+up,e+up,d+up,c+up,Color.MultiplyColorOnly(color,.9f));
        Quad(b,a,a+up,c+up,c,color);Quad(b,c,c+up,d+up,d,Color.MultiplyColorOnly(color,.8f));
        Quad(b,d,d+up,e+up,e,color);Quad(b,e,e+up,a+up,a,Color.MultiplyColorOnly(color,.75f));
    }
    static float Height(float x,float z)
    {
        float hills=Math.Max(0,1-Math.Abs(z-165)/90)*Math.Max(0,(float)(30+18*Math.Sin(x*.045)+15*Math.Cos(x*.082)));
        return -3+hills;
    }
    static void Landscape()
    {
        var b=world.FlatBatch(0,DepthStencilState.Default,RasterizerState.CullNone,BlendState.Opaque);
        for(int z=-240;z<264;z+=8)for(int x=-256;x<256;x+=8)
        {
            float h=Height(x,z);var color=h>25?new Color(137,142,149):h>8?new Color(79,108,71):new Color(75,115,63);
            Quad(b,new Vector3(x,Height(x,z),z),new Vector3(x+8,Height(x+8,z),z),new Vector3(x+8,Height(x+8,z+8),z+8),new Vector3(x,Height(x,z+8),z+8),color);
        }
        Box(b,new Vector3(-25,-3,38),new Vector3(12,11,12),new Color(170,142,111));
        Box(b,new Vector3(-26,8,37),new Vector3(14,2,14),new Color(95,64,57));
        Box(b,new Vector3(30,-3,15),new Vector3(5,6,8),new Color(159,149,120));
        for(int i=0;i<18;i++)
        {
            float x=28+i%6*6,z=32+i/6*11,y=Height(x,z);
            Box(b,new Vector3(x,y,z),new Vector3(1,6,1),new Color(91,65,46));
            for(int j=0;j<8;j++)
            {
                float a=j*MathUtils.PI/4,c=(j+1)*MathUtils.PI/4;
                b.QueueTriangle(new Vector3(x,y+12,z),new Vector3(x+4*(float)Math.Cos(a),y+3,z+4*(float)Math.Sin(a)),new Vector3(x+4*(float)Math.Cos(c),y+3,z+4*(float)Math.Sin(c)),new Color(30+i%3*5,74+i%4*6,46));
            }
        }
        Box(b,new Vector3(-5,-3,10),new Vector3(1,1.5f,2),new Color(185,133,76));
        var water=world.FlatBatch(1,DepthStencilState.Default,RasterizerState.CullNone,BlendState.AlphaBlend);
        Quad(water,new Vector3(-40,-1.8f,18),new Vector3(22,-1.8f,18),new Vector3(22,-1.8f,100),new Vector3(-40,-1.8f,100),Color.PremultiplyAlpha(new Color(48,140,187,190)));
    }
    static void Calibration()
    {
        var b=world.FlatBatch(0,DepthStencilState.Default,RasterizerState.CullNone,BlendState.Opaque);
        float[] depths={8,48,140},offsets={-.72f,0,.72f};Color[] colors={Near,Middle,Far};
        for(int i=0;i<3;i++)
        {
            float z=depths[i],x=offsets[i]*z,y=-.25f*z,s=.13f*z;
            Quad(b,new Vector3(x-s,y-s,z),new Vector3(x+s,y-s,z),new Vector3(x+s,y+s,z),new Vector3(x-s,y+s,z),colors[i]);
        }
    }
    static Color[] Frame(float t,int quality,float yaw,float pitch,bool calibration,string name)
    {
        Display.RenderTarget=target;Display.Viewport=new Viewport(0,0,960,540);
        Display.Clear(new Color(117,174,216),1f,0);
        if(calibration)Calibration();else Landscape();
        Matrix vp=View(yaw,pitch);world.Flush(vp);
        float ya=yaw*MathUtils.PI/180,pi=pitch*MathUtils.PI/180;
        var direction=new Vector3((float)Math.Sin(ya)*(float)Math.Cos(pi),(float)Math.Sin(pi),(float)Math.Cos(ya)*(float)Math.Cos(pi));
        queue.Invoke(scene,new object[]{Vector3.Zero,-Vector3.UnitY*1.6f,Vector3.UnitZ,t,quality,128f,direction});
        ((PrimitivesRenderer3D)rendererField.GetValue(scene)).Flush(vp);
        var pixels=new Color[960*540];target.GetData(pixels,0,new Rectangle(0,0,960,540));
        using(var file=File.Create(Path.Combine(output,name+".png")))RenderTarget2D.Save(target,file,ImageFileFormat.Png,false);
        return pixels;
    }
    static int Count(Color[] pixels,Color color){int n=0;foreach(var p in pixels)if(p==color)n++;return n;}
    static bool Equal(Color[] a,Color[] b){for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    static int BrightSpan(Color[] pixels)
    {
        int min=960,max=0;for(int i=0;i<pixels.Length;i++)if(pixels[i].R>190&&pixels[i].G>145&&pixels[i].B>215){int x=i%960;min=Math.Min(min,x);max=Math.Max(max,x);}return Math.Max(0,max-min);
    }
    static void Execute()
    {
        if(target!=null)return;
        var type=mod.GetType("SCIEPort.Features.AbyssReaper.VoidHorizonScene",true);scene=Activator.CreateInstance(type,true);
        queue=type.GetMethod("Queue",BindingFlags.Instance|BindingFlags.NonPublic);rendererField=type.GetField("renderer",BindingFlags.Instance|BindingFlags.NonPublic);
        world=new PrimitivesRenderer3D();target=new RenderTarget2D(960,540,1,ColorFormat.Rgba8888,DepthFormat.Depth24Stencil8);
        try
        {
            for(int q=0;q<3;q++)
            {
                string prefix="calibration-q"+q+"-";
                var before=Frame(0,q,0,0,true,prefix+"0");var birth=Frame(.5f,q,0,0,true,prefix+"birth");
                var far=Frame(1.8f,q,0,0,true,prefix+"far");var near=Frame(2.8f,q,0,0,true,prefix+"near");
                var full=Frame(3.1f,q,0,0,true,prefix+"full");var exit=Frame(4.2f,q,0,0,true,prefix+"exit");
                Check(Count(before,Near)>100&&Count(before,Middle)>100&&Count(before,Far)>100,"calibration panels render at all depths");
                Check(Count(birth,Near)==Count(before,Near)&&Count(birth,Middle)==Count(before,Middle)&&Count(birth,Far)==Count(before,Far),"birth keeps real world panels unchanged");
                Check(Count(far,Far)==0&&Count(far,Middle)==Count(before,Middle)&&Count(far,Near)==Count(before,Near),"far scene disappears before nearer geometry");
                Check(Count(near,Far)==0&&Count(near,Middle)==0&&Count(near,Near)==Count(before,Near),"middle scene disappears while near scene remains");
                Check(Count(full,Near)==0&&Count(full,Middle)==0&&Count(full,Far)==0,"complete domain covers every calibration depth");
                Check(Equal(before,exit),"4.2s restores original framebuffer exactly");
                foreach(float time in new[]{3.08f,3.09f,3.099f})
                {
                    var last=Frame(time,q,0,0,true,prefix+"near-clip-"+time.ToString("0.000",CultureInfo.InvariantCulture));
                    Check(Count(last,Near)==0&&Count(last,Middle)==0&&Count(last,Far)==0,"near-plane crossing never reveals the already engulfed world");
                }
            }
            target.Dispose();target=new RenderTarget2D(960,540,1,ColorFormat.Rgba8888,DepthFormat.Depth16);
            var lowBefore=Frame(0,0,0,0,true,"depth16-before");var lowFar=Frame(1.8f,0,0,0,true,"depth16-far");var lowNear=Frame(2.8f,0,0,0,true,"depth16-near");
            Check(Count(lowFar,Far)==0&&Count(lowFar,Near)==Count(lowBefore,Near),"16-bit depth also removes far surfaces before near ones");
            Check(Count(lowNear,Middle)==0&&Count(lowNear,Near)==Count(lowBefore,Near),"16-bit depth preserves the intermediate near-scene window");
            target.Dispose();target=new RenderTarget2D(960,540,1,ColorFormat.Rgba8888,DepthFormat.Depth24Stencil8);
            Color[] original=null,farScene=null,middleScene=null,nearScene=null,formed=null,collapsed=null,burstStart=null,burstWave=null;
            foreach(float time in new[]{0f,.35f,.7f,1.3f,1.8f,2.4f,2.8f,3.1f,3.7f,3.82f,3.94f,4.01f,4.12f,4.2f})
            {
                var pixels=Frame(time,1,0,0,false,"landscape-"+time.ToString("0.00",CultureInfo.InvariantCulture));
                if(time==0)original=pixels;if(time==1.8f)farScene=pixels;if(time==2.4f)middleScene=pixels;if(time==2.8f)nearScene=pixels;
                if(time==3.7f)formed=pixels;if(time==3.82f)collapsed=pixels;if(time==3.94f)burstStart=pixels;if(time==4.01f)burstWave=pixels;
                if(time==4.2f)Check(Equal(original,pixels),"synthetic mountain/lake/forest/building scene restores exactly");
            }
            Color lake=new Color(54,133,155),mountain=new Color(137,142,149),building=new Color(170,142,111),tree=new Color(30,74,46),animal=new Color(185,133,76);
            Check(Count(original,mountain)>100&&Count(farScene,mountain)==0,"actual GPU mountain surfaces disappear at far-distance phase");
            Check(Count(original,lake)>Count(farScene,lake)&&Count(farScene,lake)>Count(middleScene,lake)&&Count(middleScene,lake)>100&&Count(nearScene,lake)==0,"depth-writing alpha water is progressively replaced far to near");
            Check(Count(farScene,building)==Count(original,building)&&Count(middleScene,building)>0&&Count(middleScene,building)<Count(original,building)&&Count(nearScene,building)==0,"a single building is sliced progressively, not removed as one entity");
            Check(Count(farScene,tree)>Count(middleScene,tree)&&Count(middleScene,tree)>0&&Count(nearScene,tree)==0,"forest intersects moving front");
            Check(Count(farScene,animal)==Count(original,animal)&&Count(middleScene,animal)==Count(original,animal),"near model remains while farther terrain is already replaced");
            Check(Count(collapsed,new Color(0,0,2))<Count(formed,new Color(0,0,2))/2,"black hole visibly contracts during 3.7-3.92s");
            Check(BrightSpan(burstStart)>0&&BrightSpan(burstWave)>BrightSpan(burstStart)+100,"bright shockwave visibly expands rather than a uniform flash");
            var back=Frame(0,1,180,0,false,"back-before");var backBirth=Frame(.5f,1,180,0,false,"back-birth");
            Check(Equal(back,backBirth),"turning around leaves black hole behind instead of dragging it with camera");
            foreach(float yaw in new[]{90f,180f,270f})foreach(float time in new[]{2.4f,3.3f})Frame(time,1,yaw,0,false,"rotation-"+yaw+"-"+time.ToString("0.00",CultureInfo.InvariantCulture));
            foreach(float pitch in new[]{-90f,-80f,80f,90f})foreach(float time in new[]{2.4f,3.3f})Frame(time,1,0,pitch,false,"pitch-"+pitch+"-"+time.ToString("0.00",CultureInfo.InvariantCulture));
            foreach(float yaw in new[]{90f,180f,270f})Check(Count(Frame(3.09f,0,yaw,0,false,"near-clip-yaw-"+yaw),new Color(75,115,63))==0,"near-plane handoff stays closed when rotating");
            foreach(float pitch in new[]{-90f,-80f,80f,90f})Check(Count(Frame(3.09f,0,0,pitch,false,"near-clip-pitch-"+pitch),new Color(75,115,63))==0,"near-plane handoff stays closed when looking up/down");
            if(animation)
            {
                Directory.CreateDirectory(Path.Combine(output,"frames"));
                for(int i=0;i<=126;i++)Frame(i/30f,1,0,0,false,"frames/frame-"+i.ToString("000"));
            }
            File.WriteAllText(Path.Combine(output,"result.txt"),checks+" native Engine/OpenGL depth checks passed. SYNTHETIC SCENERY, NOT GAMEPLAY OR ANDROID.");
            Console.WriteLine(checks+" native Engine/OpenGL depth checks passed; captures: "+output);
        }
        finally{Display.RenderTarget=null;target.Dispose();((IDisposable)scene).Dispose();}
    }
}
