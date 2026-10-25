using System;

namespace SCIEPort.Features.AirVehicles
{
    /// <summary>Game-independent bounded flight rules for the two-seat helicopter.</summary>
    public static class HelicopterFlightRules
    {
        /// <summary>Collision body width.</summary>
        public const float BodyWidth = 2.4f;
        /// <summary>Collision body height.</summary>
        public const float BodyHeight = 2.2f;
        /// <summary>Collision body length.</summary>
        public const float BodyLength = 5.4f;
        /// <summary>Driver local X coordinate.</summary>
        public const float DriverX = -0.42f;
        /// <summary>Passenger local X coordinate.</summary>
        public const float PassengerX = 0.42f;
        /// <summary>Shared seat height.</summary>
        public const float SeatY = 1.05f;
        /// <summary>Shared seat longitudinal coordinate.</summary>
        public const float SeatZ = -0.55f;
        /// <summary>Safe side dismount coordinate.</summary>
        public const float DismountX = 1.65f;

        /// <summary>Moves a scalar toward a target without overshoot.</summary>
        public static float MoveTowards(float value, float target, float maximumDelta)
        {
            if (value < target) return Math.Min(value + maximumDelta, target);
            if (value > target) return Math.Max(value - maximumDelta, target);
            return target;
        }

        /// <summary>Advances normalized rotor speed through bounded spool-up or spool-down.</summary>
        public static float UpdateRotorSpeed(float speed, bool running, float spoolUpTime, float spoolDownTime, float dt)
        {
            float target = running ? 1f : 0f;
            float time = running ? spoolUpTime : spoolDownTime;
            float delta = dt / Math.Max(time, 0.05f);
            return MoveTowards(Clamp(speed, 0f, 1f), target, delta);
        }

        /// <summary>Selects pilot ascent/descent/hover or unattended landing speed.</summary>
        public static float GetVerticalTarget(bool ascend, bool descend, bool hasPilot, float ascentSpeed, float descentSpeed, float autoLandSpeed)
        {
            if (!hasPilot) return -Math.Abs(autoLandSpeed);
            if (ascend && !descend) return Math.Abs(ascentSpeed);
            if (descend && !ascend) return -Math.Abs(descentSpeed);
            return 0f;
        }

        /// <summary>Integrates explicit gravity, rotor lift and vertical speed limits.</summary>
        public static float IntegrateVerticalSpeed(
            float speed,
            float targetSpeed,
            float rotorSpeed,
            float gravity,
            float response,
            float maximumAscent,
            float maximumDescent,
            float dt)
        {
            float rotor = Clamp(rotorSpeed, 0f, 1f);
            float correction = Clamp((targetSpeed - speed) * response, -gravity, gravity);
            float acceleration = -gravity + rotor * (gravity + correction);
            return Clamp(speed + acceleration * dt, -Math.Abs(maximumDescent), Math.Abs(maximumAscent));
        }

        /// <summary>Integrates longitudinal acceleration, braking, drag and speed limits.</summary>
        public static float IntegrateForwardSpeed(
            float speed,
            float throttle,
            float acceleration,
            float braking,
            float drag,
            float maximumForward,
            float maximumReverse,
            float dt)
        {
            float input = Clamp(throttle, -1f, 1f);
            float change;
            if (Math.Abs(input) < 0.05f)
            {
                change = speed > 0.02f ? -drag : (speed < -0.02f ? drag : 0f);
            }
            else if ((speed > 0.05f && input < 0f) || (speed < -0.05f && input > 0f))
            {
                change = input < 0f ? -braking : braking;
            }
            else
            {
                change = input * acceleration;
            }

            float result = speed + change * dt;
            if (Math.Abs(input) < 0.05f && speed * result < 0f) result = 0f;
            return Clamp(result, -Math.Abs(maximumReverse), Math.Abs(maximumForward));
        }

        /// <summary>Returns whether the absolute safe flight ceiling has been reached.</summary>
        public static bool IsAtAltitudeLimit(float worldY, float maximumWorldY)
        {
            return worldY >= maximumWorldY;
        }

        /// <summary>Checks static cabin geometry against the native collision body.</summary>
        public static bool ContainsCabinBounds(float xMin, float xMax, float yMin, float yMax, float zMin, float zMax)
        {
            return xMin >= -BodyWidth / 2f && xMax <= BodyWidth / 2f &&
                yMin >= 0f && yMax <= BodyHeight &&
                zMin >= -BodyLength / 2f && zMax <= BodyLength / 2f;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return value < minimum ? minimum : (value > maximum ? maximum : value);
        }
    }
}
