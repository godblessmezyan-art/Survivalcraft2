namespace SCIEPort.Features.Tanks
{
    /// <summary>Pure, game-independent T-34 movement limits.</summary>
    public static class TankRules
    {
        /// <summary>Maximum forward ground speed in cells per second.</summary>
        public const float ForwardSpeed = 7f;
        /// <summary>Maximum reverse ground speed in cells per second.</summary>
        public const float ReverseSpeed = 3f;
        /// <summary>Maximum native smooth-rise height.</summary>
        public const float StepHeight = 1.05f;
        /// <summary>Minimum time between main-gun shots.</summary>
        public const float FireCooldown = 1.6f;
        /// <summary>Main-gun projectile speed in cells per second.</summary>
        public const float ShellSpeed = 42f;
        /// <summary>Explosion pressure applied when a registered shell stops.</summary>
        public const float ShellExplosionPressure = 18f;
        /// <summary>Forward or reverse acceleration in cells per second squared.</summary>
        public const float DriveAcceleration = 6.5f;
        /// <summary>Deceleration before changing direction.</summary>
        public const float BrakeAcceleration = 12f;
        /// <summary>Passive deceleration with no throttle.</summary>
        public const float CoastDeceleration = 1.8f;

        /// <summary>Only terrain contact authorizes propulsion; water and other bodies do not.</summary>
        public static bool CanApplyDrive(bool standingOnTerrain, bool standingOnBody, float immersionFactor)
        {
            return standingOnTerrain && !standingOnBody && immersionFactor < 0.25f;
        }

        /// <summary>Returns acceleration, treating opposite input as braking first.</summary>
        public static float GetLongitudinalAcceleration(float speed, float throttle)
        {
            if (throttle > -0.05f && throttle < 0.05f)
                return speed > 0.05f ? -CoastDeceleration : (speed < -0.05f ? CoastDeceleration : 0f);
            if ((speed > 0.1f && throttle < 0f) || (speed < -0.1f && throttle > 0f))
                return throttle < 0f ? -BrakeAcceleration : BrakeAcceleration;
            if ((throttle > 0f && speed >= ForwardSpeed) || (throttle < 0f && speed <= -ReverseSpeed)) return 0f;
            return throttle * DriveAcceleration;
        }

        /// <summary>Integrates longitudinal speed without coasting through zero.</summary>
        public static float IntegrateForwardSpeed(float speed, float throttle, float dt)
        {
            float result = speed + GetLongitudinalAcceleration(speed, throttle) * dt;
            if (throttle > -0.05f && throttle < 0.05f && speed * result < 0f) result = 0f;
            return result > ForwardSpeed ? ForwardSpeed : (result < -ReverseSpeed ? -ReverseSpeed : result);
        }

        /// <summary>Tracked steering reverses while backing and permits slow pivot turns.</summary>
        public static float GetSteeringDirection(float speed)
        {
            return speed < -0.1f ? -1f : 1f;
        }

        /// <summary>Reduces abrupt yaw at maximum speed.</summary>
        public static float GetSteeringScale(float absoluteSpeed)
        {
            if (absoluteSpeed <= 4f) return 1f;
            float t = (absoluteSpeed - 4f) / (ForwardSpeed - 4f);
            if (t > 1f) t = 1f;
            return 1f - 0.4f * t;
        }

        /// <summary>Checks the single-player main-gun safety and cooldown gates.</summary>
        public static bool CanFire(float cooldownRemaining, bool hasHitRay, float immersionFactor)
        {
            return cooldownRemaining <= 0f && hasHitRay && immersionFactor < 0.8f;
        }
    }
}
