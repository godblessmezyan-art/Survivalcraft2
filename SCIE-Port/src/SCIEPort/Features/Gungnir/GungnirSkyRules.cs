using System;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>Engine-independent nine-second sky timeline and bounded quality budgets.</summary>
    internal static class GungnirSkyRules
    {
        internal const float Lifetime = 9f;
        internal const float ImpactTime = 1.4f;
        internal const float RecoveryStart = 7f;

        /// <summary>Giant judgment spear. The model spans 2.2 units above its origin (blade) and
        /// 1.78 below (counterweight), so a 4.6x scale gives a ~8 unit shaft above the impact point.</summary>
        internal const float SpearScale = 4.6f;
        internal const float SpearBladeLength = 2.2f;
        internal const float SpearHoverHeight = 40f;
        internal const float SpearFormEnd = 0.95f;

        /// <summary>Origin height that drives the blade 1.4 units into the target block.</summary>
        internal static float SpearImpactHeight => SpearBladeLength * SpearScale - 1.4f;

        /// <summary>Origin height of the giant spear at any cinematic age.</summary>
        internal static float SpearHeight(float age)
        {
            if (float.IsNaN(age) || age < 0f || age >= Lifetime) return 0f;
            if (age < SpearFormEnd) return Lerp(SpearHoverHeight + 6f, SpearHoverHeight, Smooth(age / SpearFormEnd));
            if (age < ImpactTime) return Lerp(SpearHoverHeight, SpearImpactHeight, EaseIn((age - SpearFormEnd) / (ImpactTime - SpearFormEnd)));
            return SpearImpactHeight;
        }

        /// <summary>Relative scale: the blade condenses, grows during the fall and settles after impact.</summary>
        internal static float SpearScaleFactor(float age)
        {
            if (float.IsNaN(age) || age < 0f || age >= Lifetime) return 0f;
            if (age < SpearFormEnd) return Lerp(0.5f, 1f, Smooth(age / SpearFormEnd));
            if (age < ImpactTime) return Lerp(1f, 1.09f, EaseIn((age - SpearFormEnd) / (ImpactTime - SpearFormEnd)));
            return age < ImpactTime + 0.6f ? Lerp(1.09f, 1.02f, (age - ImpactTime) / 0.6f) : 1.02f;
        }

        /// <summary>Opacity: fade in while condensing, then dissolve slowly after the impact.</summary>
        internal static float SpearAlpha(float age)
        {
            if (float.IsNaN(age) || age < 0f || age >= Lifetime) return 0f;
            if (age < 0.32f) return Smooth(age / 0.32f);
            if (age < 3.4f) return 1f;
            return Smooth(1f - (age - 3.4f) / 4.4f);
        }

        /// <summary>True while the blade is visibly plunging, used to add afterimages and a trail.</summary>
        internal static bool SpearFalling(float age) => !float.IsNaN(age) && age >= SpearFormEnd && age < ImpactTime;

        internal static int AfterImageCount(int quality) => ClampQuality(quality);
        internal static int EmberCount(int quality) => ClampQuality(quality) == 0 ? 3 : ClampQuality(quality) == 1 ? 7 : 12;

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
        private static float EaseIn(float value) { value = Saturate(value); return value * value * value; }
        private static float Lerp(float a, float b, float t) => a + (b - a) * Saturate(t);
    }
}
