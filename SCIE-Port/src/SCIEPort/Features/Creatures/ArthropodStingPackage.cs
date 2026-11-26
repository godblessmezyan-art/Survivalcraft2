#pragma warning disable CS1591
using Game.NetWork;

namespace SCIEPort.Features.Creatures
{
    /// <summary>Replicates the warning and strike pose. Damage is resolved on the authority.</summary>
    public sealed class ArthropodStingPackage : IPackage
    {
        public byte ID => 232;
        public Client To { get; set; }
        public Client Except { get; set; }
        public Client From { get; set; }
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal ushort EntityId;

        public void WriteData(PackageStreamWriter writer) => writer.Write(EntityId);
        public void ReadData(PackageStreamReader reader) => EntityId = reader.ReadUInt16();

        public void Handle(ProjectNet project, NetNode node, bool isServer)
        {
            if (isServer || project == null || From == null || !ReferenceEquals(From, node?.Server)) return;
            project.FindEntityById(EntityId, entity => entity.FindComponent<ComponentWildArthropodModel>(false)?.BeginSting());
        }
    }
}
