using System;

namespace SCIEPort.Features.AirVehicles
{
    internal enum HelicopterControlMode { Grounded, Manual, Recovering, AltitudeHold, Assisted }

    /// <summary>Produces velocity targets for the existing flight integrator; never writes position.</summary>
    internal sealed class HelicopterHoverController
    {
        private bool m_capture;
        public bool Enabled { get; private set; } = true;
        public float TargetHeight { get; private set; }
        public float TargetVelocity { get; private set; }
        public HelicopterControlMode Mode { get; private set; }
        public float HeightGain { get; set; } = 0.7f;
        public float Acceleration { get; set; } = 2.5f;

        public void Reset(float height)
        {
            Enabled = true;
            TargetHeight = height;
            TargetVelocity = 0f;
            Mode = HelicopterControlMode.Grounded;
            m_capture = true;
        }

        public void Toggle(float height)
        {
            Enabled = !Enabled;
            TargetHeight = height;
            m_capture = true;
        }

        public float Update(float command, bool dragging, float height, float verticalSpeed,
            bool grounded, float maxHeight, float ascent, float descent, float dt)
        {
            command = HelicopterFlightInput.Clamp(command, -1f, 1f);
            float desired;
            if (grounded)
            {
                TargetHeight = height;
                m_capture = true;
                Mode = HelicopterControlMode.Grounded;
                desired = Math.Max(command, 0f) * ascent;
                if (command <= 0f) TargetVelocity = 0f;
            }
            else if (dragging || Math.Abs(command) > 0.001f)
            {
                Mode = HelicopterControlMode.Manual;
                m_capture = true;
                desired = command * (command >= 0f ? ascent : descent);
            }
            else if (!Enabled)
            {
                Mode = HelicopterControlMode.Assisted;
                desired = 0f; // Neutral still provides safe vertical-speed damping.
            }
            else if (m_capture && Math.Abs(verticalSpeed) > 0.12f)
            {
                // Brake first, then capture where the aircraft actually stops. This avoids
                // pulling back toward an obsolete release height and oscillating around it.
                Mode = HelicopterControlMode.Recovering;
                desired = 0f;
            }
            else
            {
                if (m_capture) { TargetHeight = Math.Min(height, maxHeight); m_capture = false; }
                Mode = HelicopterControlMode.AltitudeHold;
                float error = HelicopterFlightInput.Clamp(TargetHeight - height, -8f, 8f);
                desired = HelicopterFlightInput.Clamp(error * HeightGain, -descent, ascent);
            }
            if (height >= maxHeight) desired = Math.Min(desired, 0f);
            TargetVelocity = HelicopterFlightRules.MoveTowards(TargetVelocity, desired,
                HelicopterFlightInput.Clamp(Acceleration, 0.5f, 6f) * HelicopterFlightInput.Clamp(dt, 0f, 0.1f));
            if (height >= maxHeight) TargetVelocity = Math.Min(TargetVelocity, 0f);
            return TargetVelocity;
        }
    }
}
