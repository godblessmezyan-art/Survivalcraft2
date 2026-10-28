// Compiled only by Test-DevouringEye.ps1 against the exact target .NET Framework assemblies.
// Not included in the pure net8 test executable.
using System;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using Engine;
using Game;
using GameEntitySystem;
using TemplatesDatabase;

public static class DevouringNativeApiProbe
{
    public static void ResolveSerializerDependencies(string directory)
    {
        AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=> {
            string name=new AssemblyName(args.Name).Name;
            string path=Path.Combine(directory,name+".dll");
            if(!File.Exists(path))return null;
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name==name)??Assembly.Load(File.ReadAllBytes(path));
        };
    }
    public static byte[] DecodeContent(byte[] bytes)
    {
        byte[] header=Encoding.UTF8.GetBytes("再乱改就跑路，谁也别想玩！");
        Check(bytes.Take(header.Length).SequenceEqual(header),"unexpected Content.scpak header");
        byte[] result=new byte[bytes.Length-header.Length];int half=(result.Length+1)/2;
        for(int i=0;i<result.Length;i++)result[i]=bytes[header.Length+(i%2==0?i/2:half+i/2)];
        return result;
    }
    private static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    public static void Run(XElement databaseXml)
    {
        var database=XmlDatabaseSerializer.LoadDatabase(databaseXml);
        var game=new GameDatabase(database);
        var project=new Project();project.m_gameDatabase=game;
        project.m_projectTemplate=database.FindDatabaseObject("Project",game.ProjectTemplateType,true);
        var values=new ValuesDictionary();values.DatabaseObject=database.FindDatabaseObject("Boat",game.EntityTemplateType,true);
        // Empty native entity fixture isolates the real Project membership/save API, not a simulated tank.
        var entity=new Entity(project,values);
        int removed=0;project.EntityRemoved+=(s,e)=>removed++;
        project.AddEntity(entity);Check(project.Save().EntityDataList.EntitiesData.Count==1,"native save before removal");
        project.RemoveEntity(entity,false);Check(!entity.IsAddedToProject&&removed==1,"native removal membership and event");
        Check(project.Save().EntityDataList.EntitiesData.Count==0,"removed entity must not be serialized");
        project.RemoveEntity(entity,false);Check(removed==1,"duplicate removal does not fire twice");
        project.AddEntity(entity);Check(project.Save().EntityDataList.EntitiesData.Count==1,"rollback re-add serialized once");
        project.RemoveEntity(entity,true);Check(project.Save().EntityDataList.EntitiesData.Count==0,"disposed source absent from save");
        BlocksManager.Blocks[3]=new GraniteBlock{MaxStacking=40};
        var inventory=new ComponentInventory();
        for(int i=0;i<12;i++)inventory.m_slots.Add(new ComponentInventoryBase.Slot());
        int value=Terrain.MakeBlockValue(3,0,PaintedCubeBlock.SetColor(0,7));
        Check(inventory.AddNetSlotItems(10,value,40),"native inventory grant");
        Check(!inventory.AddNetSlotItems(10,value,1)&&inventory.GetSlotCount(10)==40,"native full slot rejects without loss");
        Check(inventory.RemoveNetSlotItems(10,1)==1&&inventory.GetSlotCount(10)==39,"native item rollback removes exactly one");
        var creative=new ComponentCreativeInventory();creative.OpenSlotsCount=2;creative.m_slots.Add(0);creative.m_slots.Add(0);
        Check(creative.AddNetSlotItems(0,value,1)&&creative.GetSlotValue(0)==value,"creative native grant value");
        creative.RemoveNetSlotItems(0,9999);Check(creative.GetSlotValue(0)==0,"creative rollback empties native slot");
        var terrain=new Terrain();terrain.AllocateChunk(0,0);
        terrain.SetCellValueFast(1,64,1,value);terrain.SetCellValueFast(2,64,1,3);
        int original=terrain.GetCellValue(1,64,1);terrain.SetCellValueFast(1,64,1,0);
        Check(terrain.GetCellValue(2,64,1)==3,"single-cell fixture preserves neighbour");
        terrain.SetCellValueFast(1,64,1,original);Check(terrain.GetCellValue(1,64,1)==value,"real terrain data roundtrip preserves color");
        terrain.Dispose();project.Dispose();
        Console.WriteLine("Devouring Eye native probe: real Project removal/save, inventory/full/rollback, creative and terrain DATA checks passed (isolated fixtures, not Android/gameplay).");
    }
    public static void JsonProbe()
    {
        var root=new ValuesDictionary();root.SetValue("ProbeToken","native-journal-check");root.SetValue("Entities",new ValuesDictionary());
        var parsed=new ValuesDictionary();byte[] packed;parsed.ApplyOverridesUseJson(root.ToJsonText(),out packed);
        Check(parsed.GetValue<string>("ProbeToken")=="native-journal-check"&&parsed.GetValue<ValuesDictionary>("Entities").Count==0,"native JSON checkpoint acknowledgement roundtrip");
        Console.WriteLine("Devouring Eye native JSON acknowledgement roundtrip passed.");
    }
}
