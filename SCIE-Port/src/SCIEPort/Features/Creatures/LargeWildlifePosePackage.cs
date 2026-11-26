#pragma warning disable CS1591
using Game.NetWork;

namespace SCIEPort.Features.Creatures
{
    /// <summary>Distributes short turtle shell poses from the host.</summary>
    public sealed class LargeWildlifePosePackage : IPackage
    {
        public byte ID => 230;
        public Client To { get; set; }
        public Client Except { get; set; }
        public Client From { get; set; }
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal ushort EntityId;
        internal byte Pose;

        public void WriteData(PackageStreamWriter writer)
        {
            writer.Write(EntityId);
            writer.Write(Pose);
        }

        public void ReadData(PackageStreamReader reader)
        {
            EntityId = reader.ReadUInt16();
            Pose = reader.ReadByte();
        }

        public void Handle(ProjectNet project, NetNode node, bool isServer)
        {
            if (isServer || project == null || From == null || !ReferenceEquals(From, node?.Server) || Pose > 2) return;
            project.FindEntityById(EntityId, entity =>
                entity.FindComponent<ComponentLargeWildlifeModel>(false)?.SetTurtlePose(Pose));
        }
    }
}
