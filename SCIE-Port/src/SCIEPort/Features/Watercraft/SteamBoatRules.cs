using System;

namespace SCIEPort.Features.Watercraft
{
    /// <summary>Audited dimensions, seats and bounded propulsion for the two-seat speedboat.</summary>
    public static class SteamBoatRules
    {
        /// <summary>Collision hull width.</summary>
        public const float BodyWidth = 2.3f;
        /// <summary>Collision hull height.</summary>
        public const float BodyHeight = 1.75f;
        /// <summary>Collision hull length.</summary>
        public const float BodyLength = 5.6f;
        /// <summary>Driver local X offset.</summary>
        public const float DriverX = -0.42f;
        /// <summary>Passenger local X offset.</summary>
        public const float PassengerX = 0.42f;
        /// <summary>Shared seat height.</summary>
        public const float SeatY = 1f;
        /// <summary>Shared seat longitudinal offset.</summary>
        public const float SeatZ = -0.45f;
        /// <summary>Safe side dismount X offset.</summary>
        public const float DismountX = 1.65f;
        /// <summary>Equivalent native boat acceleration.</summary>
        public const float BaseAcceleration = 3f;
        /// <summary>Existing SCIE-Port additional acceleration.</summary>
        public const float ExtraAcceleration = 8f;
        /// <summary>Bounded horizontal speed.</summary>
        public const float MaximumHorizontalSpeed = 15f;

        /// <summary>Horizontal distance between seat centers.</summary>
        public static float SeatSeparation => Math.Abs(PassengerX - DriverX);

        /// <summary>Returns whether native body state permits water propulsion.</summary>
        public static bool IsWaterborne(float immersionFactor, bool terrainSupported, bool bodySupported)
        {
            return immersionFactor > 0.01f && immersionFactor <= 0.95f && !terrainSupported && !bodySupported;
        }

        /// <summary>Checks that visible model bounds fit the planned collision hull.</summary>
        public static bool ContainsModelBounds(
            float xMin,
            float xMax,
            float yMin,
            float yMax,
            float zMin,
            float zMax)
        {
            return xMin >= -BodyWidth / 2f && xMax <= BodyWidth / 2f &&
                yMin >= 0f && yMax <= BodyHeight &&
                zMin >= -BodyLength / 2f && zMax <= BodyLength / 2f;
        }
    }
}
