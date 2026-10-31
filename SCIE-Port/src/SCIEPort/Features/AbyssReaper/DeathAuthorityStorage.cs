using System;
using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using GameEntitySystem;
using TemplatesDatabase;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        // Stored creatures have no live entity, renderer, AI or physics in the world.
        private static bool Occupied(ComponentCreature c)=>c.ComponentBody.ParentBody!=null||c.ComponentBody.ChildBodies.Count>0||c.Entity.FindComponent<ComponentMount>(false)?.Rider!=null;
        private static void Heal(ComponentCreature c)
        {
            if(c==null||!c.IsAddedToProject||c.ComponentHealth.Health<=0)return;
            c.ComponentHealth.Health=1;CommonLib.Net?.QueuePackage(new ComponentHealthPackage(c.ComponentHealth));
        }
        private void HealArmy(Guid owner)
        {
            foreach(var g in guards.Values)if(g.Owner==owner)Heal(g.Creature);
            foreach(var f in hidden.Values)if(f.Owner==owner)f.HealOnReturn=true;
            foreach(var f in saved.Values)if(!f.Discard&&f.Owner==owner)f.HealOnReturn=true;
        }
        private string StowArmy(ComponentPlayer p)
        {
            int done=0,skipped=0;var active=new List<DeathGuard>();foreach(var g in guards.Values)if(g.Owner==p.PlayerGuid)active.Add(g);
            foreach(var g in active)
            {
                var c=g.Creature;if(!c.IsAddedToProject||c.ComponentHealth.Health<=0||Occupied(c)){skipped++;continue;}
                Friend row=null;bool stored=false;
                try
                {
                    var snapshot=terrain.Project.SaveEntities(new[]{c.Entity});if(snapshot.EntitiesData.Count!=1){skipped++;continue;}
                    row=Row(g);row.Snapshot=new ValuesDictionary();snapshot.Save(row.Snapshot);row.Hidden=true;row.EntityId=0;
                    terrain.Project.RemoveEntity(c.Entity,false);
                    if(c.IsAddedToProject){skipped++;continue;}
                    guards.Remove(c.Entity.EntityId);hidden.Add(row.Id,row);done++;stored=true;FriendPacket(row);c.Entity.Dispose();
                }
                catch(Exception e)
                {
                    // A third-party removal callback may fail after membership changed. Keep the snapshot.
                    if(!stored&&row?.Snapshot!=null&&!c.IsAddedToProject){guards.Remove(c.Entity.EntityId);hidden[row.Id]=row;done++;FriendPacket(row);}
                    else if(!stored)skipped++;
                    Log.Warning("[SCIEPort:Death] Stow callback failed: "+e.GetType().Name);
                }
            }
            return "已收回次位面："+done+"只"+(skipped>0?"；"+skipped+"只未收回（乘客或状态不允许）":"，契约与生命保留");
        }
        private string ManifestArmy(ComponentPlayer p)
        {
            int done=0,waiting=0;var rows=new List<Friend>();foreach(var row in hidden.Values)if(row.Owner==p.PlayerGuid)rows.Add(row);
            foreach(var row in rows)
            {
                Entity entity=null;bool adopted=false;
                try
                {
                    var data=new EntityDataList(terrain.Project.GameDatabase,row.Snapshot,false);
                    if(data.EntitiesData.Count!=1||data.EntitiesData[0].ValuesDictionary.DatabaseObject.Name!=row.Template)throw new InvalidOperationException("Invalid stored creature");
                    // x25 ProjectNet.LoadEntities skips component Load for Id=0. Load explicitly,
                    // preserve self references in the temporary map, then request a fresh world ID.
                    var entry=data.EntitiesData[0];entity=new Entity(terrain.Project,entry.ValuesDictionary);
                    entity.EntityId=(ushort)entry.Id;var map=new Dictionary<int,Entity>();if(entry.Id!=0)map.Add(entry.Id,entity);
                    entity.publicLoadEntity(entry.ValuesDictionary,new IdToEntityMap(map));entity.EntityId=0;
                    var c=entity.FindComponent<ComponentCreature>(true);Vector3 at;
                    if(!FindFree(p.ComponentBody.Position,c.ComponentBody.BoxSize,out at)){entity.Dispose();entity=null;waiting++;continue;}
                    c.ComponentBody.Position=at;c.ComponentBody.Velocity=Vector3.Zero;c.ComponentSpawn.SpawnDuration=0;
                    terrain.Project.AddEntity(entity);hidden.Remove(row.Id);
                    adopted=AdoptContract(c,row.Owner,row.Special,row.Id);
                    if(!adopted){hidden[row.Id]=row;terrain.Project.RemoveEntity(entity,true);entity=null;waiting++;continue;}
                    if(row.HealOnReturn)Heal(c);done++;
                }
                catch(Exception e)
                {
                    if(!adopted)
                    {
                        hidden[row.Id]=row;
                        if(entity!=null){DeathGuard g;if(guards.TryGetValue(entity.EntityId,out g)&&g.Key==row.Id)guards.Remove(entity.EntityId);if(entity.IsAddedToProject)terrain.Project.RemoveEntity(entity,true);else entity.Dispose();}
                        FriendPacket(row);
                    }
                    Log.Warning("[SCIEPort:Death] Manifest failed: "+e.GetType().Name);waiting++;
                }
            }
            return "已召回现世："+done+"只"+(waiting>0?"；"+waiting+"只继续留在次位面（空地不足或状态不允许）":"");
        }
        private void UpdateDiscarded()
        {
            int budget=8;for(int i=discardedEntities.Count-1;i>=0&&budget-->0;i--)
            {
                var entity=discardedEntities[i];Friend row;
                if(saved.TryGetValue(entity.EntityId,out row)&&row.Discard&&entity.IsAddedToProject)
                {
                    var creature=entity.FindComponent<ComponentCreature>(false);
                    if(creature!=null&&!Occupied(creature)){terrain.Project.RemoveEntity(entity,true);saved.Remove(row.EntityId);discardedEntities.RemoveAt(i);}
                }
                else discardedEntities.RemoveAt(i);
            }
        }
        private void UpdateClientMounts()
        {
            foreach(var pair in clientMountBonuses)pair.Key.WalkSpeed-=pair.Value;clientMountBonuses.Clear();
            foreach(var f in clientFriends.Values)if(f.Warhorse&&!f.Hidden&&!f.Pending)
                terrain.Project.FindEntityById(f.EntityId,entity=>
                {
                    if(!entity.IsAddedToProject)return;
                    var locomotion=entity.FindComponent<ComponentLocomotion>(false);if(locomotion!=null&&!clientMountBonuses.ContainsKey(locomotion)){float bonus=Math.Max(0,locomotion.WalkSpeed)*.7f;locomotion.WalkSpeed+=bonus;clientMountBonuses.Add(locomotion,bonus);}
                    var steed=entity.FindComponent<ComponentSteedBehavior>(false);var rider=entity.FindComponent<ComponentMount>(false)?.Rider?.ComponentCreature as ComponentPlayer;
                    if(steed!=null)steed.IsActive=rider?.PlayerData.IsMainPlayer==true;
                });
        }
        private void ClearClientMounts(){foreach(var pair in clientMountBonuses)pair.Key.WalkSpeed-=pair.Value;clientMountBonuses.Clear();}
    }
}
