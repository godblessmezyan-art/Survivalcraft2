using System;
using System.Collections.Generic;
using SCIEPort.Features.DevouringEye;

internal static class Program
{
    private static int count;
    private static void Check(bool ok,string name){count++;if(!ok)throw new Exception(name);}
    private sealed class Fake : IRecoverySteps
    {
        internal readonly List<string> Calls=new List<string>();
        internal string Fail="";
        internal bool Source=true,Item,RollbackFails;
        private void Stage(string s){Calls.Add(s);if(Fail==s)throw new Exception(s);}
        public void Validate(){Stage("Validate");}
        public void Prepare(){Stage("Prepare");}
        public void Remove(){Source=false;Stage("Remove");}
        public void Grant(){Item=true;Stage("Grant");}
        public void Commit(){Stage("Commit");}
        public void Rollback(){Stage("Rollback");if(RollbackFails)throw new Exception("restore failed");Source=true;Item=false;}
    }
    private static void Main()
    {
        for(int color=0;color<16;color++)Check(RecoveryRules.BlockMapped("GraniteBlock",1|(color<<1)),"preserve all painted colors");
        for(int axis=0;axis<3;axis++)Check(RecoveryRules.BlockMapped("OakWoodBlock",axis),"log orientation");
        foreach(string name in new[]{"ChestBlock","CraftingTableBlock","FurnaceBlock","DoorBlock","FurnitureBlock","BedrockBlock","WaterBlock","UnknownModBlock","Player"})Check(!RecoveryRules.BlockMapped(name,0),"special denied "+name);
        Check(!RecoveryRules.BlockMapped("GraniteBlock",32),"invalid data");Check(!RecoveryRules.BlockMapped("GraniteBlock",2),"noncanonical color data");Check(!RecoveryRules.BlockMapped("OakWoodBlock",3),"invalid wood axis");
        Check(RecoveryRules.Nearest(4,2,float.PositiveInfinity)==1,"unknown entity shields ground");
        Check(RecoveryRules.Nearest(2,4,5)==0,"wall shields entity");Check(RecoveryRules.Nearest(4,5,2)==2,"moving structure shields targets");
        Check(RecoveryRules.Nearest(2,2,3)==1,"tie shields ground");
        Check(RecoveryRules.AllowExecutor(false,true,false,false),"local main allowed");
        Check(RecoveryRules.AllowExecutor(false,true,true,true),"server local administrator allowed");
        Check(!RecoveryRules.AllowExecutor(true,true,false,true),"client refuses even admin");
        Check(!RecoveryRules.AllowExecutor(false,false,false,true),"other player refuses");
        Check(!RecoveryRules.AllowExecutor(false,true,true,false),"server without permission refuses");
        Check(RecoveryRules.CanFit(false,39,3,3,40),"merge has room");
        Check(!RecoveryRules.CanFit(false,40,3,3,40),"full inventory refuses");
        Check(!RecoveryRules.CanFit(false,1,4,3,40),"other item not overwritten");
        Check(!RecoveryRules.CanFit(true,9999,3,3,99980001),"creative occupied not overwritten");
        Check(RecoveryRules.CanFit(true,0,0,924,99980001),"creative empty slot allowed");
        foreach(int y in new[]{-1,0,255,256})Check(!RecoveryRules.InBounds(0,y,0),"vertical boundary");
        Check(!RecoveryRules.InBounds(1000000,40,0),"coordinate cap");Check(RecoveryRules.InBounds(-17,64,-1),"negative coordinates");
        var success=new Fake();var tx=new RecoveryTransaction();Check(tx.Execute(success)==null&&!success.Source&&success.Item,"successful transfer");
        foreach(string failure in new[]{"Validate","Prepare","Remove","Grant","Commit"})
        {
            var fake=new Fake{Fail=failure};var transaction=new RecoveryTransaction();
            Check(transaction.Execute(fake)!=null,"fault reported "+failure);
            Check(fake.Source&&!fake.Item,"source/item conserved "+failure);
            Check(fake.Calls.Contains("Rollback")== (failure!="Validate"&&failure!="Prepare"),"only roll back prepared mutation");
            Check(!transaction.Busy,"busy clears");
        }
        var broken=new Fake{Fail="Grant",RollbackFails=true};var locked=new RecoveryTransaction();
        Check(locked.Execute(broken)!=null&&locked.Locked,"rollback failure locks world");
        var denied=new Fake();Check(locked.Execute(denied)!=null&&denied.Calls.Count==0,"no retry after uncertain rollback");
        var interrupted=new RecoveryTransaction();interrupted.Lock();Check(interrupted.Execute(new Fake())!=null,"restart journal locks, never auto-grants");
        Console.WriteLine("Devouring Eye: "+count+" mapping, occlusion, boundary and transaction fault checks passed.");
    }
}
