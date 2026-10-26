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
            public GlowPoint[] Corona;
            public int LastFrame;
        }

        private static readonly Dictionary<Project, List<GlowState>> s_glows = new Dictionary<Project, List<GlowState>>();
        private static readonly Dictionary<Project, double> s_nextArc = new Dictionary<Project, double>();

        /// <summary>
        /// Places the hammer glow, and for Mjolnir a three point corona that orbits the head and
        /// crackles between its own points.
        /// </summary>
        /// <remarks>
        /// The awakened form reuses the base hammer mesh (see StormhammerAwakenedBlock), so every
        /// bit of its "awakened" identity has to come from these live effects: a brighter head glow,
        /// an orbiting spark ring, and short arcs linking the ring. During the Thunder God window the
        /// ring tightens, brightens to white-blue and the arcs fire roughly twice as often, which is
        /// the overloaded state the player sees.
        /// </remarks>
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
                if (awakened)
                {
                    int ring = CoronaCount();
                    state.Corona = new GlowPoint[ring];
                    for (int i = 0; i < ring; i++)
                    {
                        GlowPoint point = subsystem.AddGlowPoint();
                        point.Type = GlowPointType.Round;
                        state.Corona[i] = point;
                    }
                }

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

            if (awakened && state.Corona != null)
            {
                DriveCorona(project, state, world, position, size, pulse, ultimate, overload);
            }
        }

        private static int CoronaCount()
        {
            return StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 2 : 3;
        }

        private static void DriveCorona(
            Project project,
            GlowState state,
            Matrix world,
            Vector3 position,
            float size,
            float pulse,
            float ultimate,
            bool overload)
        {
            float scale = MathUtils.Clamp(size, 0.25f, 1.4f);
            // The ring tightens as the hammer overloads, which reads as the field contracting
            // before it discharges.
            float radius = (overload ? 0.075f : 0.105f - 0.015f * ultimate) * scale;
            float spin = overload ? 7.4f : 4.2f;
            double now = Time.RealTime;
            Vector3 right = world.Right;
            Vector3 up = world.Up;
            Vector3 forward = world.Forward;
            int ring = state.Corona.Length;
            float pointSize = (overload ? 0.062f : 0.044f + 0.010f * ultimate) * pulse * scale + 0.012f;
            Color pointColor = overload
                ? new Color(232, 248, 255, StormhammerOptions.ReducedFlashes ? 150 : 205)
                : new Color(96, 196, 255, StormhammerOptions.ReducedFlashes ? 110 : 160);

            for (int i = 0; i < ring; i++)
            {
                float angle = (float)(now * spin) + i * (MathUtils.PI * 2f / ring);
                Vector3 radial = right * MathUtils.Cos(angle) + forward * MathUtils.Sin(angle);
                GlowPoint point = state.Corona[i];
                point.Position = position + radial * radius + up * (MathUtils.Sin(angle * 1.7f) * 0.035f * scale);
                point.Right = right;
                point.Up = up;
                point.Forward = forward;
                point.Size = pointSize;
                point.FarSize = pointSize * 1.4f;
                point.FarDistance = 10f;
                point.Color = pointColor;
            }

            double next;
            if (s_nextArc.TryGetValue(project, out next) && now < next)
            {
                return;
            }

            LightningScene scene = AwakenedStormhammerVisuals.GetScene(project);
            if (scene == null)
            {
                return;
            }

            // Rates are deliberately low: this runs from DrawBlock, and a per-frame bolt would
            // saturate the pool and drown the skill effects in noise.
            double interval = overload
                ? (StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 0.18 : 0.08)
                : (StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 0.34 : 0.17);
            s_nextArc[project] = now + interval;

            float coreWidth = (overload ? 0.055f : 0.036f) * scale;
            float haloWidth = (overload ? 0.16f : 0.10f) * scale;
            Color core = overload ? new Color(240, 252, 255, 235) : new Color(206, 236, 255, 200);
            Color halo = overload ? new Color(96, 194, 255, 165) : new Color(64, 162, 255, 120);
            uint seed = LightningRules.Mix((uint)(now * 977.0), (uint)(position.X * 31f + position.Z * 17f));

            for (int i = 0; i < ring; i++)
            {
                int next2 = (i + 1) % ring;
                scene.Spawn(
                    LightningBoltKind.Radial,
                    state.Corona[i].Position,
                    state.Corona[next2].Position,
                    LightningRules.Mix(seed, (uint)(0x40 + i)),
                    now,
                    0.10,
                    coreWidth,
                    haloWidth,
                    core,
                    halo,
                    0,
                    0f,
                    false);
            }

            if (overload && ring > 2)
            {
                scene.Spawn(
                    LightningBoltKind.Radial,
                    state.Corona[0].Position,
                    state.Corona[2].Position,
                    LightningRules.Mix(seed, 0x51u),
                    now,
                    0.12,
                    coreWidth * 1.15f,
                    haloWidth * 1.1f,
                    core,
                    halo,
                    0,
                    0f,
                    false);
            }
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
                        if (state.Corona != null)
                        {
                            foreach (GlowPoint point in state.Corona) state.Subsystem.RemoveGlowPoint(point);
                        }

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
