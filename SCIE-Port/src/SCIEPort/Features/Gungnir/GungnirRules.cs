using System;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>Weapon balance and bounded, engine-independent combat policy.</summary>
    public static class GungnirRules
    {
        /// <summary>Only these commands are active skills. The values are the wire command ids and must not be renumbered.</summary>
        public const byte Blessing = 1, Judgment = 2, Melee = 3, Throw = 4;
        /// <summary>Fate Severance is appended as command 5 so 1-4 keep their wire meaning.</summary>
        public const byte Fatalism = 5;
        /// <summary>Rune Blessing and Eternal Judgment cooldowns, in seconds.</summary>
        public const float BlessingCooldown = 6f, JudgmentCooldown = 50f;
        /// <summary>Rune Blessing lasts nine seconds from the accepted cast.</summary>
        public const float BlessingDuration = 9f;
        /// <summary>Fate Severance: a world-wide edict with a fixed power and cooldown.</summary>
        public const float FatalismCooldown = 39f;
        /// <summary>Fate Severance rolls one power per target inside [10, 100].</summary>
        public const float FatalismPowerMin = 10f, FatalismPowerMax = 100f;
        /// <summary>Eternal Judgment hits a flat 341 attack power inside its legal sphere.</summary>
        public const float JudgmentPower = 341f;
        /// <summary>Presentation timeline shared by the authority and every client.</summary>
        public const float FatalismMark = 0.15f, FatalismImpact = 0.45f, FatalismEnd = 0.9f;
        /// <summary>Package kinds carried in the existing Kind byte.</summary>
        internal const byte KindBlade = 2, KindSound = 3, KindFatalism = 4;
        /// <summary>Delay separating the native white hit and the gold divine follow-up.</summary>
        public const float HolyDamageDelay = 0.4f;
        /// <summary>Substates carried in the existing package Phase byte for reliable long-press throw.</summary>
        internal const byte ThrowBegin = 1, ThrowComplete = 2, ThrowCancel = 3;
        /// <summary>Minimum visible aim time before a release becomes a throw. The engine already
        /// requires a 0.2 s hold before PlayerInput.Aim exists, so 0.15 s means a ~0.35 s press.</summary>
        public const float MinThrowCharge = 0.15f;
        /// <summary>Hard lease for a purely visual flight. Exceeding it force-releases the phase so a
        /// stalled flight can never block the next throw.</summary>
        public const float ThrowLease = 3.2f;
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
        /// <summary>Edge of the judgment sphere, in blocks.</summary>
        public const float JudgmentRadius = 7f;
        /// <summary>Share of the power still dealt at the rim of the sphere.</summary>
        public const float JudgmentEdgeFactor = 0.5f;
        /// <summary>
        /// Linear radial falloff from 341 at the centre to 170.5 at the rim; nothing outside the
        /// legal sphere. Keeping half power at the edge preserves the original 300->150 shape.
        /// </summary>
        public static float JudgmentDamage(float distance) => float.IsNaN(distance) || distance < 0 || distance > JudgmentRadius
            ? 0
            : JudgmentPower * (1f - (1f - JudgmentEdgeFactor) * distance / JudgmentRadius);
        /// <summary>
        /// Uniform Fate Severance roll. The authority supplies an integer roll in [0, 90] from
        /// its own RNG; out-of-range rolls are clamped so the power can never leave [10, 100].
        /// </summary>
        public static float FatalismRoll(int roll)
            => FatalismPowerMin + (roll < 0 ? 0 : roll > 90 ? 90 : roll);
        /// <summary>Native gravity is 10 cells/s²; locomotion applies the level factor separately.</summary>
        public static float JumpSpeed(float levelFactor) => (float)Math.Sqrt(80) / Math.Max(0.1f, 1 + 0.25f * (levelFactor - 1));
        /// <summary>Wrap-safe sequence validation with a half-window rejection.</summary>
        public static bool IsNewer(uint value, uint previous) => value != previous && unchecked(value - previous) < 0x80000000u;
        internal static bool IsThrowSignal(byte signal) => signal >= ThrowBegin && signal <= ThrowCancel;
        internal static bool CanCompleteThrow(bool began, double serverCharge, float reportedCharge) => began
            && serverCharge >= 0.0 && reportedCharge >= 0f && reportedCharge <= 10f
            && (serverCharge >= MinThrowCharge || reportedCharge >= MinThrowCharge);
        /// <summary>In-progress must return false or x25.01.17 immediately cancels aiming.</summary>
        public static bool ShouldConsumeAim(bool inProgress) => !inProgress;
        /// <summary>
        /// Explicit HUD -> command mapping. The array order is the on-screen order, never derived
        /// from the button index, so the ultimate stays command 2 while the new skill is 5.
        /// </summary>
        public static byte HudCommand(int slot) => slot == 0 ? Blessing : slot == 1 ? Fatalism : slot == 2 ? Judgment : (byte)0;
        /// <summary>True only for commands that are artifact skills (not melee/throw).</summary>
        public static bool IsSkillCommand(byte command) => command == Blessing || command == Judgment || command == Fatalism;
        /// <summary>Presentation weight of the edict, closed at both ends of its 0.9 s life.</summary>
        public static float FatalismAlpha(float age)
            => age < 0f || age >= FatalismEnd ? 0f : Math.Min(1f, Math.Min(age / FatalismMark, (FatalismEnd - age) / 0.2f));
        /// <summary>Marks are visible from the mark beat until the shatter beat.</summary>
        public static bool FatalismMarks(float age) => age >= FatalismMark && age < FatalismImpact + 0.2f;
        /// <summary>Shatter flash runs briefly after the single authoritative impact.</summary>
        public static bool FatalismShattered(float age) => age >= FatalismImpact && age < FatalismImpact + 0.2f;
    }
}
