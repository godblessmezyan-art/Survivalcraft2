using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.Poseidon
{
    /// <summary>248: V2 requests and complete shield/domain snapshots. Legacy 246 layout stays reserved.</summary>
    public sealed class PoseidonV2Package:IPackage
    {
        /// <inheritdoc/>
        public byte ID=>248;
        /// <inheritdoc/>
        public Client To{get;set;}
        /// <inheritdoc/>
        public Client Except{get;set;}
        /// <inheritdoc/>
        public Client From{get;set;}
        /// <inheritdoc/>
        public ClientState MinNeedState=>ClientState.ProjectLoaded;
        internal byte Kind,Skill;
        internal ushort Caster;
        internal uint Sequence;
        internal Vector3 Origin,Direction,Cooldowns,DomainOrigin,DomainDirection;
        internal float Shield,DomainRemaining;
        internal bool Water,DomainWater;
        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter w)
        {
            w.Write(Kind);w.Write(Skill);w.Write(Caster);w.Write(Sequence);w.Write(Origin);w.Write(Direction);
            w.Write(Cooldowns);w.Write(Shield);w.Write(DomainRemaining);w.Write(Water);
            w.Write(DomainOrigin);w.Write(DomainDirection);w.Write(DomainWater);
        }
        /// <inheritdoc/>
        public void ReadData(PackageStreamReader r)
        {
            Kind=r.ReadByte();Skill=r.ReadByte();Caster=r.ReadUInt16();Sequence=r.ReadUInt32();Origin=r.ReadVector3();Direction=r.ReadVector3();
            Cooldowns=r.ReadVector3();Shield=r.ReadSingle();DomainRemaining=r.ReadSingle();Water=r.ReadBoolean();
            DomainOrigin=r.ReadVector3();DomainDirection=r.ReadVector3();DomainWater=r.ReadBoolean();
        }
        /// <inheritdoc/>
        public void Handle(ProjectNet project,NetNode node,bool isServer)
        {
            var runtime=project.FindSubsystem<SubsystemPoseidon>(false);if(runtime==null)return;
            if(isServer)
            {
                var player=From?.PlayerData?.ComponentPlayer;
                if(Kind==0&&player!=null&&player.Entity.EntityId==Caster)runtime.Enqueue(player,this);
            }
            else if(From==node.Server&&(Kind==1||Kind==2))runtime.Enqueue(null,this);
        }
    }
}
