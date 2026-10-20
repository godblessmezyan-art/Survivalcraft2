using Game;
using Game.NetWork;
using GameEntitySystem;

namespace SCIEPort.Features.LandVehicles
{
    /// <summary>Server-to-client discrete ATV seat-role assignment.</summary>
    public sealed class AtvSeatAssignmentPackage : IPackage
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
        public AtvSeatRules.Seat Seat { get; set; }

        /// <summary>Creates an empty package for the decoder.</summary>
        public AtvSeatAssignmentPackage()
        {
        }

        /// <summary>Creates an authoritative assignment event.</summary>
        public AtvSeatAssignmentPackage(Entity vehicle, ComponentRider rider, AtvSeatRules.Seat seat)
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
            Seat = (AtvSeatRules.Seat)reader.ReadByte();
        }

        /// <inheritdoc />
        public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
        {
            // Clients never assign seats. Ignore any forged client-to-server packet.
            if (isServer || (Seat != AtvSeatRules.Seat.Driver && Seat != AtvSeatRules.Seat.Passenger))
            {
                return;
            }

            projectNet.FindEntityById(VehicleId, vehicle =>
            {
                projectNet.FindEntityById(RiderId, riderEntity =>
                {
                    ComponentAtvSeatManager manager = vehicle.FindComponent<ComponentAtvSeatManager>(throwOnError: false);
                    ComponentRider rider = riderEntity.FindComponent<ComponentRider>(throwOnError: false);
                    manager?.ApplyNetworkAssignment(rider, Seat);
                });
            });
        }
    }
}
