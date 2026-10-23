namespace SCIEPort.Features.Vehicles
{
    /// <summary>Logical roles shared by two-seat vehicle implementations.</summary>
    public enum VehicleSeatRole : byte
    {
        /// <summary>No seat is available.</summary>
        None,
        /// <summary>Vehicle control seat.</summary>
        Driver,
        /// <summary>Non-controlling passenger seat.</summary>
        Passenger
    }

    /// <summary>Game-independent driver-first seat allocation rules.</summary>
    public static class TwoSeatRules
    {
        /// <summary>Returns the first available seat in driver-first order.</summary>
        public static VehicleSeatRole SelectSeat(bool driverOccupied, bool passengerOccupied)
        {
            if (!driverOccupied)
            {
                return VehicleSeatRole.Driver;
            }

            return passengerOccupied ? VehicleSeatRole.None : VehicleSeatRole.Passenger;
        }

        /// <summary>Returns whether a seat is permitted to control a vehicle.</summary>
        public static bool CanControlVehicle(VehicleSeatRole seat)
        {
            return seat == VehicleSeatRole.Driver;
        }

        /// <summary>
        /// Returns whether a settled rider's independent seat offset must be restored
        /// after the native single-Mount rider update.
        /// </summary>
        public static bool ShouldMaintainSeatOffset(bool isMountedToVehicle, bool isAnimating)
        {
            return isMountedToVehicle && !isAnimating;
        }

    }
}
