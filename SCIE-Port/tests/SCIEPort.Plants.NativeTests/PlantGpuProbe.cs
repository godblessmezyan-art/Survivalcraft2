using System;
using System.IO;
using System.Reflection;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;

public static class PlantGpuBootstrap
{
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string name=new AssemblyName(e.Name).Name;foreach(string ext in new[]{".dll",".exe"}){string p=Path.Combine(args[0],name+ext);if(File.Exists(p))return Assembly.LoadFrom(p);}return null;};
        try{typeof(PlantGpuBootstrap).Assembly.GetType("PlantGpuProbe").GetMethod("Run").Invoke(null,new object[]{args[1],args[2]});return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
public static class PlantGpuProbe
{
    const int Width=1440,Height=900;
    static MethodInfo build;
    static Assembly mod;
    static PrimitivesRenderer3D renderer;
    static RenderTarget2D target;
    static string output;
    static int triangles;
    static BlockMesh Mesh(int id,int data){return (BlockMesh)build.Invoke(null,new object[]{id,data});}
    public static void Run(string assembly,string directory)
    {
        mod=Assembly.LoadFrom(assembly);build=mod.GetType("SCIEPort.Features.Plants.PlantGeometry",true).GetMethod("Build",BindingFlags.NonPublic|BindingFlags.Static);output=directory;
        Window.Created+=()=>{object w=typeof(Window).GetField("m_gameWindow",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);w.GetType().GetProperty("Visible").SetValue(w,false,null);};
        Exception failure=null;
        Window.Frame+=()=>{try{Execute();}catch(Exception e){failure=e;}finally{Window.Close();}};
        Window.Run(Width,Height,WindowMode.Fixed,"Plants synthetic target Engine fixture");if(failure!=null)throw failure;
    }
    static void Add(int id,int data,Vector3 offset)
    {
        var mesh=Mesh(id,data);var batch=renderer.FlatBatch(0,DepthStencilState.Default,RasterizerState.CullCounterClockwise,BlendState.Opaque);
        for(int i=0;i<mesh.Indices.Count;i+=3)
        {
            var a=mesh.Vertices.Array[mesh.Indices.Array[i]];var b=mesh.Vertices.Array[mesh.Indices.Array[i+1]];var c=mesh.Vertices.Array[mesh.Indices.Array[i+2]];
            batch.QueueTriangle(a.Position+offset,b.Position+offset,c.Position+offset,a.Color,b.Color,c.Color);triangles++;
        }
    }
    static void Tree(Vector3 offset,int phase)
    {
        Add(939,16|7,offset);
        for(int y=1;y<=2;y++)Add(939,32|7|(1<<6)|(1<<8)|(y<<10),offset+new Vector3(0,y,0));
        var rules=build.DeclaringType.Assembly.GetType("SCIEPort.Features.Apple.AppleTreeRules",true);
        var parts=(System.Collections.IEnumerable)rules.GetMethod("Parts").Invoke(null,null);
        foreach(int[] d in parts)
        {
            if(d[3]!=48)continue;
            int data = 48 | ((d[0] + 1) << 6) | ((d[2] + 1) << 8) | (d[1] << 10);
            if ((bool)rules.GetMethod("BearsFruit").Invoke(null, new object[] { data })) data |= phase;
            Add(939, data, offset + new Vector3(d[0], d[1], d[2]));
        }
    }
    static void Begin()
    {
        triangles=0;renderer.Clear();Display.RenderTarget=target;Display.Viewport=new Viewport(0,0,Width,Height);Display.Clear(new Color(218,227,221),1,0);
        var ground=renderer.FlatBatch(-1,DepthStencilState.Default,RasterizerState.CullNone,BlendState.Opaque);
        for(int x=-12;x<12;x++)for(int z=-8;z<8;z++)ground.QueueQuad(new Vector3(x,-.015f,z),new Vector3(x+1,-.015f,z),new Vector3(x+1,-.015f,z+1),new Vector3(x,-.015f,z+1),((x+z)&1)==0?new Color(135,153,109):new Color(140,157,113));
    }
    static void Save(string name,Vector3 eye,Vector3 at,float fov)
    {
        Matrix view=Matrix.CreateLookAt(eye,at,Vector3.UnitY)*Matrix.CreatePerspectiveFieldOfView(fov*MathUtils.PI/180,(float)Width/Height,.1f,100);
        renderer.Flush(view);
        Color[] pixels=new Color[Width*Height];target.GetData(pixels,0,new Rectangle(0,0,Width,Height));int colored=0;
        foreach(Color c in pixels)if(c.G>c.R*1.12f&&c.G>c.B*1.2f&&c.G<140)colored++;
        using(var file=File.Create(Path.Combine(output,name+".png")))RenderTarget2D.Save(target,file,ImageFileFormat.Png,false);
        if(colored<300)throw new Exception("Model missing from native culled GPU render: "+name);
        Console.WriteLine(name+": "+triangles+" triangles, "+colored+" plant pixels (synthetic Engine/OpenGL scene)");
    }
    static void Execute()
    {
        renderer=new PrimitivesRenderer3D();target=new RenderTarget2D(Width,Height,1,ColorFormat.Rgba8888,DepthFormat.Depth24Stencil8);
        ModsManager.ModHooks=new System.Collections.Generic.Dictionary<string,ModsManager.ModHook>();
        LightingManager.CalculateLightingTables();
        try
        {
            Begin();int[] ids={929,936,934,911,903,915,905,932,909};
            for(int i=0;i<ids.Length;i++)Add(ids[i],ids[i]==903?1:ids[i]==915?0:7,new Vector3(-2.1f+i%3*1.6f,0,.9f-i/3*1.5f));
            Save("crops",new Vector3(4.2f,3.8f,6.5f),new Vector3(0,.35f,-.3f),41);
            Begin();Tree(new Vector3(-.5f,0,-.5f),15);Add(939,0,new Vector3(2.0f,0,.5f));Add(939,3,new Vector3(2.8f,0,.5f));Add(939,6,new Vector3(3.6f,0,.5f));
            Save("apple-ripe",new Vector3(7.5f,5.3f,9),new Vector3(.9f,2.0f,0),40);
            Begin();Tree(new Vector3(-.5f,0,-.5f),1);Save("apple-blossom",new Vector3(6.1f,4.8f,8.3f),new Vector3(0,2.2f,0),37);
            Begin();Tree(new Vector3(-.5f,0,-.5f),3);Save("apple-green",new Vector3(-6.1f,4.8f,-8.3f),new Vector3(0,2.2f,0),37);
            Begin();for(int stage=0;stage<8;stage++)Add(929,stage,new Vector3(-3.5f+stage,0,0));
            Save("corn-stages",new Vector3(.3f,2.9f,8.7f),new Vector3(.3f,.35f,.5f),45);
            Begin();
            string[] classes={"Apple.ApplePlantBlock","Apple.AppleBlock","Crops.CornStalkBlock","Flax.FlaxPlantBlock","Sunflower.SunflowerPlantBlock","Bamboo.BambooPlantBlock","KellyPlants.WildBerryPlantBlock","KellyPlants.CloudberryPlantBlock","KellyPlants.CattailBlock","KellyPlants.WildBerryBlock","KellyPlants.CloudberryBlock","KellyPlants.CattailShootBlock","KellyPlants.CactusFruitBlock","KellyPlants.AcornBlock","KellyPlants.MushroomPatchBlock","KellyPlants.FlyAgaricPatchBlock",
                "Crops.CornSeedBlock","Crops.CornEarBlock","Flax.FlaxFiberBlock","Sunflower.SunflowerSeedBlock","KellyPlants.SoybeanFruitBlock"};
            for(int i=0;i<classes.Length;i++)
            {
                Block block=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features."+classes[i],true));
                block.BlockIndex=(int)block.GetType().GetField("Index").GetRawConstantValue();block.Initialize();
                Matrix position=Matrix.CreateTranslation(-2.0f+i%4*1.4f,.5f,.8f-i/4*1.2f);
                int data = block.BlockIndex == 903 ? 1 :
                    block.BlockIndex == 939 || block.BlockIndex == 929 || block.BlockIndex == 934 || block.BlockIndex == 936 ? 7 : 0;
                block.DrawBlock(renderer, Terrain.MakeBlockValue(block.BlockIndex, 0, data), Color.White, 1f,
                    ref position, new DrawBlockEnvironmentData { DrawBlockMode = DrawBlockMode.UI, Light = 15 });
                triangles += Mesh(block.BlockIndex, data).Indices.Count / 3;
            }
            Save("native-items",new Vector3(5.1f,4.0f,7.8f),new Vector3(.2f,.30f,-1.3f),42);
        }
        finally{Display.RenderTarget=null;target.Dispose();}
    }
}
