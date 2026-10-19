namespace SCIEPort.Features.Tanks
{
    /// <summary>Pure T-34 prototype movement limits.</summary>
    public static class TankRules
    {
        /// <summary>Maximum forward speed.</summary>
        public const float ForwardSpeed = 7f;
        /// <summary>Maximum reverse speed.</summary>
        public const float ReverseSpeed = 3f;
        /// <summary>Maximum native smooth-rise height.</summary>
        public const float StepHeight = 0.3f;

        /// <summary>Returns acceleration, treating an opposite direction request as braking.</summary>
        public static float Acceleration(float speed, float throttle)
        {
            if (throttle > -0.05f && throttle < 0.05f) return speed > 0.05f ? -2.5f : (speed < -0.05f ? 2.5f : 0f);
            if ((speed > 0.1f && throttle < 0f) || (speed < -0.1f && throttle > 0f)) return throttle < 0f ? -14f : 14f;
            if ((throttle > 0f && speed >= ForwardSpeed) || (throttle < 0f && speed <= -ReverseSpeed)) return 0f;
            return throttle * 7f;
        }
    }
}
