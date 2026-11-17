using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;

// Exact Windows target assemblies and memory-only terrain. No player world or game connection.
public static class PlantNativeProbe
{
    const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Plants native: "+why);}
    static void Put(object o,string name,object value)
    {
        for(Type t=o.GetType();t!=null;t=t.BaseType)
        {
            FieldInfo f=t.GetField(name,F)??t.GetField("<"+name+">k__BackingField",F);
            if(f!=null){f.SetValue(o,value);return;}
        }
        throw new Exception(name);
    }
    static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,F).Invoke(o,args);}
    public static void Run(Assembly mod)
    {
        Geometry(mod);
        var saved=CommonLib.WorkType;var savedNet=CommonLib.Net;var savedHooks=ModsManager.ModHooks;
        var savedBlocks=(Block[])BlocksManager.Blocks.Clone();
        try
        {
            ModsManager.ModHooks=new Dictionary<string,ModsManager.ModHook>();
            CommonLib.Net=(NetNode)FormatterServices.GetUninitializedObject(typeof(NetNode));
            Put(CommonLib.Net,"PendingPackages",new List<IPackage>());CommonLib.Net.Clients=new Dictionary<int,Client>();
            foreach(WorkType mode in new[]{WorkType.Local,WorkType.Server,WorkType.Client}) Runtime(mod,mode);
        }
        finally{CommonLib.WorkType=saved;CommonLib.Net=savedNet;ModsManager.ModHooks=savedHooks;Array.Copy(savedBlocks,BlocksManager.Blocks,savedBlocks.Length);}
        Console.WriteLine("Plants exact-target geometry/growth/authority/native harvest checks passed: "+checks);
    }
    static void Geometry(Assembly mod)
    {
        var g=mod.GetType("SCIEPort.Features.Plants.PlantGeometry",true);
        var build=g.GetMethod("Build",BindingFlags.Static|BindingFlags.NonPublic);
        var visual=g.GetMethod("VisualData",BindingFlags.Static|BindingFlags.NonPublic);
        int[] ids={903,904,905,906,907,908,909,910,911,913,914,915,916,929,932,934,936,939,940};
        foreach(int id in ids)
        {
            var words=new List<int>();
            for(int d=0;d<16;d++)words.Add(d);
            if(id==939)
            {
                for(int h=1;h<=2;h++)words.Add(32|7|(1<<6)|(1<<8)|(h<<10));
                words.Add(16|7);
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)for(int y=2;y<=4;y++)for(int s=0;s<16;s++)
                    words.Add(48|((x+1)<<6)|((z+1)<<8)|(y<<10)|s);
            }
            var keys=new HashSet<int>();
            foreach(int data in words)
            {
                int key=(int)visual.Invoke(null,new object[]{id,data});
                Check(key==(int)visual.Invoke(null,new object[]{id,key}),"canonical visual data is stable");
                if(!keys.Add(key))continue;
                var mesh=(BlockMesh)build.Invoke(null,new object[]{id,key});
                Check(mesh.Vertices.Count>0&&mesh.Indices.Count%3==0&&mesh.Indices.Count/3<=1800,"finite mobile mesh budget "+id+"/"+key);
                for(int i=0;i<mesh.Vertices.Count;i++)
                {
                    var v=mesh.Vertices.Array[i];Vector3 p=v.Position;
                    Check(!float.IsNaN(p.X)&&!float.IsNaN(p.Y)&&!float.IsNaN(p.Z)&&p.X>=-.001f&&p.X<=1.001f&&p.Y>=-.001f&&p.Y<=1.001f&&p.Z>=-.001f&&p.Z<=1.001f,"mesh stays in its occupied cell "+id+"/"+key+": "+p);
                    Check(v.Color.A==255&&v.Face<=5,"opaque shaded faces use the native lighting schema");
                }
                for(int i=0;i<mesh.Indices.Count;i+=3)
                {
                    int a=mesh.Indices.Array[i],b=mesh.Indices.Array[i+1],c=mesh.Indices.Array[i+2];
                    Check(a<mesh.Vertices.Count&&b<mesh.Vertices.Count&&c<mesh.Vertices.Count,"index bounds");
                    Vector3 pa=mesh.Vertices.Array[a].Position,pb=mesh.Vertices.Array[b].Position,pc=mesh.Vertices.Array[c].Position;
                    Check(Vector3.Cross(pb-pa,pc-pa).LengthSquared()>1e-15f,"no degenerate triangles "+id+"/"+key);
                }
            }
            if(id==939)Check(keys.Count<=160,"tree mesh cache stays bounded across ownership/harvest states");
        }
        foreach(string name in new[]{"Apple.ApplePlantBlock","Crops.CornStalkBlock","Flax.FlaxPlantBlock","Sunflower.SunflowerPlantBlock","Bamboo.BambooPlantBlock","KellyPlants.WildBerryPlantBlock","KellyPlants.CloudberryPlantBlock"})
        {
            var b=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features."+name,true));
            b.BlockIndex=(int)b.GetType().GetField("Index").GetRawConstantValue();
            for(int d=0;d<16;d++)
            {
                var boxes=b.GetCustomCollisionBoxes(null,Terrain.MakeBlockValue(b.BlockIndex,0,d));
                Check(boxes.Length>0,"native raycast always receives a nonempty box array");
                foreach(var box in boxes)Check(box.Min.X<=box.Max.X&&box.Min.Y<box.Max.Y&&box.Min.Z<=box.Max.Z,"nonzero pickable height");
            }
        }
        // Native DATA is signed after shifting: all newly used role/offset/harvest bits must round-trip.
        for(int data=0;data<16384;data++)Check(Terrain.ExtractData(Terrain.MakeBlockValue(939,15,data))==data,"native DATA/lighting round trip");
        var concurrent=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Apple.ApplePlantBlock",true));concurrent.BlockIndex=939;
        var results=new BoundingBox[1024][];
        Parallel.For(0,results.Length,i=>results[i]=concurrent.GetCustomCollisionBoxes(null,Terrain.MakeBlockValue(939,0,48|((i%3)<<6)|((i%3)<<8)|(3<<10)|(i%8))));
        for(int i=0;i<results.Length;i++)Check(results[i].Length==1&&results[i][0].Min.Y<results[i][0].Max.Y,"concurrent cold collision/model cache access is safe");
        var food=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Apple.AppleBlock",true));food.BlockIndex=940;
        var oldFood=BlocksManager.Blocks[940];
        try
        {
            var ground=new GraniteBlock();BlocksManager.Blocks[940]=food;
            for(int face=0;face<6;face++) Check(ground.ShouldGenerateFace(null,face,Terrain.MakeBlockValue(3),Terrain.MakeBlockValue(940)),"legacy placed apple never culls any neighboring cube face");
            Check(food.GetPlacementValue(null,null,Terrain.MakeBlockValue(940),new TerrainRaycastResult()).Value==0,"edible apple rejects placement independently of CSV");
            var recovered=new List<BlockDropValue>();bool debris;
            food.GetDropValues(null,Terrain.MakeBlockValue(940),0,0,recovered,out debris);
            Check(recovered.Count==1&&recovered[0].Value==Terrain.MakeBlockValue(940)&&recovered[0].Count==1,"legacy placed apple can be recovered as one food item");
            BlocksManager.Blocks[940]=new OpaqueFoodFixture();
            Check(!ground.ShouldGenerateFace(null,4,Terrain.MakeBlockValue(3),Terrain.MakeBlockValue(940)),"negative control reproduces the old opaque-food xray face hole");
        }
        finally{BlocksManager.Blocks[940]=oldFood;}
    }
    private sealed class OpaqueFoodFixture:FlatBlock { }
    static void Runtime(Assembly mod,WorkType mode)
    {
        CommonLib.WorkType=mode;
        using(var cells=new Terrain())
        {
            TerrainChunk chunk=cells.AllocateChunk(0,0);chunk.State=TerrainChunkState.Valid;
            cells.SeasonTemperature=12;
            var terrain=(SubsystemTerrain)FormatterServices.GetUninitializedObject(typeof(SubsystemTerrain));Put(terrain,"Terrain",cells);
            terrain.m_modifiedCells=new Dictionary<Point3,bool>();
            var updater=(TerrainUpdater)FormatterServices.GetUninitializedObject(typeof(TerrainUpdater));Put(updater,"m_terrain",cells);Put(terrain,"TerrainUpdater",updater);
            var behaviors=new SubsystemBlockBehaviors();behaviors.m_blockBehaviorsByContents=new SubsystemBlockBehavior[BlocksManager.Blocks.Length][];
            for(int i=0;i<behaviors.m_blockBehaviorsByContents.Length;i++)behaviors.m_blockBehaviorsByContents[i]=new SubsystemBlockBehavior[0];
            Put(terrain,"m_subsystemBlockBehaviors",behaviors);
            var runtime=(SubsystemPollableBlockBehavior)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Apple.SubsystemAppleBlockBehavior",true));
            Put(runtime,"SubsystemTerrain",terrain);var seasons=new SubsystemSeasons{Season=Season.Spring};Put(runtime,"m_seasons",seasons);
            behaviors.m_blockBehaviorsByContents[939]=new[]{runtime};behaviors.m_blockBehaviorsByContents[940]=new[]{runtime};
            var apple=(Block)Activator.CreateInstance(mod.GetType("SCIEPort.Features.Apple.ApplePlantBlock",true));apple.BlockIndex=939;BlocksManager.Blocks[939]=apple;
            const int y=64;cells.SetCellValueFast(8,y-1,8,Terrain.MakeBlockValue(DirtBlock.Index));
            cells.SetCellValueFast(8,y,8,Terrain.MakeBlockValue(939,0,0));cells.SetCellValueFast(8,y+1,8,Terrain.MakeBlockValue(0,15,0));
            for(int s=0;s<6;s++){Call(runtime,"Update",.016f);runtime.OnPoll(Terrain.MakeBlockValue(939),8,y,8,0);}
            int grown=cells.GetCellValue(8,y,8);
            Check((Terrain.ExtractData(grown)&7)==(mode==WorkType.Client?0:6),"native poll grows only on authoritative sides "+mode);
            if(mode==WorkType.Client)
            {
                var clientDrop=new BlockDropValue{Value=Terrain.MakeBlockValue(940),Count=2};int replacement=0;
                runtime.OnItemHarvested(8,66,8,Terrain.MakeBlockValue(939,0,48|15),ref clientDrop,ref replacement);
                Check(replacement==0,"client cannot mutate harvest state");return;
            }
            // Obstruction and an unloaded adjoining chunk both leave the original sapling unchanged.
            cells.SetCellValueFast(9,y+2,8,3);Call(runtime,"Update",.016f);runtime.OnPoll(grown,8,y,8,0);
            Check(Terrain.ExtractData(cells.GetCellValue(8,y,8))==6&&cells.GetCellValue(9,y+2,8)==3,"occupied canopy cancels the entire expansion");
            cells.SetCellValueFast(9,y+2,8,0);
            Call(runtime,"Update",.016f);runtime.OnPoll(grown,8,y,8,0);
            Check((Terrain.ExtractData(cells.GetCellValue(8,y,8))&48)==16,"sapling becomes a real tree");
            int count=0;
            for(int x=7;x<=9;x++)for(int h=0;h<5;h++)for(int z=7;z<=9;z++)if(Terrain.ExtractContents(cells.GetCellValue(x,y+h,z))==939)count++;
            Check(count==21,"twenty parts plus their root are committed");
            cells.SetCellValueFast(12,y-1,8,Terrain.MakeBlockValue(DirtBlock.Index));cells.SetCellValueFast(12,y,8,Terrain.MakeBlockValue(939,0,6));cells.SetCellValueFast(12,y+1,8,Terrain.MakeBlockValue(0,15,0));
            runtime.OnPoll(0,12,y,8,0);Check(Terrain.ExtractData(cells.GetCellValue(12,y,8))==6,"second tree waits when the frame budget is exhausted");
            Call(runtime,"Update",.016f);runtime.OnPoll(0,12,y,8,0);Check((Terrain.ExtractData(cells.GetCellValue(12,y,8))&48)==16,"new frame resumes deferred growth");
            int fertile=SoilBlock.SetNitrogen(SoilBlock.SetHydration(0,true),2);
            cells.SetCellValueFast(4,y-1,12,Terrain.MakeBlockValue(SoilBlock.Index,15,fertile));
            cells.SetCellValueFast(4,y,12,Terrain.MakeBlockValue(939,15,6));cells.SetCellValueFast(4,y+1,12,Terrain.MakeBlockValue(0,15,0));
            Call(runtime,"Update",.016f);runtime.OnPoll(0,4,y,12,0);
            Check((Terrain.ExtractData(cells.GetCellValue(4,y,12))&48)==16,"fertile illuminated soil commits without a false light-signature mismatch");
            Check(SoilBlock.GetNitrogen(Terrain.ExtractData(cells.GetCellValue(4,y-1,12)))==1,"tree completion consumes one nitrogen exactly once");
            Check(!(bool)runtime.GetType().GetField("m_faulted",F).GetValue(runtime),"normal native light reset does not halt growth");
            cells.SetCellValueFast(15,y-1,8,Terrain.MakeBlockValue(DirtBlock.Index));cells.SetCellValueFast(15,y,8,Terrain.MakeBlockValue(939,0,6));cells.SetCellValueFast(15,y+1,8,Terrain.MakeBlockValue(0,15,0));
            Call(runtime,"Update",.016f);runtime.OnPoll(0,15,y,8,0);Check(Terrain.ExtractData(cells.GetCellValue(15,y,8))==6,"unloaded cross-chunk canopy defers without partial writes");
            // Real GetDropValues -> OnItemHarvested -> ChangeCell lifecycle, with native DATA and network queue.
            int leafValue=cells.GetCellValue(9,y+2,8),data=Terrain.ExtractData(leafValue);
            data=(data&~15)|15;leafValue=Terrain.MakeBlockValue(939,0,data);cells.SetCellValueFast(9,y+2,8,leafValue);
            var drops=new List<BlockDropValue>();bool debris;
            apple.GetDropValues(terrain,leafValue,0,0,drops,out debris);Check(drops.Count==1&&drops[0].Count==2,"ripe canopy yields two apples and no sapling");
            int next=0;var drop=drops[0];runtime.OnItemHarvested(9,y+2,8,leafValue,ref drop,ref next);
            Check(Terrain.ExtractContents(next)==939&&(Terrain.ExtractData(next)&8192)!=0&&(Terrain.ExtractData(next)&8)==0,"native harvest writes an empty locked canopy");
            terrain.ChangeCell(9,y+2,8,next,true,null);
            drops.Clear();apple.GetDropValues(terrain,next,0,0,drops,out debris);Check(drops.Count==0,"repeated dig cannot duplicate apples");
            // A removed trunk clears its own upper parts without affecting the neighboring tree.
            cells.SetCellValueFast(8,y+1,8,0);Call(runtime,"Update",.016f);runtime.OnPoll(0,9,y+2,8,0);
            Check(Terrain.ExtractContents(cells.GetCellValue(9,y+2,8))==0,"unsupported canopy is removed");
            Check((Terrain.ExtractData(cells.GetCellValue(12,y,8))&48)==16,"cleanup does not touch another tree");
            Call(runtime,"Update",.016f);runtime.OnPoll(0,0,0,0,0);runtime.OnPoll(0,0,255,0,0);runtime.OnPoll(0,int.MaxValue,64,0,0);
            Check(true,"world boundary polls safely return");
        }
    }
}
