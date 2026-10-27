using System;
#pragma warning disable 1591

namespace SCIEPort.Features.FourSaints
{
    /// <summary>Engine-independent constants and formulae for the Four Saints equipment.</summary>
    public static class FourSaintsRules
    {
        public const int QinglongIndex = 200;
        public const int XuanwuIndex = 201;
        public const int BaihuIndex = 202;
        public const int ZhuqueIndex = 203;
        public const float NativeHealthPerPoint = 0.1f;
        public const float QinglongMaximumHealth = 1.5f;
        public const float NativeMaximumHealth = 1f;
        public const float QinglongHealAmount = 0.1f;
        public const double QinglongHealPeriod = 5.0;
        public const float XuanwuReduction = 0.3f;
        public const float BaihuNormalFrequency = 1.2f;
        public const float BaihuAwakenedFrequency = 2f;
        public const float BaihuAwakenedDamage = 1.5f;
        public const double BaihuDuration = 5.0;
        public const double BaihuCooldown = 60.0;
        public const double ZhuqueLockDuration = 1.0;

        public static float EffectiveDamage(float amount, bool baihuAwakened, bool xuanwu)
        {
            float result = Math.Max(0f, amount) * (baihuAwakened ? BaihuAwakenedDamage : 1f);
            return xuanwu ? Math.Max(0f, result - XuanwuReduction) : result;
        }

        public static float AttackFrequency(bool baihu, bool awakened, bool mjolnir)
        {
            return (baihu ? (awakened ? BaihuAwakenedFrequency : BaihuNormalFrequency) : 1f) * (mjolnir ? 1.1f : 1f);
        }

        public static float JumpSpeedForOneExtraBlock(float rawJumpSpeed, float levelSpeedFactor)
        {
            float multiplier = 1f + 0.25f * (levelSpeedFactor - 1f);
            float effective = Math.Max(0f, rawJumpSpeed) * multiplier;
            return (float)Math.Sqrt(effective * effective + 20f) / Math.Max(0.01f, multiplier);
        }
    }
}
#pragma warning restore 1591
