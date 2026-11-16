#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using TemplatesDatabase;

namespace SCIEPort.Features.Apple
{
    public sealed class SubsystemAppleBlockBehavior : SubsystemPollableBlockBehavior,IUpdateable
    {
        private static readonly int[] s_handledBlocks={ApplePlantBlock.Index,AppleBlock.Index};
        private SubsystemSeasons m_seasons;
        private int m_budget=AppleTreeRules.FrameWrites;
        private bool m_faulted;
        private bool m_applying;
        public override int[] HandledBlocks => s_handledBlocks;
        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        public void Update(float dt){m_budget=AppleTreeRules.FrameWrites;}
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_seasons=Project.FindSubsystem<SubsystemSeasons>(false);
        }
        private int SeasonCode => m_seasons==null?-1:m_seasons.Season==Season.Winter?0:m_seasons.Season==Season.Spring?1:m_seasons.Season==Season.Summer?2:3;
        internal static bool IsGround(int id) => id==SoilBlock.Index||id==DirtBlock.Index||id==GrassBlock.Index;
        private int Read(Point3 p) => Terrain.ReplaceLight(SubsystemTerrain.Terrain.GetCellValue(p.X,p.Y,p.Z),0);
        private bool Loaded(Point3 p)
        {
            TerrainChunk chunk=SubsystemTerrain.Terrain.GetChunkAtCell(p.X,p.Z);
            return p.Y>=0&&p.Y<=255&&chunk!=null&&chunk.State>TerrainChunkState.InvalidContents4;
        }
        private bool Apply(List<Point3> cells,List<int> after)
        {
            if(m_faulted||CommonLib.WorkType==WorkType.Client||cells.Count==0||cells.Count>AppleTreeRules.MaxChanges||m_budget<cells.Count*2) return false;
            int[] before=new int[cells.Count];
            int[] normalizedAfter=after.ToArray();
            for(int i=0;i<before.Length;i++)
            {
                if(!Loaded(cells[i])) return false;
                before[i]=Read(cells[i]);normalizedAfter[i]=Terrain.ReplaceLight(normalizedAfter[i],0);
            }
            m_budget-=cells.Count*2;
            bool undoFailed;Exception error;
            bool ok;
            m_applying=true;
            try {ok=AppleTreeTransaction.Apply(before,normalizedAfter,i=>Read(cells[i]),(i,v)=>
                SubsystemTerrain.ChangeCell(cells[i].X,cells[i].Y,cells[i].Z,v,true,null),out undoFailed,out error);}
            finally {m_applying=false;}
            if(undoFailed){m_faulted=true;Log.Error("[SCIEPort:Apple] Growth halted after undo failure: "+error);}
            else if(error!=null) Log.Warning("[SCIEPort:Apple] Growth rolled back: "+error.Message);
            return ok;
        }
        private bool Change(Point3 p,int value) => Apply(new List<Point3>{p},new List<int>{value});
        public override void OnPoll(int value,int x,int y,int z,int pollPass)
        {
            if(CommonLib.WorkType==WorkType.Client||m_faulted||!AppleTreeRules.InBounds(x,y,z)) return;
            Point3 p=new Point3(x,y,z);
            if(!Loaded(p)) return;
            value=Read(p);
            if(Terrain.ExtractContents(value)!=ApplePlantBlock.Index) return;
            int data=Terrain.ExtractData(value),part=AppleTreeRules.Part(data);
            Terrain terrain=SubsystemTerrain.Terrain;
            if(part==AppleTreeRules.Trunk||part==AppleTreeRules.Leaf)
            {
                Point3 root=new Point3(x-AppleTreeRules.OffsetX(data),y-AppleTreeRules.OffsetY(data),z-AppleTreeRules.OffsetZ(data));
                if(!Loaded(root)) return;
                int rootValue=Read(root);
                if(Terrain.ExtractContents(rootValue)!=ApplePlantBlock.Index||AppleTreeRules.Part(Terrain.ExtractData(rootValue))!=AppleTreeRules.Root)
                {Change(p,0);return;}
                for(int h=1;h<AppleTreeRules.OffsetY(data)&&h<=2;h++)
                {
                    Point3 support=new Point3(root.X,root.Y+h,root.Z);
                    int sv=Read(support),sd=Terrain.ExtractData(sv);
                    if(Terrain.ExtractContents(sv)!=ApplePlantBlock.Index||AppleTreeRules.Part(sd)!=AppleTreeRules.Trunk||AppleTreeRules.OffsetY(sd)!=h)
                    {Change(p,0);return;}
                }
                if(part==AppleTreeRules.Leaf)
                {
                    int temperature=terrain.GetSeasonalTemperature(x,z)+SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
                    bool suitable=AppleTreeRules.CanGrow(Terrain.ExtractLight(terrain.GetCellValue(x,y+1,z)),temperature,false);
                    int next=AppleTreeRules.NextFruit(data,SeasonCode,suitable,pollPass%4==0);
                    if(next!=data) Change(p,Terrain.ReplaceData(value,next));
                }
                return;
            }
            int soilValue=terrain.GetCellValue(x,y-1,z),soil=Terrain.ExtractContents(soilValue);
            if(!IsGround(soil)){Change(p,0);return;}
            if(part==AppleTreeRules.Root)
            {
                if(SeasonCode==1) RestoreCanopy(p);
                return;
            }
            if(y>250) return;
            int light=Terrain.ExtractLight(terrain.GetCellValue(x,y+1,z));
            int temp=terrain.GetSeasonalTemperature(x,z)+SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
            if(!AppleTreeRules.CanGrow(light,temp,SeasonCode==0)) return;
            int soilData=Terrain.ExtractData(soilValue);
            int interval=AppleRules.GetAppleGrowthInterval(soil==SoilBlock.Index&&SoilBlock.GetHydration(soilData),soil==SoilBlock.Index?SoilBlock.GetNitrogen(soilData):0,temp);
            if(pollPass%interval!=0) return;
            int size=AppleRules.GetSize(data);
            if(size<6){Change(p,Terrain.ReplaceData(value,AppleRules.SetFruiting(AppleRules.SetSize(data,size+1),false)));return;}
            GrowTree(p,value,soilValue);
        }
        private void GrowTree(Point3 root,int rootValue,int soilValue)
        {
            if(!AppleTreeRules.CanFitTree(root.X,root.Y,root.Z)) return;
            var cells=new List<Point3>();var values=new List<int>();
            foreach(int[] d in AppleTreeRules.Parts())
            {
                Point3 p=new Point3(root.X+d[0],root.Y+d[1],root.Z+d[2]);
                if(!Loaded(p)||Terrain.ExtractContents(Read(p))!=0) return;
                int data=AppleTreeRules.Encode(d[3],d[0],d[1],d[2]);
                if(d[3]==AppleTreeRules.Leaf) data=AppleTreeRules.NextFruit(data,SeasonCode,true,false);
                cells.Add(p);values.Add(Terrain.MakeBlockValue(ApplePlantBlock.Index,0,data));
            }
            cells.Add(root);values.Add(Terrain.ReplaceData(rootValue,AppleTreeRules.Root|7));
            if(Terrain.ExtractContents(soilValue)==SoilBlock.Index&&SoilBlock.GetNitrogen(Terrain.ExtractData(soilValue))>0)
            {
                cells.Add(new Point3(root.X,root.Y-1,root.Z));
                values.Add(Terrain.ReplaceData(soilValue,SoilBlock.SetNitrogen(Terrain.ExtractData(soilValue),SoilBlock.GetNitrogen(Terrain.ExtractData(soilValue))-1)));
            }
            Apply(cells,values);
        }
        public override void OnNeighborBlockChanged(int x,int y,int z,int nx,int ny,int nz)
        {
            if(CommonLib.WorkType==WorkType.Client||m_faulted||!AppleTreeRules.InBounds(x,y,z)) return;
            int value=SubsystemTerrain.Terrain.GetCellValue(x,y,z),data=Terrain.ExtractData(value);
            if(Terrain.ExtractContents(value)!=ApplePlantBlock.Index) return;
            int part=AppleTreeRules.Part(data);
            if((part==0||part==AppleTreeRules.Root)&&!IsGround(Terrain.ExtractContents(SubsystemTerrain.Terrain.GetCellValue(x,y-1,z)))) Change(new Point3(x,y,z),0);
        }
        private void RestoreCanopy(Point3 root)
        {
            if(!AppleTreeRules.CanFitTree(root.X,root.Y,root.Z)) return;
            for(int h=1;h<=2;h++)
            {
                int v=Read(new Point3(root.X,root.Y+h,root.Z));
                if(Terrain.ExtractContents(v)!=ApplePlantBlock.Index||AppleTreeRules.Part(Terrain.ExtractData(v))!=AppleTreeRules.Trunk) return;
            }
            var cells=new List<Point3>();var values=new List<int>();
            foreach(int[] d in AppleTreeRules.Parts())
            {
                if(d[3]!=AppleTreeRules.Leaf) continue;
                Point3 p=new Point3(root.X+d[0],root.Y+d[1],root.Z+d[2]);
                if(!Loaded(p)||Terrain.ExtractContents(Read(p))!=0) continue;
                cells.Add(p);values.Add(Terrain.MakeBlockValue(ApplePlantBlock.Index,0,AppleTreeRules.Encode(d[3],d[0],d[1],d[2])|1));
            }
            Apply(cells,values);
        }
        public override void OnItemHarvested(int x,int y,int z,int value,ref BlockDropValue dropValue,ref int newValue)
        {
            // Native DestroyCell invokes the behavior for the dropped apple before ChangeCell.
            // Returning the empty canopy as newValue gives server and clients one identical write.
            int data=Terrain.ExtractData(value);
            if(m_applying||CommonLib.WorkType==WorkType.Client||!AppleTreeRules.InBounds(x,y,z)||Terrain.ExtractContents(newValue)!=0
                ||Terrain.ExtractContents(value)!=ApplePlantBlock.Index||Terrain.ExtractContents(dropValue.Value)!=AppleBlock.Index
                ||AppleTreeRules.Part(data)!=AppleTreeRules.Leaf||!AppleRules.GetFruiting(data)) return;
            int next=AppleRules.SetFruiting(AppleRules.SetSize(data,0),false)|AppleTreeRules.Harvested;
            newValue=Terrain.MakeBlockValue(ApplePlantBlock.Index,0,next);
        }
    }
}
