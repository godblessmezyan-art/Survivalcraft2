using System;

namespace SCIEPort.Features.Fishing
{
    /// <summary>Timing, reward and authority limits for the fishing minigame.</summary>
    internal static class FishingRules
    {
        internal const float MaxCastDistance = 12f;
        internal const float MaxCatchDistance = 16f;
        internal const int RequiredWaterDepth = 2;
        internal const float MinWaitDuration = 2f;
        internal const float MaxWaitDuration = 5f;
        internal const float BiteWindow = 0.9f;
        internal const float FailCooldown = 1f;
        internal const float HostMinInterval = 0.8f;
        internal const int HostMaxCatchesPerMinute = 18;
        internal const float MaxSessionDistance = 20f;

        internal enum ReelResult
        {
            Early,
            Success,
            Late
        }

        internal static float WaitDuration(Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            return MinWaitDuration + (float)random.NextDouble() * (MaxWaitDuration - MinWaitDuration);
        }

        internal static ReelResult Reel(float elapsed, float wait, float window)
        {
            if (elapsed < wait) return ReelResult.Early;
            return elapsed <= wait + window ? ReelResult.Success : ReelResult.Late;
        }

        internal static int RewardCount(int roll)
        {
            if (roll < 70) return 1;
            return roll < 95 ? 2 : 3;
        }

        internal static bool IsWaterDepthValid(int waterCells)
        {
            return waterCells >= RequiredWaterDepth;
        }

        internal static bool ShouldEndSession(float distance)
        {
            return distance > MaxSessionDistance;
        }
    }

    /// <summary>Per-player host-side success limiter.</summary>
    internal sealed class FishingRateLimiter
    {
        private double m_lastCatch = double.NegativeInfinity;
        private double m_windowStart = double.NegativeInfinity;
        private int m_windowCount;

        internal bool TryAccept(double now)
        {
            if (now - m_lastCatch < FishingRules.HostMinInterval) return false;
            if (now - m_windowStart >= 60d)
            {
                m_windowStart = now;
                m_windowCount = 0;
            }
            if (m_windowCount >= FishingRules.HostMaxCatchesPerMinute) return false;
            m_lastCatch = now;
            m_windowCount++;
            return true;
        }

        internal void Reset()
        {
            m_lastCatch = double.NegativeInfinity;
            m_windowStart = double.NegativeInfinity;
            m_windowCount = 0;
        }
    }
}
