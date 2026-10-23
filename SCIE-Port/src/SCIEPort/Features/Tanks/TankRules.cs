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
        /// <summary>World draw multiplier for the native 0.3 projectile render size.</summary>
        public const float ShellVisualScale = 3.5f;
        /// <summary>
        /// Explosion pressure applied when a registered shell stops. This is the exact
        /// DefaultExplosionPressure of LargeGunpowderKegBlock (ID 107) in the audited
        /// Net_x25.01.17 Content.scpak, so native explosion propagation destroys blocks.
        /// </summary>
        public const float ShellExplosionPressure = 2160f;
        /// <summary>Clearance beyond the tank collision box used for shell spawning.</summary>
        public const float MuzzleClearance = .75f;
        /// <summary>Horizontal velocity impulse applied immediately after a successful shot.</summary>
        public const float RecoilSpeed = 1.25f;
        /// <summary>Maximum horizontal turret traverse speed in radians per second.</summary>
        public const float TurretTraverseSpeed = 1.2f;
        /// <summary>Maximum gun elevation speed in radians per second.</summary>
        public const float GunElevationSpeed = .65f;
        /// <summary>Mechanical gun-depression limit (minus five degrees).</summary>
        public const float MinimumGunPitch = -.08726646f;
        /// <summary>Mechanical gun-elevation limit (plus twenty-five degrees).</summary>
        public const float MaximumGunPitch = .43633232f;
        /// <summary>Height of the gun pivot above the bottom-center body position.</summary>
        public const float GunPivotHeight = 1.566f;
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

        /// <summary>Wrap-safe ordering for reliable driver control messages.</summary>
        public static bool IsNewerSequence(ushort candidate, ushort previous)
        {
            ushort delta = (ushort)(candidate - previous);
            return delta != 0 && delta < 32768;
        }

        /// <summary>Wraps an angle to [-PI, PI] for shortest-path turret traversal.</summary>
        public static float NormalizeAngle(float angle)
        {
            float twoPi = 2f * (float)System.Math.PI;
            while (angle > (float)System.Math.PI) angle -= twoPi;
            while (angle < -(float)System.Math.PI) angle += twoPi;
            return angle;
        }

        /// <summary>Moves a wrapped angle toward a target without snapping or taking the long arc.</summary>
        public static float MoveAngleTowards(float current, float target, float maximumDelta)
        {
            float delta = NormalizeAngle(target - current);
            if (delta > maximumDelta) delta = maximumDelta;
            else if (delta < -maximumDelta) delta = -maximumDelta;
            return NormalizeAngle(current + delta);
        }

        /// <summary>Moves a scalar toward a target by a bounded amount.</summary>
        public static float MoveTowards(float current, float target, float maximumDelta)
        {
            float delta = target - current;
            if (delta > maximumDelta) return current + maximumDelta;
            if (delta < -maximumDelta) return current - maximumDelta;
            return target;
        }

        /// <summary>Returns the first positive exit distance from an axis-aligned box.</summary>
        public static float GetBoxExitDistance(
            float offsetX, float offsetY, float offsetZ,
            float directionX, float directionY, float directionZ,
            float halfX, float halfY, float halfZ)
        {
            float result = float.MaxValue;
            AccumulateAxisExit(offsetX, directionX, halfX, ref result);
            AccumulateAxisExit(offsetY, directionY, halfY, ref result);
            AccumulateAxisExit(offsetZ, directionZ, halfZ, ref result);
            return result;
        }

        private static void AccumulateAxisExit(float offset, float direction, float halfSize, ref float result)
        {
            float distance;
            if (direction > .0001f) distance = (halfSize - offset) / direction;
            else if (direction < -.0001f) distance = (-halfSize - offset) / direction;
            else return;
            if (distance >= 0f && distance < result) result = distance;
        }

    }
}
