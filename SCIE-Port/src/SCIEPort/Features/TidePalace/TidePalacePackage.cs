using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.TidePalace
{
    /// <summary>Client interaction with one tide wheel or counterweight.</summary>
    public sealed class TidePalacePackage : IPackage
    {
        /// <inheritdoc/>
        public byte ID => 235;
        /// <inheritdoc/>
        public Client To { get; set; }
        /// <inheritdoc/>
        public Client Except { get; set; }
        /// <inheritdoc/>
        public Client From { get; set; }
        /// <inheritdoc/>
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal Point3 Point;
        internal byte ExpectedData;

        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter writer)
        {
            writer.Write(Point.X);
            writer.Write(Point.Y);
            writer.Write(Point.Z);
            writer.Write(ExpectedData);
        }

        /// <inheritdoc/>
        public void ReadData(PackageStreamReader reader)
        {
            Point = new Point3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            ExpectedData = reader.ReadByte();
        }

        /// <inheritdoc/>
        public void Handle(ProjectNet project, NetNode node, bool server)
        {
            if (!server || project == null || node == null || From?.SubsystemPlayers == null || From.State < ClientState.ProjectLoaded ||
                Point.Y <= 0 || Point.Y >= 255 || !PalaceSite.InBounds(Point.X, Point.Z) || ExpectedData > TidePalaceBlock.Weight)
                return;
            ComponentPlayer player = From.PlayerData?.ComponentPlayer;
            if (player != null)
                project.FindSubsystem<SubsystemTidePalace>(false)?.Enqueue(player, Point, ExpectedData);
        }
    }
}
