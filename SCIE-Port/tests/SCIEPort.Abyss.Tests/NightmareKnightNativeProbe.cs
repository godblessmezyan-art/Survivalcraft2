using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;

// Real target component/path/attack dispatch fixtures, without a game/renderer/network session.
public static partial class DeathAuthorityNativeProbe
{
    static ComponentCreature Knight(Fixture f,ushort id,Vector3 at)
    {
        var c=f.Creature(id,at);Put(c.Entity,"m_valuesDictionary",new ValuesDictionary{DatabaseObject=database.FindDatabaseObject("SCIEPortNightmareKnight",game.EntityTemplateType,true)});
        c.ComponentLocomotion.WalkSpeed=6;c.ComponentBody.BoxSize=new Vector3(1.8f,3.65f,3.8f);c.ComponentBody.Rotation=Quaternion.Identity;c.ComponentBody.StandingOnValue=3;
        c.ComponentCreatureModel.m_eyePosition=at+Vector3.UnitY*2.9f;
        Check((bool)Call(f.Death,"Adopt",c,f.Player.PlayerGuid,true),"knight adopts as one normal contract");return c;
    }
    static void KnightStrike(Fixture f,object g,int budget=24){Call(f.Death,"UpdateKnightAttack",g,f.Player,budget);}
    static void KnightRulesAndCombat()
    {
        CommonLib.WorkType=WorkType.Local;
        var swordAngle=mod.GetType("SCIEPort.Features.AbyssReaper.NightmareKnightRules").GetMethod("SwordAngle",BindingFlags.Static|BindingFlags.NonPublic);
        Check((float)swordAngle.Invoke(null,new object[]{.22})<-.3f,"sword impact occurs during the downward stroke, not the raised windup");
        using(var f=new Fixture())
        {
            var c=Knight(f,120,new Vector3(24.5f,64,24.5f));var g=Guard(f,c);
            Check(c.DisplayName=="夜魇骑士"&&c.ComponentHealth.AttackResilience==70&&c.ComponentLocomotion.WalkSpeed==8,"knight native name/70 health/8 speed applied");
            Knight(f,121,new Vector3(30,64,24));Check((int)Call(f.Death,"Count",f.Player.PlayerGuid)==2,"knights repeat without a unique-unit gate and each consumes one slot");
            var target=f.Creature(122,new Vector3(24.5f,64,14.5f),true);Put(g,"Target",target);Put(g,"PursuitUntil",f.Clock.GameTime+20);
            Action move=()=>{Put(f.Death,"roamBudget",4096);Call(f.Death,"MoveGuard",g,f.Player);};move();
            Check((bool)Get(g,"Charging")&&c.ComponentLocomotion.WalkSpeed==10,"straight supported 10-block pursuit enters charge speed 10");
            for(int i=0;i<20;i++)move();Check(c.ComponentLocomotion.WalkSpeed==10,"repeated charge updates never multiply speed");
            c.ComponentLocomotion.WalkSpeed+=3;move();Check(c.ComponentLocomotion.WalkSpeed==13,"charge preserves an external additive speed bonus");
            target.ComponentBody.Position=new Vector3(34.5f,64,24.5f);move();Check(!(bool)Get(g,"Charging")&&c.ComponentLocomotion.WalkSpeed==11,"turning cancels charge and returns only its own speed increment");
            target.ComponentBody.Position=new Vector3(24.5f,64,14.5f);var cells=(Terrain)Get(f.Base,"Cells");cells.SetCellValueFast(24,63,20,0);move();
            Check(!(bool)Get(g,"Charging"),"gap in the forward footprint prevents charge");cells.SetCellValueFast(24,63,20,3);
            cells.SetCellValueFast(24,65,19,3);move();Check(!(bool)Get(g,"Charging"),"wall prevents charge");cells.SetCellValueFast(24,65,19,0);
            c.ComponentBody.ImmersionFactor=.5f;move();Check(!(bool)Get(g,"Charging"),"water prevents charge");c.ComponentBody.ImmersionFactor=0;
            c.ComponentBody.StandingOnValue=null;move();Check(!(bool)Get(g,"Charging"),"airborne knight cannot charge");c.ComponentBody.StandingOnValue=3;
            target.ComponentBody.Position=new Vector3(24.5f,64,6.5f);move();Check(!(bool)Get(g,"Charging"),"target beyond 16 blocks uses normal approach");
            target.ComponentBody.Position=new Vector3(24.5f,64,22.5f);move();Check(!(bool)Get(g,"Charging")&&!c.Entity.FindComponent<ComponentPathfinding>(true).Destination.HasValue,"close knight brakes and stays in melee instead of retreating");
            target.ComponentBody.Position=new Vector3(24.5f,64,14.5f);move();f.Player.ComponentBody.Position=new Vector3(24.5f,64,25.5f);Call(f.Death,"BrakeNearPlayers",g);
            Check(!(bool)Get(g,"Charging")&&c.ComponentLocomotion.WalkSpeed==11&&!c.Entity.FindComponent<ComponentPathfinding>(true).Destination.HasValue,"player proximity cancels charge and stops without an outward path");
            Mode(f,c,1);Check(!(bool)Get(g,"Charging")&&Get(g,"Target")==null,"free mode clears charge/target");
        }
        using(var f=new Fixture())
        {
            var c=Knight(f,123,new Vector3(24.5f,64,24.5f));var g=Guard(f,c);var target=f.Creature(124,new Vector3(24.5f,64,22.1f),true);
            Put(g,"Target",target);Put(g,"PursuitUntil",f.Clock.GameTime+20);var recorder=new AttackRecorder();ModsManager.RegisterHook("AttackBody",recorder);
            KnightStrike(f,g);Check(recorder.Targets.Count==0&&Get(g,"SwordTarget")==target,"sword windup does not deal instant or native contact damage");
            Check(Math.Abs((double)Get(g,"NextAttack")-f.Clock.GameTime-1.2)<.00001,"sword attack interval is exactly 1.2 seconds");
            f.Clock.m_gameTime+=.21;KnightStrike(f,g);Check(recorder.Targets.Count==0,"sword cannot hit before .22-second windup");
            f.Clock.m_gameTime+=.02;KnightStrike(f,g);KnightStrike(f,g);
            Check(recorder.Targets.Count==1&&recorder.Powers[0]==15&&recorder.Targets[0]==target.ComponentBody,"one sword impact dispatches exactly 15 base damage through native AttackBody");
            f.Clock.m_gameTime+=.96;KnightStrike(f,g);Check(Get(g,"SwordTarget")==null,"sword cannot restart before the 1.2-second interval");
            f.Clock.m_gameTime+=.02;KnightStrike(f,g);Check(Get(g,"SwordTarget")==target,"next swing begins after interval without retreating");
            Mode(f,c,1);f.Clock.m_gameTime+=.3;KnightStrike(f,g);Check(recorder.Targets.Count==1&&Get(g,"SwordTarget")==null,"switching to free mode cancels a pending impact");
            Mode(f,c,2);Put(g,"Target",target);Put(g,"NextAttack",0d);KnightStrike(f,g);target.ComponentBody.Position+=Vector3.UnitZ*8;f.Clock.m_gameTime+=.23;KnightStrike(f,g);
            Check(recorder.Targets.Count==1,"moving out of range during windup avoids the hit");
            target.ComponentBody.Position=new Vector3(24.5f,64,22.1f);Put(g,"NextAttack",0d);KnightStrike(f,g);f.Clock.m_gameTime+=.23;KnightStrike(f,g,0);KnightStrike(f,g);
            Check(recorder.Targets.Count==1,"exhausted frame damage budget consumes impact without deferred duplicate damage");
            var ally=Knight(f,125,new Vector3(24.5f,64,22.1f));Put(g,"Target",ally);Put(g,"NextAttack",0d);KnightStrike(f,g);
            Check(Get(g,"SwordTarget")==null&&!(bool)Call(f.Death,"GuardEnemy",g,f.Player,f.Player),"knight cannot swing at another knight or player even in attack mode");
            Put(g,"Target",target);Put(g,"NextAttack",0d);KnightStrike(f,g);c.ComponentHealth.Health=0;f.Clock.m_gameTime+=.3;Call(f.Death,"UpdateGuards");Check(recorder.Targets.Count==1,"death cancels pending sword damage");ModsManager.ModHooks.Clear();
        }
        using(var f=new Fixture())
        {
            var c=Knight(f,126,new Vector3(24.5f,64,24.5f));var g=Guard(f,c);Call(g,"Charge",true);((IDisposable)f.Death).Dispose();
            Check(c.ComponentLocomotion.WalkSpeed==8,"world teardown returns the transient charge increment");
        }
    }
    static void KnightNetworkAndStorage()
    {
        CommonLib.WorkType=WorkType.Server;
        using(var f=new Fixture(false))
        {
            var c=Knight(f,127,new Vector3(24.5f,64,24.5f));var g=Guard(f,c);Call(f.Death,"SetKnightCharge",g,true);
            var packets=(List<IPackage>)Get(CommonLib.Net,"PendingPackages");Check(packets.Any(p=>p.GetType().Name=="DeathAuthorityPackage"&&(byte)Get(p,"Kind")==3&&(float)Get(p,"C")==3),"host publishes knight charge through existing Kind3 C=3");
            Put(g,"NextAttack",f.Clock.GameTime+.9);var data=(ValuesDictionary)Call(f.Death,"Save");var row=data.GetValue<ValuesDictionary>("Ally0");
            Check(Math.Abs(row.GetValue<float>("KnightCooldown")-.9f)<.001f&&row.GetValue<float>("HealthMaximum")==70,"contract saves sword cooldown and one shared 70-health maximum");
            var replacement=New("DeathAuthorityRuntime",f.Owner,Get(f.Base,"Terrain"),f.Bodies,f.Clock,Get(f.Base,"Players"));Call(replacement,"Load",data);
            var pending=((IList)Call(replacement,"Friends",f.Player.PlayerGuid))[0];Check((bool)Get(pending,"Knight")&&!(bool)Get(pending,"Charging"),"pending load recognizes knight but never persists transient charge");
            Call(replacement,"EntityAdded",c.Entity);var restored=((IDictionary)Get(replacement,"guards"))[c.Entity.EntityId];
            Check((bool)restored.GetType().GetProperty("Knight",F).GetValue(restored,null)&&Math.Abs((double)Get(restored,"NextAttack")-f.Clock.GameTime-.9)<.001,"entity rebind restores knight and remaining sword cooldown");((IDisposable)replacement).Dispose();
            row.SetValue("Id",0);row.SetValue("Hidden",true);row.SetValue("Snapshot",new ValuesDictionary());replacement=New("DeathAuthorityRuntime",f.Owner,Get(f.Base,"Terrain"),f.Bodies,f.Clock,Get(f.Base,"Players"));Call(replacement,"Load",data);
            Check((bool)Get(((IList)Call(replacement,"Friends",f.Player.PlayerGuid))[0],"Knight"),"hidden knight remains one identifiable contract");((IDisposable)replacement).Dispose();
            row.SetValue("KnightCooldown",1.2f);replacement=New("DeathAuthorityRuntime",f.Owner,Get(f.Base,"Terrain"),f.Bodies,f.Clock,Get(f.Base,"Players"));Call(replacement,"Load",data);
            Check(((IList)Call(replacement,"Friends",f.Player.PlayerGuid)).Count==1&&Get(replacement,"loadFailure")==null,"full 1.2-second float cooldown roundtrips after an immediate save");((IDisposable)replacement).Dispose();
            row.SetValue("KnightCooldown",2f);replacement=New("DeathAuthorityRuntime",f.Owner,Get(f.Base,"Terrain"),f.Bodies,f.Clock,Get(f.Base,"Players"));Call(replacement,"Load",data);
            Check(ReferenceEquals(Call(replacement,"Save"),data),"invalid saved knight cooldown preserves original data");((IDisposable)replacement).Dispose();
        }
        CommonLib.WorkType=WorkType.Client;
        using(var f=new Fixture())
        {
            CommonLib.Net.Server=f.Client;Put(f.Death,"serverToken",f.Client.TokenId);Put(f.Death,"worldKnown",true);Put(f.Death,"hasClock",true);
            var q=Packet(f,0,0);Put(q,"Kind",(byte)3);Put(q,"Target",(ushort)128);Put(q,"Caster",(ushort)129);Put(q,"Owner",f.Player.PlayerGuid);Put(q,"Name","夜魇骑士");Put(q,"Revision",1u);Put(q,"C",3f);Call(f.Death,"Receive",q);
            var row=((IList)Call(f.Death,"Friends",f.Player.PlayerGuid))[0];Check((bool)Get(row,"Knight")&&(bool)Get(row,"Charging"),"client reads knight identity and charging state");
            foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-1f,.5f,1f,4f}){Put(q,"Revision",2u);Put(q,"C",invalid);Call(f.Death,"Receive",q);Check((uint)Get(((IList)Call(f.Death,"Friends",f.Player.PlayerGuid))[0],"Revision")==1,"invalid knight type/charge encoding rejected");}
            Put(q,"Revision",2u);Put(q,"C",2f);Call(f.Death,"Receive",q);row=((IList)Call(f.Death,"Friends",f.Player.PlayerGuid))[0];Check((bool)Get(row,"Knight")&&!(bool)Get(row,"Charging"),"client receives return to ordinary knight speed");
            var model=New("ComponentNightmareKnightModel");Put(model,"m_subsystemTime",f.Clock);Call(model,"ReceiveSwing",5u,.2f);double started=(double)Get(model,"swingStarted");
            Check(Math.Abs(started-(f.Clock.GameTime-.2))<.001,"client sword presentation ages the host event");Call(model,"ReceiveSwing",5u,0f);Call(model,"ReceiveSwing",4u,0f);Check((double)Get(model,"swingStarted")==started,"duplicate/older sword events do not restart animation");
            var victim=f.Creature(130,new Vector3(24.5f,64,22.1f));var g=New("DeathGuard");Put(g,"Creature",f.Player);Put(g,"Target",victim);KnightStrike(f,g);Check(victim.ComponentHealth.Health==1,"client knight attack path cannot generate damage");CommonLib.Net.Server=null;
        }
    }
}
