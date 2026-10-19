namespace SCIEPort.Features.LandVehicles
{
    /// <summary>Pure, game-independent tuning rules for the first land vehicle.</summary>
    public static class AtvDriveRules
    {
        /// <summary>Maximum forward ground speed in cells per second.</summary>
        public const float MaximumForwardSpeed = 11f;
        /// <summary>Maximum reverse ground speed in cells per second.</summary>
        public const float MaximumReverseSpeed = 4f;
        /// <summary>Forward or reverse acceleration in cells per second squared.</summary>
        public const float DriveAcceleration = 10f;
        /// <summary>Deceleration applied before a requested direction reversal.</summary>
        public const float BrakeAcceleration = 18f;
        /// <summary>Passive deceleration when the driver releases movement input.</summary>
        public const float CoastDeceleration = 3f;
        /// <summary>Largest native smooth-rise obstacle height allowed to the ATV.</summary>
        public const float MaximumSmoothRiseHeight = 0.35f;

        /// <summary>Returns true only when the native body has ground contact and is not submerged.</summary>
        public static bool CanDrive(bool isStandingOnGround, float immersionFactor)
        {
            return isStandingOnGround && immersionFactor < 0.25f;
        }

        /// <summary>
        /// Calculates signed acceleration along the vehicle forward axis. Opposite input
        /// first brakes instead of immediately applying full reverse propulsion.
        /// </summary>
        public static float GetLongitudinalAcceleration(float forwardSpeed, float throttle)
        {
            if (throttle > -0.05f && throttle < 0.05f)
            {
                return forwardSpeed > 0.05f ? -CoastDeceleration :
                    (forwardSpeed < -0.05f ? CoastDeceleration : 0f);
            }

            if ((forwardSpeed > 0.1f && throttle < 0f) ||
                (forwardSpeed < -0.1f && throttle > 0f))
            {
                return throttle < 0f ? -BrakeAcceleration : BrakeAcceleration;
            }

            if (throttle > 0f && forwardSpeed >= MaximumForwardSpeed)
            {
                return 0f;
            }

            if (throttle < 0f && forwardSpeed <= -MaximumReverseSpeed)
            {
                return 0f;
            }

            return throttle * DriveAcceleration;
        }

        /// <summary>Steering reverses when the ATV is backing up and is reduced at a standstill.</summary>
        public static float GetSteeringDirection(float forwardSpeed)
        {
            if (forwardSpeed < -0.1f)
            {
                return -1f;
            }

            return forwardSpeed > 0.1f ? 1f : 0.35f;
        }
    }
}
