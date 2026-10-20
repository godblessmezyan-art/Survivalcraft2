using Game;
using Game.NetWork;
using GameEntitySystem;

namespace SCIEPort.Features.Vehicles
{
    /// <summary>Server-to-client discrete two-seat vehicle role assignment.</summary>
    public sealed class TwoSeatAssignmentPackage : IPackage
    {
        /// <summary>Reserved SCIE-Port package identifier.</summary>
        public byte ID => 240;

        /// <inheritdoc />
        public Client To { get; set; }

        /// <inheritdoc />
        public Client Except { get; set; }

        /// <inheritdoc />
        public Client From { get; set; }

        /// <inheritdoc />
        public ClientState MinNeedState => ClientState.ProjectLoaded;

        /// <summary>Vehicle entity ID.</summary>
        public ushort VehicleId { get; set; }

        /// <summary>Rider entity ID.</summary>
        public ushort RiderId { get; set; }

        /// <summary>Authoritative seat role.</summary>
        public VehicleSeatRole Seat { get; set; }

        /// <summary>Creates an empty package for the decoder.</summary>
        public TwoSeatAssignmentPackage()
        {
        }

        /// <summary>Creates an authoritative assignment event.</summary>
        public TwoSeatAssignmentPackage(Entity vehicle, ComponentRider rider, VehicleSeatRole seat)
        {
            VehicleId = vehicle.EntityId;
            RiderId = rider.Entity.EntityId;
            Seat = seat;
        }

        /// <inheritdoc />
        public void WriteData(PackageStreamWriter writer)
        {
            writer.Write(VehicleId);
            writer.Write(RiderId);
            writer.Write((byte)Seat);
        }

        /// <inheritdoc />
        public void ReadData(PackageStreamReader reader)
        {
            VehicleId = reader.ReadUInt16();
            RiderId = reader.ReadUInt16();
            Seat = (VehicleSeatRole)reader.ReadByte();
        }

        /// <inheritdoc />
        public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
        {
            if (isServer || (Seat != VehicleSeatRole.Driver && Seat != VehicleSeatRole.Passenger))
            {
                return;
            }

            projectNet.FindEntityById(VehicleId, vehicle =>
            {
                projectNet.FindEntityById(RiderId, riderEntity =>
                {
                    ComponentTwoSeatManager manager = vehicle.FindComponent<ComponentTwoSeatManager>(throwOnError: false);
                    ComponentRider rider = riderEntity.FindComponent<ComponentRider>(throwOnError: false);
                    manager?.ApplyNetworkAssignment(rider, Seat);
                });
            });
        }
    }
}
