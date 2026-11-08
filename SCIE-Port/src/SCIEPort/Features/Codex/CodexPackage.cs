using System;
using Game;using Game.NetWork;
namespace SCIEPort.Features.Codex
{
    /// <summary>254: authenticated unsealing requests and server-only permission snapshots.</summary>
    public sealed class CodexPackage:IPackage
    {
        /// <inheritdoc/>
        public byte ID=>254;
        /// <inheritdoc/>
        public Client To{get;set;}
        /// <inheritdoc/>
        public Client Except{get;set;}
        /// <inheritdoc/>
        public Client From{get;set;}
        /// <inheritdoc/>
        public ClientState MinNeedState=>ClientState.ProjectLoaded;
        internal byte Kind,Entry;internal ushort Caster;internal Guid Owner;internal uint Sequence,Mask;
        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter w){w.Write(Kind);w.Write(Entry);w.Write(Caster);w.Write(Owner.ToByteArray());w.Write(Sequence);w.Write(Mask);}
        /// <inheritdoc/>
        public void ReadData(PackageStreamReader r){Kind=r.ReadByte();Entry=r.ReadByte();Caster=r.ReadUInt16();Owner=new Guid(r.ReadBytes(16));Sequence=r.ReadUInt32();Mask=r.ReadUInt32();}
        /// <inheritdoc/>
        public void Handle(ProjectNet project,NetNode node,bool server)
        {
            var runtime=project.FindSubsystem<SubsystemArtifactCodex>(false);if(runtime==null||node==null)return;
            if(server){var p=From?.PlayerData?.ComponentPlayer;if(From!=null&&From.State>=ClientState.ProjectLoaded&&p!=null&&p.PlayerGuid==Owner&&p.Entity.EntityId==Caster&&(Kind==0||Kind==2))runtime.Enqueue(p,this);}
            else if(SCIEPort.Compatibility.ServerConnection.IsServer(node,From)&&Kind==1)runtime.Enqueue(null,this);
        }
    }
}
