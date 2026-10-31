using System;
using Engine;
using Game;
namespace SCIEPort.Features.AbyssReaper
{
    internal sealed partial class DeathAuthorityRuntime
    {
        private bool HasDragon(Guid owner)
        {
            foreach(var g in guards.Values)if(g.Owner==owner&&g.Dragon&&g.Creature.IsAddedToProject&&g.Creature.ComponentHealth.Health>0)return true;
            foreach(var f in hidden.Values)if(f.Owner==owner&&f.Template==BoneDragonRules.Template)return true;
            foreach(var f in saved.Values)if(!f.Discard&&f.Owner==owner&&f.Template==BoneDragonRules.Template)return true;
            return false;
        }
        private void ToggleDragonFlight(ComponentPlayer player,ushort key)
        {
            var g=RiddenGuard(player);var flight=g?.Creature.ComponentLocomotion as ComponentBoneDragonLocomotion;
            if(g==null||g.Key!=key||!g.Dragon||flight==null){StatePacket(player,"请先骑上自己的骨龙");return;}
            flight.SetFlight(!flight.FlightEnabled);ManualRide(player,g);FriendPacket(Row(g));
            StatePacket(player,flight.FlightEnabled?"骨龙起飞 · 松开升降键悬停":"骨龙降落 · 缓慢下降至地面");
        }
        internal void DragonVertical(ComponentPlayer player,float value)
        {
            var row=RiddenFriend(player);if(row?.Dragon!=true||!player.PlayerData.IsMainPlayer)return;
            (player.ComponentRider.Mount.Entity.FindComponent<ComponentBoneDragonLocomotion>(false))?.SetVertical(value);
        }
        private void UpdateDragonFlight()
        {
            if(!Authority)return;
            foreach(var g in guards.Values)if(g.Dragon)
            {
                var flight=g.Creature.ComponentLocomotion as ComponentBoneDragonLocomotion;if(flight==null)continue;
                flight.AutoDriving=g.AutoDriving;
                if(flight.FlightEnabled&&(!g.Ridden||OwnerPlayer(g.Owner)==null))
                {flight.SetFlight(false);FriendPacket(Row(g));}
            }
        }
    }
}
