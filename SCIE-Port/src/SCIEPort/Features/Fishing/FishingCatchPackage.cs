#pragma warning disable CS1591
using Engine;
using Game.NetWork;

namespace SCIEPort.Features.Fishing
{
    /// <summary>Native reliable transport for fishing inputs and authoritative world presentation.</summary>
    public sealed class FishingCatchPackage : IPackage
    {
        // Kinds 0–2 belong to the retired cast protocols.
        internal const byte Hook = 3, Hold = 4, Release = 5, Cancel = 6;
        internal const byte Waiting = 7, Approach = 8, Bite = 9, Reeling = 10;
        internal const byte Landed = 11, Escaped = 12, Closed = 13, InvalidWater = 14, LostBait = 15;
        internal const byte Charge = 16, Cast = 17, AbortCharge = 18;
        internal const byte Windup = 19, FlightOrigin = 20, Flight = 21;
        internal const byte TenseBit = 128;

        internal static bool IsTerminal(byte kind) => kind >= Landed && kind <= LostBait;

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
            SubsystemFishing runtime = project?.FindSubsystem<SubsystemFishing>(false);
            if (runtime == null) return;
            if (isServer)
            {
                var player = From?.SubsystemPlayers != null ? From.PlayerData?.ComponentPlayer : null;
                if (((Kind >= Hook && Kind <= Cancel) || (Kind >= Charge && Kind <= AbortCharge))
                    && player != null && player.Entity.EntityId == Caster)
                    runtime.AcceptInput(player, this, From);
            }
            else if (From != null && ReferenceEquals(From, node.Server) && ((Kind >= Waiting && Kind <= LostBait) || (Kind >= Windup && Kind <= Flight)))
                runtime.ReceiveState(this);
        }
    }
}
