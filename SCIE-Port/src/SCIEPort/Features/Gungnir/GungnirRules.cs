using System;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>Weapon balance and bounded, engine-independent combat policy.</summary>
    public static class GungnirRules
    {
        /// <summary>Only these two commands are active skills.</summary>
        public const byte Blessing = 1, Judgment = 2;
        /// <summary>Native interaction commands, never HUD skills.</summary>
        public const byte Melee = 3, Throw = 4;
        /// <summary>Rune Blessing and Eternal Judgment cooldowns, in seconds.</summary>
        public const float BlessingCooldown = 8f, JudgmentCooldown = 50f;
        /// <summary>Delay separating the native white hit and the gold divine follow-up.</summary>
        public const float HolyDamageDelay = 0.4f;
        /// <summary>Substates carried in the existing package Phase byte for reliable long-press throw.</summary>
        internal const byte ThrowBegin = 1, ThrowComplete = 2, ThrowCancel = 3;
        /// <summary>Maximum outbound distance, in terrain cells.</summary>
        public const float ThrowRange = 40f;
        /// <summary>Outbound flight velocity.</summary>
        public const float ThrowSpeed = 35f;
        /// <summary>Maximum distinct creatures pierced by one flight.</summary>
        public const int MaxTargets = 8;
        /// <summary>Original hit gate divided by additive attack rate.</summary>
        public static double AttackInterval(bool blessed, float externalFrequency = 1f) => 0.6600000262260437 / ((blessed ? 2.0 : 1.5) * Math.Max(1f, externalFrequency));
        /// <summary>Additive multipliers relative to the unmodified equipment baseline.</summary>
        public static float SpeedFactor(bool blessed) => blessed ? 2f : 1.5f;
        /// <summary>Linear radial falloff, with no damage outside the legal sphere.</summary>
        public static float JudgmentDamage(float distance) => float.IsNaN(distance) || distance < 0 || distance > 7 ? 0 : 300f - 150f * distance / 7f;
        /// <summary>Native gravity is 10 cells/s²; locomotion applies the level factor separately.</summary>
        public static float JumpSpeed(float levelFactor) => (float)Math.Sqrt(80) / Math.Max(0.1f, 1 + 0.25f * (levelFactor - 1));
        /// <summary>Wrap-safe sequence validation with a half-window rejection.</summary>
        public static bool IsNewer(uint value, uint previous) => value != previous && unchecked(value - previous) < 0x80000000u;
        internal static bool IsThrowSignal(byte signal) => signal >= ThrowBegin && signal <= ThrowCancel;
        internal static bool CanCompleteThrow(bool began, double serverCharge, float reportedCharge) => began
            && serverCharge >= 0.0 && reportedCharge >= 0f && reportedCharge <= 10f
            && (serverCharge >= 0.25 || reportedCharge >= 0.25f);
        /// <summary>In-progress must return false or x25.01.17 immediately cancels aiming.</summary>
        public static bool ShouldConsumeAim(bool inProgress) => !inProgress;
    }
}
