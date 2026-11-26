#pragma warning disable CS1591
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Flashlight
{
    public sealed class FlashlightPackage : IPackage
    {
        public byte ID => 231;
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        public Client From { get; set; }
        public Client To { get; set; }
        public Client Except { get; set; }
        internal byte Slot;
        internal bool TurnOn;

        public void WriteData(PackageStreamWriter writer)
        {
            writer.Write(Slot);
            writer.Write(TurnOn);
        }

        public void ReadData(PackageStreamReader reader)
        {
            Slot = reader.ReadByte();
            TurnOn = reader.ReadBoolean();
        }

        public void Handle(ProjectNet project, NetNode node, bool isServer)
        {
            if (!isServer || Slot >= 10 || From?.SubsystemPlayers == null || From.State < ClientState.ProjectLoaded)
                return;
            ComponentPlayer player = From.PlayerData?.ComponentPlayer;
            if (player != null && ReferenceEquals(player.PlayerData.Client, From))
                project.FindSubsystem<SubsystemFlashlight>(false)?.Enqueue(player, this);
        }
    }
}
