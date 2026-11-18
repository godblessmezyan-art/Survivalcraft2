using Engine;

namespace SCIEPort.Features.Fishing
{
    internal struct FishingCastArc
    {
        internal const float Gravity = 16.5f;
        internal const float MaxDuration = 2f;
        internal const float MinimumCharge = 0.10f;
        internal const float FullCharge = 1.15f;
        internal const float ChargeTimeout = 10f;
        private const float MinSpeed = 6.5f;
        private const float MaxSpeed = 15.5f;
        private const float WireScale = 256f;

        internal Vector3 Origin;
        internal Vector3 Velocity;
        internal float Duration;

        internal Vector3 At(float seconds)
        {
            return Origin + Velocity * seconds - Vector3.UnitY * (0.5f * Gravity * seconds * seconds);
        }

        internal Vector3 End => At(Duration);
        internal float Force => MathUtils.Saturate((Velocity.Length() - MinSpeed) / (MaxSpeed - MinSpeed));

        internal static float Charge(double seconds)
        {
            return MathUtils.Saturate((float)((seconds - MinimumCharge) / (FullCharge - MinimumCharge)));
        }

        internal static FishingCastArc Launch(Vector3 eye, Vector3 direction, float force)
        {
            Vector3 forward = Vector3.Normalize(new Vector3(direction.X, 0, direction.Z));
            // Keep a little upward flick when looking level. The player still controls yaw and elevation.
            float pitch = MathUtils.Clamp(MathUtils.Asin(MathUtils.Clamp(direction.Y, -1f, 1f)) + 0.12f, -0.74f, 0.76f);
            Vector3 right = Vector3.Cross(forward, Vector3.UnitY);
            float speed = MathUtils.Lerp(MinSpeed, MaxSpeed, force);
            return new FishingCastArc
            {
                Origin = eye + forward * 0.38f + right * 0.26f + Vector3.UnitY * 0.19f,
                Velocity = (forward * MathUtils.Cos(pitch) + Vector3.UnitY * MathUtils.Sin(pitch)) * speed,
                Duration = MaxDuration
            };
        }

        internal static bool TryDirection(Vector3 direction, out Vector3 normalized)
        {
            float length = direction.LengthSquared();
            normalized = Vector3.Zero;
            if (!(length >= 0.8f && length <= 1.2f) ||
                direction.X * direction.X + direction.Z * direction.Z < 0.001f) return false;
            normalized = direction / MathUtils.Sqrt(length);
            return true;
        }

        internal static Point3 Pack(Vector3 value)
        {
            return new Point3((int)MathUtils.Round(value.X * WireScale),
                (int)MathUtils.Round(value.Y * WireScale), (int)MathUtils.Round(value.Z * WireScale));
        }

        internal static Vector3 Unpack(Point3 value)
        {
            return new Vector3(value.X / WireScale, value.Y / WireScale, value.Z / WireScale);
        }
    }
}
