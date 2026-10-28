using System;

namespace SCIEPort.Features.AirVehicles
{
    /// <summary>Visual and audio rotor lifecycle derived from the existing engine flag and rotor speed.</summary>
    public enum HelicopterRotorState
    {
        /// <summary>Rotor is fully at rest.</summary>
        Stopped,
        /// <summary>Rotor is accelerating toward flight speed.</summary>
        Starting,
        /// <summary>Rotor is at sustained flight speed.</summary>
        Running,
        /// <summary>Rotor is decelerating toward rest.</summary>
        Stopping
    }

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

        /// <summary>Maps the existing flight engine flag and normalized rotor speed to an explicit render state.</summary>
        public static HelicopterRotorState GetRotorState(bool engineRunning, float rotorSpeed)
        {
            float speed = Clamp(rotorSpeed, 0f, 1f);
            if (engineRunning)
            {
                return speed >= 0.995f ? HelicopterRotorState.Running : HelicopterRotorState.Starting;
            }

            return speed <= 0.005f ? HelicopterRotorState.Stopped : HelicopterRotorState.Stopping;
        }

        /// <summary>Wraps any positive or negative angle into one full turn.</summary>
        public static float WrapAngle(float angle)
        {
            const float fullTurn = 6.2831855f;
            float wrapped = angle % fullTurn;
            return wrapped < 0f ? wrapped + fullTurn : wrapped;
        }

        /// <summary>Returns the shortest signed angular delta from one wrapped angle to another.</summary>
        public static float ShortestAngleDelta(float from, float to)
        {
            const float halfTurn = 3.1415927f;
            return WrapAngle(to - from + halfTurn) - halfTurn;
        }

        /// <summary>Calculates a bounded visual-only pitch target from longitudinal velocity.</summary>
        public static float GetVisualPitch(float forwardSpeed, float maximumForward, float maximumReverse, float maximumPitch)
        {
            float denominator = forwardSpeed >= 0f ? Math.Max(maximumForward, 0.01f) : Math.Max(maximumReverse, 0.01f);
            return -Clamp(forwardSpeed / denominator, -1f, 1f) * Math.Abs(maximumPitch);
        }

        /// <summary>Calculates a bounded visual-only bank target from lateral velocity and yaw rate.</summary>
        public static float GetVisualRoll(
            float lateralSpeed,
            float lateralReferenceSpeed,
            float yawRate,
            float yawReferenceRate,
            float maximumRoll)
        {
            float lateral = Clamp(lateralSpeed / Math.Max(Math.Abs(lateralReferenceSpeed), 0.01f), -1f, 1f);
            float turn = Clamp(yawRate / Math.Max(Math.Abs(yawReferenceRate), 0.01f), -1f, 1f);
            return -Clamp(0.65f * lateral + 0.35f * turn, -1f, 1f) * Math.Abs(maximumRoll);
        }

        /// <summary>Applies frame-rate-independent exponential damping to a visual angle.</summary>
        public static float SmoothVisualAngle(float current, float target, float response, float dt)
        {
            float alpha = 1f - (float)Math.Exp(-Math.Max(response, 0f) * Math.Max(dt, 0f));
            return current + (target - current) * alpha;
        }

        /// <summary>Returns a 0..1 blend for the prebuilt high-speed rotor geometry.</summary>
        public static float GetRotorBlurBlend(float rotorSpeed, float threshold)
        {
            return Clamp((rotorSpeed - threshold) / 0.12f, 0f, 1f);
        }

        /// <summary>Minimum eye-forward Y magnitude before view pitch commands a climb or dive.</summary>
        public const float VerticalPitchDeadzone = 0.2f;

        /// <summary>
        /// Reads a climb intent from the driver's eye-forward Y. Mounted riders do not
        /// reliably emit jump input on touch devices, so view elevation is the primary
        /// vertical channel (matching the proven airship control) while jump/sneak remain
        /// secondary inputs.
        /// </summary>
        public static bool IsViewClimb(float eyeForwardY, float deadzone)
        {
            return eyeForwardY > Math.Abs(deadzone);
        }

        /// <summary>Reads a dive intent from the driver's eye-forward Y using the same deadzone.</summary>
        public static bool IsViewDive(float eyeForwardY, float deadzone)
        {
            return eyeForwardY < -Math.Abs(deadzone);
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
