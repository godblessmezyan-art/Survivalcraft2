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

        private static readonly Dictionary<Project, GlowState> s_glows = new Dictionary<Project, GlowState>();

        public static void TouchModelGlow(DrawBlockEnvironmentData environmentData, Matrix matrix, float size, bool awakened = false)
        {
            if (environmentData == null || environmentData.SubsystemTerrain == null || environmentData.DrawBlockMode == DrawBlockMode.UI)
            {
                return;
            }

            Project project = environmentData.SubsystemTerrain.Project;
            GlowState state;
            if (!s_glows.TryGetValue(project, out state))
            {
                SubsystemGlow subsystem = project.FindSubsystem<SubsystemGlow>(false);
                if (subsystem == null)
                {
                    return;
                }

                state = new GlowState { Subsystem = subsystem, Point = subsystem.AddGlowPoint() };
                state.Point.Type = GlowPointType.Round;
                s_glows.Add(project, state);
            }

            float pulse = 0.5f + 0.16f * MathUtils.Sin((float)Time.RealTime * 2.8f);
            // DrawBlock's matrix is camera-space in first/third person. The engine
            // also provides the corresponding world matrix explicitly.
            Matrix world = environmentData.InWorldMatrix;
            state.Point.Position = world.Translation + world.Up * (0.60f * size);
            state.Point.Right = Vector3.UnitX;
            state.Point.Up = Vector3.UnitY;
            state.Point.Forward = Vector3.UnitZ;
            state.Point.Color = awakened
                ? new Color(76, 118, 255, (int)(190f * pulse))
                : new Color(34, 174, 255, (int)(150f * pulse));
            state.Point.Size = (awakened ? 0.21f : 0.16f) * MathUtils.Clamp(size, 0.25f, 1.4f);
            state.Point.FarSize = state.Point.Size;
            state.LastFrame = Time.FrameIndex;
        }

        public static void Update()
        {
            List<Project> expired = null;
            foreach (KeyValuePair<Project, GlowState> pair in s_glows)
            {
                if (Time.FrameIndex - pair.Value.LastFrame > 2)
                {
                    pair.Value.Subsystem.RemoveGlowPoint(pair.Value.Point);
                    if (expired == null)
                    {
                        expired = new List<Project>();
                    }

                    expired.Add(pair.Key);
                }
            }

            if (expired != null)
            {
                foreach (Project project in expired)
                {
                    s_glows.Remove(project);
                }
            }
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
