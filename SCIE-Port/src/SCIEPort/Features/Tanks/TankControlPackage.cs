using Game;
using Game.NetWork;
using SCIEPort.Features.Vehicles;

namespace SCIEPort.Features.Tanks
{
    /// <summary>
    /// Carries authenticated tank turret state and fire requests. Movement stays on
    /// SCNET's native mounted-parent BodyUpdate path; only the server creates shells.
    /// </summary>
    public sealed class TankControlPackage : IPackage
    {
        /// <summary>SCIE-Port tank-control package identifier.</summary>
        public byte ID => 241;

        /// <inheritdoc />
        public Client To { get; set; }

        /// <inheritdoc />
        public Client Except { get; set; }

        /// <inheritdoc />
        public Client From { get; set; }

        /// <inheritdoc />
        public ClientState MinNeedState => ClientState.ProjectLoaded;

        /// <summary>Tank entity ID.</summary>
        public ushort VehicleId { get; set; }

        /// <summary>Driver entity ID.</summary>
        public ushort RiderId { get; set; }

        /// <summary>Monotonic driver sequence.</summary>
        public ushort Sequence { get; set; }

        /// <summary>Hull-local turret yaw.</summary>
        public float TurretYaw { get; set; }

        /// <summary>Hull-local gun pitch.</summary>
        public float GunPitch { get; set; }

        /// <summary>Whether this client request asks the server to fire.</summary>
        public bool Fire { get; set; }

        /// <summary>True for a client request, false for server state replication.</summary>
        public bool IsRequest { get; set; }

        /// <summary>Creates an empty decoder instance.</summary>
        public TankControlPackage()
        {
        }

        /// <summary>Creates a driver request or authoritative replicated state.</summary>
        public TankControlPackage(
            ComponentTankDrive drive,
            ComponentRider rider,
            ushort sequence,
            bool fire,
            bool isRequest)
        {
            VehicleId = drive.Entity.EntityId;
            RiderId = rider?.Entity.EntityId ?? 0;
            Sequence = sequence;
            TurretYaw = drive.TurretYaw;
            GunPitch = drive.GunPitch;
            Fire = fire;
            IsRequest = isRequest;
        }

        /// <inheritdoc />
        public void WriteData(PackageStreamWriter writer)
        {
            writer.Write(VehicleId);
            writer.Write(RiderId);
            writer.Write(Sequence);
            writer.Write(TurretYaw);
            writer.Write(GunPitch);
            writer.Write(Fire);
            writer.Write(IsRequest);
        }

        /// <inheritdoc />
        public void ReadData(PackageStreamReader reader)
        {
            VehicleId = reader.ReadUInt16();
            RiderId = reader.ReadUInt16();
            Sequence = reader.ReadUInt16();
            TurretYaw = reader.ReadSingle();
            GunPitch = reader.ReadSingle();
            Fire = reader.ReadBoolean();
            IsRequest = reader.ReadBoolean();
        }

        /// <inheritdoc />
        public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
        {
            if (isServer)
            {
                if (!IsRequest || From == null)
                {
                    return;
                }

                ComponentPlayer sender = From.PlayerData?.ComponentPlayer;
                if (sender == null || sender.Entity.EntityId != RiderId)
                {
                    return;
                }

                // The host verified and recorded this player's activation session by
                // network GUID; without it a remote driver cannot move or fire the tank.
                if (!VehicleActivationService.IsGranted(From.GUID))
                {
                    return;
                }

                projectNet.FindEntityById(VehicleId, vehicle =>
                {
                    ComponentTankDrive drive = vehicle.FindComponent<ComponentTankDrive>(throwOnError: false);
                    if (drive != null && drive.AcceptRemoteControl(sender, Sequence, TurretYaw, GunPitch, Fire))
                    {
                        netNode.QueuePackage(new TankControlPackage(
                            drive,
                            sender.ComponentRider,
                            Sequence,
                            fire: false,
                            isRequest: false)
                        {
                            Except = From
                        });
                    }
                });
                return;
            }

            if (IsRequest || From == null || From != netNode.Server)
            {
                return;
            }

            projectNet.FindEntityById(VehicleId, vehicle =>
            {
                vehicle.FindComponent<ComponentTankDrive>(throwOnError: false)
                    ?.ApplyNetworkTurretState(TurretYaw, GunPitch);
            });
        }
    }
}
