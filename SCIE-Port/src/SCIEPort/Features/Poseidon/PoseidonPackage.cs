using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Poseidon
{
    /// <summary>246: authenticated requests, authoritative cast events and cooldown snapshots.</summary>
    public sealed class PoseidonPackage : IPackage
    {
        /// <inheritdoc />
        public byte ID => 246;
        /// <inheritdoc />
        public Client To { get; set; }
        /// <inheritdoc />
        public Client Except { get; set; }
        /// <inheritdoc />
        public Client From { get; set; }
        /// <inheritdoc />
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal byte Kind, Skill;
        internal ushort Caster;
        internal uint Sequence;
        internal Vector3 Origin, Direction;
        internal float DashRemaining, WaveRemaining;
        internal bool Water;
        /// <inheritdoc />
        public void WriteData(PackageStreamWriter w)
        {
            w.Write(Kind); w.Write(Skill); w.Write(Caster); w.Write(Sequence); w.Write(Origin); w.Write(Direction);
            w.Write(DashRemaining); w.Write(WaveRemaining); w.Write(Water);
        }
        /// <inheritdoc />
        public void ReadData(PackageStreamReader r)
        {
            Kind = r.ReadByte(); Skill = r.ReadByte(); Caster = r.ReadUInt16(); Sequence = r.ReadUInt32(); Origin = r.ReadVector3(); Direction = r.ReadVector3();
            DashRemaining = r.ReadSingle(); WaveRemaining = r.ReadSingle(); Water = r.ReadBoolean();
        }
        /// <inheritdoc />
        public void Handle(ProjectNet project, NetNode node, bool isServer)
        {
            // Reserved V1 wire format. V2 uses authenticated package 248; mixed versions cannot cast.
        }
    }
}
