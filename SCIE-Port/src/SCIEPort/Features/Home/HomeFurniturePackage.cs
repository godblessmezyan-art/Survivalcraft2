using Engine;
using Game;
using Game.NetWork;
namespace SCIEPort.Features.Home
{
    /// <summary>239: bounded host-validated furniture interactions. State uses native terrain synchronization.</summary>
    public sealed class HomeFurniturePackage : IPackage
    {
        /// <inheritdoc/>
        public byte ID => 239;
        /// <inheritdoc/>
        public Client To { get; set; }
        /// <inheritdoc/>
        public Client Except { get; set; }
        /// <inheritdoc/>
        public Client From { get; set; }
        /// <inheritdoc/>
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal Point3 Point; internal int Expected; internal byte Action;
        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter writer) { writer.Write(Point.X); writer.Write(Point.Y); writer.Write(Point.Z); writer.Write(Expected); writer.Write(Action); }
        /// <inheritdoc/>
        public void ReadData(PackageStreamReader reader) { Point = new Point3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32()); Expected = reader.ReadInt32(); Action = reader.ReadByte(); }
        /// <inheritdoc/>
        public void Handle(ProjectNet project, NetNode node, bool server)
        {
            if (!server || node == null || From == null || From.State < ClientState.ProjectLoaded || Point.Y < 1 || Point.Y > 254 || Action > 1) return;
            ComponentPlayer player = From.PlayerData?.ComponentPlayer;
            if (player != null) project.FindSubsystem<SubsystemHomeFurniture>(false)?.Enqueue(player, Point, Expected, Action, From);
        }
    }
}
