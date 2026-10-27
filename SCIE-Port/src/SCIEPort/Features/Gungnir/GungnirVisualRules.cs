using System;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>Pure presentation budgets. Quality is 0=low, 1=medium, 2=high.</summary>
    internal static class GungnirVisualRules
    {
        internal const float OutboundTrailLength = 5.6f;
        internal const float ReturnTrailLength = 6.8f;
        internal const float HitCueLife = 0.24f;
        internal const float ReturnCueLife = 0.38f;

        internal static int TrailBands(int quality) => ClampQuality(quality) + 1;
        internal static int ReturnSpiralSegments(int quality) => 5 + ClampQuality(quality) * 2;
        internal static int ReturnRuneCount(int quality) => ClampQuality(quality) + 1;
        internal static int ReturnRingCount(int quality) => ClampQuality(quality) == 2 ? 2 : 1;

        internal static float CueAlpha(float age, float life)
        {
            if (float.IsNaN(age) || float.IsNaN(life) || age < 0f || life <= 0f || age >= life) return 0f;
            float t = age / life;
            float attack = Math.Min(1f, t / 0.12f);
            return attack * (1f - t) * (1f - t);
        }

        private static int ClampQuality(int quality) => quality < 0 ? 0 : quality > 2 ? 2 : quality;
    }
}
