namespace SCIEPort.Features.OffroadVehicles
{
    /// <summary>Pure dimensions and driving rules for the two-seat off-road car.</summary>
    public static class OffroadCarRules
    {
        /// <summary>Native body width in cells.</summary>
        public const float BodyWidth = 2.4f;
        /// <summary>Native body height in cells.</summary>
        public const float BodyHeight = 1.55f;
        /// <summary>Native body length in cells.</summary>
        public const float BodyLength = 4f;
        /// <summary>Driver seat local X coordinate.</summary>
        public const float DriverX = -0.45f;
        /// <summary>Passenger seat local X coordinate.</summary>
        public const float PassengerX = 0.45f;
        /// <summary>Shared seat height.</summary>
        public const float SeatY = 0.86f;
        /// <summary>Shared longitudinal seat coordinate.</summary>
        public const float SeatZ = 0.18f;
        /// <summary>Safe right-side dismount coordinate.</summary>
        public const float DismountX = 1.72f;
        /// <summary>Maximum forward speed.</summary>
        public const float MaximumForwardSpeed = 9.5f;
        /// <summary>Maximum reverse speed.</summary>
        public const float MaximumReverseSpeed = 3.5f;
        /// <summary>Drive acceleration.</summary>
        public const float DriveAcceleration = 7.5f;
        /// <summary>Direction-change braking acceleration.</summary>
        public const float BrakeAcceleration = 13f;
        /// <summary>Coasting deceleration.</summary>
        public const float CoastDeceleration = 1.8f;
        /// <summary>One-cell native smooth-rise clearance.</summary>
        public const float MaximumSmoothRiseHeight = 1.05f;

        /// <summary>Distance between the two seat centers.</summary>
        public static float SeatSeparation => PassengerX - DriverX;

        /// <summary>Allows propulsion only on terrain and outside deep water.</summary>
        public static bool CanApplyDrive(bool standingOnTerrain, bool standingOnBody, float immersionFactor)
        {
            return standingOnTerrain && !standingOnBody && immersionFactor < 0.25f;
        }

        /// <summary>Integrates longitudinal speed with braking, coasting and asymmetric limits.</summary>
        public static float IntegrateForwardSpeed(float speed, float throttle, float dt)
        {
            float acceleration;
            if (throttle > -0.05f && throttle < 0.05f)
            {
                acceleration = speed > 0.05f ? -CoastDeceleration :
                    (speed < -0.05f ? CoastDeceleration : 0f);
            }
            else if ((speed > 0.1f && throttle < 0f) || (speed < -0.1f && throttle > 0f))
            {
                acceleration = throttle < 0f ? -BrakeAcceleration : BrakeAcceleration;
            }
            else
            {
                acceleration = throttle * DriveAcceleration;
            }

            float result = speed + acceleration * dt;
            if (throttle > -0.05f && throttle < 0.05f && speed * result < 0f)
            {
                result = 0f;
            }

            if (result > MaximumForwardSpeed)
            {
                return MaximumForwardSpeed;
            }

            return result < -MaximumReverseSpeed ? -MaximumReverseSpeed : result;
        }

        /// <summary>Returns steering direction, including reversed steering while backing up.</summary>
        public static float GetSteeringDirection(float speed)
        {
            return speed < -0.1f ? -1f : (speed > 0.1f ? 1f : 0.3f);
        }

        /// <summary>Reduces steering sensitivity at higher speed.</summary>
        public static float GetSteeringScale(float absoluteSpeed)
        {
            if (absoluteSpeed < 0.5f)
            {
                return 0.3f + 1.4f * absoluteSpeed;
            }

            if (absoluteSpeed <= 6f)
            {
                return 1f;
            }

            float t = (absoluteSpeed - 6f) / (MaximumForwardSpeed - 6f);
            return 1f - 0.4f * (t > 1f ? 1f : t);
        }

        /// <summary>Checks a model envelope against the planned native body.</summary>
        public static bool ContainsModelBounds(float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
        {
            return xMin >= -BodyWidth / 2f && xMax <= BodyWidth / 2f &&
                yMin >= 0f && yMax <= BodyHeight &&
                zMin >= -BodyLength / 2f && zMax <= BodyLength / 2f;
        }
    }
}
