using System;
using System.IO;
using System.Reflection;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;
using GameEntitySystem;
using System.Collections.Generic;
using System.Runtime.Serialization;
// Target Engine/OpenGL synthetic scene using the real shipped ExcaliburScene geometry.
// No game worlds, game network, user saves or Android device are opened.
public static class ExcaliburGpuBootstrap
{
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{var n=new AssemblyName(e.Name).Name;foreach(var ext in new[]{".dll",".exe"}){var p=Path.Combine(args[0],n+ext);if(File.Exists(p))return Assembly.LoadFrom(p);}return null;};
        try{Assembly.GetExecutingAssembly().GetType("ExcaliburGpuProbe").GetMethod("Run").Invoke(null,new object[]{args[1],args[2],args[3]});return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
public static class ExcaliburGpuProbe
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static Assembly mod;static object scene,cast;static PrimitivesRenderer3D renderer,world;static RenderTarget2D target;static string output,assetRoot;static int checks;
    static BlockMesh sword;static Texture2D swordTexture;static readonly List<bool> luminous=new List<bool>();static ComponentFirstPersonModel fp;
    static Vector3 csvOffset,csvRotation;static float csvScale;
    static void Put(object o,string n,object v){o.GetType().GetField(n,F).SetValue(o,v);}
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    public static void Run(string path,string directory,string assets)
    {
        mod=Assembly.LoadFrom(path);output=directory;assetRoot=assets;
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
            if(skill==1)mod.GetType("SCIEPort.Features.Excalibur.ExcaliburBlock").GetMethod("QueueGlow",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{renderer,Matrix.CreateTranslation(new Vector3(0,1.5f,15)),1f,age,null});
            else scene.GetType().GetMethod(skill==3?"Avalon":"Ultimate",F).Invoke(scene,new object[]{cast,age});
        }
        int triangles=(int)scene.GetType().GetField("triangles",F).GetValue(scene);Check(triangles<=(quality==0?3072:6144),"triangle cap");
        renderer.Flush(view);var pixels=new Color[960*540];target.GetData(pixels,0,new Rectangle(0,0,960,540));
        using(var f=File.Create(Path.Combine(output,name+".png")))RenderTarget2D.Save(target,f,ImageFileFormat.Png,false);return pixels;
    }
    static int Diff(Color[] a,Color[] b){int n=0;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])n++;return n;}
    static Vector3 ParseVector(string text){var v=text.Split(',');return new Vector3(float.Parse(v[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(v[1],System.Globalization.CultureInfo.InvariantCulture),float.Parse(v[2],System.Globalization.CultureInfo.InvariantCulture));}
    static void PrepareSword(ComponentPlayer player)
    {
        var entity=(Entity)FormatterServices.GetUninitializedObject(typeof(Entity));player.m_entity=entity;
        player.ComponentCreatureModel=new ComponentCreatureModel();player.ComponentCreatureModel.m_entity=entity;fp=new ComponentFirstPersonModel();fp.m_entity=entity;
        typeof(Entity).GetField("m_components",F).SetValue(entity,new List<Component>{player,player.ComponentCreatureModel,fp});
        sword=new BlockMesh();using(var stream=File.OpenRead(Path.Combine(assetRoot,"Models/Excalibur.dae")))
        using(var model=Model.Load(stream,true))foreach(var mesh in model.Meshes)foreach(var part in mesh.MeshParts)
        {int first=sword.Vertices.Count;sword.AppendModelMeshPart(part,BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone),false,false,false,false,Color.White);for(int i=first;i<sword.Vertices.Count;i++)luminous.Add(mesh.Name=="SilverWhiteDiamondBlade"||mesh.Name.StartsWith("Inscription"));}
        using(var stream=File.OpenRead(Path.Combine(assetRoot,"Textures/Excalibur.png")))swordTexture=Texture2D.Load(stream,false,1);
        var rows=File.ReadAllLines(Path.Combine(assetRoot,"Blocks/Excalibur.csv"));var head=rows[0].Split(';');var row=rows[1].Split(';');
        csvOffset=ParseVector(row[Array.IndexOf(head,"FirstPersonOffset")]);csvRotation=ParseVector(row[Array.IndexOf(head,"FirstPersonRotation")])*(MathUtils.PI/180);csvScale=float.Parse(row[Array.IndexOf(head,"FirstPersonScale")],System.Globalization.CultureInfo.InvariantCulture);
        Check(sword.Indices.Count/3==1040,"real target-imported sword triangles used for FPP render");
    }
    static Color[] Fpp(float age,bool glow,string name)
    {
        Display.RenderTarget=target;Display.Viewport=new Viewport(0,0,960,540);Display.Clear(new Color(25,35,61),1,0);world.Clear();
        Put(cast,"Skill",(byte)2);mod.GetType("SCIEPort.Features.Excalibur.SubsystemExcalibur").GetMethod("Pose",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{cast,age});
        Vector3 rotation=csvRotation+fp.ItemRotationOrder,offset=csvOffset+fp.ItemOffsetOrder*2;
        Matrix m=Matrix.CreateFromYawPitchRoll(rotation.Y,rotation.X,rotation.Z)*Matrix.CreateTranslation(offset);
        var batch=world.TexturedBatch(swordTexture,false,0,DepthStencilState.Default,RasterizerState.CullNone,BlendState.Opaque,SamplerState.PointClamp);
        for(int i=0;i<sword.Indices.Count;i+=3)
        {
            int a=sword.Indices.Array[i],b=sword.Indices.Array[i+1],c=sword.Indices.Array[i+2];var x=sword.Vertices.Array[a];var y=sword.Vertices.Array[b];var z=sword.Vertices.Array[c];
            batch.QueueTriangle(Vector3.Transform(x.Position*csvScale,m),Vector3.Transform(y.Position*csvScale,m),Vector3.Transform(z.Position*csvScale,m),x.TextureCoordinates,y.TextureCoordinates,z.TextureCoordinates,
                glow&&luminous[a]?new Color(255,218,112):x.Color,glow&&luminous[b]?new Color(255,218,112):y.Color,glow&&luminous[c]?new Color(255,218,112):z.Color);
        }
        if(glow)mod.GetType("SCIEPort.Features.Excalibur.ExcaliburBlock").GetMethod("QueueGlow",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{world,m,csvScale,1f,null});
        world.Flush(Matrix.CreatePerspectiveFieldOfView(65*MathUtils.PI/180,960f/540,.05f,100));var pixels=new Color[960*540];target.GetData(pixels,0,new Rectangle(0,0,960,540));
        using(var stream=File.Create(Path.Combine(output,name+".png")))RenderTarget2D.Save(target,stream,ImageFileFormat.Png,false);return pixels;
    }
    static void GlowAlignment()
    {
        foreach(float yaw in new[]{-1.5f,0f,1.5f})foreach(float scale in new[]{.6f,1f})
        {
            world.Clear();Matrix m=Matrix.CreateFromYawPitchRoll(yaw,.4f,-.2f)*Matrix.CreateTranslation(new Vector3(5,8,-7));
            mod.GetType("SCIEPort.Features.Excalibur.ExcaliburBlock").GetMethod("QueueGlow",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{world,m,scale,1f,null});
            var batch=world.FlatBatch(2,DepthStencilState.DepthRead,RasterizerState.CullNone,BlendState.Additive);Matrix inv=Matrix.Invert(Matrix.CreateScale(scale)*m);
            Check(batch.TriangleVertices.Count==192&&batch.TriangleIndices.Count==288,"bounded 96-triangle model-bound glow");
            for(int i=0;i<batch.TriangleVertices.Count;i++){Vector3 p=Vector3.Transform(batch.TriangleVertices.Array[i].Position,inv);Check(Math.Abs(p.X)<.08f&&Math.Abs(p.Z)<.07f&&p.Y>=.19f&&p.Y<=1.61f,"glow stays in blade-local bounds after camera/hand transform");}
        }
        world.Clear();
    }
    static void Execute()
    {
        if(target!=null)return;
        scene=Activator.CreateInstance(mod.GetType("SCIEPort.Features.Excalibur.ExcaliburScene",true),F,null,new object[]{null},null);renderer=(PrimitivesRenderer3D)scene.GetType().GetField("renderer",F).GetValue(scene);
        cast=Activator.CreateInstance(mod.GetType("SCIEPort.Features.Excalibur.SubsystemExcalibur+Cast",true),true);
        var game=Assembly.Load("Survivalcraft");var player=Activator.CreateInstance(game.GetType("Game.ComponentPlayer",true));var body=Activator.CreateInstance(game.GetType("Game.ComponentBody",true));
        body.GetType().GetProperty("Position").SetValue(body,new Vector3(0,0,15),null);player.GetType().GetProperty("ComponentBody").SetValue(player,body,null);Put(cast,"Player",player);Put(cast,"Origin",new Vector3(0,1.5f,15));Put(cast,"Direction",Vector3.UnitZ);Put(cast,"Skill",(byte)3);PrepareSword((ComponentPlayer)player);
        world=new PrimitivesRenderer3D();target=new RenderTarget2D(960,540,1,ColorFormat.Rgba8888,DepthFormat.Depth24Stencil8);
        try
        {
            var eye=new Vector3(13,8,-3);
            for(int q=0;q<3;q++)
            {
                var baseline=Frame(0,q,eye,"q"+q+"-baseline",false,true);
                foreach(float age in new[]{.3f,.8f,1.2f,1.5f,1.7f,1.8f,3f,5.7f,6.7f,7.5f})Check(Diff(baseline,Frame(age,q,eye,"q"+q+"-t"+age.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)))>150,"visible phase q"+q+" age"+age);
                Check(Diff(baseline,Frame(7.75f,q,eye,"q"+q+"-end"))==0,"exact end clears geometry");
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
            GlowAlignment();var idle=Fpp(0,false,"fpp-idle");
            foreach(float age in new[]{.3f,.8f,1.2f,1.5f,1.75f,2.1f})Check(Diff(idle,Fpp(age,false,"fpp-t"+age.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)))>100,"real sword FPP raised/cut/recovery phase");
            Check(Diff(Fpp(0,false,"fpp-normal"),Fpp(0,true,"fpp-enchanted"))>100,"real sword glow appears on the blade");
            File.WriteAllText(Path.Combine(output,"result.txt"),checks+" target Engine/OpenGL geometry/pixel/budget/occlusion checks passed. Synthetic scene, not gameplay/Android.");Console.WriteLine(checks+" target Engine/OpenGL Excalibur checks passed: "+output);
        }
        finally{Display.RenderTarget=null;target.Dispose();swordTexture.Dispose();scene.GetType().GetMethod("Dispose",F).Invoke(scene,null);}
    }
}
