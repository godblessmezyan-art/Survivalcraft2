using System;

namespace SCIEPort.Features.Fishing
{
    /// <summary>Timing, reward and authority limits for the fishing minigame.</summary>
    internal static class FishingRules
    {
        internal const float MaxCastDistance = 12f;
        internal const float MaxCatchDistance = 16f;
        internal const int RequiredWaterDepth = 1;
        internal const float MinWaitDuration = 2f;
        internal const float MaxWaitDuration = 5f;
        internal const float BiteWindow = 1.35f;
        internal const float NetworkGrace = 1f;
        internal const float ReplyTimeout = 8f;
        internal const float FailCooldown = 1f;
        internal const float HostMinInterval = 0.8f;
        internal const int HostMaxCatchesPerMinute = 30;
        internal const float MaxSessionDistance = 20f;

        internal enum ReelResult
        {
            Early,
            Success,
            Late
        }

        internal static float WaitDuration(float roll)
        {
            if (float.IsNaN(roll) || float.IsInfinity(roll)) roll = 0f;
            return MinWaitDuration + Math.Max(0f, Math.Min(1f, roll)) * (MaxWaitDuration - MinWaitDuration);
        }

        internal static ReelResult Reel(float elapsed, float wait, float window)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return ReelResult.Late;
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

        internal static bool NewerSequence(uint sequence, uint previous)
            => sequence != 0 && unchecked((int)(sequence - previous)) > 0;
    }

    /// <summary>Per-player host-side success limiter.</summary>
    internal sealed class FishingRateLimiter
    {
        private double m_lastCatch = double.NegativeInfinity;
        private double m_windowStart = double.NegativeInfinity;
        private int m_windowCount;

        internal bool TryAccept(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return false;
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

    // One host-created round, consumed even on an early/late reel. No replay can grant twice.
    internal sealed class FishingRound
    {
        internal readonly uint Sequence;
        internal readonly double Start;
        internal readonly float Wait;
        internal bool Consumed { get; private set; }
        internal FishingRound(uint sequence, double start, float wait)
        { Sequence = sequence; Start = start; Wait = wait; }
        internal bool TryReel(uint sequence, double now, float grace)
        {
            if (Consumed || sequence != Sequence || double.IsNaN(now) || double.IsInfinity(now)) return false;
            Consumed = true;
            double elapsed = now - Start;
            return elapsed >= Wait && elapsed <= Wait + FishingRules.BiteWindow + grace;
        }
        internal bool Expired(double now, float grace) => now > Start + Wait + FishingRules.BiteWindow + grace;
    }
}
