using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.Excalibur
{
    /// <summary>253: authenticated requests and complete authoritative cast/cooldown snapshots.</summary>
    public sealed class ExcaliburPackage:IPackage
    {
        /// <inheritdoc/>
        public byte ID=>253;
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
        internal uint Sequence,CastId;
        internal Vector3 Origin,Direction,Cooldowns;
        internal float Age,Blessing;
        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter w)
        {w.Write(Kind);w.Write(Skill);w.Write(Caster);w.Write(Sequence);w.Write(CastId);w.Write(Origin);w.Write(Direction);w.Write(Cooldowns);w.Write(Age);w.Write(Blessing);}
        /// <inheritdoc/>
        public void ReadData(PackageStreamReader r)
        {Kind=r.ReadByte();Skill=r.ReadByte();Caster=r.ReadUInt16();Sequence=r.ReadUInt32();CastId=r.ReadUInt32();Origin=r.ReadVector3();Direction=r.ReadVector3();Cooldowns=r.ReadVector3();Age=r.ReadSingle();Blessing=r.ReadSingle();}
        /// <inheritdoc/>
        public void Handle(ProjectNet project,NetNode node,bool isServer)
        {
            var runtime=project.FindSubsystem<SubsystemExcalibur>(false);if(runtime==null)return;
            if(isServer){var p=From?.PlayerData?.ComponentPlayer;if(Kind==0&&p!=null&&p.Entity.EntityId==Caster)runtime.Enqueue(p,this);}
            else if(From==node.Server&&Kind==1)runtime.Enqueue(null,this);
        }
    }
}
