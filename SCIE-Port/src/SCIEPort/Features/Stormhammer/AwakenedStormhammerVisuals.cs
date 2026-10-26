using System.Collections.Generic;
using Engine;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.Stormhammer
{
    internal enum StormhammerEffectQuality
    {
        Low,
        Medium,
        High
    }

    internal static class StormhammerOptions
    {
        public static StormhammerEffectQuality Quality = StormhammerEffectQuality.High;
        public static bool ReducedFlashes;
        // Reserved until a camera-offset hook is verified in the target SCNET ABI.
        public static bool CameraShake;
        public static bool Diagnostics;
    }

    /// <summary>Layered client presentation only; never writes weather, time, terrain or damage.</summary>
    internal static class AwakenedStormhammerVisuals
    {
        private sealed class StormState
        {
            public uint CastId;
            public double Start;
            public double End;
            public Vector3 Center;
            public double NextFlash;
            public int FlashIndex;
        }

        private sealed class AvatarGlow
        {
            public SubsystemGlow Subsystem;
            public readonly List<GlowPoint> Points = new List<GlowPoint>();
            public Vector4? OriginalEmission;
            public int LastFrame;
            public double NextArc;
            public Vector3 LastCenter;
        }

        private static readonly Dictionary<Project, List<StormState>> s_storms = new Dictionary<Project, List<StormState>>();
        private static readonly Dictionary<Project, Dictionary<ulong, double>> s_seenEvents = new Dictionary<Project, Dictionary<ulong, double>>();
        private static readonly Dictionary<ComponentModel, AvatarGlow> s_avatars = new Dictionary<ComponentModel, AvatarGlow>();

        public static void Apply(Project project, StormhammerSkillPackage package)
        {
            if (package.CastId != 0u && !AcceptEvent(project, package)) return;
            SubsystemAudio audio = project.FindSubsystem<SubsystemAudio>(false);
            switch (package.Event)
            {
                case StormhammerSkillEvent.JudgmentCharge:
                    StormhammerVisuals.EmitBurst(project, package.Position + Vector3.UnitY * 1.2f, 0.9f);
                    audio?.PlaySound("Audio/Stormhammer/Charge", 0.9f, -0.08f, package.Position, 10f, false);
                    break;
                case StormhammerSkillEvent.JudgmentBurst:
                    EmitJudgment(project, package.Position, package.CastId);
                    audio?.PlaySound("Audio/Stormhammer/Thunder", 1f, -0.02f, package.Position, 22f, false);
                    break;
                case StormhammerSkillEvent.UltimateStart:
                    AddStorm(project, package.CastId, package.Position, package.ServerTime, package.Value);
                    EmitRuneField(project, package.Position, 7f, 1.5f);
                    audio?.PlaySound("Audio/Stormhammer/Charge", 1f, -0.18f, package.Position, 24f, false);
                    break;
                case StormhammerSkillEvent.UltimateLock:
                    EmitLock(project, package.Position, package.Step);
                    break;
                case StormhammerSkillEvent.UltimateStrike:
                    EmitSkyBolt(project, package.Position, false, package.CastId + package.Step);
                    audio?.PlaySound("Audio/Stormhammer/Thunder", 0.78f, 0.12f, package.Position, 28f, false);
                    break;
                case StormhammerSkillEvent.UltimateMainWarning:
                    EmitRuneField(project, package.Position, 7f, 0.8f);
                    break;
                case StormhammerSkillEvent.UltimateMainStrike:
                    EmitSkyBolt(project, package.Position, true, package.CastId);
                    audio?.PlaySound("Audio/Stormhammer/Thunder", 1f, -0.22f, package.Position, 40f, false);
                    break;
                case StormhammerSkillEvent.UltimateAftershock:
                    EmitAftershock(project, package.Position, package.Step);
                    break;
                case StormhammerSkillEvent.UltimateEnd:
                    EndStorm(project, package.CastId);
                    audio?.PlaySound("Audio/Stormhammer/Charge", 0.58f, -0.3f, package.Position, 18f, false);
                    break;
            }
        }

        private static bool AcceptEvent(Project project, StormhammerSkillPackage package)
        {
            Dictionary<ulong, double> seen;
            if (!s_seenEvents.TryGetValue(project, out seen))
            {
                seen = new Dictionary<ulong, double>();
                s_seenEvents.Add(project, seen);
            }
            double now = Time.RealTime;
            if (seen.Count > 192)
            {
                List<ulong> expired = new List<ulong>();
                foreach (KeyValuePair<ulong, double> pair in seen)
                    if (pair.Value <= now) expired.Add(pair.Key);
                foreach (ulong key in expired) seen.Remove(key);
            }
            ulong eventKey = package.CastId;
            eventKey = eventKey * 1099511628211UL ^ (uint)package.Event;
            eventKey = eventKey * 1099511628211UL ^ (uint)package.Step;
            eventKey = eventKey * 1099511628211UL ^ (uint)package.TargetId;
            double expiry;
            if (seen.TryGetValue(eventKey, out expiry) && expiry > now) return false;
            seen[eventKey] = now + 20.0;
            return true;
        }

        public static void TouchAvatar(ComponentModel model, Camera camera)
        {
            ComponentHumanModel human = model as ComponentHumanModel;
            if (human == null || model.Entity == null) return;
            ComponentPlayer player = model.Entity.FindComponent<ComponentPlayer>(false);
            if (!SubsystemAwakenedStormhammerBlockBehavior.IsEquipped(player) || player.ComponentHealth.Health <= 0f)
            {
                ReleaseAvatar(model);
                return;
            }
            AvatarGlow state;
            if (!s_avatars.TryGetValue(model, out state))
            {
                SubsystemGlow glow = model.Project.FindSubsystem<SubsystemGlow>(false);
                if (glow == null) return;
                state = new AvatarGlow { Subsystem = glow, OriginalEmission = model.EmissionColor };
                int count = StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 3 : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 5 : 7;
                for (int i = 0; i < count; i++)
                {
                    GlowPoint point = glow.AddGlowPoint();
                    point.Type = GlowPointType.Round;
                    state.Points.Add(point);
                }
                s_avatars.Add(model, state);
            }
            state.LastFrame = Time.FrameIndex;
            float pulse = 0.78f + 0.22f * MathUtils.Sin((float)Time.RealTime * 2.15f + player.Entity.EntityId * 0.11f);
            float ultimate = GetStormBlend();
            model.EmissionColor = new Vector4(0.035f + 0.025f * ultimate, 0.13f + 0.12f * ultimate, 0.22f + 0.16f * ultimate, 0f) * pulse;
            Vector3 center = player.ComponentBody.BoundingBox.Center();
            Matrix bodyMatrix = player.ComponentBody.Matrix;
            Vector3[] anchors =
            {
                BoneWorld(human, human.m_headBone != null ? human.m_headBone.Index : -1, camera, center + bodyMatrix.Up * 0.78f),
                BoneWorld(human, human.m_hand1Bone != null ? human.m_hand1Bone.Index : -1, camera, center - bodyMatrix.Right * 0.34f + bodyMatrix.Up * 0.38f),
                BoneWorld(human, human.m_hand2Bone != null ? human.m_hand2Bone.Index : -1, camera, center + bodyMatrix.Right * 0.34f + bodyMatrix.Up * 0.38f),
                BoneWorld(human, human.m_bodyBone != null ? human.m_bodyBone.Index : -1, camera, center + bodyMatrix.Up * 0.14f),
                BoneWorld(human, human.m_leg1Bone != null ? human.m_leg1Bone.Index : -1, camera, center - bodyMatrix.Right * 0.20f - bodyMatrix.Up * 0.42f),
                BoneWorld(human, human.m_leg2Bone != null ? human.m_leg2Bone.Index : -1, camera, center + bodyMatrix.Right * 0.20f - bodyMatrix.Up * 0.42f),
                center - bodyMatrix.Up * 0.68f
            };
            for (int i = 0; i < state.Points.Count; i++)
            {
                Vector3 p = anchors[i];
                GlowPoint glow = state.Points[i];
                glow.Position = p;
                glow.Right = bodyMatrix.Right;
                glow.Up = bodyMatrix.Up;
                glow.Forward = bodyMatrix.Forward;
                glow.Size = (i == 0 ? 0.34f : 0.24f) * pulse * (1f + 0.35f * ultimate);
                glow.FarSize = glow.Size * 1.5f;
                glow.FarDistance = 14f;
                glow.Color = new Color(62, 198, 255, StormhammerOptions.ReducedFlashes ? 55 : 82);
            }
            double interval = StormhammerOptions.Quality == StormhammerEffectQuality.High ? 0.18 : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 0.32 : 0.55;
            if (Time.RealTime >= state.NextArc)
            {
                int a = (Time.FrameIndex + player.Entity.EntityId) % state.Points.Count;
                int b = (a + 2 + Time.FrameIndex % 3) % state.Points.Count;
                StormhammerVisuals.EmitArc(model.Project, state.Points[a].Position, state.Points[b].Position, 0.48f);
                if (Vector3.DistanceSquared(center, state.LastCenter) > 0.03f && StormhammerOptions.Quality != StormhammerEffectQuality.Low)
                    StormhammerVisuals.EmitArc(model.Project, state.LastCenter, center, 0.28f);
                state.NextArc = Time.RealTime + interval;
                state.LastCenter = center;
            }
        }

        public static void Update(Project project, double now)
        {
            List<StormState> list;
            if (s_storms.TryGetValue(project, out list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    StormState state = list[i];
                    if (now >= state.End) { list.RemoveAt(i); continue; }
                    if (now >= state.NextFlash)
                    {
                        float angle = state.FlashIndex * 2.399963f;
                        Vector3 offset = new Vector3(MathUtils.Cos(angle) * 22f, 0f, MathUtils.Sin(angle) * 22f);
                        StormhammerVisuals.EmitArc(project, state.Center + offset + Vector3.UnitY * 22f, state.Center + offset + Vector3.UnitY * 6f, 0.8f);
                        state.FlashIndex++;
                        state.NextFlash = now + (StormhammerOptions.Quality == StormhammerEffectQuality.High ? 0.72 : 1.25);
                    }
                }
                if (list.Count == 0) s_storms.Remove(project);
            }
            List<ComponentModel> stale = null;
            foreach (KeyValuePair<ComponentModel, AvatarGlow> pair in s_avatars)
            {
                if (pair.Key.Project == project && Time.FrameIndex - pair.Value.LastFrame > 3) (stale ?? (stale = new List<ComponentModel>())).Add(pair.Key);
            }
            if (stale != null) foreach (ComponentModel model in stale) ReleaseAvatar(model);
        }

        public static float GetStormBlend()
        {
            float combined = 0f;
            foreach (KeyValuePair<Project, List<StormState>> pair in s_storms)
            {
                SubsystemTime time = pair.Key.FindSubsystem<SubsystemTime>(false);
                if (time == null) continue;
                foreach (StormState state in pair.Value)
                {
                    double now = time.GameTime;
                    float fadeIn = MathUtils.Saturate((float)((now - state.Start) / 1.35));
                    float fadeOut = MathUtils.Saturate((float)((state.End - now) / 1.45));
                    combined = 1f - (1f - combined) * (1f - MathUtils.Min(fadeIn, fadeOut));
                }
            }
            return MathUtils.Saturate(combined);
        }

        private static void EmitJudgment(Project project, Vector3 center, uint seed)
        {
            int arcs = StormhammerOptions.Quality == StormhammerEffectQuality.High ? 12 : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 10 : 8;
            for (int i = 0; i < arcs; i++)
            {
                float angle = 2f * MathUtils.PI * i / arcs + (seed % 17) * 0.013f;
                Vector3 end = center + new Vector3(MathUtils.Cos(angle) * 9f, 0.15f + 0.6f * ((i % 3) / 2f), MathUtils.Sin(angle) * 9f);
                StormhammerVisuals.EmitArc(project, center + Vector3.UnitY * 1.15f, end, 2.1f);
                if (i % 2 == 0) StormhammerVisuals.EmitArc(project, center + new Vector3(MathUtils.Cos(angle) * 4f, 0.35f, MathUtils.Sin(angle) * 4f), end + new Vector3(-MathUtils.Sin(angle), 0.45f, MathUtils.Cos(angle)) * 1.4f, 1.05f);
            }
            StormhammerVisuals.EmitBurst(project, center + Vector3.UnitY * 0.7f, 2.4f);
            EmitRing(project, center, 4f, new Color(205, 248, 255, 250), 0.48f, 38);
            EmitRing(project, center + Vector3.UnitY * 0.16f, 9f, new Color(42, 157, 255, 205), 0.82f, 56);
        }

        private static void EmitLock(Project project, Vector3 target, byte step)
        {
            Vector3 p = target + Vector3.UnitY * 1.25f;
            EmitRing(project, p, 0.65f, new Color(134, 226, 255, 240), 0.72f, 18);
            StormhammerVisuals.EmitArc(project, p + Vector3.UnitY * 3.5f, p, 0.45f);
        }

        private static void EmitSkyBolt(Project project, Vector3 target, bool main, uint seed)
        {
            float height = main ? 28f : 17f;
            StormhammerVisuals.EmitArc(project, target + Vector3.UnitY * height, target, main ? 4.2f : 2.0f);
            int branches = main ? 8 : 3;
            for (int i = 0; i < branches; i++)
            {
                float a = 2f * MathUtils.PI * i / branches + (seed % 11) * 0.07f;
                Vector3 start = target + Vector3.UnitY * (height * (0.45f + 0.05f * (i % 3)));
                Vector3 end = target + new Vector3(MathUtils.Cos(a) * (main ? 4.8f : 1.7f), 0.2f, MathUtils.Sin(a) * (main ? 4.8f : 1.7f));
                StormhammerVisuals.EmitArc(project, start, end, main ? 2.1f : 0.85f);
            }
            StormhammerVisuals.EmitBurst(project, target, main ? 3.4f : 1.55f);
            EmitRing(project, target, main ? 3.4f : 1.5f, new Color(220, 250, 255, 250), main ? 0.62f : 0.38f, main ? 52 : 26);
            if (main) EmitRing(project, target + Vector3.UnitY * 0.18f, 7f, new Color(51, 162, 255, 220), 1.05f, 64);
        }

        private static void EmitRuneField(Project project, Vector3 center, float radius, float lifetime)
        {
            EmitRing(project, center, radius, new Color(83, 126, 255, 205), lifetime, 64);
            EmitRing(project, center + Vector3.UnitY * 0.08f, radius * 0.62f, new Color(83, 231, 255, 235), lifetime, 48);
        }

        private static void EmitAftershock(Project project, Vector3 center, byte step)
        {
            EmitRing(project, center, 7f - step * 0.45f, new Color(65, 185, 255, 170), 0.65f, 42);
            for (int i = 0; i < 4; i++)
            {
                float a = i * MathUtils.PI / 2f + step * 0.31f;
                StormhammerVisuals.EmitArc(project, center + new Vector3(MathUtils.Cos(a) * 0.7f, 0.1f, MathUtils.Sin(a) * 0.7f), center + new Vector3(MathUtils.Cos(a) * 6f, 0.12f, MathUtils.Sin(a) * 6f), 0.65f);
            }
        }

        private static void EmitRing(Project project, Vector3 position, float radius, Color color, float lifetime, int count)
        {
            SubsystemParticles particles = project.FindSubsystem<SubsystemParticles>(false);
            if (particles == null) return;
            int budget = StormhammerOptions.Quality == StormhammerEffectQuality.Low ? count / 3 : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? count * 2 / 3 : count;
            particles.AddParticleSystem(StormhammerParticleSystem.CreateRing(position, radius, color, lifetime, MathUtils.Max(12, budget)));
        }

        private static void AddStorm(Project project, uint castId, Vector3 center, double start, float duration)
        {
            List<StormState> list;
            if (!s_storms.TryGetValue(project, out list)) { list = new List<StormState>(); s_storms.Add(project, list); }
            list.RemoveAll(s => s.CastId == castId);
            list.Add(new StormState { CastId = castId, Start = start, End = start + MathUtils.Max(duration, 10f), Center = center, NextFlash = start + 0.55 });
        }

        private static void EndStorm(Project project, uint castId)
        {
            List<StormState> list;
            if (!s_storms.TryGetValue(project, out list)) return;
            SubsystemTime time = project.FindSubsystem<SubsystemTime>(false);
            foreach (StormState state in list) if (state.CastId == castId) state.End = MathUtils.Max(state.End, (time?.GameTime ?? state.End) + 1.35);
        }

        private static void ReleaseAvatar(ComponentModel model)
        {
            AvatarGlow state;
            if (!s_avatars.TryGetValue(model, out state)) return;
            foreach (GlowPoint point in state.Points) state.Subsystem.RemoveGlowPoint(point);
            model.EmissionColor = state.OriginalEmission;
            s_avatars.Remove(model);
        }

        private static Vector3 BoneWorld(ComponentHumanModel model, int index, Camera camera, Vector3 fallback)
        {
            Matrix[] transforms = model.AbsoluteBoneTransformsForCamera;
            if (index < 0 || transforms == null || index >= transforms.Length) return fallback;
            return Vector3.Transform(transforms[index].Translation, camera.InvertedViewMatrix);
        }
    }
}
