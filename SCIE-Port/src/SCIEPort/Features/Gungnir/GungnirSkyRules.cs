using System;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>Engine-independent nine-second sky timeline and bounded quality budgets.</summary>
    internal static class GungnirSkyRules
    {
        internal const float Lifetime = 9f;
        internal const float ImpactTime = 1.4f;
        internal const float RecoveryStart = 7f;

        internal static float SkyBlend(float age)
        {
            if (float.IsNaN(age) || age < 0f || age >= Lifetime) return 0f;
            float fadeIn = Saturate(age / 1.2f);
            float fadeOut = age <= RecoveryStart ? 1f : Saturate((Lifetime - age) / (Lifetime - RecoveryStart));
            return Smooth(fadeIn) * Smooth(fadeOut);
        }

        internal static float ImpactFlash(float age, bool reducedFlashes)
        {
            if (float.IsNaN(age)) return 0f;
            float distance = Math.Abs(age - ImpactTime);
            float flash = distance >= 0.22f ? 0f : 1f - distance / 0.22f;
            flash *= flash;
            return reducedFlashes ? flash * 0.35f : flash;
        }

        internal static float Combine(float first, float second)
        {
            first = Saturate(first); second = Saturate(second);
            return 1f - (1f - first) * (1f - second);
        }

        /// <summary>Divine share wins slightly at equal strength without erasing an active storm.</summary>
        internal static float DivineShare(float storm, float divine)
        {
            storm = Saturate(storm); divine = Saturate(divine);
            float total = storm + divine * 1.18f;
            return total <= 0.0001f ? 0f : divine * 1.18f / total;
        }

        internal static int CloudBands(int quality) => ClampQuality(quality) == 0 ? 0 : ClampQuality(quality) == 1 ? 3 : 5;
        internal static int SkyRunes(int quality) => ClampQuality(quality) == 0 ? 2 : ClampQuality(quality) == 1 ? 5 : 9;
        internal static int LightColumns(int quality) => ClampQuality(quality) == 0 ? 0 : ClampQuality(quality) == 1 ? 3 : 6;
        internal static int ConvergenceBeams(int quality) => ClampQuality(quality) == 0 ? 2 : ClampQuality(quality) == 1 ? 4 : 7;

        internal static bool ShouldRemove(float age, float silence, bool alive) => !alive || age >= Lifetime || silence > 2.1f;

        private static int ClampQuality(int quality) => quality < 0 ? 0 : quality > 2 ? 2 : quality;
        private static float Saturate(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
        private static float Smooth(float value) { value = Saturate(value); return value * value * (3f - 2f * value); }
    }
}
