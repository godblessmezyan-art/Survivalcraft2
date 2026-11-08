using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Home
{
    /// <summary>238: authenticated seat intentions and server-only temporary seat state.</summary>
    public sealed class HomeSeatingPackage : IPackage
    {
        /// <inheritdoc/>
        public byte ID => 238;
        /// <inheritdoc/>
        public Client To { get; set; }
        /// <inheritdoc/>
        public Client Except { get; set; }
        /// <inheritdoc/>
        public Client From { get; set; }
        /// <inheritdoc/>
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        // 0 sit, 1 stand, 2 authoritative state, 3 snapshot request, 4 lease, 5 refusal.
        internal byte Kind, Seat;
        internal ushort Actor;
        internal uint Revision;
        internal Point3 Root;
        internal int Expected;
        internal bool Active, MoveOnExit;
        internal Vector3 Exit;
        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter w)
        {
            w.Write(Kind); w.Write(Actor); w.Write(Revision); w.Write(Root.X); w.Write(Root.Y); w.Write(Root.Z);
            w.Write(Expected); w.Write(Seat); w.Write(Active); w.Write(MoveOnExit); w.Write(Exit.X); w.Write(Exit.Y); w.Write(Exit.Z);
        }
        /// <inheritdoc/>
        public void ReadData(PackageStreamReader r)
        {
            Kind = r.ReadByte(); Actor = r.ReadUInt16(); Revision = r.ReadUInt32(); Root = new Point3(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            Expected = r.ReadInt32(); Seat = r.ReadByte(); Active = r.ReadBoolean(); MoveOnExit = r.ReadBoolean(); Exit = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }
        /// <inheritdoc/>
        public void Handle(ProjectNet project, NetNode node, bool server)
        {
            if (node == null || Kind > 5 || !HomeSeatRules.Finite(Exit)) return;
            var runtime = project.FindSubsystem<SubsystemHomeFurniture>(false); if (runtime == null) return;
            if (server)
            {
                var p = From?.PlayerData?.ComponentPlayer;
                if (Kind != 2 && Kind != 5 && From != null && From.State >= ClientState.ProjectLoaded && p != null && p.Entity.EntityId == Actor)
                    runtime.QueueSeat(p, this, From);
            }
            else if ((Kind == 2 || Kind == 5) && From != null && ReferenceEquals(From, node.Server)) runtime.QueueSeat(null, this, null);
        }
    }
}
