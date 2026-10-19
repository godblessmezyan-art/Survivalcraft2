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
        public const float BrakeAcceleration = 16f;
        /// <summary>Passive deceleration when the driver releases movement input.</summary>
        public const float CoastDeceleration = 2.2f;
        /// <summary>Time reported as a short airborne transition instead of sustained airtime.</summary>
        public const float AirborneGraceTime = 0.35f;
        /// <summary>
        /// Native smooth-rise clearance. One full cell plus collision tolerance is needed
        /// because ComponentBody raises continuously instead of teleporting to the top.
        /// </summary>
        public const float MaximumSmoothRiseHeight = 1.05f;

        /// <summary>
        /// Propulsion requires terrain contact. Body contact counts as support for airborne
        /// diagnostics but never authorizes driving through or climbing another entity.
        /// </summary>
        public static bool CanApplyDrive(bool standingOnTerrain, bool standingOnBody, float immersionFactor)
        {
            return standingOnTerrain && !standingOnBody && immersionFactor < 0.25f;
        }

        /// <summary>Returns true during the short transition immediately after losing support.</summary>
        public static bool IsAirborneGrace(bool supported, float airborneTime, float immersionFactor)
        {
            return !supported && airborneTime <= AirborneGraceTime && immersionFactor < 0.25f;
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

        /// <summary>Integrates forward speed without overshooting zero while coasting.</summary>
        public static float IntegrateForwardSpeed(float forwardSpeed, float throttle, float dt)
        {
            float acceleration = GetLongitudinalAcceleration(forwardSpeed, throttle);
            float result = forwardSpeed + acceleration * dt;
            if (throttle > -0.05f && throttle < 0.05f && forwardSpeed * result < 0f)
            {
                result = 0f;
            }

            if (result > MaximumForwardSpeed)
            {
                return MaximumForwardSpeed;
            }

            return result < -MaximumReverseSpeed ? -MaximumReverseSpeed : result;
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

        /// <summary>Reduces twitchiness at high speed while retaining low-speed maneuverability.</summary>
        public static float GetSteeringScale(float absoluteForwardSpeed)
        {
            if (absoluteForwardSpeed < 0.5f)
            {
                return 0.35f + 1.3f * absoluteForwardSpeed;
            }

            if (absoluteForwardSpeed <= 8f)
            {
                return 1f;
            }

            float t = (absoluteForwardSpeed - 8f) / (MaximumForwardSpeed - 8f);
            if (t > 1f)
            {
                t = 1f;
            }

            return 1f - 0.45f * t;
        }
    }
}
