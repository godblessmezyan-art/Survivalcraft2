using System;

namespace SCIEPort.Features.Poseidon
{
    /// <summary>Bounded, engine-independent first edition combat and presentation rules.</summary>
    internal static class PoseidonRules
    {
        internal const float DashCooldown = 8f, WaveCooldown = 26f;
        internal const float DashDuration = 0.55f, WaveWindup = 0.3f, WaveDuration = 1.5f;
        internal const float WaveSpeed = 16f, WaveWidth = 8f, WaveHeight = 4f;
        internal const int MaxCasts = 8, MaxHits = 24, MaxCandidates = 96;
        internal const float SwimMultiplier = 2.6f;
        internal static float Cooldown(byte skill) => skill == 1 ? DashCooldown : WaveCooldown;
        internal static float DashSpeed(bool water) => water ? 22f : 17f;
        internal static float Damage(byte skill, bool water) => skill == 1 ? (water ? 95f : 70f) : 135f;
        internal static bool Newer(uint a, uint b) => unchecked((int)(a - b)) > 0;
        internal static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        internal static float SwimDelta(float current, float owned, bool active) => active ? Math.Max(0,current-owned)*(SwimMultiplier-1) : 0;
        internal static float WaveDistance(float age) => Math.Max(0f, Math.Min(WaveDuration, age - WaveWindup)) * WaveSpeed;
        internal static bool Alive(byte skill, float age) => age >= 0 && age < (skill == 1 ? DashDuration : WaveWindup + WaveDuration);
        internal static bool InSweep(float along, float across, float height, float previous, float next, float radius)
            => along >= previous - radius && along <= next + radius && Math.Abs(across) <= WaveWidth / 2 + radius && height >= -radius && height <= WaveHeight + radius;
        internal static float SegmentDistanceSquared(float px, float py, float pz, float ax, float ay, float az, float bx, float by, float bz)
        {
            float x = bx - ax, y = by - ay, z = bz - az;
            float n = x*x + y*y + z*z;
            float t = n > 0.00001f ? Math.Max(0, Math.Min(1, ((px-ax)*x + (py-ay)*y + (pz-az)*z)/n)) : 0;
            x = px-ax-t*x; y = py-ay-t*y; z = pz-az-t*z;
            return x*x + y*y + z*z;
        }
    }
}
