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

        /// <summary>Every one of the four outfits now shares this durability.</summary>
        public const float UnifiedSturdiness = 500f;
        /// <summary>Baihu leg armour passive movement-speed multiplier (120%).</summary>
        public const float BaihuMovementSpeedFactor = 1.2f;
        /// <summary>Zhuque boots movement-speed multiplier (150%).</summary>
        public const float ZhuqueMovementSpeedFactor = 1.5f;

        // x25.01.17 body-temperature thresholds, verified by decompiling
        // ComponentVitalStats.UpdateTemperature and ComponentFlu.Update:
        //   comfortable neutral ............ 12
        //   flu onset gate ................. Temperature < 6 (sustained cold accumulates m_fluOnset)
        //   cold damage tick ............... Temperature < 3 (index 26), <= 0 severe (index 25)
        //   ice screen overlay ............. Temperature < 6
        //   overheat warning / damage ...... Temperature > 20 / > 24 (index 35)
        // The floor therefore has to clear 6, not 3, or the player still catches a cold.
        /// <summary>Body-temperature floor enforced while wearing Xuanwu: cold- and flu-immune.</summary>
        public const float XuanwuTemperatureFloor = 8f;
        /// <summary>Body-temperature ceiling enforced while wearing Zhuque: overheat-immune.</summary>
        public const float ZhuqueTemperatureCeiling = 8f;

        /// <summary>
        /// Clamps body temperature for the Four Saints temperature control. Xuanwu raises a too-cold
        /// body to <see cref="XuanwuTemperatureFloor"/> (above the flu gate at 6, so no cold damage,
        /// no ice overlay and no catching a cold); Zhuque lowers a too-hot body to
        /// <see cref="ZhuqueTemperatureCeiling"/> (far below the overheat damage at 24). The floor is
        /// applied first and the ceiling second, so wearing both pins temperature to the shared safe
        /// value. Pure and side-effect free for unit testing.
        /// </summary>
        /// <param name="temperature">Current body temperature.</param>
        /// <param name="xuanwu">True while the Xuanwu torso armour is worn.</param>
        /// <param name="zhuque">True while the Zhuque boots are worn.</param>
        /// <returns>The body temperature after Four Saints clamping.</returns>
        public static float ClampTemperature(float temperature, bool xuanwu, bool zhuque)
        {
            float result = temperature;
            if (xuanwu && result < XuanwuTemperatureFloor)
            {
                result = XuanwuTemperatureFloor;
            }

            if (zhuque && result > ZhuqueTemperatureCeiling)
            {
                result = ZhuqueTemperatureCeiling;
            }

            return result;
        }

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

        public static bool IsXuanwuEnvironmentalImmunity(bool equipped, string cause, string drowningCause, string lethalColdCause, string freezingCause)
        {
            return equipped && (cause == drowningCause || cause == lethalColdCause || cause == freezingCause);
        }
    }
}
#pragma warning restore 1591
