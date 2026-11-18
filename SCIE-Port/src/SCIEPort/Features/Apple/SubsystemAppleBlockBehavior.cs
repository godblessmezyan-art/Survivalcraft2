#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using SCIEPort.Features.Plants;
using TemplatesDatabase;

namespace SCIEPort.Features.Apple
{
    public sealed class SubsystemAppleBlockBehavior : SubsystemPollableBlockBehavior,IUpdateable
    {
        private static readonly int[] s_handledBlocks={ApplePlantBlock.Index,AppleBlock.Index};
        private SubsystemSeasons m_seasons;
        private SubsystemGameInfo m_gameInfo;
        private int m_budget=AppleTreeRules.FrameWrites;
        private bool m_faulted;
        private bool m_applying;
        public override int[] HandledBlocks => s_handledBlocks;
        public UpdateOrder UpdateOrder => UpdateOrder.Default;
        public void Update(float dt){m_budget=AppleTreeRules.FrameWrites;}
        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_seasons = Project.FindSubsystem<SubsystemSeasons>(false);
            m_gameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
            if (SubsystemTerrain.TerrainContentsGenerator != null &&
                !(SubsystemTerrain.TerrainContentsGenerator is PlantTerrainGenerator))
            {
                SubsystemTerrain.TerrainContentsGenerator =
                    new PlantTerrainGenerator(SubsystemTerrain.TerrainContentsGenerator, m_gameInfo);
            }
        }
        private int SeasonCode => m_seasons==null || (m_gameInfo != null && !m_gameInfo.WorldSettings.AreSeasonsChanging) ? -1:m_seasons.Season==Season.Winter?0:m_seasons.Season==Season.Spring?1:m_seasons.Season==Season.Summer?2:3;
        internal static bool IsGround(int id) => PlantHabitat.IsRootingSoil(id);
        private int Read(Point3 p) => Terrain.ReplaceLight(SubsystemTerrain.Terrain.GetCellValue(p.X,p.Y,p.Z),0);
        private bool Loaded(Point3 p)
        {
            TerrainChunk chunk=SubsystemTerrain.Terrain.GetChunkAtCell(p.X,p.Z);
            return p.Y>=0&&p.Y<=255&&chunk!=null&&chunk.State>TerrainChunkState.InvalidContents4;
        }
        private bool Apply(List<Point3> cells,List<int> after)
        {
            if(m_faulted||m_applying||CommonLib.WorkType==WorkType.Client||cells.Count==0||cells.Count>AppleTreeRules.MaxChanges||m_budget<cells.Count*2) return false;
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
                int dx = AppleTreeRules.OffsetX(data);
                int dz = AppleTreeRules.OffsetZ(data);
                int height = AppleTreeRules.OffsetY(data);
                if (Math.Abs(dx) > 1 || Math.Abs(dz) > 1 || height < 1 || height > 4)
                {
                    Change(p, 0);
                    return;
                }
                var root = new Point3(x - dx, y - height, z - dz);
                if(!Loaded(root)) return;
                int rootValue=Read(root);
                if(Terrain.ExtractContents(rootValue)!=ApplePlantBlock.Index||AppleTreeRules.Part(Terrain.ExtractData(rootValue))!=AppleTreeRules.Root)
                {Change(p,0);return;}
                for(int h=1;h<AppleTreeRules.OffsetY(data)&&h<=2;h++)
                {
                    Point3 support=new Point3(root.X,root.Y+h,root.Z);
                    int sv=Read(support),sd=Terrain.ExtractData(sv);
                    if(Terrain.ExtractContents(sv)!=ApplePlantBlock.Index||AppleTreeRules.Part(sd)!=AppleTreeRules.Trunk||AppleTreeRules.OffsetY(sd)!=h||AppleTreeRules.OffsetX(sd)!=0||AppleTreeRules.OffsetZ(sd)!=0)
                    {Change(p,0);return;}
                }
                if (part == AppleTreeRules.Leaf && PlantGrowthSite.IsLiving(SubsystemTerrain))
                {
                    int branchLight = terrain.GetCellLight(x, y + 1, z);
                    int sideX = x + dx;
                    int sideZ = z + dz;
                    if (Loaded(new Point3(sideX, y, sideZ)))
                        branchLight = Math.Max(branchLight, terrain.GetCellLight(sideX, y, sideZ));
                    int temperature = PlantHabitat.TemperatureAt(terrain, x, y, z);
                    int rootSoil = terrain.GetCellValue(root.X, root.Y - 1, root.Z);
                    bool watered = Terrain.ExtractContents(rootSoil) == SoilBlock.Index && SoilBlock.GetHydration(Terrain.ExtractData(rootSoil));
                    bool suitable = PlantHabitat.For(ApplePlantBlock.Index)
                        .AllowsGrowth(temperature, terrain.GetSeasonalHumidity(x, z), branchLight, watered);
                    // Old saves can still contain ripe fruit on the upper crown.
                    int next = AppleTreeRules.BearsFruit(data)
                        ? AppleTreeRules.NextFruit(data, SeasonCode, suitable, pollPass % 4 == 0)
                        : SeasonCode == 0 || SeasonCode == 1 || !AppleRules.GetFruiting(data)
                            ? data & ~15 : data;
                    if(next!=data) Change(p,Terrain.ReplaceData(value,next));
                }
                return;
            }
            int soilValue=terrain.GetCellValue(x,y-1,z),soil=Terrain.ExtractContents(soilValue);
            if(!IsGround(soil)){Change(p,0);return;}
            if (!PlantGrowthSite.IsLiving(SubsystemTerrain)) return;
            if(part==AppleTreeRules.Root)
            {
                if (SeasonCode == 1 || SeasonCode < 0 && pollPass % 8 == 0) RestoreCanopy(p);
                return;
            }
            if(y>250) return;
            int light=Terrain.ExtractLight(terrain.GetCellValue(x,y+1,z));
            int temp=terrain.GetSeasonalTemperature(x,z)+SubsystemWeather.GetTemperatureAdjustmentAtHeight(y);
            int soilData = Terrain.ExtractData(soilValue);
            bool hydrated = soil == SoilBlock.Index && SoilBlock.GetHydration(soilData);
            if (!AppleTreeRules.CanGrow(light, temp, SeasonCode == 0) ||
                !PlantHabitat.For(ApplePlantBlock.Index).AllowsGrowth(temp, terrain.GetSeasonalHumidity(x, z), light, hydrated)) return;
            int interval = AppleRules.GetAppleGrowthInterval(hydrated, soil == SoilBlock.Index ? SoilBlock.GetNitrogen(soilData) : 0, temp);
            if(pollPass%interval!=0) return;
            int size=AppleRules.GetSize(data);
            if(size<6){Change(p,Terrain.ReplaceData(value,AppleRules.SetFruiting(AppleRules.SetSize(data,size+1),false)));return;}
            GrowTree(p,value,soilValue);
        }
        private void GrowTree(Point3 root,int rootValue,int soilValue)
        {
            if(!AppleTreeRules.CanFitTree(root.X,root.Y,root.Z)) return;
            var cells=new List<Point3>();var values=new List<int>();
            foreach (TerrainBrush.Cell part in AppleTreeLayout.ForStage(SeasonCode == 1 ? 1 : 0).Cells)
            {
                if (part.Y == 0) continue;
                var p = new Point3(root.X + part.X, root.Y + part.Y, root.Z + part.Z);
                if (!Loaded(p) || Read(p) != 0) return;
                cells.Add(p);
                values.Add(part.Value);
            }
            if (Read(root) != rootValue) return;
            cells.Add(root);
            values.Add(Terrain.ReplaceData(rootValue, AppleTreeRules.Root | 7));
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
            for (int h = 1; h <= 2; h++)
            {
                int expected = Terrain.MakeBlockValue(ApplePlantBlock.Index, 0, AppleTreeRules.Encode(AppleTreeRules.Trunk, 0, h, 0));
                if (Read(new Point3(root.X, root.Y + h, root.Z)) != expected) return;
            }
            var cells = new List<Point3>();
            var values = new List<int>();
            foreach (TerrainBrush.Cell part in AppleTreeLayout.ForStage(SeasonCode == 1 ? 1 : 0).Cells)
            {
                if (AppleTreeRules.Part(Terrain.ExtractData(part.Value)) != AppleTreeRules.Leaf) continue;
                var p = new Point3(root.X + part.X, root.Y + part.Y, root.Z + part.Z);
                if (!Loaded(p) || Read(p) != 0) continue;
                cells.Add(p);
                values.Add(part.Value);
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
