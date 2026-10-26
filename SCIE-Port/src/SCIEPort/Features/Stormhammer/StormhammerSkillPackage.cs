using Engine;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Stormhammer
{
    internal enum StormhammerSkill : byte
    {
        ThunderLance = 1,
        ThunderDomain = 2,
        MyriadTribulation = 3
    }

    internal enum StormhammerSkillEvent : byte
    {
        Request = 0,
        Accepted = 1,
        LanceFlight = 2,
        LanceImpact = 3,
        DomainStart = 4,
        UltimateStart = 5,
        UltimateWarning = 6,
        UltimateStrike = 7,
        UltimateEnd = 8
    }

    /// <summary>Authenticated skill request and authoritative presentation event.</summary>
    public sealed class StormhammerSkillPackage : IPackage
    {
        /// <summary>First free SCIE-Port package id after seat/tank control.</summary>
        public byte ID => 242;
        /// <inheritdoc />
        public Client To { get; set; }
        /// <inheritdoc />
        public Client Except { get; set; }
        /// <inheritdoc />
        public Client From { get; set; }
        /// <inheritdoc />
        public ClientState MinNeedState => ClientState.ProjectLoaded;

        /// <summary>Authoritative caster entity id.</summary>
        public ushort CasterId { get; set; }
        /// <summary>Per-caster replay protection sequence.</summary>
        public ushort Sequence { get; set; }
        internal StormhammerSkill Skill { get; set; }
        internal StormhammerSkillEvent Event { get; set; }
        /// <summary>Origin, center, or impact point according to event type.</summary>
        public Vector3 Position { get; set; }
        /// <summary>Aim direction or secondary point according to event type.</summary>
        public Vector3 Direction { get; set; }
        /// <summary>Charge, duration, radius, cooldown, or damage according to event type.</summary>
        public float Value { get; set; }
        /// <summary>Chain or staged-strike index.</summary>
        public byte Step { get; set; }

        /// <summary>Creates an empty decoder instance.</summary>
        public StormhammerSkillPackage()
        {
        }

        internal static StormhammerSkillPackage CreateRequest(ComponentPlayer player, ushort sequence, StormhammerSkill skill, Vector3 position, Vector3 direction, float charge)
        {
            return new StormhammerSkillPackage
            {
                CasterId = player.Entity.EntityId,
                Sequence = sequence,
                Skill = skill,
                Event = StormhammerSkillEvent.Request,
                Position = position,
                Direction = direction,
                Value = charge
            };
        }

        internal static StormhammerSkillPackage CreateEvent(ushort casterId, StormhammerSkill skill, StormhammerSkillEvent kind, Vector3 position, Vector3 direction, float value, byte step = 0)
        {
            return new StormhammerSkillPackage
            {
                CasterId = casterId,
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
            writer.Write(Sequence);
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
            Sequence = reader.ReadUInt16();
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
            if (behavior == null)
            {
                return;
            }

            if (isServer)
            {
                if (Event != StormhammerSkillEvent.Request || From == null)
                {
                    return;
                }

                ComponentPlayer sender = From.PlayerData?.ComponentPlayer;
                if (sender != null && sender.Entity.EntityId == CasterId)
                {
                    behavior.AcceptRequest(sender, Skill, Position, Direction, Value, Sequence);
                }
                return;
            }

            if (Event != StormhammerSkillEvent.Request && From == netNode.Server)
            {
                behavior.ApplyNetworkEvent(this);
            }
        }
    }
}
