using System;

namespace SCIEPort.Features.Watercraft
{
    /// <summary>Audited dimensions, seats and bounded propulsion for the two-seat speedboat.</summary>
    public static class SteamBoatRules
    {
        public const float BodyWidth = 2.3f;
        public const float BodyHeight = 1.75f;
        public const float BodyLength = 5.2f;
        public const float DriverX = -0.42f;
        public const float PassengerX = 0.42f;
        public const float SeatY = 1f;
        public const float SeatZ = -0.45f;
        public const float DismountX = 1.65f;
        public const float BaseAcceleration = 3f;
        public const float ExtraAcceleration = 8f;
        public const float MaximumHorizontalSpeed = 15f;

        public static float SeatSeparation => Math.Abs(PassengerX - DriverX);

        public static bool IsWaterborne(float immersionFactor, bool terrainSupported, bool bodySupported)
        {
            return immersionFactor > 0.01f && immersionFactor <= 0.95f && !terrainSupported && !bodySupported;
        }

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
