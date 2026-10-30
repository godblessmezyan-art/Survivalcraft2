using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>249: owner-authenticated requests, server-owned domain state. No client damage fields.</summary>
    public sealed class VoidHorizonPackage : IPackage
    {
        /// <inheritdoc/>
        public byte ID => 249;
        /// <inheritdoc/>
        public Client To { get; set; }
        /// <inheritdoc/>
        public Client Except { get; set; }
        /// <inheritdoc/>
        public Client From { get; set; }
        /// <inheritdoc/>
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal byte Kind,Reason;
        internal ushort Caster;
        internal uint Sequence,Revision;
        internal Vector3 Origin,Direction;
        internal float Elapsed,Cooldown;
        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter w)
        {
            w.Write(Kind);w.Write(Reason);w.Write(Caster);w.Write(Sequence);w.Write(Revision);
            w.Write(Origin);w.Write(Direction);w.Write(Elapsed);w.Write(Cooldown);
        }
        /// <inheritdoc/>
        public void ReadData(PackageStreamReader r)
        {
            Kind=r.ReadByte();Reason=r.ReadByte();Caster=r.ReadUInt16();Sequence=r.ReadUInt32();Revision=r.ReadUInt32();
            Origin=r.ReadVector3();Direction=r.ReadVector3();Elapsed=r.ReadSingle();Cooldown=r.ReadSingle();
        }
        internal VoidHorizonPackage Snapshot() => new VoidHorizonPackage { Kind=Kind,Reason=Reason,Caster=Caster,Sequence=Sequence,Revision=Revision,Origin=Origin,Direction=Direction,Elapsed=Elapsed,Cooldown=Cooldown };
        /// <inheritdoc/>
        public void Handle(ProjectNet project,NetNode node,bool isServer)
        {
            var runtime=project.FindSubsystem<SubsystemAbyssReaper>(false)?.Horizon;
            if(runtime==null)return;
            var player=From?.PlayerData?.ComponentPlayer;
            if(VoidHorizonRules.Dispatch(isServer,From!=null && From==node.Server,player!=null && player.Entity.EntityId==Caster,Kind))
                runtime.Enqueue(isServer?player:null,isServer?From:null,Snapshot());
        }
    }
}
