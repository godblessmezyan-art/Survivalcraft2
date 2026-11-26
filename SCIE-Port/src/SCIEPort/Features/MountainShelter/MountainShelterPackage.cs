using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.MountainShelter
{
    /// <summary>Requests one host-validated placement or interaction.</summary>
    public sealed class MountainShelterPackage : IPackage
    {
        /// <inheritdoc/>
        public byte ID => 233;
        /// <inheritdoc/>
        public Client To { get; set; }
        /// <inheritdoc/>
        public Client Except { get; set; }
        /// <inheritdoc/>
        public Client From { get; set; }
        /// <inheritdoc/>
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal Point3 Point;
        internal int Expected;
        internal bool Place;
        // The expected terrain signature excludes light. Use those bits to carry a hit face without resizing the packet.
        internal int Signature => Terrain.ReplaceLight(Expected, 0);
        internal int Face => Terrain.ExtractLight(Expected) - 1;

        /// <inheritdoc/>
        public void WriteData(PackageStreamWriter writer)
        {
            writer.Write(Point);
            writer.Write(Expected);
            writer.Write(Place);
        }

        /// <inheritdoc/>
        public void ReadData(PackageStreamReader reader)
        {
            Point = reader.ReadPoint3();
            Expected = reader.ReadInt32();
            Place = reader.ReadBoolean();
        }

        /// <inheritdoc/>
        public void Handle(ProjectNet project, NetNode node, bool isServer)
        {
            if (!isServer || From?.SubsystemPlayers == null || From.State < ClientState.ProjectLoaded)
                return;
            var player = From.PlayerData?.ComponentPlayer;
            if (player != null && ReferenceEquals(player.PlayerData.Client, From))
                project.FindSubsystem<SubsystemMountainShelter>(false)?.Enqueue(player, this, From);
        }
    }
}
