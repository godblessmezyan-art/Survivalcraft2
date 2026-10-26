using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>Owns bounded glow points and effect emission for Stormhammer.</summary>
    internal static class StormhammerVisuals
    {
        private sealed class GlowState
        {
            public SubsystemGlow Subsystem;
            public GlowPoint Point;
            public int LastFrame;
        }

        private static readonly Dictionary<Project, List<GlowState>> s_glows = new Dictionary<Project, List<GlowState>>();

        public static void TouchModelGlow(DrawBlockEnvironmentData environmentData, Matrix matrix, float size, bool awakened = false)
        {
            if (environmentData == null || environmentData.SubsystemTerrain == null || environmentData.DrawBlockMode == DrawBlockMode.UI)
            {
                return;
            }

            Project project = environmentData.SubsystemTerrain.Project;
            List<GlowState> states;
            if (!s_glows.TryGetValue(project, out states))
            {
                states = new List<GlowState>();
                s_glows.Add(project, states);
            }

            float pulse = 0.5f + 0.16f * MathUtils.Sin((float)Time.RealTime * 2.8f);
            float ultimate = awakened ? AwakenedStormhammerVisuals.GetStormBlend() : 0f;
            // DrawBlock's matrix is camera-space in first/third person. The engine
            // also provides the corresponding world matrix explicitly.
            Matrix world = environmentData.InWorldMatrix;
            Vector3 position = world.Translation + world.Up * (0.60f * size);
            bool overload = awakened && AwakenedStormhammerVisuals.IsOverchargedNear(project, position);
            GlowState state = null;
            foreach (GlowState candidate in states)
            {
                if (candidate.LastFrame != Time.FrameIndex && Vector3.DistanceSquared(candidate.Point.Position, position) < 0.36f)
                {
                    state = candidate;
                    break;
                }
            }
            if (state == null)
            {
                SubsystemGlow subsystem = project.FindSubsystem<SubsystemGlow>(false);
                if (subsystem == null || states.Count >= 24) return;
                state = new GlowState { Subsystem = subsystem, Point = subsystem.AddGlowPoint() };
                state.Point.Type = GlowPointType.Round;
                states.Add(state);
            }
            state.Point.Position = position;
            state.Point.Right = Vector3.UnitX;
            state.Point.Up = Vector3.UnitY;
            state.Point.Forward = Vector3.UnitZ;
            state.Point.Color = overload
                ? new Color(196, 232, 255, (int)(MathUtils.Min(255f, 214f * pulse + 40f)))
                : awakened
                    ? new Color(76, 168, 255, (int)(MathUtils.Min(255f, 190f * pulse + 74f * ultimate)))
                    : new Color(34, 174, 255, (int)(150f * pulse));
            float baseSize = awakened ? 0.21f + 0.09f * ultimate : 0.16f;
            state.Point.Size = (overload ? baseSize * 1.45f : baseSize) * MathUtils.Clamp(size, 0.25f, 1.4f);
            state.Point.FarSize = state.Point.Size;
            state.LastFrame = Time.FrameIndex;
        }

        public static void Update()
        {
            List<Project> empty = null;
            foreach (KeyValuePair<Project, List<GlowState>> pair in s_glows)
            {
                for (int i = pair.Value.Count - 1; i >= 0; i--)
                {
                    GlowState state = pair.Value[i];
                    if (Time.FrameIndex - state.LastFrame > 2)
                    {
                        state.Subsystem.RemoveGlowPoint(state.Point);
                        pair.Value.RemoveAt(i);
                    }
                }
                if (pair.Value.Count == 0) (empty ?? (empty = new List<Project>())).Add(pair.Key);
            }
            if (empty != null) foreach (Project project in empty) s_glows.Remove(project);
        }

        public static void EmitArc(Project project, Vector3 start, Vector3 end, float strength)
        {
            SubsystemParticles particles = project.FindSubsystem<SubsystemParticles>(false);
            if (particles != null)
            {
                particles.AddParticleSystem(StormhammerParticleSystem.CreateArc(start, end, strength));
            }
        }

        public static void EmitBurst(Project project, Vector3 position, float strength)
        {
            SubsystemParticles particles = project.FindSubsystem<SubsystemParticles>(false);
            if (particles != null)
            {
                particles.AddParticleSystem(StormhammerParticleSystem.CreateBurst(position, strength));
            }
        }
    }
}
