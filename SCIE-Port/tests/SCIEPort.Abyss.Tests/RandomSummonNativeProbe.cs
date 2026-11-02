using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Engine;
using Game;
using Game.NetWork;

public static partial class DeathAuthorityNativeProbe
{
    sealed class SummonRolls:System.Random
    {
        internal int Value=80,Calls;
        public override int Next(int maxValue)
        {
            if(maxValue!=100)throw new Exception("summon random range must be [0,100)");
            Calls++;return Value;
        }
    }
    static SummonRolls SummonRandom(Fixture f)
    {var rolls=new SummonRolls();Put(f.Death,"summonRandom",rolls);return rolls;}
    static object SummonState(Fixture f)
    {var state=Call(f.Death,"Get",f.Player);Put(state,"Death",true);return state;}
    static void SummonCommand(Fixture f,uint sequence,ushort forgedType=0)
    {
        Put(Call(f.Death,"Get",f.Player),"NextManagementRequest",0d);
        var packet=Packet(f,4,sequence);Put(packet,"Target",forgedType);
        Call(f.Death,"Enqueue",f.Player,CommonLib.WorkType==WorkType.Server?f.Client:null,packet);Call(f.Death,"Update");
    }
    static object StoredSummon(Fixture f,ushort key,string template,bool hidden,Guid owner)
    {
        var row=New("DeathAuthorityRuntime+Friend");Put(row,"Id",key);Put(row,"Owner",owner);Put(row,"Template",template);Put(row,"Name",template);Put(row,"Hidden",hidden);Put(row,"Pending",!hidden);Put(row,"RegenStamp",f.Clock.GameTime);
        ((IDictionary)Get(f.Death,hidden?"hidden":"saved")).Add(key,row);return row;
    }
    static void RandomSummonRulesAndCooldown()
    {
        var kind=mod.GetType("SCIEPort.Features.AbyssReaper.DeathSummonRules",true).GetMethod("Kind",BindingFlags.NonPublic|BindingFlags.Static);
        ushort[] outcomes={2,3,4,1,0,ushort.MaxValue};int[] weights={5,10,15,20,30,20};
        var expected=outcomes.SelectMany((outcome,index)=>Enumerable.Repeat(outcome,weights[index])).ToArray();
        var counts=new Dictionary<ushort,int>();
        for(int roll=0;roll<100;roll++)
        {
            ushort actual=(ushort)kind.Invoke(null,new object[]{roll});Check(actual==expected[roll],"random bucket including boundary "+roll);
            int count;counts.TryGetValue(actual,out count);counts[actual]=count+1;
        }
        Check(counts[2]==5&&counts[3]==10&&counts[4]==15&&counts[1]==20&&counts[0]==30&&counts[ushort.MaxValue]==20,"exact exhaustive 5/10/15/20/30/20 weights");
        foreach(int invalid in new[]{-1,100,int.MinValue,int.MaxValue})
        {
            bool rejected=false;try{kind.Invoke(null,new object[]{invalid});}catch(TargetInvocationException e){rejected=e.InnerException is ArgumentOutOfRangeException;}
            Check(rejected,"invalid random draw never becomes a summon "+invalid);
        }
        CommonLib.WorkType=WorkType.Local;
        using(var f=new Fixture())
        {
            var rolls=SummonRandom(f);SummonState(f);double start=f.Clock.GameTime;SummonCommand(f,1,4);
            Check(rolls.Calls==1&&(int)Call(f.Death,"Count",f.Player.PlayerGuid)==0,"20 percent failure rolls once without creating a contract");
            Check((string)Call(f.Death,"Feedback",f.Player)=="召唤失败"&&Math.Abs((double)Call(f.Death,"Remaining",f.Player,(byte)4)-60)<.001,"failure gives short feedback and consumes sixty seconds");
            f.Clock.m_gameTime=start+59.99;SummonCommand(f,2,2);Check(rolls.Calls==1&&(double)Call(f.Death,"Remaining",f.Player,(byte)4)>0,"retry before sixty seconds never draws again");
            rolls.Value=99;f.Clock.m_gameTime=start+60;SummonCommand(f,3,3);
            Check(rolls.Calls==2&&Math.Abs((double)Call(f.Death,"Remaining",f.Player,(byte)4)-60)<.001,"cooldown opens exactly at sixty seconds, including last failure bucket");
        }
        using(var f=new Fixture())
        {
            var rolls=SummonRandom(f);var state=Call(f.Death,"Get",f.Player);SummonCommand(f,1);
            Check(rolls.Calls==0&&(double)Get(state,"SummonReady")==0,"wrong authority mode does not roll or consume cooldown");
            Put(state,"Death",true);f.Player.ComponentMiner.Inventory.RemoveSlotItems(0,1);SummonCommand(f,2);
            Check(rolls.Calls==0&&(double)Get(state,"SummonReady")==0,"invalid held item does not roll or consume cooldown");
        }
        foreach(bool worldFull in new[]{false,true})using(var f=new Fixture())
        {
            var rolls=SummonRandom(f);var state=SummonState(f);
            for(ushort key=1;key<=(worldFull?24:7);key++)StoredSummon(f,key,"Wolf_Gray",true,worldFull?Guid.NewGuid():f.Player.PlayerGuid);
            SummonCommand(f,1);Check(rolls.Calls==0&&(double)Get(state,"SummonReady")==0,"full owner/world capacity is rejected before drawing "+worldFull);
        }
        foreach(bool hidden in new[]{true,false})foreach(bool dragon in new[]{false,true})using(var f=new Fixture())
        {
            var rolls=SummonRandom(f);rolls.Value=dragon?5:0;SummonState(f);
            StoredSummon(f,1,dragon?"SCIEPortBoneDragon_Saddled":"SCIEPortCerberus",hidden,f.Player.PlayerGuid);SummonCommand(f,1,4);
            Check(rolls.Calls==1&&(int)Call(f.Death,"Count",f.Player.PlayerGuid)==1,"unique collision never rerolls or creates a second hidden/pending unit");
            Check((string)Call(f.Death,"Feedback",f.Player)==(dragon?"已契约骨龙":"已契约刻尔柏洛斯")&&(double)Call(f.Death,"Remaining",f.Player,(byte)4)==60,"unique collision retains cooldown and ignores selected knight request");
        }
        using(var f=new Fixture())
        {
            var rolls=SummonRandom(f);rolls.Value=50;SummonState(f);f.Player.ComponentBody.Position=new Vector3(1000,64,1000);SummonCommand(f,1);
            Check(rolls.Calls==1&&(int)Call(f.Death,"Count",f.Player.PlayerGuid)==0&&(double)Call(f.Death,"Remaining",f.Player,(byte)4)==60,"unloaded unsafe spawn location creates nothing and retains cooldown");
            Check((string)Call(f.Death,"Feedback",f.Player)=="附近没有已加载的安全召唤位置","unsafe location reports the actual reason");
        }
    }
    static void RandomSummonNetwork()
    {
        CommonLib.WorkType=WorkType.Server;
        using(var f=new Fixture(false))
        {
            var rolls=SummonRandom(f);var state=SummonState(f);
            foreach(ushort forged in new ushort[]{0,1,2,3,4,ushort.MaxValue})
            {
                f.Clock.m_gameTime+=60;((List<IPackage>)Get(CommonLib.Net,"PendingPackages")).Clear();SummonCommand(f,(uint)(rolls.Calls+1),forged);
                Check((string)Call(f.Death,"Feedback",f.Player)=="召唤失败"&&(int)Call(f.Death,"Count",f.Player.PlayerGuid)==0,"authenticated client cannot choose a type with Target "+forged);
                var reply=((List<IPackage>)Get(CommonLib.Net,"PendingPackages")).Last(p=>p.GetType().Name=="DeathAuthorityPackage"&&(byte)Get(p,"Kind")==1);
                Check((float)Get(reply,"D")==60&&(Guid)Get(reply,"Owner")==f.Player.PlayerGuid&&(ushort)Get(reply,"Caster")==f.Player.Entity.EntityId,"host publishes sixty-second cooldown for the authenticated actor");
            }
            int draws=rolls.Calls;f.Clock.m_gameTime+=60;SummonCommand(f,(uint)draws);
            Check(rolls.Calls==draws,"replayed authenticated summon cannot reroll after cooldown expires");
            var q=Packet(f,4,100);Put(q,"World",Guid.NewGuid());Call(f.Death,"Enqueue",f.Player,f.Client,q);Call(f.Death,"Update");Check(rolls.Calls==draws,"cross-world summon cannot consume RNG");
            q=Packet(f,4,101);Call(f.Death,"Enqueue",f.Player,f.Client,q);Put(f.Client,"<TokenId>k__BackingField",Guid.NewGuid());Call(f.Death,"Update");Check(rolls.Calls==draws,"stale connection summon cannot consume RNG");
        }
        CommonLib.WorkType=WorkType.Client;
        using(var f=new Fixture())
        {
            CommonLib.Net.Server=f.Client;Put(f.Death,"serverToken",f.Client.TokenId);Put(f.Death,"worldKnown",true);Put(f.Death,"hasClock",true);var state=SummonState(f);var rolls=SummonRandom(f);
            var q=Packet(f,0,0);Put(q,"Kind",(byte)1);Put(q,"Owner",f.Player.PlayerGuid);Put(q,"Flags",(byte)1);Put(q,"Revision",2u);Put(q,"Stamp",f.Clock.GameTime-1.5);Put(q,"D",60f);Call(f.Death,"Receive",q);
            Check(Math.Abs((double)Call(f.Death,"Remaining",f.Player,(byte)4)-58.5)<.001,"client accepts sixty seconds and subtracts packet age");
            Put(q,"D",0f);Put(q,"Revision",1u);Call(f.Death,"Receive",q);Check((double)Call(f.Death,"Remaining",f.Player,(byte)4)>58,"old state cannot clear the sixty-second cooldown");
            foreach(float invalid in new[]{61f,-1f,float.NaN,float.PositiveInfinity})
            {Put(q,"Revision",3u);Put(q,"D",invalid);Call(f.Death,"Receive",q);Check((uint)Get(state,"Revision")==2u,"malformed cooldown rejected "+invalid);}
            var before=(double)Get(state,"SummonReady");SummonCommand(f,200,2);
            Check(rolls.Calls==0&&(int)Call(f.Death,"Count",f.Player.PlayerGuid)==0&&(double)Get(state,"SummonReady")==before,"client command path cannot draw or create locally");CommonLib.Net.Server=null;
        }
    }
}
