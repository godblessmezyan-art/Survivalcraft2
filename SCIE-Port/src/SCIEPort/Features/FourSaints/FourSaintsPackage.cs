using Game;
using Game.NetWork;
#pragma warning disable 1591

namespace SCIEPort.Features.FourSaints
{
    /// <summary>Authenticated Baihu activation request and authoritative timed-state snapshot.</summary>
    public sealed class FourSaintsPackage : IPackage
    {
        public byte ID => 245;
        public Client To { get; set; }
        public Client Except { get; set; }
        public Client From { get; set; }
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal byte Kind, Equipment;
        internal ushort PlayerId, Sequence;
        internal uint Revision;
        internal float AwakenRemaining, CooldownRemaining, RebirthRemaining;

        public void WriteData(PackageStreamWriter writer)
        {
            writer.Write(Kind); writer.Write(Equipment); writer.Write(PlayerId); writer.Write(Sequence); writer.Write(Revision);
            writer.Write(AwakenRemaining); writer.Write(CooldownRemaining); writer.Write(RebirthRemaining);
        }

        public void ReadData(PackageStreamReader reader)
        {
            Kind = reader.ReadByte(); Equipment = reader.ReadByte(); PlayerId = reader.ReadUInt16(); Sequence = reader.ReadUInt16(); Revision = reader.ReadUInt32();
            AwakenRemaining = reader.ReadSingle(); CooldownRemaining = reader.ReadSingle(); RebirthRemaining = reader.ReadSingle();
        }

        public void Handle(ProjectNet project, NetNode node, bool isServer)
        {
            SubsystemFourSaints runtime = project.FindSubsystem<SubsystemFourSaints>(false);
            if (runtime == null) return;
            if (isServer)
            {
                ComponentPlayer sender = From?.PlayerData?.ComponentPlayer;
                if (Kind == 0 && sender != null && sender.Entity.EntityId == PlayerId) runtime.AcceptAwaken(sender, Sequence);
            }
            else if (Kind == 1 && From == node.Server) runtime.Receive(this);
        }
    }
}
#pragma warning restore 1591
