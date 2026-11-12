using System;
using System.Collections.Generic;
using System.Reflection;
using Game;
using GameEntitySystem;
using Game.NetWork;
using Game.NetWork.Packages;
using SCIEPort.Features.Blueprints;
using SCIEPort.Features.Blueprints.Core;

internal static partial class BlueprintTests
{
    private static SubsystemTerrain NativeTerrain(Terrain cells)
    {
        var native=Bare<SubsystemTerrain>();Put(native,"<Terrain>k__BackingField",cells);
        native.m_modifiedCells=new Dictionary<Engine.Point3,bool>();
        var updater=Bare<TerrainUpdater>();Put(updater,"m_terrain",cells);Put(native,"<TerrainUpdater>k__BackingField",updater);
        native.m_subsystemBlockBehaviors=new SubsystemBlockBehaviors();
        native.m_subsystemBlockBehaviors.m_blockBehaviorsByContents=new SubsystemBlockBehavior[BlocksManager.Blocks.Length][];
        for(int i=0;i<BlocksManager.Blocks.Length;i++)native.m_subsystemBlockBehaviors.m_blockBehaviorsByContents[i]=new SubsystemBlockBehavior[0];
        return native;
    }
    private static void NativeDirectTests()
    {
        var mode=CommonLib.WorkType;var net=CommonLib.Net;
        using(var clientCells=new Terrain())using(var serverCells=new Terrain())
        {
            try
            {
                clientCells.AllocateChunk(0,0).State=TerrainChunkState.Valid;serverCells.AllocateChunk(0,0).State=TerrainChunkState.Valid;
                var client=NativeTerrain(clientCells);var server=NativeTerrain(serverCells);
                var project=new Project();Put(server,"m_project",project);project.m_subsystems.Add(server);
                var proxy=Bare<ProjectNet>();foreach(var field in typeof(Project).GetFields(Fields))field.SetValue(proxy,field.GetValue(project));
                CommonLib.Net=Bare<NetNode>();Put(CommonLib.Net,"PendingPackages",new List<IPackage>());
                var gateway=new GameTerrainGateway(client);var cell=new CellPos(8,64,8);
                foreach(int value in new[]{3,0})
                {
                    CommonLib.WorkType=WorkType.Client;clientCells.GetChunkAtCell(8,8).State=TerrainChunkState.Valid;
                    Check(gateway.SetCellValue(cell,value),"native client prediction updates the local terrain");
                    Check(clientCells.GetCellValue(8,64,8)==value,"predicted client cell matches requested value");
                    var pending=Pending;Check(pending.Count==1&&pending[0] is SubsystemTerrainPackage,"exactly one native terrain packet, no mod handshake");
                    var request=(SubsystemTerrainPackage)pending[0];Check(request.m_type==SubsystemTerrainPackage.DataType.RequestChangeCell&&request.ID==7,"native request flag and packet ID7");
                    using(var writer=new PackageStreamWriter())
                    {
                        request.WriteData(writer);using(var reader=new PackageStreamReader(writer.Data()))
                        {
                            var received=new SubsystemTerrainPackage();received.ReadData(reader);pending.Clear();CommonLib.WorkType=WorkType.Server;
                            received.Handle(proxy,CommonLib.Net,true);
                            Check(serverCells.GetCellValue(8,64,8)==value,"actual native handler applies build/undo request on host");
                            Check(Pending.Count==1&&((SubsystemTerrainPackage)Pending[0]).m_type==SubsystemTerrainPackage.DataType.ChangeCell,"actual host handler broadcasts authoritative terrain result");
                            Pending.Clear();
                        }
                    }
                }
            }
            finally {CommonLib.WorkType=mode;CommonLib.Net=net;}
        }
    }
}
