using System;
using System.Collections.Generic;
using System.Reflection;
using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    // x25.01.17 queues location changes separately; RemoveUpdateLocation alone does not cancel a queued add.
    // This adapter is used only on the main update thread, checks both maps and fails closed if the audited ABI changes.
    internal sealed class VoidChunkLease : IDisposable
    {
        private static readonly FieldInfo PendingField=typeof(TerrainUpdater).GetField("m_pendingLocations",BindingFlags.Instance|BindingFlags.NonPublic);
        private const float Distance=32f;
        private readonly TerrainUpdater updater;
        private readonly Dictionary<int,TerrainUpdater.UpdateLocation?> pending;
        private readonly int id;
        private readonly Vector2 center;
        private bool released;
        private VoidChunkLease(TerrainUpdater updater,Dictionary<int,TerrainUpdater.UpdateLocation?> pending,int id,Vector2 center)
        {this.updater=updater;this.pending=pending;this.id=id;this.center=center;}
        internal static bool TryCreate(TerrainUpdater updater,Vector2 center,out VoidChunkLease lease)
            => Create(updater,center,false,out lease);
        // Only the authenticated portal preflight calls this. Do not advertise a second native
        // player update location to the server while that player is still at the source.
        internal static bool TryCreateClient(TerrainUpdater updater,Vector2 center,out VoidChunkLease lease)
            => CommonLib.WorkType==WorkType.Client?Create(updater,center,true,out lease):Fail(out lease);
        private static bool Fail(out VoidChunkLease lease){lease=null;return false;}
        private static bool Create(TerrainUpdater updater,Vector2 center,bool client,out VoidChunkLease lease)
        {
            lease=null;
            if(updater==null || (CommonLib.WorkType==WorkType.Client&&!client) || PendingField==null || !WorldCutRules.Finite(center.X) || !WorldCutRules.Finite(center.Y))return false;
            Dictionary<int,TerrainUpdater.UpdateLocation?> pending;
            Dictionary<int,TerrainUpdater.UpdateLocation> active;
            try { pending=PendingField.GetValue(updater) as Dictionary<int,TerrainUpdater.UpdateLocation?>;active=updater.UpdateLocations; }
            catch(Exception error) { Log.Warning("[SCIEPort:Abyss] Chunk lease unavailable: "+error.GetType().Name);return false; }
            if(pending==null || active==null)return false;
            // Randomized negative IDs avoid native nonnegative player indices and native -1; collision is checked in both maps.
            int seed=Guid.NewGuid().GetHashCode() & 0x3fffffff;
            for(int i=0;i<128;i++)
            {
                int id=int.MinValue+((seed+i)&0x3fffffff);
                if(active.ContainsKey(id)||pending.ContainsKey(id))continue;
                var created=new VoidChunkLease(updater,pending,id,center);
                try
                {
                    if(client)pending[id]=new TerrainUpdater.UpdateLocation { Center=center,LastChunksUpdateCenter=center,VisibilityDistance=Distance,ContentDistance=Distance };
                    else updater.SetUpdateLocation(id,center,Distance,Distance);
                    TerrainUpdater.UpdateLocation? value;
                    if(!pending.TryGetValue(id,out value)||!value.HasValue||!created.Owns(value.Value)){created.Dispose();return false;}
                    lease=created;return true;
                }
                catch { created.Dispose();throw; }
            }
            return false;
        }
        private bool Owns(TerrainUpdater.UpdateLocation value)
            => value.Center==center && value.VisibilityDistance==Distance && value.ContentDistance==Distance;
        public void Dispose()
        {
            if(released)return;released=true;
            TerrainUpdater.UpdateLocation? waiting;
            bool hasPending=pending.TryGetValue(id,out waiting);
            // A changed pending entry belongs to another writer; never remove or overwrite it.
            if(hasPending && (!waiting.HasValue || !Owns(waiting.Value)))return;
            TerrainUpdater.UpdateLocation live;
            if(updater.UpdateLocations.TryGetValue(id,out live) && !Owns(live))return;
            // Always queue removal: it causes the next updater pass to refresh chunk allocation,
            // including when the original add has already been applied.
            pending[id]=null;
            if(updater.UpdateLocations.ContainsKey(id))updater.RemoveUpdateLocation(id);
        }
    }
}
