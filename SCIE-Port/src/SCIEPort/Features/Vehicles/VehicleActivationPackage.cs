using System;
using Game;
using Game.NetWork;

namespace SCIEPort.Features.Vehicles
{
    /// <summary>
    /// Carries a vehicle activation request (client to host) and its authorization
    /// reply (host to the one requesting client). Reserved SCIE-Port package id 242:
    /// the native package id space is 0..38 and ids 240/241 are already taken by the
    /// two-seat assignment and tank-control packages, leaving 242 free. The host is
    /// the sole authority: it validates the code against <see
    /// cref="VehicleActivationRules"/> and records the grant by the requester's
    /// network GUID, then unicasts the result only to that client so other clients
    /// never unlock from a broadcast.
    /// </summary>
    public sealed class VehicleActivationPackage : IPackage
    {
        /// <summary>SCIE-Port vehicle activation package identifier.</summary>
        public byte ID => 242;

        /// <inheritdoc />
        public Client To { get; set; }

        /// <inheritdoc />
        public Client Except { get; set; }

        /// <inheritdoc />
        public Client From { get; set; }

        /// <inheritdoc />
        public ClientState MinNeedState => ClientState.ProjectLoaded;

        /// <summary>Entered code on a request; unused on a reply.</summary>
        public string Code { get; set; }

        /// <summary>True for a client request, false for a host reply.</summary>
        public bool IsRequest { get; set; }

        /// <summary>Reply flag: whether the host granted the requesting session.</summary>
        public bool Granted { get; set; }

        /// <summary>Creates an empty decoder instance.</summary>
        public VehicleActivationPackage()
        {
            Code = string.Empty;
        }

        /// <summary>Creates a client request carrying the entered code.</summary>
        public VehicleActivationPackage(string code, bool isRequest)
        {
            Code = code ?? string.Empty;
            IsRequest = isRequest;
        }

        /// <inheritdoc />
        public void WriteData(PackageStreamWriter writer)
        {
            writer.Write(Code);
            writer.Write(IsRequest);
            writer.Write(Granted);
        }

        /// <inheritdoc />
        public void ReadData(PackageStreamReader reader)
        {
            Code = reader.ReadString();
            IsRequest = reader.ReadBoolean();
            Granted = reader.ReadBoolean();
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

                bool grant = VehicleActivationRules.ShouldGrantActivation(
                    VehicleActivationRules.IsValidCode(Code));
                if (grant)
                {
                    // Authority-side record so host-verified remote control (for example
                    // tank fire through TankControlPackage) can consult this later.
                    VehicleActivationService.Grant(From.GUID);
                }

                netNode.QueuePackage(new VehicleActivationPackage(string.Empty, isRequest: false)
                {
                    Granted = grant,
                    To = From
                });
                return;
            }

            // Client side: only an affirmative reply that originated from the server
            // unlocks this machine's single local session.
            if (IsRequest || From != netNode.Server)
            {
                return;
            }

            if (Granted)
            {
                VehicleActivationService.Grant(Guid.Empty);
            }
        }
    }
}
