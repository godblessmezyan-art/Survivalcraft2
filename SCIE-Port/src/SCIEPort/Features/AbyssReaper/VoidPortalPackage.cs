using System;
using System.IO;
using System.Text;
using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.AbyssReaper
{
    /// <summary>250: shared portal records, authenticated commands and teleport handshakes.</summary>
    public sealed class VoidPortalPackage:IPackage
    {
        /// <inheritdoc/>
        public byte ID => 250;
        /// <inheritdoc/>
        public Client To{get;set;}
        /// <inheritdoc/>
        public Client Except{get;set;}
        /// <inheritdoc/>
        public Client From{get;set;}
        /// <inheritdoc/>
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal byte Kind,Command,Count,Limit;
        internal ushort Caster;
        internal uint Sequence,Revision;
        internal Guid Portal,Target,Owner;
        internal Vector3 Point,Direction;
        internal float Facing;
        internal string Text="";
        private static readonly Encoding Utf8=new UTF8Encoding(false,true);
        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter w)
        {
            byte[] text=Utf8.GetBytes(Text??"");if(text.Length>384)throw new InvalidDataException("Portal text too long");
            w.Write(Kind);w.Write(Command);w.Write(Count);w.Write(Limit);w.Write(Caster);w.Write(Sequence);w.Write(Revision);
            w.Write(Portal.ToByteArray());w.Write(Target.ToByteArray());w.Write(Owner.ToByteArray());
            w.Write(Point);w.Write(Direction);w.Write(Facing);w.Write((ushort)text.Length);w.Write(text);
        }
        private static Guid ReadId(PackageStreamReader r){byte[] b=r.ReadBytes(16);if(b.Length!=16)throw new EndOfStreamException();return new Guid(b);}
        /// <inheritdoc/>
        public void ReadData(PackageStreamReader r)
        {
            Kind=r.ReadByte();Command=r.ReadByte();Count=r.ReadByte();Limit=r.ReadByte();Caster=r.ReadUInt16();Sequence=r.ReadUInt32();Revision=r.ReadUInt32();
            Portal=ReadId(r);Target=ReadId(r);Owner=ReadId(r);Point=r.ReadVector3();Direction=r.ReadVector3();Facing=r.ReadSingle();
            int length=r.ReadUInt16();if(length>384)throw new InvalidDataException("Portal text too long");
            byte[] b=r.ReadBytes(length);if(b.Length!=length)throw new EndOfStreamException();Text=Utf8.GetString(b);
        }
        internal VoidPortalPackage Copy() => (VoidPortalPackage)MemberwiseClone();
        /// <inheritdoc/>
        public void Handle(ProjectNet project,NetNode node,bool isServer)
        {
            if(project==null||node==null)return;
            var runtime=project.FindSubsystem<SubsystemAbyssReaper>(false)?.Gateways;if(runtime==null)return;
            if(isServer)
            {
                if(From==null||From.State<ClientState.ProjectLoaded||From.SubsystemPlayers==null||Kind!=0)return;
                var actor=From.PlayerData?.ComponentPlayer;
                if(actor==null||actor.Entity.EntityId!=Caster)return;
                runtime.Enqueue(actor,From,Copy());
            }
            else if(ReferenceEquals(From,node.Server)&&From!=null&&Kind>=1&&Kind<=7)runtime.Enqueue(null,null,Copy());
        }
    }
}
