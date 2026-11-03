using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Game;
using Game.NetWork;

public static partial class DeathAuthorityNativeProbe
{
    static void EnableVoidMelee(Fixture f)
    {Put(f.Owner,"terrain",Get(f.Base,"Terrain"));Put(f.Owner,"bodies",f.Bodies);}
    static object VoidStrikePacket(Fixture f,uint sequence,Vector3 direction)
    {var q=Packet(f,5,sequence);Put(q,"Direction",direction);return q;}
    static void DispatchVoidStrike(Fixture f,uint sequence,Vector3 direction)
    {Call(f.Death,"Enqueue",f.Player,CommonLib.WorkType==WorkType.Server?f.Client:null,VoidStrikePacket(f,sequence,direction));Call(f.Death,"Update");}
    static void VoidMeleeMultiplayer()
    {
        foreach(bool host in new[]{true,false})
        {
            CommonLib.WorkType=host?WorkType.Local:WorkType.Server;
            using(var f=new Fixture(host))
            {
                EnableVoidMelee(f);var recorder=new AttackRecorder();ModsManager.RegisterHook("AttackBody",recorder);
                var victim=f.Creature(150,new Vector3(8.5f,64,2));DispatchVoidStrike(f,1,-Vector3.UnitZ);
                Check(recorder.Targets.Count==1&&recorder.Targets[0]==victim.ComponentBody&&recorder.Powers[0]==22,"void normal hit works for Local and authenticated ordinary client "+host);
                DispatchVoidStrike(f,1,-Vector3.UnitZ);DispatchVoidStrike(f,2,-Vector3.UnitZ);
                Check(recorder.Targets.Count==1,"replay and same-frame request cannot duplicate a normal hit");
                f.Clock.m_gameTime+=.5;DispatchVoidStrike(f,3,-Vector3.UnitZ);f.Clock.m_gameTime+=.5;DispatchVoidStrike(f,4,-Vector3.UnitZ);
                Check(recorder.Powers.SequenceEqual(new[]{22f,22f,34f}),"host selects the same three-hit damage sequence for either owner");
                f.Clock.m_gameTime+=.43;DispatchVoidStrike(f,5,-Vector3.UnitZ);
                Check(recorder.Powers.SequenceEqual(new[]{22f,22f,34f,22f}),"next normal combo is not dropped while the third swing recovers");
                var packets=(List<IPackage>)Get(CommonLib.Net,"PendingPackages");
                if(!host)Check(packets.Any(p=>p.GetType().Name=="DeathAuthorityPackage"&&(byte)Get(p,"Kind")==5&&(Guid)Get(p,"Owner")==f.Player.PlayerGuid),"remote normal swing uses existing authoritative animation event");
                f.Clock.m_gameTime+=1;var cells=(Terrain)Get(f.Base,"Cells");for(int x=7;x<=10;x++)for(int y=64;y<=67;y++)cells.SetCellValueFast(x,y,4,3);
                DispatchVoidStrike(f,6,-Vector3.UnitZ);Check(recorder.Targets.Count==4,"remote and local normal attacks cannot pass through a wall");
                ModsManager.ModHooks.Clear();
            }
        }
        CommonLib.WorkType=WorkType.Server;
        using(var f=new Fixture(false))
        {
            EnableVoidMelee(f);var recorder=new AttackRecorder();ModsManager.RegisterHook("AttackBody",recorder);f.Creature(151,new Vector3(8.5f,64,2));
            var q=VoidStrikePacket(f,1,-Vector3.UnitZ);Put(q,"World",Guid.NewGuid());Call(f.Death,"Enqueue",f.Player,f.Client,q);Call(f.Death,"Update");
            Check(recorder.Targets.Count==0,"cross-world void melee rejected");
            q=VoidStrikePacket(f,2,-Vector3.UnitZ);Put(q,"Caster",(ushort)(f.Player.Entity.EntityId+1));Call(f.Death,"Enqueue",f.Player,f.Client,q);Call(f.Death,"Update");
            Check(recorder.Targets.Count==0,"forged caster rejected");
            q=VoidStrikePacket(f,3,-Vector3.UnitZ);Call(f.Death,"Enqueue",f.Player,f.Client,q);var token=f.Client.TokenId;Put(f.Client,"<TokenId>k__BackingField",Guid.NewGuid());Call(f.Death,"Update");
            Check(recorder.Targets.Count==0,"queued stale connection cannot strike");Put(f.Client,"<TokenId>k__BackingField",token);
            DispatchVoidStrike(f,4,new Vector3(float.NaN,0,-1));DispatchVoidStrike(f,5,Vector3.UnitZ);DispatchVoidStrike(f,6,-Vector3.UnitZ*100);
            Check(recorder.Targets.Count==0,"nonfinite, backwards and unnormalized client aim cannot select a target");
            var codex=f.Project.FindSubsystem(mod.GetType("SCIEPort.Features.Codex.SubsystemArtifactCodex"),null,true);var records=(Dictionary<Guid,uint>)Get(codex,"Records");records[f.Player.PlayerGuid]=0;
            DispatchVoidStrike(f,7,-Vector3.UnitZ);Check(recorder.Targets.Count==0,"unreleased true name still rejects normal attack for an ordinary client");records[f.Player.PlayerGuid]=255;
            q=VoidStrikePacket(f,8,-Vector3.UnitZ);Put(q,"Target",ushort.MaxValue);Put(q,"A",999f);Call(f.Death,"Enqueue",f.Player,f.Client,q);Call(f.Death,"Update");
            Check(recorder.Powers.Count==1&&recorder.Powers[0]==22,"forged damage/stage metadata cannot override host damage");
            f.Clock.m_gameTime+=1;Call(f.Death,"Adopt",f.Creature(152,new Vector3(10,64,2)),f.Player.PlayerGuid,false);int previous=recorder.Targets.Count;
            DispatchVoidStrike(f,9,-Vector3.UnitZ);Check(recorder.Targets.Count==previous+1,"contracted ally inside the void sweep is protected");
            f.Clock.m_gameTime+=1;f.Player.ComponentMiner.Inventory.RemoveSlotItems(0,1);DispatchVoidStrike(f,10,-Vector3.UnitZ);Check(recorder.Targets.Count==previous+1,"weapon removal invalidates a queued normal strike");
            ModsManager.ModHooks.Clear();
        }
        foreach(var role in new[]{WorkType.Local,WorkType.Server,WorkType.Client})
        {
            CommonLib.WorkType=role;
            using(var f=new Fixture())
            {
                EnableVoidMelee(f);var recorder=new AttackRecorder();ModsManager.RegisterHook("AttackBody",recorder);var victim=f.Creature(153,new Vector3(8.5f,64,2));
                if(role==WorkType.Client){CommonLib.Net.Server=f.Client;Put(f.Death,"serverToken",f.Client.TokenId);Put(f.Death,"worldKnown",true);}
                var state=Activator.CreateInstance(f.Owner.GetType().GetNestedType("State",System.Reflection.BindingFlags.NonPublic),true);Put(state,"SwingDirection",-Vector3.UnitZ);
                ((List<IPackage>)Get(CommonLib.Net,"PendingPackages")).Clear();Call(f.Owner,"Strike",f.Player,state);
                Check(recorder.Targets.Count==0,"normal input queues before any world damage "+role);
                if(role==WorkType.Client)
                {
                    var request=((List<IPackage>)Get(CommonLib.Net,"PendingPackages")).Single(p=>p.GetType().Name=="DeathAuthorityPackage");
                    Check((byte)Get(request,"Action")==5&&(ushort)Get(request,"Caster")==f.Player.Entity.EntityId,"ordinary client input emits an owned normal attack request");
                    Call(f.Owner,"ApplyVoidMelee",f.Player,-Vector3.UnitZ,0);Call(f.Death,"Strike",f.Player,-Vector3.UnitZ);
                    Check(recorder.Targets.Count==0&&victim.ComponentHealth.Health==1,"client cannot duplicate damage locally");CommonLib.Net.Server=null;
                }
                else{Call(f.Death,"Update");Check(recorder.Targets.Count==1,"local and host input use the authenticated shared strike path "+role);}
                ModsManager.ModHooks.Clear();
            }
        }
    }
}
