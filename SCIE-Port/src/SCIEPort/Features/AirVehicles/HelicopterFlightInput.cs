using System;

namespace SCIEPort.Features.AirVehicles
{
    /// <summary>Bounded analog collective, independent of widgets and network entities.</summary>
    internal sealed class HelicopterFlightInput
    {
        public float Deadzone { get; set; } = 0.08f;
        public float Sensitivity { get; set; } = 1f;
        public float ReturnRate { get; set; } = 3f;
        public float Value { get; private set; }
        public bool IsDragging { get; private set; }

        public void Reset() { Value = 0f; IsDragging = false; }

        public void Update(float raw, bool dragging, float dt)
        {
            IsDragging = dragging;
            float target = dragging ? Clamp(raw, -1f, 1f) : 0f;
            Value = HelicopterFlightRules.MoveTowards(Value, target,
                (dragging ? 8f : Clamp(ReturnRate, 0.5f, 12f)) * Clamp(dt, 0f, 0.1f));
        }

        public float Command
        {
            get
            {
                float deadzone = Clamp(Deadzone, 0f, 0.35f);
                float magnitude = Math.Max(Math.Abs(Value) - deadzone, 0f) / (1f - deadzone);
                return Math.Sign(Value) * Clamp(magnitude * Clamp(Sensitivity, 0.25f, 2f), 0f, 1f);
            }
        }

        public static float Clamp(float value, float min, float max)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? min : Math.Max(min, Math.Min(max, value));
        }
    }
}
