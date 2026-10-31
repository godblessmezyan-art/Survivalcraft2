using System;
using System.Collections.Generic;
using System.Globalization;
using Engine;
using Game;
using Game.NetWork;
using GameEntitySystem;
using TemplatesDatabase;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        internal sealed class Friend{internal ushort Id,EntityId;internal Guid Owner;internal string Name,Template;internal bool Special,Hidden,Discard,HealOnReturn,Warhorse,Pending,Cerberus,HealthKnown;internal double SpawnTime;internal float BiteCooldown,HealthFraction,HealthMaximum;internal uint Revision;internal ValuesDictionary Snapshot;}
        private sealed class Threat{internal ComponentCreature Creature;internal double Until;}
        private readonly Dictionary<ushort,DeathGuard> guards=new Dictionary<ushort,DeathGuard>();
        private readonly Dictionary<ushort,Friend> clientFriends=new Dictionary<ushort,Friend>(),saved=new Dictionary<ushort,Friend>();
        private readonly Dictionary<ushort,Friend> hidden=new Dictionary<ushort,Friend>();
        private readonly List<Entity> discardedEntities=new List<Entity>();
        private readonly Dictionary<ComponentLocomotion,DeathSummonStats.Bonus> clientSummonBonuses=new Dictionary<ComponentLocomotion,DeathSummonStats.Bonus>();
        private readonly HashSet<ComponentLocomotion> clientSummonsSeen=new HashSet<ComponentLocomotion>();
        private readonly List<ComponentLocomotion> retiredClientSummons=new List<ComponentLocomotion>();
        private readonly Dictionary<Guid,Threat> threats=new Dictionary<Guid,Threat>();
        private readonly Dictionary<ushort,uint> friendRevisions=new Dictionary<ushort,uint>();
        private readonly Dictionary<ushort,Friend> snapshotRows=new Dictionary<ushort,Friend>();private uint snapshotId,lastSnapshot;private int snapshotCount;
        private readonly List<ushort> retired=new List<ushort>();private ValuesDictionary loadFailure;
        internal bool IsFriendly(ComponentCreature c)
        {if(c==null)return false;if(Authority)return guards.ContainsKey(c.Entity.EntityId)&&guards[c.Entity.EntityId].Creature==c;foreach(var f in clientFriends.Values)if(!f.Hidden&&!f.Pending&&f.EntityId==c.Entity.EntityId)return true;return false;}
        private int ArmyCount{get{int n=guards.Count+hidden.Count;foreach(var f in saved.Values)if(!f.Discard)n++;return n;}}
        internal int Count(Guid id){int n=0;if(Authority){foreach(var g in guards.Values)if(g.Owner==id)n++;foreach(var f in saved.Values)if(!f.Discard&&f.Owner==id)n++;foreach(var f in hidden.Values)if(f.Owner==id)n++;}else foreach(var f in clientFriends.Values)if(f.Owner==id)n++;return n;}
        internal int HiddenCount(Guid id){int n=0;foreach(var f in (Authority?hidden:clientFriends).Values)if(f.Owner==id&&f.Hidden)n++;return n;}
        internal List<Friend> Friends(Guid owner)
        {
            var list=new List<Friend>();if(Authority){foreach(var g in guards.Values)if(g.Owner==owner)list.Add(Row(g));foreach(var f in saved.Values)if(!f.Discard&&f.Owner==owner)list.Add(f);foreach(var f in hidden.Values)if(f.Owner==owner)list.Add(f);}else foreach(var f in clientFriends.Values)if(f.Owner==owner)list.Add(f);return list;
        }
        private Friend Row(DeathGuard g)
        {
            var row=new Friend{Id=g.Key,EntityId=g.Creature.Entity.EntityId,Owner=g.Owner,Name=g.Creature.DisplayName,Template=g.Template,SpawnTime=g.SpawnTime,Special=g.Special,Warhorse=g.Warhorse,Cerberus=g.Cerberus,BiteCooldown=g.Cerberus?(float)Math.Max(0,Math.Min(CerberusRules.Cooldown,g.NextAttack-Now)):0};
            ReadLiveHealth(row,g.Creature);return row;
        }
        // A/B were unused for Kind3. Keep the 252 byte layout and use flag32 for health metadata.
        private void FriendPacket(Friend row,Client to=null,uint snapshot=0)=>Send(new DeathAuthorityPackage{Kind=3,Target=row.Id,Caster=row.Hidden||row.Pending?(ushort)0:row.EntityId,Owner=row.Owner,Sequence=snapshot,Flags=(byte)((row.Special?1:0)|(row.Hidden?2:0)|(row.Pending?4:0)|(row.Warhorse?8:0)|(row.Cerberus?16:0)|(row.HealthKnown?32:0)),A=row.HealthKnown?row.HealthFraction:0,B=row.HealthKnown?row.HealthMaximum:0,Name=(row.Name??"契约生物").Length>64?row.Name.Substring(0,64):row.Name??"契约生物"},to);
        private ushort FreeKey()
        {
            for(int i=1;i<=ushort.MaxValue;i++){ushort key=(ushort)i;if(hidden.ContainsKey(key))continue;bool used=false;foreach(var g in guards.Values)if(g.Key==key){used=true;break;}if(!used)foreach(var f in saved.Values)if(f.Id==key){used=true;break;}if(!used)return key;}return 0;
        }
        private bool Adopt(ComponentCreature c,Guid owner,bool special)
            =>AdoptContract(c,owner,special,FreeKey());
        private bool AdoptContract(ComponentCreature c,Guid owner,bool special,ushort key)
        {
            if(!Authority||key==0||c==null||!c.IsAddedToProject||c.ComponentHealth.Health<=0||Count(owner)>=DeathAuthorityRules.OwnerLimit||ArmyCount>=DeathAuthorityRules.WorldLimit||IsFriendly(c)||c is ComponentPlayer)return false;
            if(c.Entity.ValuesDictionary.DatabaseObject.Name==CerberusRules.Template&&HasCerberus(owner))return false;
            var guard=new DeathGuard{Key=key};if(!guard.Bind(c,owner,special))return false;guards.Add(c.Entity.EntityId,guard);FriendPacket(Row(guard));return true;
        }
        private void SyncFriends(Client to)
        {
            if(to==null)return;var start=new DeathAuthorityPackage{Kind=6,Target=(ushort)ArmyCount};Send(start,to);foreach(var g in guards.Values)FriendPacket(Row(g),to,start.Revision);foreach(var f in saved.Values)if(!f.Discard)FriendPacket(f,to,start.Revision);foreach(var f in hidden.Values)FriendPacket(f,to,start.Revision);Send(new DeathAuthorityPackage{Kind=7,Sequence=start.Revision},to);
            foreach(var w in waves)if(Now-w.Started<1.6)Send(new DeathAuthorityPackage{Kind=2,Caster=w.Owner.Entity.EntityId,Owner=w.Guid,Sequence=w.Sequence,Position=w.Center,A=(float)(Now-w.Started)},to);
            foreach(var g in guards.Values)if(g.Cerberus&&Now-g.BiteStarted<CerberusRules.AnimationDuration)Send(BitePacket(g),to);
        }
        private void ReceiveFriend(DeathAuthorityPackage q)
        {
            if(q.Kind==3&&(q.Flags&32)!=0&&!ValidHealth(q.A,q.B))return;
            if(q.Kind==6){if(q.Target>24||!WorldCutRules.Newer(q.Revision,lastSnapshot))return;snapshotRows.Clear();snapshotId=q.Revision;snapshotCount=q.Target;return;}
            if(q.Kind==7)
            {
                if(q.Sequence!=snapshotId||snapshotRows.Count!=snapshotCount)return;
                var keep=new Dictionary<ushort,Friend>();foreach(var row in clientFriends)if(friendRevisions[row.Key]>snapshotId)keep[row.Key]=row.Value;
                foreach(var row in snapshotRows){uint newer;friendRevisions.TryGetValue(row.Key,out newer);if(newer<=row.Value.Revision){keep[row.Key]=row.Value;friendRevisions[row.Key]=row.Value.Revision;}}
                clientFriends.Clear();foreach(var row in keep)clientFriends[row.Key]=row.Value;lastSnapshot=snapshotId;snapshotId=0;snapshotRows.Clear();return;
            }
            if(q.Target==0)return;
            if(q.Kind==3&&q.Sequence!=0){if(q.Sequence==snapshotId&&q.Owner!=Guid.Empty&&snapshotRows.Count<24)snapshotRows[q.Target]=ReadFriend(q);return;}
            uint last;friendRevisions.TryGetValue(q.Target,out last);if(!WorldCutRules.Newer(q.Revision,last)||!WorldCutRules.Newer(q.Revision,lastSnapshot))return;friendRevisions[q.Target]=q.Revision;
            if(q.Kind==4){clientFriends.Remove(q.Target);return;}if(q.Owner==Guid.Empty||q.Name.Length>64||clientFriends.Count>=24&&!clientFriends.ContainsKey(q.Target))return;
            clientFriends[q.Target]=ReadFriend(q);
        }
        private static Friend ReadFriend(DeathAuthorityPackage q)=>new Friend{Id=q.Target,EntityId=q.Caster,Owner=q.Owner,Name=q.Name,Special=(q.Flags&1)!=0,Hidden=(q.Flags&2)!=0,Pending=(q.Flags&4)!=0,Warhorse=(q.Flags&8)!=0,Cerberus=(q.Flags&16)!=0,HealthKnown=(q.Flags&32)!=0,HealthFraction=q.A,HealthMaximum=q.B,Revision=q.Revision};
        private bool Summon(ComponentPlayer p,ushort kind,out string reason)
        {
            reason="召唤名额已满（每人七只、世界24只，含次位面）";if(Count(p.PlayerGuid)>=DeathAuthorityRules.OwnerLimit||ArmyCount>=DeathAuthorityRules.WorldLimit)return false;
            if(kind>CerberusRules.SummonKind){reason="未知的召唤类型";return false;}
            if(kind==CerberusRules.SummonKind&&HasCerberus(p.PlayerGuid)){reason="已契约刻尔柏洛斯（含次位面/待加载），每人仅一头";return false;}
            Vector3 at;if(!FindFree(p.ComponentBody.Position,new Vector3(.9f,1.2f,.9f),out at)){reason="附近没有已加载的安全召唤位置";return false;}
            Entity entity=null;
            try
            {
                entity=DatabaseManager.CreateEntity(terrain.Project,kind==CerberusRules.SummonKind?CerberusRules.Template:kind==1?DeathAuthorityRules.MountTemplate:DeathAuthorityRules.HoundTemplate,true);var c=entity.FindComponent<ComponentCreature>(true);if(kind==1&&entity.FindComponent<ComponentMount>(false)==null)throw new InvalidOperationException("Mount component missing");if(!FindFree(p.ComponentBody.Position,c.ComponentBody.BoxSize,out at)){entity.Dispose();reason="没有容纳召唤生物的安全位置";return false;}c.ComponentBody.Position=at;c.ComponentSpawn.SpawnDuration=0;
                terrain.Project.AddEntity(entity);if(!Adopt(c,p.PlayerGuid,true)){terrain.Project.RemoveEntity(entity,true);reason="召唤生物AI接管失败，已回收";return false;}
                reason=kind==CerberusRules.SummonKind?"刻尔柏洛斯已响应 · 三首噬魂37伤害/3格/1秒":kind==1?"夜魇战马已响应召唤 · 生命37 · 移速10":"冥界猎犬已响应 · 生命37 · 伤害17 · 移速5";return true;
            }
            catch(Exception e){if(entity?.IsAddedToProject==true)terrain.Project.RemoveEntity(entity,true);Log.Warning("[SCIEPort:Death] Summon failed: "+e.GetType().Name);reason="召唤失败，冷却未消耗";return false;}
        }
        private bool FindFree(Vector3 center,Vector3 size,out Vector3 at)
        {
            at=center;
            for(int i=0;i<8;i++)
            {
                float angle=i*MathUtils.PI/4;int x=Terrain.ToCell(center.X+(float)Math.Cos(angle)*3),z=Terrain.ToCell(center.Z+(float)Math.Sin(angle)*3);
                if(!VoidPortalSafety.Ready(terrain,x,z)||!VoidPortalSafety.Unprotected(x,z))continue;
                for(int dy=3;dy>=-3;dy--)
                {
                    int y=Terrain.ToCell(center.Y)+dy;if(y<2||y+size.Y>=254)continue;
                    if(!BlocksManager.Blocks[Terrain.ExtractContents(terrain.Terrain.GetCellValue(x,y-1,z))].IsCollidable)continue;
                    Vector3 candidate=new Vector3(x+.5f,y+.03f,z+.5f);var box=new BoundingBox(candidate-new Vector3(size.X*.5f,0,size.Z*.5f),candidate+new Vector3(size.X*.5f,size.Y,size.Z*.5f));bool clear=true;
                    for(int bx=Terrain.ToCell(box.Min.X);bx<=Terrain.ToCell(box.Max.X)&&clear;bx++)for(int bz=Terrain.ToCell(box.Min.Z);bz<=Terrain.ToCell(box.Max.Z)&&clear;bz++)for(int by=y;by<=Terrain.ToCell(box.Max.Y);by++)
                    {if(!VoidPortalSafety.Ready(terrain,bx,bz)||!VoidPortalSafety.Unprotected(bx,bz)){clear=false;break;}var b=BlocksManager.Blocks[Terrain.ExtractContents(terrain.Terrain.GetCellValue(bx,by,bz))];if(b.IsCollidable||b is MagmaBlock){clear=false;break;}}
                    if(clear){var moving=terrain.Project.FindSubsystem<SubsystemMovingBlocks>(false);if(moving!=null){if(moving.MovingBlockSets.Count>256)clear=false;else foreach(var set in moving.MovingBlockSets)if(box.Intersection(set.BoundingBox(true))){clear=false;break;}}}
                    if(clear){candidates.Clear();bodies.FindBodiesInArea(box.Min.XZ,box.Max.XZ,candidates);foreach(var body in candidates)if(box.Intersection(body.BoundingBox)){clear=false;break;}}
                    if(clear){at=candidate;return true;}
                }
            }
            return false;
        }
        private string Dismiss(ComponentPlayer p,ushort id)
        {
            Friend row;if(hidden.TryGetValue(id,out row)&&row.Owner==p.PlayerGuid){hidden.Remove(id);Send(new DeathAuthorityPackage{Kind=4,Target=id});return "契约已解除，次位面生物已消失";}
            foreach(var f in saved.Values)if(f.Id==id&&f.Owner==p.PlayerGuid&&!f.Discard){int count=0;foreach(var other in saved.Values)if(other.Discard)count++;if(count>=DeathAuthorityRules.DiscardLimit)return "待加载的解除记录已满，请先靠近原生物区域";f.Discard=true;Send(new DeathAuthorityPackage{Kind=4,Target=id});return "契约已解除，生物加载后将直接消失";}
            DeathGuard g=null;foreach(var candidate in guards.Values)if(candidate.Key==id&&candidate.Owner==p.PlayerGuid){g=candidate;break;}if(g==null)return "未找到自己的契约生物";
            if(Occupied(g.Creature))return "请先让乘客下马，再解除契约";
            terrain.Project.RemoveEntity(g.Creature.Entity,true);guards.Remove(g.Creature.Entity.EntityId);Send(new DeathAuthorityPackage{Kind=4,Target=id});return "契约已解除，生物已消失";
        }
        private void Recall(ComponentPlayer p)
        {foreach(var g in guards.Values)if(g.Owner==p.PlayerGuid&&!Occupied(g.Creature)){g.Target=null;g.NextThink=0;Vector3 at;if(Vector3.DistanceSquared(g.Creature.ComponentBody.Position,p.ComponentBody.Position)>48*48&&FindFree(p.ComponentBody.Position,g.Creature.ComponentBody.BoxSize,out at))VoidBlinkBody(g.Creature.ComponentBody,at);}}
        private static void VoidBlinkBody(ComponentBody body,Vector3 at){body.Position=at;body.Velocity=Vector3.Zero;body.netPosition=new NetPosition(at);body.netVelocity=new NetVelocity(Vector3.Zero);body.SendPosition=at;body.SendVelocity=Vector3.Zero;}
        private ComponentPlayer OwnerPlayer(Guid id){foreach(var p in players.ComponentPlayers)if(p.PlayerGuid==id&&Actor(p,false))return p;return null;}
        private void UpdateGuards()
        {
            retired.Clear();int hitBudget=24,scanBudget=128;
            foreach(var pair in guards)
            {
                var g=pair.Value;var c=g.Creature;if(!c.IsAddedToProject||c.ComponentHealth.Health<=0){retired.Add(pair.Key);continue;}g.Suppress();
                if(g.Ridden||Occupied(c)){g.Target=null;continue;}
                var owner=OwnerPlayer(g.Owner);if(owner==null){g.Target=g.BiteTarget=null;if(g.Path.Destination.HasValue)g.Path.Stop();continue;}
                if(g.Target!=null&&!Hostile(g.Target,owner))g.Target=null;
                if(g.Path.IsStuck){if(g.StuckSince<=0)g.StuckSince=Now;}else g.StuckSince=0;
                if(Now>=g.NextThink&&scanBudget>0)
                {
                    g.NextThink=Now+.5;float best=-1;ComponentCreature chosen=null;
                    candidates.Clear();bodies.FindBodiesInArea(owner.ComponentBody.Position.XZ-new Vector2(24),owner.ComponentBody.Position.XZ+new Vector2(24),candidates);int checkedBodies=0;
                    foreach(var body in candidates){if(++checkedBodies>128||scanBudget--<=0)break;var candidate=body.Entity.FindComponent<ComponentCreature>(false);if(!Hostile(candidate,owner))continue;float distance=Vector3.DistanceSquared(owner.ComponentBody.Position,body.Position);if(distance>24*24)continue;Threat threat;float score=1000-distance;if(threats.TryGetValue(owner.PlayerGuid,out threat)&&threat.Creature==candidate&&Now<threat.Until)score+=10000;if(score>best){best=score;chosen=candidate;}}
                    g.Target=chosen;Vector3 destination=chosen!=null?chosen.ComponentBody.Position:owner.ComponentBody.Position;
                    if(Vector3.DistanceSquared(c.ComponentBody.Position,owner.ComponentBody.Position)>48*48||g.StuckSince>0&&Now-g.StuckSince>4){g.Target=null;g.StuckSince=0;Vector3 at;if(FindFree(owner.ComponentBody.Position,c.ComponentBody.BoxSize,out at))VoidBlinkBody(c.ComponentBody,at);}
                    else{float stop=chosen==null?2.5f:g.Cerberus?2.6f:1.2f+(c.ComponentBody.BoxSize.X+chosen.ComponentBody.BoxSize.X)*.5f;if(Vector3.DistanceSquared(c.ComponentBody.Position,destination)>stop*stop)g.Path.SetDestination(destination,.85f,stop,128,true,false,true,owner.ComponentBody);else g.Path.Stop();}
                }
                if(g.Cerberus){UpdateCerberusAttack(g,owner,ref hitBudget);continue;}
                if(g.Target==null)continue;float range=1.4f+(c.ComponentBody.BoxSize.X+g.Target.ComponentBody.BoxSize.X)*.5f;bool close=Vector3.DistanceSquared(c.ComponentBody.Position,g.Target.ComponentBody.Position)<range*range;
                c.ComponentCreatureModel.AttackOrder=close;c.ComponentCreatureModel.LookAtOrder=g.Target.ComponentCreatureModel.EyePosition;
                if(close&&Now>=g.NextAttack&&hitBudget-->0&&VoidCombatSafety.ClearPath(terrain,c.ComponentCreatureModel.EyePosition,g.Target.ComponentBody.BoundingBox.Center(),6))
                {
                    g.NextAttack=Now+1;var miner=c.Entity.FindComponent<ComponentMiner>(false);float power=g.Hound?DeathSummonStats.HoundDamage:Math.Max(6,miner.AttackPower)*(g.Special?2:1);Vector3 delta=g.Target.ComponentBody.Position-c.ComponentBody.Position;
                    ComponentMiner.AttackBody(g.Target.ComponentBody,c,g.Target.ComponentBody.BoundingBox.Center(),delta.LengthSquared()>.001f?Vector3.Normalize(delta):Vector3.UnitZ,power,true);
                }
            }
            foreach(var id in retired){var key=guards[id].Key;guards.Remove(id);Send(new DeathAuthorityPackage{Kind=4,Target=key});}
        }
        internal void EntityAdded(Entity entity)
        {
            if(!Authority)return;Friend f;if(!saved.TryGetValue(entity.EntityId,out f))return;var c=entity.FindComponent<ComponentCreature>(false);
            if(c==null||c.Entity.ValuesDictionary.DatabaseObject.Name!=f.Template||Math.Abs((c.ComponentSpawn?.SpawnTime??0)-f.SpawnTime)>.001)return;
            if(f.Discard){if(discardedEntities.Count<DeathAuthorityRules.DiscardLimit)discardedEntities.Add(entity);return;}
            saved.Remove(entity.EntityId);if(!AdoptContract(c,f.Owner,f.Special,f.Id)){saved[entity.EntityId]=f;return;}
            if(f.Cerberus)guards[entity.EntityId].NextAttack=Now+f.BiteCooldown;
            if(f.HealOnReturn)Heal(c);
        }
        internal void Load(ValuesDictionary data)
        {
            if(data==null)return;
            try
            {
                int version=data.GetValue<int>("Version",0);if(version!=1&&version!=2){loadFailure=data;return;}
                worldId=data.GetValue<Guid>("World",worldId);if(worldId==Guid.Empty)throw new InvalidOperationException();
                int count=data.GetValue<int>("Count",0);if(count<0||count>DeathAuthorityRules.WorldLimit+DeathAuthorityRules.DiscardLimit)throw new InvalidOperationException();
                var keys=new HashSet<ushort>();int discarded=0;
                for(int i=0;i<count;i++)
                {
                    var row=data.GetValue<ValuesDictionary>("Ally"+i);int entity=row.GetValue<int>("Id"),key=row.GetValue<int>("Key",entity);
                    var f=new Friend{Id=(ushort)key,EntityId=(ushort)entity,Owner=row.GetValue<Guid>("Owner"),Template=row.GetValue<string>("Template"),SpawnTime=row.GetValue<double>("Spawn"),Special=row.GetValue<bool>("Special"),Hidden=row.GetValue<bool>("Hidden",false),Discard=row.GetValue<bool>("Discard",false),HealOnReturn=row.GetValue<bool>("HealOnReturn",false),Snapshot=row.GetValue<ValuesDictionary>("Snapshot",null)};
                    f.Pending=!f.Hidden;f.Warhorse=f.Special&&f.Template==DeathAuthorityRules.MountTemplate;f.Cerberus=f.Template==CerberusRules.Template;f.BiteCooldown=row.GetValue<float>("BiteCooldown",0);f.Name=row.GetValue<string>("Name",f.Cerberus?CerberusRules.Name:f.Special?(f.Warhorse?"夜魇战马":"冥界猎犬"):"契约 · "+f.Template);
                    if(!WorldCutRules.Finite(f.BiteCooldown)||f.BiteCooldown<0||f.BiteCooldown>4||f.Cerberus&&!f.Discard&&HasCerberus(f.Owner))throw new InvalidOperationException();
                    // Older v2 rows can contain up to four seconds of the old bite cooldown.
                    f.BiteCooldown=Math.Min((float)CerberusRules.Cooldown,f.BiteCooldown);ReadSavedHealth(f,row);
                    if(key<1||key>ushort.MaxValue||entity<0||entity>ushort.MaxValue||!keys.Add(f.Id)||f.Owner==Guid.Empty||f.Template.Length>96||f.Name.Length>96||!DeathAuthorityRules.Finite(f.SpawnTime)||f.Hidden&&(f.Snapshot==null||f.Discard)||!f.Hidden&&entity==0)throw new InvalidOperationException();
                    if(f.Discard){if(++discarded>DeathAuthorityRules.DiscardLimit)throw new InvalidOperationException();}else if(Count(f.Owner)>=DeathAuthorityRules.OwnerLimit||ArmyCount>=DeathAuthorityRules.WorldLimit)throw new InvalidOperationException();
                    if(f.Hidden)hidden.Add(f.Id,f);else saved.Add(f.EntityId,f);
                }
            }
            catch(Exception){saved.Clear();hidden.Clear();loadFailure=data;Log.Warning("[SCIEPort:Death] Contract data invalid; preserved without overwriting.");}
        }
        internal ValuesDictionary Save()
        {
            if(loadFailure!=null)return loadFailure;var data=new ValuesDictionary();data.SetValue("Version",2);data.SetValue("World",worldId);int n=0;
            foreach(var g in guards.Values)if(g.Creature.IsAddedToProject&&g.Creature.ComponentHealth.Health>0)SaveRow(data,Row(g),n++);
            foreach(var row in saved.Values)SaveRow(data,row,n++);foreach(var row in hidden.Values)SaveRow(data,row,n++);data.SetValue("Count",n);return data;
        }
        private static void SaveRow(ValuesDictionary data,Friend f,int index){var row=new ValuesDictionary();row.SetValue("Id",(int)f.EntityId);row.SetValue("Key",(int)f.Id);row.SetValue("Owner",f.Owner);row.SetValue("Template",f.Template);row.SetValue("Spawn",f.SpawnTime);row.SetValue("Special",f.Special);row.SetValue("Name",f.Name??"契约生物");row.SetValue("Hidden",f.Hidden);row.SetValue("Discard",f.Discard);row.SetValue("HealOnReturn",f.HealOnReturn);if(f.HealthKnown){row.SetValue("HealthFraction",f.HealthFraction);row.SetValue("HealthMaximum",f.HealthMaximum);}if(f.Cerberus)row.SetValue("BiteCooldown",f.BiteCooldown);if(f.Snapshot!=null)row.SetValue("Snapshot",f.Snapshot);data.SetValue("Ally"+index,row);}
        internal void Tint(Game.ComponentModel model)
        {
            var c=model?.Entity.FindComponent<ComponentCreature>(false);if(IsFriendly(c)&&!(model is ComponentCerberusModel))model.DiffuseColor=new Vector3(.35f,.23f,.48f);
        }
    }
}
