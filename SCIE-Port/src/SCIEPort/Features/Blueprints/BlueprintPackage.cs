using System;
using System.IO;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Blueprints
{
    /// <summary>239: authenticated blueprint commands, bounded chunks and private replies.</summary>
    public sealed class BlueprintPackage : IPackage
    {
        /// <inheritdoc/>
        public byte ID { get { return 239; } }
        /// <inheritdoc/>
        public Client To { get; set; }
        /// <inheritdoc/>
        public Client Except { get; set; }
        /// <inheritdoc/>
        public Client From { get; set; }
        /// <inheritdoc/>
        public ClientState MinNeedState { get { return ClientState.ProjectLoaded; } }
        internal const byte Begin = 0, Chunk = 1, Finish = 2, Confirm = 3, Cancel = 4, Undo = 5;
        internal const byte Ack = 10, Ready = 11, Status = 12;
        internal byte Kind;
        internal Guid Owner, Request;
        internal ushort Caster;
        internal uint Sequence;
        internal int X, Y, Z, Total, Offset;
        internal bool Clear;
        internal byte[] Payload = new byte[0];
        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter w)
        {
            if (Payload == null || Payload.Length > BlueprintTransfer.ChunkBytes) throw new InvalidDataException("Blueprint packet too large");
            w.Write(Kind); w.Write(Owner.ToByteArray()); w.Write(Request.ToByteArray()); w.Write(Caster); w.Write(Sequence);
            w.Write(X); w.Write(Y); w.Write(Z); w.Write(Total); w.Write(Offset); w.Write(Clear);
            w.Write((ushort)Payload.Length); w.Write(Payload);
        }
        private static Guid ReadGuid(PackageStreamReader r) { byte[] b = r.ReadBytes(16); if (b.Length != 16) throw new EndOfStreamException(); return new Guid(b); }
        /// <inheritdoc/>
        public void ReadData(PackageStreamReader r)
        {
            Kind = r.ReadByte(); Owner = ReadGuid(r); Request = ReadGuid(r); Caster = r.ReadUInt16(); Sequence = r.ReadUInt32();
            X = r.ReadInt32(); Y = r.ReadInt32(); Z = r.ReadInt32(); Total = r.ReadInt32(); Offset = r.ReadInt32(); Clear = r.ReadBoolean();
            int length = r.ReadUInt16(); if (length > BlueprintTransfer.ChunkBytes) throw new InvalidDataException("Blueprint packet too large");
            Payload = r.ReadBytes(length); if (Payload.Length != length) throw new EndOfStreamException();
        }
        /// <inheritdoc/>
        public void Handle(ProjectNet project, NetNode node, bool server)
        {
            if (project == null || node == null) return;
            var runtime = project.FindSubsystem<SubsystemBlueprintArtifact>(false); if (runtime == null) return;
            if (server)
            {
                var actor = From == null ? null : From.PlayerData == null ? null : From.PlayerData.ComponentPlayer;
                if (From != null && From.State >= ClientState.ProjectLoaded && actor != null &&
                    actor.Entity.EntityId == Caster && actor.PlayerGuid == Owner && Kind <= Undo)
                    runtime.Enqueue(actor, From, this);
            }
            else if (From != null && ReferenceEquals(From, node.Server) && Kind >= Ack && Kind <= Status)
                runtime.Enqueue(null, From, this);
        }
    }
}
