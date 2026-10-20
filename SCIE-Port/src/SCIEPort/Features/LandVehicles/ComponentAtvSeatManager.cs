using System.Collections.Generic;
using Engine;
using Game;
using Game.NetWork;
using Game.NetWork.Packages;
using GameEntitySystem;
using TemplatesDatabase;

namespace SCIEPort.Features.LandVehicles
{
    /// <summary>
    /// Authoritative two-seat allocator. Both riders remain direct children of the
    /// ATV body, while a discrete server package assigns the non-controlling role.
    /// </summary>
    public sealed class ComponentAtvSeatManager : Component, IUpdateable
    {
        private readonly HashSet<ushort> m_rejectedRiders = new HashSet<ushort>();
        private ComponentBody m_body;
        private ComponentMount m_mount;
        private ushort m_driverId;
        private ushort m_passengerId;

        /// <inheritdoc />
        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        /// <summary>Current authoritative driver.</summary>
        public ComponentRider Driver { get; private set; }

        /// <summary>Current authoritative non-controlling passenger.</summary>
        public ComponentRider Passenger { get; private set; }

        /// <inheritdoc />
        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_body = Entity.FindComponent<ComponentBody>(throwOnError: true);
            m_mount = Entity.FindComponent<ComponentMount>(throwOnError: true);
        }

        /// <inheritdoc />
        public void Update(float dt)
        {
            List<ComponentRider> riders = GetRiders();
            RefreshKnownOccupants(riders);

            if (!IsAuthority())
            {
                return;
            }

            List<ComponentRider> unassigned = new List<ComponentRider>();
            foreach (ComponentRider rider in riders)
            {
                if (rider != Driver && rider != Passenger)
                {
                    unassigned.Add(rider);
                }
            }

            // On world reload there are no transient IDs. The saved native parent
            // offsets distinguish the two roles without persisting player identity.
            if (Driver == null && Passenger == null && riders.Count > 0)
            {
                ComponentRider first = riders[0];
                bool savedPassengerOnly = riders.Count == 1 && !first.m_isAnimating &&
                    DistanceTo(first, AtvSeatRules.PassengerZ) < DistanceTo(first, AtvSeatRules.DriverZ);
                if (savedPassengerOnly)
                {
                    Passenger = first;
                }
                else
                {
                    Driver = ClosestTo(riders, AtvSeatRules.DriverZ);
                    Passenger = FirstExcept(riders, Driver);
                }

                unassigned.Remove(Driver);
                unassigned.Remove(Passenger);
                Assign(Driver, AtvSeatRules.Seat.Driver);
                Assign(Passenger, AtvSeatRules.Seat.Passenger);
            }
            else
            {
                if (Driver == null && unassigned.Count > 0)
                {
                    Driver = unassigned[0];
                    unassigned.RemoveAt(0);
                    Assign(Driver, AtvSeatRules.Seat.Driver);
                }

                if (Passenger == null && unassigned.Count > 0)
                {
                    Passenger = unassigned[0];
                    unassigned.RemoveAt(0);
                    Assign(Passenger, AtvSeatRules.Seat.Passenger);
                }
            }

            foreach (ComponentRider rider in unassigned)
            {
                Reject(rider);
            }

            m_driverId = Driver?.Entity.EntityId ?? 0;
            m_passengerId = Passenger?.Entity.EntityId ?? 0;
            ClearDetachedRejections(riders);
        }

        /// <summary>Applies one server-issued role on a client replica.</summary>
        public void ApplyNetworkAssignment(ComponentRider rider, AtvSeatRules.Seat seat)
        {
            if (rider == null)
            {
                return;
            }

            if (rider.ComponentCreature.ComponentBody.ParentBody != m_body)
            {
                rider.StartNetMounting(m_mount);
            }

            ApplySeatOffset(rider, seat);
            if (seat == AtvSeatRules.Seat.Driver)
            {
                Driver = rider;
                m_driverId = rider.Entity.EntityId;
            }
            else
            {
                Passenger = rider;
                m_passengerId = rider.Entity.EntityId;
            }
        }

        private static bool IsAuthority()
        {
            return CommonLib.WorkType == WorkType.Local || CommonLib.WorkType == WorkType.Server;
        }

        private List<ComponentRider> GetRiders()
        {
            List<ComponentRider> result = new List<ComponentRider>();
            foreach (ComponentBody child in m_body.ChildBodies)
            {
                ComponentRider rider = child.Entity.FindComponent<ComponentRider>(throwOnError: false);
                if (rider != null)
                {
                    result.Add(rider);
                }
            }

            return result;
        }

        private void RefreshKnownOccupants(List<ComponentRider> riders)
        {
            Driver = FindById(riders, m_driverId);
            Passenger = FindById(riders, m_passengerId);
            if (Driver == null)
            {
                m_driverId = 0;
            }

            if (Passenger == null)
            {
                m_passengerId = 0;
            }
        }

        private void Assign(ComponentRider rider, AtvSeatRules.Seat seat)
        {
            if (rider == null)
            {
                return;
            }

            ApplySeatOffset(rider, seat);
            if (CommonLib.WorkType == WorkType.Server)
            {
                CommonLib.Net.QueuePackage(new AtvSeatAssignmentPackage(Entity, rider, seat));
            }
        }

        private static void ApplySeatOffset(ComponentRider rider, AtvSeatRules.Seat seat)
        {
            Vector3 seatOffset = seat == AtvSeatRules.Seat.Driver
                ? new Vector3(AtvSeatRules.DriverX, AtvSeatRules.DriverY, AtvSeatRules.DriverZ)
                : new Vector3(AtvSeatRules.PassengerX, AtvSeatRules.PassengerY, AtvSeatRules.PassengerZ);
            rider.m_targetPositionOffset = seatOffset + rider.m_riderOffset;
            rider.m_targetRotationOffset = Quaternion.Identity;
        }

        private static ComponentRider FindById(List<ComponentRider> riders, ushort id)
        {
            if (id == 0)
            {
                return null;
            }

            foreach (ComponentRider rider in riders)
            {
                if (rider.Entity.EntityId == id)
                {
                    return rider;
                }
            }

            return null;
        }

        private static ComponentRider ClosestTo(List<ComponentRider> riders, float localZ)
        {
            ComponentRider result = riders[0];
            float best = float.MaxValue;
            foreach (ComponentRider rider in riders)
            {
                float distance = DistanceTo(rider, localZ);
                if (distance < best)
                {
                    best = distance;
                    result = rider;
                }
            }

            return result;
        }

        private static float DistanceTo(ComponentRider rider, float localZ)
        {
            return MathUtils.Abs(rider.ComponentCreature.ComponentBody.ParentBodyPositionOffset.Z - localZ);
        }

        private static ComponentRider FirstExcept(List<ComponentRider> riders, ComponentRider excluded)
        {
            foreach (ComponentRider rider in riders)
            {
                if (rider != excluded)
                {
                    return rider;
                }
            }

            return null;
        }

        private void Reject(ComponentRider rider)
        {
            ushort id = rider.Entity.EntityId;
            if (!m_rejectedRiders.Add(id))
            {
                return;
            }

            rider.StartNetDismounting();
            CommonLib.Net.QueuePackage(new ComponentMountPackage(rider));
        }

        private void ClearDetachedRejections(List<ComponentRider> riders)
        {
            m_rejectedRiders.RemoveWhere(id => FindById(riders, id) == null);
        }
    }
}
