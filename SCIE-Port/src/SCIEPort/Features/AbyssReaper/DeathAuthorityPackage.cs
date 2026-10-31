using System;
using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>252: authority forms, empowered melee and friendly creature ownership. No client damage field.</summary>
    public sealed class DeathAuthorityPackage:IPackage
    {
        /// <inheritdoc/>
        public byte ID=>252;
        /// <inheritdoc/>
        public Client To{get;set;}
        /// <inheritdoc/>
        public Client From{get;set;}
        /// <inheritdoc/>
        public Client Except{get;set;}
        /// <inheritdoc/>
        public ClientState MinNeedState=>ClientState.ProjectLoaded;
        internal byte Kind,Action,Flags;internal ushort Caster,Target;internal uint Sequence,Revision;
        internal Guid Owner,World;internal Vector3 Position,Direction;internal float A,B,C,D;internal double Stamp,Echo;internal string Name="";
        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter w){w.Write(Kind);w.Write(Action);w.Write(Flags);w.Write(Caster);w.Write(Target);w.Write(Sequence);w.Write(Revision);w.Write(Owner.ToByteArray());w.Write(World.ToByteArray());w.Write(Position);w.Write(Direction);w.Write(A);w.Write(B);w.Write(C);w.Write(D);w.Write(Stamp);w.Write(Echo);w.Write(Name??"");}
        /// <inheritdoc/>
        public void ReadData(PackageStreamReader r){Kind=r.ReadByte();Action=r.ReadByte();Flags=r.ReadByte();Caster=r.ReadUInt16();Target=r.ReadUInt16();Sequence=r.ReadUInt32();Revision=r.ReadUInt32();Owner=new Guid(r.ReadBytes(16));World=new Guid(r.ReadBytes(16));Position=r.ReadVector3();Direction=r.ReadVector3();A=r.ReadSingle();B=r.ReadSingle();C=r.ReadSingle();D=r.ReadSingle();Stamp=r.ReadDouble();Echo=r.ReadDouble();Name=r.ReadString();}
        internal DeathAuthorityPackage Copy()=>(DeathAuthorityPackage)MemberwiseClone();
        /// <inheritdoc/>
        public void Handle(ProjectNet project,NetNode node,bool server)
        {
            var death=project?.FindSubsystem<SubsystemAbyssReaper>(false)?.Death;if(death==null||node==null)return;
            if(server){var p=From?.PlayerData?.ComponentPlayer;if(Kind==0&&From!=null&&From.State>=ClientState.ProjectLoaded&&p!=null&&p.Entity.EntityId==Caster)death.Enqueue(p,From,this);}
            else if(From!=null&&ReferenceEquals(From,node.Server))death.Enqueue(null,From,this);
        }
    }
}
