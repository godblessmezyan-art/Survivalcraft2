using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>247: authenticated cut requests and server-owned presentation/cooldown events.</summary>
    public sealed class WorldCutPackage : IPackage
    {
        /// <inheritdoc />
        public byte ID => 247;
        /// <inheritdoc />
        public Client To { get; set; }
        /// <inheritdoc />
        public Client Except { get; set; }
        /// <inheritdoc />
        public Client From { get; set; }
        /// <inheritdoc />
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal byte Kind,Reason;
        internal ushort Caster;
        internal uint Sequence;
        internal Vector3 Origin,Direction;
        internal float Distance,Cooldown;
        /// <inheritdoc />
        public void WriteData(PackageStreamWriter w)
        {
            w.Write(Kind);w.Write(Reason);w.Write(Caster);w.Write(Sequence);w.Write(Origin);w.Write(Direction);w.Write(Distance);w.Write(Cooldown);
        }
        /// <inheritdoc />
        public void ReadData(PackageStreamReader r)
        {
            Kind=r.ReadByte();Reason=r.ReadByte();Caster=r.ReadUInt16();Sequence=r.ReadUInt32();Origin=r.ReadVector3();Direction=r.ReadVector3();Distance=r.ReadSingle();Cooldown=r.ReadSingle();
        }
        internal WorldCutPackage Snapshot() => new WorldCutPackage { Kind=Kind,Reason=Reason,Caster=Caster,Sequence=Sequence,Origin=Origin,Direction=Direction,Distance=Distance,Cooldown=Cooldown };
        /// <inheritdoc />
        public void Handle(ProjectNet project,NetNode node,bool isServer)
        {
            var runtime=project.FindSubsystem<SubsystemAbyssReaper>(false)?.WorldCut;
            if(runtime==null)return;
            if(isServer)
            {
                var player=From?.PlayerData?.ComponentPlayer;
                if(WorldCutRules.Dispatch(true,false,player!=null && player.Entity.EntityId==Caster,Kind))runtime.Enqueue(player,From,Snapshot());
            }
            else if(WorldCutRules.Dispatch(false,From!=null && ReferenceEquals(From,node.Server),false,Kind))runtime.Enqueue(null,From,Snapshot());
        }
    }
}
