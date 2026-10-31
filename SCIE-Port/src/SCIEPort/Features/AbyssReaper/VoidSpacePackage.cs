using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>251: audited free slot; V2 effects and short-range authoritative movement. No damage field.</summary>
    public sealed class VoidSpacePackage:IPackage
    {
        /// <inheritdoc/>
        public byte ID => 251;
        /// <inheritdoc/>
        public Client To{get;set;}
        /// <inheritdoc/>
        public Client Except{get;set;}
        /// <inheritdoc/>
        public Client From{get;set;}
        /// <inheritdoc/>
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal byte Kind,Skill,Reason;internal ushort Caster;internal uint Sequence,Seed;
        internal Vector3 Origin,Point,Direction;internal double Started,Stamp,Echo;internal float Cooldown;
        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter w)
        {
            w.Write(Kind);w.Write(Skill);w.Write(Reason);w.Write(Caster);w.Write(Sequence);w.Write(Seed);
            w.Write(Origin);w.Write(Point);w.Write(Direction);w.Write(Started);w.Write(Stamp);w.Write(Echo);w.Write(Cooldown);
        }
        /// <inheritdoc/>
        public void ReadData(PackageStreamReader r)
        {
            Kind=r.ReadByte();Skill=r.ReadByte();Reason=r.ReadByte();Caster=r.ReadUInt16();Sequence=r.ReadUInt32();Seed=r.ReadUInt32();
            Origin=r.ReadVector3();Point=r.ReadVector3();Direction=r.ReadVector3();Started=r.ReadDouble();Stamp=r.ReadDouble();Echo=r.ReadDouble();Cooldown=r.ReadSingle();
        }
        internal VoidSpacePackage Copy() => (VoidSpacePackage)MemberwiseClone();
        /// <inheritdoc/>
        public void Handle(ProjectNet project,NetNode node,bool isServer)
        {
            if(project==null||node==null)return;var runtime=project.FindSubsystem<SubsystemAbyssReaper>(false)?.Space;if(runtime==null)return;
            if(isServer)
            {
                if(From==null||From.State<ClientState.ProjectLoaded||From.SubsystemPlayers==null)return;
                var actor=From.PlayerData?.ComponentPlayer;if(actor==null||actor.Entity.EntityId!=Caster)return;
                if(Kind==VoidSpaceRules.Request||Kind==VoidSpaceRules.Ready||Kind==VoidSpaceRules.Ack||Kind==VoidSpaceRules.Cancel||Kind==VoidSpaceRules.Clock)runtime.Enqueue(actor,From,Copy());
            }
            else if(From!=null&&ReferenceEquals(From,node.Server))runtime.Enqueue(null,From,Copy());
        }
    }
}
