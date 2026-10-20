namespace SCIEPort.Features.LandVehicles
{
    /// <summary>Game-independent rules and dimensions for the tandem ATV seats.</summary>
    public static class AtvSeatRules
    {
        /// <summary>Driver mount local X offset.</summary>
        public const float DriverX = 0f;
        /// <summary>Driver mount local Y offset.</summary>
        public const float DriverY = 0.9f;
        /// <summary>Driver mount local Z offset.</summary>
        public const float DriverZ = -0.3f;

        /// <summary>Passenger mount local X offset.</summary>
        public const float PassengerX = 0f;
        /// <summary>Passenger mount local Y offset.</summary>
        public const float PassengerY = 0.9f;
        /// <summary>Passenger mount local Z offset.</summary>
        public const float PassengerZ = 0.62f;

        /// <summary>Logical role assigned by the authoritative allocator.</summary>
        public enum Seat
        {
            /// <summary>No seat is available.</summary>
            None,
            /// <summary>Vehicle control seat.</summary>
            Driver,
            /// <summary>Non-controlling rear seat.</summary>
            Passenger
        }

        /// <summary>Returns the first available seat in driver-first order.</summary>
        public static Seat SelectSeat(bool driverOccupied, bool passengerOccupied)
        {
            if (!driverOccupied)
            {
                return Seat.Driver;
            }

            return passengerOccupied ? Seat.None : Seat.Passenger;
        }

        /// <summary>Returns whether a logical seat is permitted to drive.</summary>
        public static bool CanControlVehicle(Seat seat)
        {
            return seat == Seat.Driver;
        }
    }
}
