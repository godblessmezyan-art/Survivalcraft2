using System;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>
    /// Engine-independent policy for 雷霆王域 (Thunder Dominion): a 7 s, 8-block domain that
    /// follows the caster and pays exactly one 12-power tick per whole second, seven times.
    /// </summary>
    internal static class StormhammerDomainRules
    {
        public const float Cooldown = 24f;
        public const float Duration = 7f;
        public const float Radius = 8f;
        public const float TickPower = 12f;
        public const int TickCount = 7;
        /// <summary>Visual-only: the ground ring expands from the caster to the full radius.</summary>
        public const float ExpandTime = 0.35f;
        public const float CollapseTime = 0.6f;

        /// <summary>Full ticks that have elapsed. Frame rate can never add or skip a tick.</summary>
        public static int TicksDue(double elapsed)
        {
            if (double.IsNaN(elapsed) || elapsed < 0.0) return 0;
            double due = Math.Floor(elapsed) + 1.0;
            return due >= TickCount ? TickCount : (int)due;
        }

        /// <summary>True while the domain is on the authoritative timeline.</summary>
        public static bool Active(double elapsed) => elapsed >= 0.0 && elapsed < Duration;

        /// <summary>Domain cleanup: duration, death, unequip, leaving the world or disposal.</summary>
        public static bool ShouldEnd(double elapsed, bool alive, bool equipped) => !alive || !equipped || elapsed >= Duration;

        /// <summary>0→1 expansion of the visual ring during the first ExpandTime seconds.</summary>
        public static float Expand(double elapsed)
        {
            if (elapsed <= 0.0) return 0f;
            return elapsed >= ExpandTime ? 1f : (float)(elapsed / ExpandTime);
        }

        /// <summary>1→0 collapse of the visual ring in the final CollapseTime seconds.</summary>
        public static float Collapse(double elapsed)
        {
            double left = Duration - elapsed;
            if (left >= CollapseTime) return 0f;
            if (left <= 0.0) return 1f;
            return (float)(1.0 - left / CollapseTime);
        }

        /// <summary>Overall opacity of the domain floor, closed at both ends.</summary>
        public static float Alpha(double elapsed)
            => Active(elapsed) ? Math.Min(1f, Math.Min((float)elapsed / 0.12f, (float)(Duration - elapsed) / 0.25f)) : 0f;

        /// <summary>Radius of the drawn ring: expands, holds, then collapses.</summary>
        public static float DrawnRadius(double elapsed)
            => Radius * Math.Max(0f, Expand(elapsed) - Collapse(elapsed));
    }
}
