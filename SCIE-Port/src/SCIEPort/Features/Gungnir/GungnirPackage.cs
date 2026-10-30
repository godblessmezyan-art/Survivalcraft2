using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>Authenticated commands and complete authoritative weapon snapshots.</summary>
    public sealed class GungnirPackage : IPackage
    {
        /// <inheritdoc />
        public byte ID => 244;
        /// <inheritdoc />
        public Client To { get; set; }
        /// <inheritdoc />
        public Client Except { get; set; }
        /// <inheritdoc />
        public Client From { get; set; }
        /// <inheritdoc />
        public ClientState MinNeedState => ClientState.ProjectLoaded;
        internal byte Kind, Command, Phase;
        internal ushort Caster;
        internal uint Sequence;
        internal Vector3 Position, Direction, Target;
        internal float Blessing, BlessingCooldown, JudgmentCooldown, JudgmentAge;
        /// <summary>Appended tail field (0.56.0): Fate Severance cooldown. Existing field order is untouched.</summary>
        internal float FatalismCooldown;
        internal bool Equipped;
        /// <inheritdoc />
        public void WriteData(PackageStreamWriter w)
        {
            w.Write(Kind); w.Write(Command); w.Write(Phase); w.Write(Caster); w.Write(Sequence);
            w.Write(Position); w.Write(Direction); w.Write(Target);
            w.Write(Blessing); w.Write(BlessingCooldown); w.Write(JudgmentCooldown); w.Write(JudgmentAge); w.Write(Equipped);
            w.Write(FatalismCooldown);
        }
        /// <inheritdoc />
        public void ReadData(PackageStreamReader r)
        {
            Kind = r.ReadByte(); Command = r.ReadByte(); Phase = r.ReadByte(); Caster = r.ReadUInt16(); Sequence = r.ReadUInt32();
            Position = r.ReadVector3(); Direction = r.ReadVector3(); Target = r.ReadVector3();
            Blessing = r.ReadSingle(); BlessingCooldown = r.ReadSingle(); JudgmentCooldown = r.ReadSingle(); JudgmentAge = r.ReadSingle(); Equipped = r.ReadBoolean();
            FatalismCooldown = r.ReadSingle();
        }
        /// <inheritdoc />
        public void Handle(ProjectNet project, NetNode node, bool isServer)
        {
            var runtime = project.FindSubsystem<SubsystemGungnir>(false);
            if (runtime == null) return;
            if (isServer)
            {
                var player = From?.PlayerData?.ComponentPlayer;
                if (Kind == 0 && player != null && player.Entity.EntityId == Caster) runtime.Accept(player, this);
            }
            else if (From == node.Server && Kind != 0) runtime.Receive(this);
        }
    }
}
