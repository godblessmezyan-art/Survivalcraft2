using System;

namespace SCIEPort.Features.Fishing
{
    internal static class FishingRules
    {
        internal const float MaxCastDistance = 18f;
        internal const float MaxSessionDistance = 22f;
        internal const int RequiredWaterColumns = 4;
        internal const float BiteWindow = 3f;
        internal const float RemoteBiteGrace = 0.65f;
        internal const float HeldLease = 0.85f;
        internal const float ReelTimeout = 16f;
        internal const float UpdateInterval = 0.2f;
        internal const float CastInterval = 0.45f;
        internal const float ClientTimeout = 4f;

        internal static float WaitDuration(Random random, bool baited)
        {
            return Range(random, baited ? 9f : 12f, baited ? 17f : 22f);
        }

        internal static float ReelDuration(Random random, bool large)
        {
            return Range(random, large ? 6f : 3f, large ? 8f : 5f);
        }

        internal static bool IsLarge(Random random, bool baited) => baited && random.Next(100) < 25;

        internal static byte PackReward(int count, int dropped) => (byte)(count | (dropped << 4));

        private static float Range(Random random, float min, float max)
        {
            return min + (random.Next(int.MaxValue) / (float)int.MaxValue) * (max - min);
        }
    }

    internal enum FishingPhase
    {
        Waiting,
        Approach,
        Bite,
        Reeling,
        Landed,
        Escaped
    }

    /// <summary>Authoritative cycle driven by a monotonic clock. Input signals cannot advance its clock.</summary>
    internal sealed class FishingCycle
    {
        private readonly double m_biteAt;
        private readonly bool m_remote;
        private readonly bool m_large;
        private readonly float m_duration;
        private double m_lastAdvance;
        private double m_hookDeadline;
        private double m_reelDeadline;
        private double m_heldUntil;
        private bool m_held;
        private int m_surges;
        private float m_strain;
        private float m_slack;

        internal FishingPhase Phase { get; private set; }
        internal float Progress { get; private set; }
        internal bool Tense { get; private set; }
        internal int Reward => m_large ? 2 : 1;

        internal FishingCycle(double now, float wait, float duration, bool large, bool remote)
        {
            m_biteAt = now + wait;
            m_duration = duration;
            m_large = large;
            m_remote = remote;
            m_lastAdvance = now;
        }

        internal void Advance(double now)
        {
            if (now < m_lastAdvance) return;
            double previous = m_lastAdvance;
            m_lastAdvance = now;
            switch (Phase)
            {
                case FishingPhase.Waiting:
                case FishingPhase.Approach:
                    if (now >= m_biteAt)
                    {
                        Phase = FishingPhase.Bite;
                        m_hookDeadline = now + FishingRules.BiteWindow + (m_remote ? FishingRules.RemoteBiteGrace : 0f);
                    }
                    else if (now >= m_biteAt - 0.9)
                        Phase = FishingPhase.Approach;
                    break;
                case FishingPhase.Bite:
                    if (now > m_hookDeadline) Phase = FishingPhase.Escaped;
                    break;
                case FishingPhase.Reeling:
                    if (now >= m_reelDeadline) Phase = FishingPhase.Escaped;
                    else AdvanceReel(previous, now);
                    break;
            }
        }

        internal bool Hook(double now, bool held)
        {
            // The bite must already have been emitted by the host.
            if (Phase != FishingPhase.Bite || now > m_hookDeadline) return false;
            Phase = FishingPhase.Reeling;
            m_lastAdvance = now;
            m_reelDeadline = now + FishingRules.ReelTimeout;
            SetHeld(now, held);
            return true;
        }

        internal void SetHeld(double now, bool held)
        {
            if (Phase != FishingPhase.Reeling) return;
            Advance(now);
            m_held = held;
            m_heldUntil = m_remote ? now + FishingRules.HeldLease : double.PositiveInfinity;
        }

        private void AdvanceReel(double previous, double now)
        {
            float elapsed = (float)(now - previous);
            float pulling = m_held ? (float)Math.Max(0, Math.Min(now, m_heldUntil) - previous) : 0f;
            if (Tense)
            {
                m_strain = Math.Max(0, m_strain + pulling - (elapsed - pulling));
                if (pulling > 0f) m_slack = 0f;
                m_slack += elapsed - pulling;
                if (m_strain >= 1.8f)
                    Phase = FishingPhase.Escaped;
                else if (m_slack >= 0.55f)
                {
                    Tense = false;
                    m_strain = 0f;
                    m_slack = 0f;
                }
                return;
            }

            Progress = Math.Min(1f, Progress + pulling / m_duration);
            if (m_large && m_surges < 2 && Progress >= (m_surges + 1) * 0.33f)
            {
                Progress = (m_surges + 1) * 0.33f;
                m_surges++;
                Tense = true;
            }
            else if (Progress >= 1f)
                Phase = FishingPhase.Landed;
        }
    }
}
