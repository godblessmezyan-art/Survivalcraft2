using Engine;
using Game;
using GameEntitySystem;
using SCIEPort.Features.PortableFortress;
using TemplatesDatabase;

namespace SCIEPort.Features.PortableFortress
{
    /// <summary>Marks the anchor entity of one deployed fortress and owns its record.</summary>
    public sealed class ComponentPortableFortress : Component
    {
        private SubsystemPortableFortress m_subsystem;

        /// <summary>Authoritative instance record.</summary>
        public FortressRecord Record { get; private set; }

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            Record = new FortressRecord
            {
                Id = valuesDictionary.GetValue("FortressId", string.Empty),
                Facing = (FortressFacing)valuesDictionary.GetValue("Facing", 0),
                BaseY = valuesDictionary.GetValue("BaseY", 0),
                AnchorX = valuesDictionary.GetValue("AnchorX", 0),
                AnchorZ = valuesDictionary.GetValue("AnchorZ", 0),
                CoreX = valuesDictionary.GetValue("CoreX", 0),
                CoreY = valuesDictionary.GetValue("CoreY", 0),
                CoreZ = valuesDictionary.GetValue("CoreZ", 0),
                Revision = valuesDictionary.GetValue("Revision", 1),
                State = (FortressState)valuesDictionary.GetValue("State", 0)
            };
            m_subsystem = Project.FindSubsystem<SubsystemPortableFortress>(throwOnError: false);
            m_subsystem?.Attach(this);
        }

        /// <inheritdoc />
        public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
        {
            base.Save(valuesDictionary, entityToIdMap);
            if (Record == null)
            {
                return;
            }

            valuesDictionary.SetValue("FortressId", Record.Id ?? string.Empty);
            valuesDictionary.SetValue("Facing", (int)Record.Facing);
            valuesDictionary.SetValue("BaseY", Record.BaseY);
            valuesDictionary.SetValue("AnchorX", Record.AnchorX);
            valuesDictionary.SetValue("AnchorZ", Record.AnchorZ);
            valuesDictionary.SetValue("CoreX", Record.CoreX);
            valuesDictionary.SetValue("CoreY", Record.CoreY);
            valuesDictionary.SetValue("CoreZ", Record.CoreZ);
            valuesDictionary.SetValue("Revision", Record.Revision);
            valuesDictionary.SetValue("State", (int)Record.State);
        }

        /// <summary>Installs the record for an entity created at runtime.</summary>
        internal void Initialize(FortressRecord record)
        {
            Record = record;
        }

        /// <summary>Replaces the record state; used by the subsystem when a deployment settles.</summary>
        internal void SetState(FortressState state)
        {
            if (Record != null)
            {
                Record.State = state;
            }
        }
    }

    /// <summary>Marks one collision proxy of a fortress instance.</summary>
    public sealed class ComponentPortableFortressSegment : Component
    {
        /// <summary>Owning instance id.</summary>
        public string FortressId { get; private set; }

        /// <summary>Index into <see cref="PortableFortressGeometry.Segments"/>.</summary>
        public int SegmentIndex { get; private set; }

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            FortressId = valuesDictionary.GetValue("FortressId", string.Empty);
            SegmentIndex = valuesDictionary.GetValue("SegmentIndex", -1);
            Project.FindSubsystem<SubsystemPortableFortress>(throwOnError: false)?.AttachSegment(this);
        }

        /// <inheritdoc />
        public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
        {
            base.Save(valuesDictionary, entityToIdMap);
            valuesDictionary.SetValue("FortressId", FortressId ?? string.Empty);
            valuesDictionary.SetValue("SegmentIndex", SegmentIndex);
        }

        /// <summary>Sets the identity when the entity is created at runtime.</summary>
        internal void Initialize(string fortressId, int segmentIndex)
        {
            FortressId = fortressId;
            SegmentIndex = segmentIndex;
        }
    }
}
