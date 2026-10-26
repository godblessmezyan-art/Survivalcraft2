using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.Stormhammer
{
    internal static class StormhammerOptions
    {
        public static bool ReducedEffects;
    }

    /// <summary>Client-safe presentation for Skywrath V2; never changes terrain or world time.</summary>
    internal static class AwakenedStormhammerVisuals
    {
        private sealed class StormState
        {
            public double Start;
            public double End;
            public Vector3 Center;
            public double NextFlash;
            public int FlashIndex;
        }

        private static readonly Dictionary<Project, StormState> s_storms = new Dictionary<Project, StormState>();

        public static void Apply(Project project, StormhammerSkillPackage package)
        {
            SubsystemAudio audio = project.FindSubsystem<SubsystemAudio>(false);
            switch (package.Event)
            {
                case StormhammerSkillEvent.LanceFlight:
                    SubsystemParticles lanceParticles = project.FindSubsystem<SubsystemParticles>(false);
                    lanceParticles?.AddParticleSystem(new ThunderSpearModelParticleSystem(project, package.Position, package.Direction));
                    StormhammerVisuals.EmitArc(project, package.Position, package.Direction, 1.55f);
                    EmitRing(project, package.Direction, 0.72f, new Color(75, 225, 255, 245), 0.30f);
                    audio?.PlaySound("Audio/Stormhammer/Charge", 0.86f, 0.12f, package.Position, 7f, false);
                    break;
                case StormhammerSkillEvent.LanceImpact:
                    StormhammerVisuals.EmitArc(project, package.Position, package.Direction, 1.35f);
                    StormhammerVisuals.EmitBurst(project, package.Direction, package.Step == 0 ? 1.5f : 1.0f);
                    if (package.Step == 0) audio?.PlaySound("Audio/Stormhammer/Thunder", 1f, 0.08f, package.Direction, 16f, false);
                    break;
                case StormhammerSkillEvent.DomainStart:
                    EmitRing(project, package.Position, 5f, new Color(48, 195, 255, 210), 0.42f);
                    EmitRing(project, package.Position + Vector3.UnitY * 0.12f, 3.35f, new Color(111, 102, 255, 185), 0.38f);
                    break;
                case StormhammerSkillEvent.UltimateStart:
                    SubsystemTime time = project.FindSubsystem<SubsystemTime>(false);
                    if (time != null && !s_storms.ContainsKey(project))
                    {
                        s_storms.Add(project, new StormState { Start = time.GameTime, End = time.GameTime + 6.5, Center = package.Position, NextFlash = time.GameTime + 0.45 });
                    }
                    EmitRing(project, package.Position, 12f, new Color(91, 91, 255, 185), 0.9f);
                    break;
                case StormhammerSkillEvent.UltimateWarning:
                    EmitRing(project, package.Position, package.Value, new Color(161, 112, 255, 235), 0.62f);
                    EmitRing(project, package.Position, package.Value * 0.55f, new Color(69, 224, 255, 245), 0.62f);
                    break;
                case StormhammerSkillEvent.UltimateStrike:
                    StormhammerVisuals.EmitArc(project, package.Position + Vector3.UnitY * (package.Step == 6 ? 18f : 12f), package.Position, package.Step == 6 ? 2.8f : 1.55f);
                    StormhammerVisuals.EmitBurst(project, package.Position, package.Step == 6 ? 2.5f : 1.25f);
                    EmitRing(project, package.Position, package.Step == 6 ? 4.8f : 2.2f, new Color(115, 220, 255, 245), package.Step == 6 ? 0.9f : 0.45f);
                    audio?.PlaySound("Audio/Stormhammer/Thunder", package.Step == 6 ? 1f : 0.78f, package.Step == 6 ? -0.12f : 0.1f, package.Position, 24f, false);
                    break;
                case StormhammerSkillEvent.UltimateEnd:
                    break;
            }
        }

        public static void Update(Project project, double now)
        {
            StormState state;
            if (s_storms.TryGetValue(project, out state) && now >= state.End)
            {
                s_storms.Remove(project);
            }
            else if (state != null && now >= state.NextFlash)
            {
                float angle = state.FlashIndex * 2.399963f;
                Vector3 offset = new Vector3(MathUtils.Cos(angle) * 18f, 0f, MathUtils.Sin(angle) * 18f);
                StormhammerVisuals.EmitArc(project, state.Center + offset + Vector3.UnitY * 18f, state.Center + offset + Vector3.UnitY * 7f, 0.7f);
                state.FlashIndex++;
                state.NextFlash = now + (StormhammerOptions.ReducedEffects ? 1.25 : 0.68);
            }
        }

        public static float GetStormBlend()
        {
            float best = 0f;
            foreach (KeyValuePair<Project, StormState> pair in s_storms)
            {
                SubsystemTime time = pair.Key.FindSubsystem<SubsystemTime>(false);
                if (time == null) continue;
                double now = time.GameTime;
                float fadeIn = MathUtils.Saturate((float)((now - pair.Value.Start) / 0.9));
                float fadeOut = MathUtils.Saturate((float)((pair.Value.End - now) / 1.1));
                best = MathUtils.Max(best, MathUtils.Min(fadeIn, fadeOut));
            }
            return best;
        }

        private static void EmitRing(Project project, Vector3 position, float radius, Color color, float lifetime)
        {
            SubsystemParticles particles = project.FindSubsystem<SubsystemParticles>(false);
            if (particles != null)
            {
                int count = StormhammerOptions.ReducedEffects ? 18 : 42;
                particles.AddParticleSystem(StormhammerParticleSystem.CreateRing(position, radius, color, lifetime, count));
            }
        }
    }
}
