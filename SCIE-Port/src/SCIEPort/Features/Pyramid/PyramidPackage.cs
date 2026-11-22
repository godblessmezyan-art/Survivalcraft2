using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Pyramid
{
    /// <summary>Requests one native interaction with a generated pyramid seal.</summary>
    public sealed class PyramidPackage : IPackage
    {
        /// <inheritdoc/>
        public byte ID => 234;
        /// <inheritdoc/>
        public Client To { get; set; }
        /// <inheritdoc/>
        public Client Except { get; set; }
        /// <inheritdoc/>
        public Client From { get; set; }
        /// <inheritdoc/>
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal Point3 Point;

        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter writer) => writer.Write(Point);

        /// <inheritdoc/>
        public void ReadData(PackageStreamReader reader) => Point = reader.ReadPoint3();

        /// <inheritdoc/>
        public void Handle(ProjectNet project, NetNode node, bool server)
        {
            if (!server || project == null || node == null || From?.SubsystemPlayers == null ||
                From.State < ClientState.ProjectLoaded || Point.Y <= 0 || Point.Y >= 255 ||
                !PyramidSite.InBounds(Point.X, Point.Z))
                return;
            ComponentPlayer player = From.PlayerData?.ComponentPlayer;
            if (player != null) project.FindSubsystem<SubsystemPyramid>(false)?.Enqueue(player, Point);
        }
    }
}
