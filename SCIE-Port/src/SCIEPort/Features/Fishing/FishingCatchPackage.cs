#pragma warning disable CS1591
using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Fishing
{
    /// <summary>Client reel result request and host-authoritative catch confirmation.</summary>
    public sealed class FishingCatchPackage : IPackage
    {
        internal const byte Request = 0;
        internal const byte Result = 1;

        public byte ID => 236;
        public Client To { get; set; }
        public Client Except { get; set; }
        public Client From { get; set; }
        public ClientState MinNeedState => ClientState.ProjectLoaded;

        internal byte Kind;
        internal ushort Caster;
        internal Point3 Cell;
        internal uint Sequence;
        internal byte Count;

        public void WriteData(PackageStreamWriter writer)
        {
            writer.Write(Kind);
            writer.Write(Caster);
            writer.Write(Cell.X);
            writer.Write(Cell.Y);
            writer.Write(Cell.Z);
            writer.Write(Sequence);
            writer.Write(Count);
        }

        public void ReadData(PackageStreamReader reader)
        {
            Kind = reader.ReadByte();
            Caster = reader.ReadUInt16();
            Cell = new Point3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            Sequence = reader.ReadUInt32();
            Count = reader.ReadByte();
        }

        public void Handle(ProjectNet project, NetNode node, bool isServer)
        {
            SubsystemFishing runtime = project.FindSubsystem<SubsystemFishing>(false);
            if (runtime == null) return;
            if (isServer)
            {
                ComponentPlayer player = From?.PlayerData?.ComponentPlayer;
                if (Kind == Request && player != null && player.Entity.EntityId == Caster)
                {
                    runtime.AcceptCatch(player, this, From);
                }
            }
            else if (From == node.Server && Kind == Result)
            {
                runtime.ReceiveCatch(this);
            }
        }
    }
}
