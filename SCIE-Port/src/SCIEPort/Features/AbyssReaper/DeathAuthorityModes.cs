using System;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        private int roamBudget;
        internal static string ModeText(DeathGuardMode mode)=>mode==DeathGuardMode.Free?"自由行动":mode==DeathGuardMode.Attack?"攻击":"护卫";
        private string SetGuardMode(ComponentPlayer player,ushort key,DeathGuardMode mode)
        {
            if(!Authority||key==0)return "请先选择自己的契约生物";
            foreach(var guard in guards.Values)if(guard.Key==key&&guard.Owner==player.PlayerGuid)
            {
                guard.Mode=mode;guard.Target=guard.BiteTarget=guard.SwordTarget=null;SetKnightCharge(guard,false);guard.Following=guard.AutoDriving=false;
                guard.PursuitUntil=guard.NextThink=guard.NextPlan=guard.NextRoam=0;
                guard.RoamCenter=guard.Creature.ComponentBody.Position;guard.Path.Stop();
                guard.Creature.ComponentCreatureModel.AttackOrder=false;guard.Creature.ComponentLocomotion.WalkOrder=null;
                if(mode==DeathGuardMode.Free)guard.RidingAuto=false;
                FriendPacket(Row(guard));PublishRidePlan(guard);return ModeText(mode);
            }
            Friend row=null;
            foreach(var candidate in hidden.Values)if(candidate.Id==key&&candidate.Owner==player.PlayerGuid){row=candidate;break;}
            if(row==null)foreach(var candidate in saved.Values)if(candidate.Id==key&&!candidate.Discard&&candidate.Owner==player.PlayerGuid){row=candidate;break;}
            if(row==null)return "未找到自己的契约生物";
            row.Mode=mode;if(mode==DeathGuardMode.Free)row.RidingAuto=false;FriendPacket(row);
            return ModeText(mode);
        }
        // Flat, fully supported short routes only. No native detour search or blind ground probes.
        private bool SafeRoamRoute(ComponentBody body,Vector3 destination)
            =>SafeRoamRoute(body,destination,0);
        private bool SafeRoamRoute(ComponentBody body,Vector3 destination,float margin)
        {
            Vector3 start=body.Position,size=body.BoxSize;
            size.X+=margin*2;size.Z+=margin*2;
            if(!WorldCutDamage.Finite(start)||!WorldCutDamage.Finite(destination)||!WorldCutDamage.Finite(size)||size.X<=0||size.Z<=0||size.Y<=0||size.X>6||size.Z>8||size.Y>6)return false;
            float length=Vector3.Distance(start,destination);int y=Terrain.ToCell(start.Y+.05f);
            if(length>4||Math.Abs(start.Y-y)>.15f||Math.Abs(destination.Y-start.Y)>.01f||y<2||y+size.Y>=254)return false;
            int steps=Math.Max(1,(int)Math.Ceiling(length/.5f)),localBudget=1024;
            for(int i=0;i<=steps;i++)
            {
                Vector3 p=Vector3.Lerp(start,destination,(float)i/steps);
                int minX=Terrain.ToCell(p.X-size.X*.5f+.001f),maxX=Terrain.ToCell(p.X+size.X*.5f-.001f);
                int minZ=Terrain.ToCell(p.Z-size.Z*.5f+.001f),maxZ=Terrain.ToCell(p.Z+size.Z*.5f-.001f);
                for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
                {
                    if(--roamBudget<0||--localBudget<0||!VoidPortalSafety.StableFloor(terrain,x,y-1,z))return false;
                    for(int cy=y;cy<=Terrain.ToCell(p.Y+size.Y-.001f);cy++)
                        if(--roamBudget<0||--localBudget<0||Terrain.ExtractContents(terrain.Terrain.GetCellValue(x,cy,z))!=0)return false;
                }
            }
            return true;
        }
        private void RoamGuard(DeathGuard guard)
        {
            var body=guard.Creature.ComponentBody;
            if(guard.Path.Destination.HasValue)
            {
                if(!guard.Path.IsStuck&&Vector3.DistanceSquared(body.Position,guard.RoamCenter)<=36&&SafeRoamRoute(body,guard.Path.Destination.Value))return;
                guard.Path.Stop();guard.Creature.ComponentLocomotion.WalkOrder=null;
            }
            if(Now<guard.NextRoam)return;guard.NextRoam=Now+3+(guard.Key%3)*.5;
            double angle=(guard.Key+guard.RoamStep++*3)*Math.PI/4;
            Vector3 at=body.Position+new Vector3((float)Math.Cos(angle)*2.5f,0,(float)Math.Sin(angle)*2.5f);
            if(Vector3.DistanceSquared(at,guard.RoamCenter)>36||!SafeRoamRoute(body,at))return;
            guard.Path.SetDestination(at,.35f,.5f,0,false,false,true,null);
        }
    }
}
