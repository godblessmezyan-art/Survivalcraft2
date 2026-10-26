using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Stormhammer
{
    internal enum StormhammerSkill : byte
    {
        ThunderousJudgment = 1,
        MyriadTribulation = 2
    }

    internal enum StormhammerSkillEvent : byte
    {
        Request = 0,
        Accepted = 1,
        JudgmentCharge = 2,
        JudgmentBurst = 3,
        UltimateStart = 4,
        UltimateLock = 5,
        UltimateStrike = 6,
        UltimateMainWarning = 7,
        UltimateMainStrike = 8,
        UltimateAftershock = 9,
        UltimateEnd = 10,
        AvatarState = 11
    }

    /// <summary>Authenticated V3.2 skill request and authoritative presentation event.</summary>
    public sealed class StormhammerSkillPackage : IPackage
    {
        /// <inheritdoc />
        public byte ID => 243;
        /// <inheritdoc />
        public Client To { get; set; }
        /// <inheritdoc />
        public Client Except { get; set; }
        /// <inheritdoc />
        public Client From { get; set; }
        /// <inheritdoc />
        public ClientState MinNeedState => ClientState.ProjectLoaded;

        /// <summary>Authoritative caster entity identifier.</summary>
        public ushort CasterId { get; set; }
        /// <summary>Optional authoritative target entity identifier.</summary>
        public ushort TargetId { get; set; }
        /// <summary>Per-caster replay-protection sequence.</summary>
        public ushort Sequence { get; set; }
        /// <summary>Unique authoritative cast identifier.</summary>
        public uint CastId { get; set; }
        /// <summary>Authoritative game-time timestamp for phase recovery.</summary>
        public double ServerTime { get; set; }
        internal StormhammerSkill Skill { get; set; }
        internal StormhammerSkillEvent Event { get; set; }
        /// <summary>World-space event position.</summary>
        public Vector3 Position { get; set; }
        /// <summary>World-space direction or secondary vector.</summary>
        public Vector3 Direction { get; set; }
        /// <summary>Event-specific scalar value.</summary>
        public float Value { get; set; }
        /// <summary>Bounded phase or target index.</summary>
        public byte Step { get; set; }

        /// <summary>Creates an empty decoder instance.</summary>
        public StormhammerSkillPackage()
        {
        }

        internal static StormhammerSkillPackage CreateRequest(ComponentPlayer player, ushort sequence, StormhammerSkill skill, Vector3 position, Vector3 direction)
        {
            return new StormhammerSkillPackage
            {
                CasterId = player.Entity.EntityId,
                Sequence = sequence,
                Skill = skill,
                Event = StormhammerSkillEvent.Request,
                Position = position,
                Direction = direction
            };
        }

        internal static StormhammerSkillPackage CreateEvent(ushort casterId, uint castId, double serverTime, StormhammerSkill skill, StormhammerSkillEvent kind, Vector3 position, Vector3 direction, float value = 0f, byte step = 0, ushort targetId = 0)
        {
            return new StormhammerSkillPackage
            {
                CasterId = casterId,
                TargetId = targetId,
                CastId = castId,
                ServerTime = serverTime,
                Skill = skill,
                Event = kind,
                Position = position,
                Direction = direction,
                Value = value,
                Step = step
            };
        }

        /// <inheritdoc />
        public void WriteData(PackageStreamWriter writer)
        {
            writer.Write(CasterId);
            writer.Write(TargetId);
            writer.Write(Sequence);
            writer.Write(CastId);
            writer.Write(ServerTime);
            writer.Write((byte)Skill);
            writer.Write((byte)Event);
            writer.Write(Position);
            writer.Write(Direction);
            writer.Write(Value);
            writer.Write(Step);
        }

        /// <inheritdoc />
        public void ReadData(PackageStreamReader reader)
        {
            CasterId = reader.ReadUInt16();
            TargetId = reader.ReadUInt16();
            Sequence = reader.ReadUInt16();
            CastId = reader.ReadUInt32();
            ServerTime = reader.ReadDouble();
            Skill = (StormhammerSkill)reader.ReadByte();
            Event = (StormhammerSkillEvent)reader.ReadByte();
            Position = reader.ReadVector3();
            Direction = reader.ReadVector3();
            Value = reader.ReadSingle();
            Step = reader.ReadByte();
        }

        /// <inheritdoc />
        public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
        {
            SubsystemAwakenedStormhammerBlockBehavior behavior = projectNet.FindSubsystem<SubsystemAwakenedStormhammerBlockBehavior>(false);
            if (behavior == null) return;
            if (isServer)
            {
                if (Event != StormhammerSkillEvent.Request || From == null) return;
                ComponentPlayer sender = From.PlayerData?.ComponentPlayer;
                if (sender != null && sender.Entity.EntityId == CasterId)
                {
                    behavior.AcceptRequest(sender, Skill, Position, Direction, Sequence);
                }
            }
            else if (Event != StormhammerSkillEvent.Request && From == netNode.Server)
            {
                behavior.ApplyNetworkEvent(this);
            }
        }
    }
}
