using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Engine;
using Engine.Graphics;
using Game;
using Game.NetWork;
using GameEntitySystem;

// Actual target assemblies, isolated project/body/terrain/RPC fixtures. Not live networking/device proof.
public static class VoidSpaceNativeProbe
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static int checks;
    static Assembly mod;
    static void Check(bool ok,string name){checks++;if(!ok)throw new Exception("Space V2: "+name);}
    static T Bare<T>(){return (T)FormatterServices.GetUninitializedObject(typeof(T));}
    static FieldInfo Field(Type t,string n){for(;t!=null;t=t.BaseType){var f=t.GetField(n,F);if(f!=null)return f;}throw new Exception("Missing fixture field: "+n);}
    static void Put(object o,string n,object v){Field(o.GetType(),n).SetValue(o,v);}
    static object Get(object o,string n){return Field(o.GetType(),n).GetValue(o);}
    static object Call(object o,string n,params object[] a){return o.GetType().GetMethods(F).Single(m=>m.Name==n&&m.GetParameters().Length==a.Length).Invoke(o,a);}
    static object New(string name,params object[] a){return Activator.CreateInstance(mod.GetType("SCIEPort.Features.AbyssReaper."+name,true),F,null,a,null);}
    static List<IPackage> Pending(){return (List<IPackage>)Get(CommonLib.Net,"PendingPackages");}
    static object Packet(byte kind,byte skill,uint sequence){var p=New("VoidSpacePackage");Put(p,"Kind",kind);Put(p,"Skill",skill);Put(p,"Sequence",sequence);Put(p,"Direction",Vector3.UnitZ);return p;}
    sealed class Recorder:ModLoader
    {
        internal readonly List<ComponentBody> Bodies=new List<ComponentBody>();internal readonly List<float> Powers=new List<float>();
        public override bool AttackBody(ComponentBody target,ComponentCreature attacker,Vector3 point,Vector3 direction,ref float power,bool melee)
        {Bodies.Add(target);Powers.Add(power);power=0;return true;}
        internal int Count(ComponentBody body){return Bodies.Count(x=>x==body);}
    }
    sealed class Fixture:IDisposable
    {
        internal readonly Project Project=new Project();internal readonly Terrain Cells=new Terrain();
        internal readonly SubsystemBodies Bodies=new SubsystemBodies();internal readonly SubsystemPlayers Players=new SubsystemPlayers();
        internal readonly SubsystemTime Clock=new SubsystemTime();internal SubsystemTerrain Terrain;internal ComponentPlayer Player;
        internal object Owner,Blink,Space,Safety,Cut;internal Client Client;
        internal Fixture(bool main,byte id)
        {
            Terrain=Bare<SubsystemTerrain>();Put(Terrain,"m_project",Project);Put(Terrain,"<Terrain>k__BackingField",Cells);
            Put(Bodies,"m_project",Project);Put(Players,"m_project",Project);
            var moving=Bare<SubsystemMovingBlocks>();Put(moving,"m_project",Project);moving.m_movingBlockSets=new List<SubsystemMovingBlocks.MovingBlockSet>();moving.m_result=new DynamicArray<IMovingBlockSet>();
            Project.m_subsystems.AddRange(new Subsystem[]{Terrain,Bodies,Players,moving});
            for(int x=-2;x<=4;x++)for(int z=-2;z<=4;z++)Cells.AllocateChunk(x,z).State=TerrainChunkState.Valid;
            for(int x=-32;x<80;x++)for(int z=-32;z<80;z++)Cells.SetCellValueFast(x,63,z,3);
            Player=AbyssHostNativeProbe.Player(Project,Bodies,952);Put(Player.PlayerData,"m_isMainPlayer",main);
            Client=new Client(Guid.NewGuid(),null);Put(Client,"<ID>k__BackingField",id);Put(Client,"<SubsystemPlayers>k__BackingField",Players);Client.State=ClientState.ProjectLoaded;
            Player.PlayerData.PlayerGUID=Client.GUID;Players.m_playersData.Add(Player.PlayerData);Players.m_componentPlayers.Add(Player);CommonLib.Net.Clients[id]=Client;
            Owner=New("SubsystemAbyssReaper");Put(Owner,"m_project",Project);Project.m_subsystems.Add((Subsystem)Owner);
            Blink=New("VoidBlinkRuntime",Terrain,Bodies,Clock,null,false,false,1);Put(Owner,"blink",Blink);Space=Get(Blink,"Space");
            Safety=New("VoidBlinkSafety",Terrain);Clock.m_gameTime=10;Clock.m_gameTimeDelta=.05f;
        }
        internal void Wall(int z,int minY,int maxY){for(int x=0;x<20;x++)for(int y=minY;y<=maxY;y++)Cells.SetCellValueFast(x,y,z,3);}
        internal ComponentCreature Creature(Vector3 position,bool player=false)
        {
            Entity e=Bare<Entity>();Put(e,"m_project",Project);Put(e,"m_isAddedToProject",true);
            ComponentCreature c=player?(ComponentCreature)AbyssHostNativeProbe.Player(Project,Bodies,952):new ComponentCreature();
            if(!player)
            {
                c.m_entity=e;c.ComponentHealth=new ComponentHealth();c.ComponentHealth.m_entity=e;c.ComponentHealth.Health=1;
                c.ComponentBody=new ComponentBody();c.ComponentBody.m_entity=e;Put(e,"m_components",new List<Component>{c,c.ComponentBody,c.ComponentHealth});
            }
            c.ComponentBody.BoxSize=new Vector3(.6f,1.6f,.6f);c.ComponentBody.Position=position;c.ComponentBody.m_subsystemBodies=Bodies;Bodies.AddBody(c.ComponentBody);return c;
        }
        internal void EnableCut()
        {
            var info=Bare<SubsystemGameInfo>();Put(info,"m_project",Project);Project.m_subsystems.Add(info);
            Cut=New("WorldCutRuntime",Project,Players,Clock,Terrain,Bodies,false,false,false,true,100f,40f);Put(Owner,"<WorldCut>k__BackingField",Cut);
        }
        public void Dispose(){if(Cut!=null)((IDisposable)Cut).Dispose();((IDisposable)Blink).Dispose();Cells.Dispose();CommonLib.Net.Clients.Remove(Client.ID);}
    }
    static object Request(Fixture f,byte skill,uint sequence)
    {var p=Packet(0,skill,sequence);Put(p,"Caster",f.Player.Entity.EntityId);return p;}
    static object[] Find(Fixture f,Vector3 direction)
    {object[] args={f.Player,direction,Vector3.Zero,""};Call(f.Safety,"TryFind",args);return args;}
    static void LandingCases()
    {
        CommonLib.WorkType=WorkType.Local;
        foreach(var direction in new[]{Vector3.UnitZ,Vector3.UnitY,-Vector3.UnitY,Vector3.Normalize(new Vector3(1,1,1))})
        using(var f=new Fixture(true,2))
        {
            if(direction.Y<0){f.Player.ComponentBody.Position+=Vector3.UnitY*25;f.Player.ComponentCreatureModel.m_eyePosition+=Vector3.UnitY*25;}
            Vector3 start=f.Player.ComponentBody.Position;object[] args={f.Player,direction,Vector3.Zero,""};
            Check((bool)Call(f.Safety,"TryFind",args),"all-direction body sweep finds free-space destination");
            Check(Vector3.Distance((Vector3)args[2],start+direction*18)<.02f,"empty space reaches full 18 units without a floor");
        }
        using(var f=new Fixture(true,2))
        {
            f.Wall(12,64,67);object[] args={f.Player,Vector3.UnitZ,Vector3.Zero,""};
            Check((bool)Call(f.Safety,"TryFind",args),"wall stops at nearest valid body position");
            Check(((Vector3)args[2]).Z<11.66f&&((Vector3)args[2]).Z>11.5f,"collision margin and refined stop before wall");
        }
        using(var f=new Fixture(true,2))
        {
            f.Wall(12,64,64);object[] args={f.Player,Vector3.UnitZ,Vector3.Zero,""};
            Check((bool)Call(f.Safety,"TryFind",args)&&((Vector3)args[2]).Z<12,"feet obstacle blocks a ray that clears at eye height");
        }
        using(var f=new Fixture(true,2))
        {
            for(int x=7;x<=10;x++)for(int z=4;z<=7;z++)f.Cells.SetCellValueFast(x,68,z,3);
            object[] args={f.Player,Vector3.UnitY,Vector3.Zero,""};
            Check((bool)Call(f.Safety,"TryFind",args)&&((Vector3)args[2]).Y<66.3f,"ceiling checked against head volume");
        }
        using(var f=new Fixture(true,2))
        {
            f.Wall(6,64,67);Call(f.Space,"Enqueue",f.Player,null,Request(f,2,1));Call(f.Space,"Update");
            Check((double)Call(f.Space,"Remaining",f.Player,(byte)2)==0&&f.Player.ComponentBody.Position.Z==5.5f,"no usable space fails without cooldown or movement");
        }
        using(var f=new Fixture(true,2))
        {
            f.Cells.GetChunkAtCell(20,5).State=TerrainChunkState.InvalidContents1;object[] args={f.Player,Vector3.UnitX,Vector3.Zero,""};
            Check((bool)Call(f.Safety,"TryFind",args)&&((Vector3)args[2]).X<16,"unloaded chunk stops sweep before the boundary");
            f.Cells.GetChunkAtCell(20,5).State=TerrainChunkState.Valid;
            var id=Guid.NewGuid();SubsystemBedrockBlockBehavior.Territories[id]=new Territoriy{Origin=new Point2(32,5),OwnerGuid=id};
            Check((bool)Call(f.Safety,"TryFind",args)&&((Vector3)args[2]).X<16,"protected columns cannot be crossed in the air");SubsystemBedrockBlockBehavior.Territories.Clear();
        }
    }
    static void DamageCases(Recorder recorder)
    {
        CommonLib.WorkType=WorkType.Local;
        using(var f=new Fixture(true,2))
        {
            var victim=f.Creature(new Vector3(9,64,12));var cast=Activator.CreateInstance(mod.GetType("SCIEPort.Features.AbyssReaper.WorldCutRuntime+Cast",true),true);
            Put(cast,"Owner",f.Player);Put(cast,"Origin",new Vector3(8.5f,65,5.5f));Put(cast,"Direction",Vector3.UnitZ);Put(cast,"Right",Vector3.UnitX);
            var damage=New("WorldCutDamage",f.Terrain,f.Bodies,null,false,false,100f,40f);Call(damage,"BeginFrame",true);
            Call(damage,"Apply",cast,0f,20f,false);Call(damage,"Apply",cast,0f,20f,false);Call(damage,"Apply",cast,0f,20f,true);
            Call(f.Space,"AddCutRift",f.Player,1u,new Vector3(8.5f,65,5.5f),Vector3.UnitZ,20f);for(int i=0;i<5;i++){f.Clock.m_gameTime+=.1;Call(f.Space,"Update");}
            Check(recorder.Count(victim.ComponentBody)==2&&recorder.Powers.SequenceEqual(new[]{100f,60f}),"same target receives initial 100 and residual 60 once each, never legacy closing 40");
        }
        recorder.Bodies.Clear();recorder.Powers.Clear();
        using(var f=new Fixture(true,2))
        {
            var npc=f.Creature(new Vector3(10,64,12));var friend=f.Creature(new Vector3(10,64,13),true);
            var carrier=f.Creature(new Vector3(11,64,13));friend.ComponentBody.ParentBody=carrier.ComponentBody;
            Call(f.Space,"AddCutRift",f.Player,1u,new Vector3(8.5f,65,5.5f),Vector3.UnitZ,15f);
            for(int i=0;i<8;i++){f.Clock.m_gameTime+=.1;Call(f.Space,"Update");}
            Check(recorder.Count(npc.ComponentBody)==1&&recorder.Powers.Last()==60,"four-second cut rift nominal 60 is applied once to an ordinary animal");
            Check(recorder.Count(friend.ComponentBody)==0&&recorder.Count(carrier.ComponentBody)==0,"player and directly occupied carrier never enter V2 area damage");
            f.Clock.m_gameTime=13.8;var late=f.Creature(new Vector3(10,64,15));Call(f.Space,"Update");
            Check(recorder.Count(late.ComponentBody)==1,"late entrant can touch lingering four-second rift");
            f.Clock.m_gameTime=14.01;Call(f.Space,"Update");var after=f.Creature(new Vector3(10,64,17));Call(f.Space,"Update");
            Check(recorder.Count(after.ComponentBody)==0,"expired cut rift cannot damage");
        }
        recorder.Bodies.Clear();recorder.Powers.Clear();
        using(var f=new Fixture(true,2))
        {
            ComponentCreature a=f.Creature(new Vector3(10,64,6)),b=f.Creature(new Vector3(10,64,23));
            Call(f.Space,"Enqueue",f.Player,null,Request(f,2,1));Call(f.Space,"Update");
            for(int i=0;i<5;i++){f.Clock.m_gameTime+=.1;Call(f.Space,"Update");}
            Check(Math.Abs(f.Player.ComponentBody.Position.Z-23.5f)<.02,"local successful blink moves instantly");
            Check(recorder.Count(a.ComponentBody)==1&&recorder.Count(b.ComponentBody)==1&&recorder.Powers.All(p=>p==75),"twin rifts each cover their own endpoint at 75 power with shared per-cast dedup");
            Check((double)Call(f.Space,"Remaining",f.Player,(byte)2)>2&&(double)Call(f.Space,"Remaining",f.Player,(byte)2)<=3,"success starts authoritative three-second cooldown");
            Check(f.Player.ComponentBody.netPosition.Get(.1f)==f.Player.ComponentBody.Position,"native blink position interpolation is reset");
        }
        recorder.Bodies.Clear();recorder.Powers.Clear();
        using(var f=new Fixture(true,2))
        {
            ComponentCreature near=f.Creature(new Vector3(10,64,19)),far=f.Creature(new Vector3(10,64,32));
            Call(f.Space,"Enqueue",f.Player,null,Request(f,3,1));Call(f.Space,"Update");
            f.Clock.m_gameTime=11.349;Call(f.Space,"Update");Check(recorder.Count(near.ComponentBody)==0,"shatter cannot damage before 1.35s");
            f.Clock.m_gameTime=11.35;Call(f.Space,"Update");Call(f.Space,"Update");
            Check(recorder.Count(near.ComponentBody)==1&&recorder.Powers.Single()==160&&recorder.Count(far.ComponentBody)==0,"shatter applies one native 160-power hit within ten-unit radius");
            f.Clock.m_gameTime=12;Call(f.Space,"Update");Check(((IList)Get(f.Space,"shatters")).Count==0,"1.8-second shatter visual is retired independently of cooldown");
            Check((double)Call(f.Space,"Remaining",f.Player,(byte)3)==34,"shatter keeps a 36-second cooldown after visuals end");
        }
        recorder.Bodies.Clear();recorder.Powers.Clear();
        using(var f=new Fixture(true,2))
        {
            f.Wall(15,64,69);var hidden=f.Creature(new Vector3(8.5f,64,18));
            Call(f.Space,"Enqueue",f.Player,null,Request(f,3,1));Call(f.Space,"Update");f.Clock.m_gameTime+=1.4;Call(f.Space,"Update");
            Check(recorder.Count(hidden.ComponentBody)==0,"shatter centre and hit LOS cannot pass through a building wall");
        }
        foreach(int mode in new[]{0,1,2})using(var f=new Fixture(true,2))
        {
            var excluded=f.Creature(new Vector3(17.5f,64,20));
            if(mode==0){var id=Guid.NewGuid();SubsystemBedrockBlockBehavior.Territories[id]=new Territoriy{Origin=new Point2(33,20),OwnerGuid=id};}
            else if(mode==1)f.Cells.GetChunkAtCell(17,20).State=TerrainChunkState.InvalidContents1;
            else excluded.ComponentHealth.Health=0;
            Call(f.Space,"Enqueue",f.Player,null,Request(f,3,1));Call(f.Space,"Update");f.Clock.m_gameTime+=1.4;Call(f.Space,"Update");
            Check(recorder.Count(excluded.ComponentBody)==0,"protected/unloaded/dead target excluded from shatter");SubsystemBedrockBlockBehavior.Territories.Clear();
        }
    }
    static void FallCases()
    {
        CommonLib.WorkType=WorkType.Local;
        using(var f=new Fixture(true,2))
        {
            Call(f.Space,"ArmFall",f.Player);f.Clock.m_gameTime+=.05;
            f.Player.ComponentBody.StandingOnValue=3;f.Player.ComponentBody.CollisionVelocityChange=new Vector3(2,20,3);
            Call(f.Space,"Update");
            Check(f.Player.ComponentBody.CollisionVelocityChange==new Vector3(2,0,3),"only protected landing's vertical impact is removed before native health");
            Check(((IDictionary)Get(f.Space,"fall")).Count==0,"first safe landing releases guard immediately");
            Call(f.Space,"ArmFall",f.Player);
            Check(!(bool)Call(f.Space,"ProtectFall",f.Player.ComponentHealth,null,false,"fire"),"guard is not global immunity to environmental damage");
            Check(!(bool)Call(f.Space,"ProtectFall",f.Player.ComponentHealth,f.Player,false,"fall"),"guard never blocks an attack");
            f.Clock.m_gameTime+=4.01;Check(!(bool)Call(f.Space,"ProtectFall",f.Player.ComponentHealth,null,false,"fall"),"fall guard expires at four seconds");
            Call(f.Space,"ArmFall",f.Player);string cause=LanguageControl.Get("ComponentHealth",2);
            Check(!string.IsNullOrEmpty(cause)&&(bool)Call(f.Space,"ProtectFall",f.Player.ComponentHealth,null,false,cause),"native nonempty fall reason is protected once");
            Check(!(bool)Call(f.Space,"ProtectFall",f.Player.ComponentHealth,null,false,cause),"fall hook does not create repeated or permanent immunity");
        }
        Check((int)UpdateOrder.Input+1<(int)UpdateOrder.Default&&(int)UpdateOrder.Default<(int)UpdateOrder.Body,"target native health/body ordering supports protected client landing without a fall RPC");
    }
    static void NetworkCases()
    {
        CommonLib.WorkType=WorkType.Server;
        using(var f=new Fixture(false,2))
        {
            var request=Request(f,2,1);Vector3 origin=f.Player.ComponentBody.Position;Put(request,"Origin",new Vector3(900,100,900));Put(request,"Point",new Vector3(999,100,999));
            Call(f.Space,"Enqueue",f.Player,f.Client,request);Call(f.Space,"Update");
            Check(f.Player.ComponentBody.Position==origin&&(double)Call(f.Space,"Remaining",f.Player,(byte)2)==0,"ordinary client request waits for owning-client preflight without consuming cooldown");
            var transfers=(IDictionary)Get(f.Space,"transfers");Check(transfers.Count==1,"authenticated remote blink creates one bounded transfer");
            var t=transfers[f.Player.PlayerGuid];Vector3 target=(Vector3)Get(t,"Target");
            Check(Vector3.Distance(target,origin+Vector3.UnitZ*18)<.02f,"host ignores forged client origin and destination");
            var ready=Packet(4,2,1);Put(ready,"Caster",f.Player.Entity.EntityId);Call(f.Space,"Enqueue",f.Player,f.Client,ready);Call(f.Space,"Update");
            Check((bool)Get(t,"Committed")&&f.Player.ComponentBody.Position==origin,"commit waits for owner ACK before authority moves");
            f.Player.ComponentBody.netPosition=new NetPosition(new Vector3(999,64,999));
            var ack=Packet(6,2,1);Put(ack,"Caster",f.Player.Entity.EntityId);Call(f.Space,"Enqueue",f.Player,f.Client,ack);Call(f.Space,"Update");
            Check(f.Player.ComponentBody.Position==target&&f.Player.ComponentBody.netPosition.Get(.1f)==target,"remote ACK resets authority position and old native interpolation");
            double cooldown=(double)Call(f.Space,"Remaining",f.Player,(byte)2);
            Call(f.Space,"Enqueue",f.Player,f.Client,ack);Call(f.Space,"Update");
            Check((double)Call(f.Space,"Remaining",f.Player,(byte)2)==cooldown&&((IList)Get(f.Space,"effects")).Count==1,"ACK replay is idempotent and does not duplicate hazards/cooldown");
        }
        using(var f=new Fixture(false,2))
        {
            var request=Request(f,2,1);Call(f.Space,"Enqueue",f.Player,f.Client,request);
            Guid original=f.Client.TokenId;Put(f.Client,"<TokenId>k__BackingField",Guid.NewGuid());Call(f.Space,"Update");
            Check(((IDictionary)Get(f.Space,"transfers")).Count==0,"queued old connection token cannot cast after reconnect");Put(f.Client,"<TokenId>k__BackingField",original);
            Call(f.Space,"Enqueue",f.Player,f.Client,request);Call(f.Space,"Update");f.Clock.m_gameTime+=2.1;Call(f.Space,"Update");
            Check(((IDictionary)Get(f.Space,"transfers")).Count==0&&(double)Call(f.Space,"Remaining",f.Player,(byte)2)==0,"handoff timeout cancels without spending cooldown");
        }
        using(var f=new Fixture(false,2))
        {
            Call(f.Space,"Enqueue",f.Player,f.Client,Request(f,2,1));Call(f.Space,"Update");f.Wall(12,64,69);
            var ready=Packet(4,2,1);Put(ready,"Caster",f.Player.Entity.EntityId);Call(f.Space,"Enqueue",f.Player,f.Client,ready);Call(f.Space,"Update");
            Check(((IDictionary)Get(f.Space,"transfers")).Count==0&&(double)Call(f.Space,"Remaining",f.Player,(byte)2)==0,"new wall during preflight cannot be bypassed");
        }
    }
    static void WireAndGeometry()
    {
        var type=mod.GetType("SCIEPort.Features.AbyssReaper.VoidSpacePackage");
        foreach(var assembly in new[]{typeof(IPackage).Assembly,mod})foreach(var candidate in assembly.GetTypes())
            if(candidate.IsClass&&!candidate.IsAbstract&&typeof(IPackage).IsAssignableFrom(candidate)&&candidate!=type)Check(((IPackage)FormatterServices.GetUninitializedObject(candidate)).ID!=251,"251 remains unique across native and mod packet inventory");
        for(int i=0;i<20;i++)
        {
            var packet=Packet((byte)(i%11),(byte)(i%4),(uint)(i+1));Put(packet,"Origin",new Vector3(-150.5f,64,22));Put(packet,"Point",new Vector3(8.5f,82,5.5f));Put(packet,"Started",1234.125d);Put(packet,"Stamp",1235.25d);Put(packet,"Echo",97.33d);Put(packet,"Cooldown",8.5f);Put(packet,"Seed",uint.MaxValue-(uint)i);
            var writer=new PackageStreamWriter();((IPackage)packet).WriteData(writer);var data=writer.Data();Check(data.Length<128,"new packet wire remains bounded");
            var read=New("VoidSpacePackage");((IPackage)read).ReadData(new PackageStreamReader(data));
            foreach(var field in new[]{"Kind","Skill","Caster","Sequence","Origin","Point","Direction","Started","Stamp","Echo","Cooldown","Seed"})Check(Get(packet,field).Equals(Get(read,field)),"native packet field roundtrip: "+field);
        }
        var scene=New("VoidShatterScene");var factory=scene.GetType().GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic);object shape=factory.Invoke(null,new object[]{123u});
        var renderer=(PrimitivesRenderer3D)Get(scene,"renderer");
        foreach(int q in new[]{0,1,2})foreach(float t in new[]{0f,.2f,.3f,1.1f,1.35f,1.4f,1.79f,1.8f})
        {
            renderer.Clear();int count=(int)Call(scene,"Queue",shape,new Vector3(0,2,12),Vector3.UnitZ,t,q);
            Check(count>=0&&count<1300&&(t<1.8f||count==0),"shatter mesh budget and exact end");
            foreach(var batch in renderer.m_allBatches.Cast<FlatBatch3D>())foreach(var vertex in batch.TriangleVertices)
                Check(!float.IsNaN(vertex.Position.X)&&!float.IsInfinity(vertex.Position.X)&&!float.IsNaN(vertex.Position.Y)&&!float.IsInfinity(vertex.Position.Y)&&!float.IsNaN(vertex.Position.Z)&&!float.IsInfinity(vertex.Position.Z),"deterministic fragment vertex finite");
        }
        ((IDisposable)scene).Dispose();
    }
    static object ClientBegin(Fixture f,uint sequence)
    {
        CommonLib.Net.Server=f.Client;Put(f.Space,"serverToken",f.Client.TokenId);Put(f.Space,"hasClock",true);
        var peer=Call(f.Space,"Get",f.Player);Put(peer,"PendingSequence",sequence);Put(peer,"PendingSkill",(byte)2);Put(peer,"PendingUntil",f.Clock.GameTime+3);
        return peer;
    }
    static object ClientTransfer(Fixture f,byte kind,uint sequence,Vector3 from,Vector3 to)
    {
        var packet=Packet(kind,2,sequence);Put(packet,"Caster",f.Player.Entity.EntityId);Put(packet,"Origin",from);Put(packet,"Point",to);Put(packet,"Stamp",f.Clock.GameTime);
        Call(f.Space,"Receive",packet);return packet;
    }
    static void ClientCases(Recorder recorder)
    {
        CommonLib.WorkType=WorkType.Client;
        using(var f=new Fixture(true,2))
        {
            ClientBegin(f,1);var rejected=Packet(9,2,1);Put(rejected,"Caster",f.Player.Entity.EntityId);Put(rejected,"Cooldown",8f);Put(rejected,"Stamp",10d);
            Call(f.Space,"Receive",rejected);f.Clock.m_gameTime+=.5;Call(f.Space,"Receive",rejected);
            Check((double)Call(f.Space,"Remaining",f.Player,(byte)2)==7.5,"duplicate rejection cannot extend a host cooldown");
        }
        using(var f=new Fixture(true,2))
        {
            ClientBegin(f,1);Vector3 from=f.Player.ComponentBody.Position,to=from+Vector3.UnitZ*18;
            ClientTransfer(f,3,1,from,to);Check(f.Player.ComponentBody.Position==from,"client prepare cannot move before commit");
            ClientTransfer(f,5,1,from,to+Vector3.UnitX);Check(f.Player.ComponentBody.Position==from,"commit cannot substitute a different target");
            ClientTransfer(f,5,1,from,to);Check(f.Player.ComponentBody.Position==to&&((IList)Get(f.Space,"effects")).Count==1,"owning client commit resets native body once and presents twin rifts");
            var finish=Packet(7,2,1);Put(finish,"Caster",f.Player.Entity.EntityId);Put(finish,"Origin",from);Put(finish,"Point",to);Put(finish,"Stamp",10d);Put(finish,"Cooldown",8f);Call(f.Space,"Receive",finish);
            double cd=(double)Call(f.Space,"Remaining",f.Player,(byte)2);f.Clock.m_gameTime+=.5;f.Player.ComponentBody.Position+=Vector3.UnitX;Call(f.Space,"Receive",finish);
            Check((double)Call(f.Space,"Remaining",f.Player,(byte)2)==cd-.5&&f.Player.ComponentBody.Position==to+Vector3.UnitX,"duplicate final receipt neither extends cooldown nor snaps a moving client back");
            int attacks=recorder.Bodies.Count;Call(f.Space,"Update");Check(attacks==recorder.Bodies.Count,"client presentation never calls native attack");
        }
        using(var f=new Fixture(true,2))
        {
            ClientBegin(f,1);Vector3 from=f.Player.ComponentBody.Position,to=from+Vector3.UnitZ*18;
            ClientTransfer(f,3,1,from,to);ClientTransfer(f,5,1,from,to);f.Clock.m_gameTime+=2.1;Call(f.Space,"Update");
            Check(f.Player.ComponentBody.Position==from&&(double)Call(f.Space,"Remaining",f.Player,(byte)2)==0&&((IList)Get(f.Space,"effects")).Count==0,"unconfirmed client movement rolls back on timeout without hazards/cooldown");
            ClientTransfer(f,7,1,from,to);Check(f.Player.ComponentBody.Position==from,"late success cannot revive a timed out transfer");
            object[] request={f.Player,(byte)2,""};Check((bool)Call(f.Space,"Request",request),"failed transfer may retry immediately while old receipt is retained");
            Check(Get(f.Space,"clientTransfer")==null,"new request retires the previous client tombstone");
        }
        using(var f=new Fixture(true,2))
        {
            ClientBegin(f,1);Vector3 from=f.Player.ComponentBody.Position,to=from+Vector3.UnitZ*18;
            ClientTransfer(f,3,1,from,to);ClientTransfer(f,5,1,from,to);f.Player.ComponentHealth.Health=0;ClientTransfer(f,7,1,from,to);
            Check((double)Call(f.Space,"Remaining",f.Player,(byte)2)==0,"dead client ignores a delayed success receipt");
        }
        using(var f=new Fixture(true,2))
        {
            ClientBegin(f,1);var p=Packet(2,3,4);Put(p,"Caster",f.Player.Entity.EntityId);Put(p,"Point",new Vector3(8,66,20));Put(p,"Origin",new Vector3(8,66,20));Put(p,"Started",7d);Put(p,"Stamp",10d);
            Call(f.Space,"Receive",p);Check(((IList)Get(f.Space,"shatters")).Count==0,"already expired observer cast cannot replay a fresh animation");
            Put(p,"Started",10d);Put(p,"Cooldown",36f);Call(f.Space,"Receive",p);Call(f.Space,"Receive",p);Check(((IList)Get(f.Space,"shatters")).Count==1&&(double)Call(f.Space,"Remaining",f.Player,(byte)3)==36,"observer accepts the new 36-second cooldown and caches one geometry per sequence");
            var end=Packet(10,3,4);Put(end,"Caster",f.Player.Entity.EntityId);Call(f.Space,"Receive",end);Call(f.Space,"Receive",p);
            Check(((IList)Get(f.Space,"shatters")).Count==0,"cancel tombstone rejects an out-of-order observer event");
            Put(p,"Sequence",3u);Call(f.Space,"Receive",p);Check(((IList)Get(f.Space,"shatters")).Count==0,"cancel tombstone also rejects older sequences");
            Put(p,"Sequence",5u);Put(f.Space,"hasClock",false);Call(f.Space,"Receive",p);Check(((IList)Get(f.Space,"shatters")).Count==0,"observer waits for host clock before timing a visual");
        }
    }
    static void LifecycleCases(Recorder recorder)
    {
        CommonLib.WorkType=WorkType.Local;
        using(var f=new Fixture(true,2))
        {
            var creatures=new List<ComponentCreature>();for(int i=0;i<280;i++)creatures.Add(f.Creature(new Vector3(8+(i%8)*.08f,64,18+(i/8)*.01f)));
            int before=recorder.Bodies.Count;Call(f.Space,"Enqueue",f.Player,null,Request(f,3,1));Call(f.Space,"Update");f.Clock.m_gameTime+=1.35;Call(f.Space,"Update");
            Check(recorder.Bodies.Count-before<=128,"dense shatter obeys the global per-frame 128 candidate budget");
            for(int i=0;i<6;i++)Call(f.Space,"Update");
            Check(creatures.All(c=>recorder.Count(c.ComponentBody)==1),"large local candidate set completes over bounded frames exactly once");
        }
        foreach(int mode in new[]{0,1,2,3})using(var f=new Fixture(true,2))
        {
            var victim=f.Creature(new Vector3(8,64,19));int before=recorder.Count(victim.ComponentBody);
            Call(f.Space,"Enqueue",f.Player,null,Request(f,3,1));Call(f.Space,"Update");
            if(mode==0)Call(f.Space,"Cancel",f.Player);else if(mode==1)f.Player.ComponentHealth.Health=0;else if(mode==2)Put(f.Player.Entity,"m_isAddedToProject",false);else ((ComponentInventory)f.Player.ComponentMiner.Inventory).m_slots[0]=new ComponentInventoryBase.Slot{Value=3,Count=1};
            f.Clock.m_gameTime+=1.4;Call(f.Space,"Update");
            Check(recorder.Count(victim.ComponentBody)==before&&((IList)Get(f.Space,"shatters")).Count==0,"cancel/death/removal cancels pre-impact shatter and all visuals");
        }
        using(var f=new Fixture(true,2))
        {
            Call(f.Space,"AddCutRift",f.Player,1u,new Vector3(8,65,5),Vector3.UnitZ,15f);Call(f.Space,"ArmFall",f.Player);((IDisposable)f.Blink).Dispose();
            Check(((IList)Get(f.Space,"effects")).Count==0&&((IDictionary)Get(f.Space,"fall")).Count==0&&((ICollection)Get(f.Space,"queue")).Count==0,"world disposal clears effects, guards and queued actions");
        }
        foreach(bool cancelFirst in new[]{false,true})using(var f=new Fixture(true,2))
        {
            var other=AbyssHostNativeProbe.Player(f.Project,f.Bodies,952);other.PlayerData.PlayerGUID=other.PlayerGuid;f.Players.m_componentPlayers.Add(other);f.Players.m_playersData.Add(other.PlayerData);f.Bodies.AddBody(other.ComponentBody);
            var victim=f.Creature(new Vector3(9,64,20));var request=Packet(0,3,1);Put(request,"Caster",other.Entity.EntityId);
            Call(f.Space,"Enqueue",f.Player,null,Request(f,3,1));Call(f.Space,"Enqueue",other,null,request);Call(f.Space,"Update");
            Check(((IList)Get(f.Space,"shatters")).Count==2,"two casters with the same sequence retain independent cast records");
            if(cancelFirst)Call(f.Space,"Cancel",f.Player);f.Clock.m_gameTime+=1.35;Call(f.Space,"Update");
            Check(recorder.Count(victim.ComponentBody)==(cancelFirst?1:2)&&recorder.Count(other.ComponentBody)==0,"independent multi-caster damage/cancellation never affects a player");
        }
    }
    static object CutPacket(Fixture f,byte kind,uint seq)
    {var p=New("WorldCutPackage");Put(p,"Kind",kind);Put(p,"Caster",f.Player.Entity.EntityId);Put(p,"Sequence",seq);return p;}
    static void HeavyCases(Recorder recorder)
    {
        CommonLib.WorkType=WorkType.Local;
        using(var f=new Fixture(true,2))
        {
            f.EnableCut();f.Player.ComponentCreatureModel.m_eyeRotation=new Quaternion(0,1,0,0);var victim=f.Creature(new Vector3(9,64,12));var peer=Call(f.Cut,"Get",f.Player,true);Put(peer,"ReadyAt",20d);
            var state=Activator.CreateInstance(f.Owner.GetType().GetNestedType("State",BindingFlags.NonPublic),true);Put(state,"CombatTime",10d);
            Call(f.Owner,"UpdateMeleeInput",f.Player,state,true,0f);Call(f.Cut,"Update",.05f);
            f.Clock.m_gameTime=10.8;Put(state,"CombatTime",10.8d);Call(f.Owner,"UpdateMeleeInput",f.Player,state,false,0f);Call(f.Cut,"Update",.05f);
            Check(((IList)Get(f.Cut,"casts")).Count==1&&!(bool)Get(state,"NormalImpactPending"),"charged input starts exactly one authoritative cut, without an old melee impact");
            Check((double)Get(peer,"ReadyAt")==20,"heavy works during skill-one cooldown and never spends/resets that cooldown");
            for(int i=0;i<50;i++){f.Clock.m_gameTime+=.05;Call(f.Cut,"Update",.05f);Call(f.Space,"Update");}
            var powers=recorder.Bodies.Select((body,i)=>new{body,power=recorder.Powers[i]}).Where(x=>x.body==victim.ComponentBody).Select(x=>x.power);
            Check(powers.SequenceEqual(new[]{100f,60f}),"charged remote blade and lingering rift apply 100+60 once, without old 52/40 damage (actual "+string.Join(",",powers)+", casts="+((IList)Get(f.Cut,"casts")).Count+", rifts="+((IList)Get(f.Space,"effects")).Count+")");
            Check(((IList)Get(f.Cut,"casts")).Count==0&&((IList)Get(f.Space,"effects")).Count==1,"heavy finishes its input/pose while a four-second world rift remains");
            Call(f.Cut,"BeginCharge",f.Player);Call(f.Cut,"Update",.05f);Call(f.Cut,"EndCharge",f.Player,true);Call(f.Cut,"Update",.05f);
            Check(((IList)Get(f.Cut,"casts")).Count==0,"host will not release a fresh heavy without its own charge dwell");
            f.Clock.m_gameTime+=.7;Call(f.Cut,"Update",.05f);Check(((IList)Get(f.Cut,"casts")).Count==1,"another fully charged heavy is available without ten-second skill cooldown");
        }
        foreach(byte end in new byte[]{4,8})using(var f=new Fixture(true,2))
        {
            f.EnableCut();Call(f.Cut,"Enqueue",f.Player,null,CutPacket(f,6,1));Call(f.Cut,"Update",.05f);
            Call(f.Cut,"Enqueue",f.Player,null,CutPacket(f,7,1));Call(f.Cut,"Update",.05f);Call(f.Cut,"Enqueue",f.Player,null,CutPacket(f,end,1));Call(f.Cut,"Update",.05f);
            f.Clock.m_gameTime+=1;Call(f.Cut,"Update",.05f);Check(((IList)Get(f.Cut,"casts")).Count==0,"cancelled charge/release cannot create a delayed blade");
        }
        CommonLib.WorkType=WorkType.Server;
        using(var f=new Fixture(false,2))
        {
            f.EnableCut();Call(f.Cut,"Enqueue",f.Player,f.Client,CutPacket(f,7,1));Call(f.Cut,"Update",.05f);
            Check(((IList)Get(f.Cut,"casts")).Count==0,"unpaired remote heavy release is rejected");
            Call(f.Cut,"Enqueue",f.Player,f.Client,CutPacket(f,6,2));Call(f.Cut,"Update",.05f);
            Call(f.Cut,"Enqueue",f.Player,f.Client,CutPacket(f,7,2));f.Clock.m_gameTime+=.8;Call(f.Cut,"Update",.05f);
            Check(((IList)Get(f.Cut,"casts")).Count==1&&(double)Call(f.Cut,"Remaining",f.Player)==0,"ordinary client heavy is host-owned and independent of button cooldown");
            Call(f.Cut,"Enqueue",f.Player,f.Client,CutPacket(f,7,2));Call(f.Cut,"Update",.05f);Check(((IList)Get(f.Cut,"casts")).Count==1,"duplicate heavy release cannot spawn another blade");
        }
        using(var f=new Fixture(false,2))
        {
            f.EnableCut();Call(f.Cut,"Enqueue",f.Player,f.Client,CutPacket(f,6,1));Call(f.Cut,"Update",.05f);
            Call(f.Cut,"Enqueue",f.Player,f.Client,CutPacket(f,7,1));Put(f.Client,"<TokenId>k__BackingField",Guid.NewGuid());f.Clock.m_gameTime+=1;Call(f.Cut,"Update",.05f);
            Check(((IList)Get(f.Cut,"casts")).Count==0,"old connection cannot release a saved heavy after reconnect");
        }
        CommonLib.WorkType=WorkType.Client;
        using(var f=new Fixture(true,2))
        {
            f.EnableCut();Pending().Clear();Call(f.Cut,"BeginCharge",f.Player);Call(f.Cut,"EndCharge",f.Player,true);
            Check(Pending().Count==2&&(byte)Get(Pending()[0],"Kind")==6&&(byte)Get(Pending()[1],"Kind")==7,"ordinary client submits start/release rather than calculating heavy damage");
            Check(((IList)Get(f.Cut,"casts")).Count==0,"client has no local authoritative heavy cast");
        }
    }
    public static void Run(Assembly assembly)
    {
        mod=assembly;var oldWork=CommonLib.WorkType;var oldNet=CommonLib.Net;var oldHooks=ModsManager.ModHooks;var oldTerritories=SubsystemBedrockBlockBehavior.Territories;
        var oldAir=BlocksManager.Blocks[0];var oldStone=BlocksManager.Blocks[3];
        try
        {
            CommonLib.Net=Bare<NetNode>();Put(CommonLib.Net,"PendingPackages",new List<IPackage>());CommonLib.Net.Clients=new Dictionary<int,Client>();
            ModsManager.ModHooks=new Dictionary<string,ModsManager.ModHook>();var recorder=new Recorder();ModsManager.RegisterHook("AttackBody",recorder);
            SubsystemBedrockBlockBehavior.Territories=new Dictionary<Guid,Territoriy>();BlocksManager.Blocks[0]=new AirBlock{BlockIndex=0,IsCollidable=false};BlocksManager.Blocks[3]=new GraniteBlock{BlockIndex=3,IsCollidable=true};
            WireAndGeometry();LandingCases();DamageCases(recorder);FallCases();NetworkCases();ClientCases(recorder);LifecycleCases(recorder);HeavyCases(recorder);
        }
        finally{CommonLib.WorkType=oldWork;CommonLib.Net=oldNet;ModsManager.ModHooks=oldHooks;SubsystemBedrockBlockBehavior.Territories=oldTerritories;BlocksManager.Blocks[0]=oldAir;BlocksManager.Blocks[3]=oldStone;}
        Console.WriteLine("Space V2 native geometry/packet/body-sweep/nominal-attack/landing/authority checks: "+checks+" passed (isolated fixtures; not live network or Android).");
    }
}
