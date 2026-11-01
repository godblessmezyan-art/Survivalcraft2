using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Stormhammer;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>Pooled, quality-scaled Gungnir presentation. It never applies damage or edits terrain.</summary>
    internal sealed class GungnirScene
    {
        private const float TwoPi = 6.283185307f;
        private readonly SubsystemGungnir m_runtime;
        private readonly PrimitivesRenderer3D m_renderer = new PrimitivesRenderer3D();
        private readonly DrawBlockEnvironmentData m_environment;
        private readonly List<Slash> m_slashes = new List<Slash>(24);
        private FlatBatch3D m_lines;
        private TexturedBatch3D m_runeEtch;
        private TexturedBatch3D m_runeGlow;
        private Camera m_camera;
        private int m_segments, m_dynamicGlyphs, m_tailBands, m_quality;
        private readonly FlightCue[] m_flightCues = new FlightCue[24];
        private int m_nextFlightCue;

        private struct Slash { internal Vector3 Start, Direction; internal float Age; }
        private struct FlightCue { internal bool Active; internal byte Kind; internal Vector3 Position, Direction; internal float Age; }

        internal GungnirScene(SubsystemGungnir runtime, SubsystemTerrain terrain)
        {
            m_runtime = runtime;
            m_environment = new DrawBlockEnvironmentData { SubsystemTerrain = terrain, Light = 15 };
            Texture2D runes = ContentManager.Get<Texture2D>("Textures/GungnirRunes");
            m_lines = m_renderer.FlatBatch(0, DepthStencilState.DepthRead, RasterizerState.CullNoneScissor, BlendState.Additive);
            m_runeEtch = m_renderer.TexturedBatch(runes, false, 0, DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.AlphaBlend, SamplerState.LinearClamp);
            m_runeGlow = m_renderer.TexturedBatch(runes, false, 0, DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.Additive, SamplerState.LinearClamp);
        }

        internal void Stab(Vector3 start, Vector3 direction)
        {
            if (m_slashes.Count == 24) m_slashes.RemoveAt(0);
            m_slashes.Add(new Slash { Start = start, Direction = direction });
        }

        internal void FlightHit(Vector3 position, Vector3 direction) => AddFlightCue(1, position, direction);
        internal void FlightReturn(Vector3 position, Vector3 direction) => AddFlightCue(2, position, direction);

        private void AddFlightCue(byte kind, Vector3 position, Vector3 direction)
        {
            int index = m_nextFlightCue++ % m_flightCues.Length;
            m_flightCues[index] = new FlightCue { Active = true, Kind = kind, Position = position, Direction = direction, Age = 0f };
        }

        internal void Update(float dt)
        {
            for (int i = m_slashes.Count - 1; i >= 0; i--)
            {
                Slash slash = m_slashes[i];
                slash.Age += dt;
                if (slash.Age > 0.2f) m_slashes.RemoveAt(i); else m_slashes[i] = slash;
            }
            for (int i = 0; i < m_flightCues.Length; i++)
            {
                if (!m_flightCues[i].Active) continue;
                FlightCue cue = m_flightCues[i];
                cue.Age += dt;
                float life = cue.Kind == 2 ? GungnirVisualRules.ReturnCueLife : GungnirVisualRules.HitCueLife;
                if (cue.Age >= life) cue.Active = false;
                m_flightCues[i] = cue;
            }
        }

        internal void Draw(Camera camera)
        {
            m_camera = camera;
            StormhammerEffectQuality quality = StormhammerOptions.Quality;
            m_quality = quality == StormhammerEffectQuality.Low ? 0 : quality == StormhammerEffectQuality.Medium ? 1 : 2;
            m_segments = quality == StormhammerEffectQuality.Low ? 24 : quality == StormhammerEffectQuality.Medium ? 40 : 64;
            m_dynamicGlyphs = quality == StormhammerEffectQuality.Low ? 5 : quality == StormhammerEffectQuality.Medium ? 8 : 12;
            m_tailBands = GungnirVisualRules.TrailBands(m_quality);
            double now = m_runtime.Now;
            foreach (SubsystemGungnir.State state in m_runtime.States.Values)
            {
                if (state.Player == null || !state.Player.IsAddedToProject) continue;
                Vector3 foot = state.Player.ComponentBody.Position + Vector3.UnitY * 0.04f;
                if (Vector3.DistanceSquared(foot, camera.ViewPosition) > 14400f) continue;
                float remaining = (float)(state.BuffEnd - now);
                if (state.Equipped && remaining > 0f) DrawBlessing(state, foot, (float)(now - state.BuffVisualStart), remaining);
                if (state.Phase != 0) DrawThrownSpear(state);
                float fatalismAge = (float)(now - state.FatalismStart);
                if (fatalismAge >= 0f && fatalismAge < GungnirRules.FatalismEnd) DrawFatalism(state, foot, fatalismAge);
                float judgmentAge = (float)(now - state.CastStart);
                if (judgmentAge >= 0f && judgmentAge < GungnirSkyRules.Lifetime && Vector3.DistanceSquared(state.Target, camera.ViewPosition) < 22500f)
                    DrawJudgment(state.Target, judgmentAge);
            }
            for (int i = 0; i < m_slashes.Count; i++)
            {
                Slash slash = m_slashes[i];
                float fade = 1f - slash.Age / 0.2f;
                Beam(slash.Start + slash.Direction * 0.5f, slash.Start + slash.Direction * 3.5f, 0.035f * fade, Gold(fade));
                Beam(slash.Start + slash.Direction, slash.Start + slash.Direction * 3.5f, 0.11f * fade, Gold(fade * 0.18f));
            }
            DrawFlightCues();
            m_renderer.Flush(camera.ViewProjectionMatrix);
        }


        /// <summary>
        /// Fate Severance: a Norse mandate seal under the caster, thin fate threads on nearby
        /// marked enemies (only those the client can actually see) and a single shatter beat.
        /// Nothing here creates damage, and no particle is spawned for an off-screen target.
        /// </summary>
        private void DrawFatalism(SubsystemGungnir.State state, Vector3 foot, float age)
        {
            float alpha = GungnirRules.FatalismAlpha(age);
            if (alpha <= 0.003f) return;
            float reveal = MathUtils.Saturate(age / GungnirRules.FatalismMark);
            float radius = 1.1f + 3.6f * EaseOut(reveal);
            Color platinum = new Color(247, 242, 214) * alpha;
            Color gold = Gold(alpha);
            Ring(foot, radius, 0.055f, platinum);
            Ring(foot, radius * 0.74f, 0.022f, gold * 0.75f);
            Seal(foot, radius * 0.92f, age * 1.7f, alpha * 0.9f, 8, reveal);
            int spokes = m_quality == 0 ? 8 : 16;
            for (int i = 0; i < spokes; i++)
            {
                float angle = age * 0.9f + i * TwoPi / spokes;
                Vector3 radial = new Vector3(MathUtils.Cos(angle), 0f, MathUtils.Sin(angle));
                Beam(foot + radial * radius * 0.52f + Vector3.UnitY * 0.02f,
                     foot + radial * radius * 1.1f + Vector3.UnitY * 0.02f, 0.012f, platinum * 0.62f);
            }
            // Marks exist only on visible hosts; far targets still take their single hit.
            bool marks = GungnirRules.FatalismMarks(age);
            bool shattered = GungnirRules.FatalismShattered(age);
            if (!marks) return;
            float shatter = shattered ? MathUtils.Saturate(1f - (age - GungnirRules.FatalismImpact) / 0.2f) : 0f;
            int drawn = 0, budget = m_quality == 0 ? 6 : m_quality == 1 ? 10 : 16;
            foreach (ComponentBody body in m_runtime.NearbyHostiles(state.Player))
            {
                if (drawn >= budget) break;
                if (body == null || !body.Entity.IsAddedToProject) continue;
                Vector3 center = body.BoundingBox.Center();
                if (Vector3.DistanceSquared(center, m_camera.ViewPosition) > 3600f) continue;
                float markAlpha = marks && !shattered ? alpha : shatter * alpha;
                if (markAlpha <= 0.01f) continue;
                drawn++;
                DrawFatalismMark(body, center, markAlpha, shatter);
            }
        }

        private void DrawFatalismMark(ComponentBody body, Vector3 center, float alpha, float shatter)
        {
            Color platinum = new Color(250, 246, 222) * alpha;
            Color gold = Gold(alpha * 0.84f);
            Vector3 top = center + Vector3.UnitY * ((body.BoundingBox.Max.Y - body.BoundingBox.Min.Y) * 0.5f + 0.42f);
            float size = 0.16f;
            // A small cross rune above the head, with a halo ring.
            Beam(top - Vector3.UnitX * size, top + Vector3.UnitX * size, 0.018f, platinum);
            Beam(top - Vector3.UnitZ * size, top + Vector3.UnitZ * size, 0.018f, platinum);
            Beam(top - Vector3.UnitY * size * 0.8f, top + Vector3.UnitY * size * 0.8f, 0.018f, gold);
            OrientedRing(top + Vector3.UnitY * 0.05f, Vector3.UnitY, size * 1.5f, 0.012f, platinum * 0.8f, m_segments / 2);
            if (shatter <= 0f)
            {
                // Thin fate threads: three vertical filaments hugging the body silhouette.
                Vector3 foot = center - Vector3.UnitY * ((body.BoundingBox.Max.Y - body.BoundingBox.Min.Y) * 0.5f);
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * TwoPi / 3f + (float)m_camera.ViewPosition.X * 0.013f;
                    Vector3 radial = new Vector3(MathUtils.Cos(angle), 0f, MathUtils.Sin(angle)) * 0.28f;
                    Beam(foot + radial, top + radial * 0.5f, 0.008f, platinum * 0.55f);
                }
                return;
            }
            // Shatter beat: a brief platinum cross flash and eight short shards.
            float spread = (1f - shatter) * 1.35f;
            Beam(top - Vector3.UnitX * spread, top + Vector3.UnitX * spread, 0.05f * shatter, platinum);
            Beam(top - Vector3.UnitY * spread, top + Vector3.UnitY * spread, 0.05f * shatter, platinum);
            int shards = m_quality == 0 ? 4 : 8;
            for (int i = 0; i < shards; i++)
            {
                float angle = i * TwoPi / shards + 0.4f;
                Vector3 radial = new Vector3(MathUtils.Cos(angle), 0f, MathUtils.Sin(angle));
                Vector3 from = top + radial * spread * 0.35f;
                Beam(from, from + radial * spread * 0.8f + Vector3.UnitY * (0.25f * shatter), 0.02f * shatter, gold);
            }
        }
        private void DrawBlessing(SubsystemGungnir.State state, Vector3 foot, float age, float remaining)
        {
            float endFade = MathUtils.Saturate(remaining / 2f);
            float invoke = MathUtils.Saturate(age / 0.9f);
            float radius = 0.2f + 1.1f * EaseOut(invoke);
            Seal(foot, radius, age * 0.45f, endFade * (0.35f + 0.5f * invoke), 8, invoke);

            int orbitCount = MathUtils.Min(m_dynamicGlyphs, 8);
            for (int i = 0; i < orbitCount; i++)
            {
                float reveal = MathUtils.Saturate((age - i * 0.075f) / 0.24f);
                float angle = age * (0.52f + 0.035f * i) + i * TwoPi / orbitCount;
                Vector3 radial = new Vector3(MathUtils.Cos(angle), 0f, MathUtils.Sin(angle));
                float rise = age < 1.15f ? EaseOut(MathUtils.Saturate((age - i * 0.055f) / 0.8f)) * 1.15f : 0.72f + 0.18f * MathUtils.Sin(age * 1.3f + i);
                Vector3 p = foot + radial * (0.62f + 0.05f * (i & 1)) + Vector3.UnitY * rise;
                Glyph(p, Vector3.Cross(radial, Vector3.UnitY), Vector3.UnitY, 0.18f, i, endFade * reveal * 0.72f);
                if (age < 1.2f)
                {
                    Vector3 weapon = state.Player.ComponentCreatureModel.EyePosition + state.Player.GameWidget.ActiveCamera.ViewDirection * 0.45f;
                    Beam(p, Vector3.Lerp(p, weapon, MathUtils.Saturate((age - 0.35f - i * 0.035f) / 0.6f)), 0.012f, Gold(endFade * reveal * 0.32f));
                }
            }

            Vector3 velocity = state.Player.ComponentBody.Velocity;
            float speed = new Vector2(velocity.X, velocity.Z).Length();
            if (speed > 1.5f)
            {
                Vector3 horizontal = new Vector3(velocity.X, 0f, velocity.Z);
                for (int i = 0; i < m_tailBands; i++)
                {
                    float offset = 0.22f + i * 0.16f;
                    Beam(foot + Vector3.UnitY * (0.18f + i * 0.24f), foot + Vector3.UnitY * (0.18f + i * 0.24f) - horizontal * offset, 0.025f + i * 0.014f, Gold(endFade * (0.24f - i * 0.045f)));
                }
            }
            if (velocity.Y > 1f) Ring(foot - Vector3.UnitY * 0.08f, 0.45f + age % 0.38f * 1.8f, 0.025f, Gold(endFade * 0.48f));
        }

        private void DrawThrownSpear(SubsystemGungnir.State state)
        {
            Vector3 direction = state.Direction.LengthSquared() > 0.1f ? Vector3.Normalize(state.Direction) : Vector3.UnitY;
            bool returning = state.Phase == 2;
            Spear(state.Position, direction, returning ? 1.14f : 1.1f, returning ? new Color(255, 236, 185) : new Color(255, 252, 226), true, returning);
            if (returning) DrawReturnTrail(state.Position, direction); else DrawOutboundTrail(state.Position, direction);
        }

        private void DrawOutboundTrail(Vector3 position, Vector3 direction)
        {
            float length = GungnirVisualRules.OutboundTrailLength;
            Beam(position - direction * 0.45f, position - direction * length, 0.026f, new Color(255, 255, 244, 250));
            Beam(position - direction * 0.6f, position - direction * length, 0.068f, new Color(255, 228, 151, 170));
            for (int i = 0; i < m_tailBands; i++)
                Beam(position - direction * (0.72f + i * 0.1f), position - direction * (length * (1f - i * 0.1f)), 0.11f + i * 0.045f, Gold(0.28f / (i + 1)));
            if (m_quality > 0)
            {
                Vector3 side = Perpendicular(direction);
                int fragments = m_quality;
                for (int i = 0; i < fragments; i++)
                    Glyph(position - direction * (2.1f + i * 1.35f) + side * ((i & 1) == 0 ? 0.12f : -0.12f), side, Vector3.Normalize(Vector3.Cross(direction, side)), 0.09f, i + 2, 0.42f);
            }
        }

        private void DrawReturnTrail(Vector3 position, Vector3 direction)
        {
            float length = GungnirVisualRules.ReturnTrailLength;
            Beam(position - direction * 0.4f, position - direction * length, 0.038f, new Color(255, 251, 226, 255));
            Beam(position - direction * 0.55f, position - direction * length, 0.095f, new Color(255, 204, 105, 195));
            for (int i = 0; i < m_tailBands; i++)
                Beam(position - direction * (0.68f + i * 0.08f), position - direction * (length * (1f - i * 0.08f)), 0.15f + i * 0.055f, new Color(255, 182, 72) * (0.3f / (i + 1)));

            Vector3 side = Perpendicular(direction);
            Vector3 up = Vector3.Normalize(Vector3.Cross(direction, side));
            int segments = GungnirVisualRules.ReturnSpiralSegments(m_quality);
            int strands = m_quality == 2 ? 2 : 1;
            float clock = (float)m_runtime.Now * 8.5f;
            for (int strand = 0; strand < strands; strand++)
            {
                Vector3 previous = position - direction * 0.55f;
                for (int i = 1; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float angle = clock + strand * 3.14159265f + t * TwoPi * 1.35f;
                    float radius = (0.07f + 0.12f * t) * (1f - 0.18f * t);
                    Vector3 point = position - direction * MathUtils.Lerp(0.55f, length * 0.86f, t) + (side * MathUtils.Cos(angle) + up * MathUtils.Sin(angle)) * radius;
                    Beam(previous, point, 0.018f + 0.007f * (1f - t), new Color(255, 218, 128) * (0.72f - t * 0.34f));
                    previous = point;
                }
            }
            int runes = GungnirVisualRules.ReturnRuneCount(m_quality);
            for (int i = 0; i < runes; i++)
            {
                float t = (i + 1f) / (runes + 1f);
                float phase = clock * 0.45f + i * 2.1f;
                Glyph(position - direction * MathUtils.Lerp(1.4f, length * 0.76f, t) + side * MathUtils.Sin(phase) * 0.16f, side, up, 0.105f + 0.025f * (1f - t), i + 4, 0.6f - 0.18f * t);
            }
            for (int i = 0; i < GungnirVisualRules.ReturnRingCount(m_quality); i++)
                OrientedRing(position - direction * (0.72f + i * 0.48f), direction, 0.18f + i * 0.055f, 0.018f, new Color(255, 228, 150) * (0.76f - i * 0.2f), 12);
        }

        private void DrawFlightCues()
        {
            for (int i = 0; i < m_flightCues.Length; i++)
            {
                FlightCue cue = m_flightCues[i];
                if (!cue.Active || Vector3.DistanceSquared(cue.Position, m_camera.ViewPosition) > 14400f) continue;
                float life = cue.Kind == 2 ? GungnirVisualRules.ReturnCueLife : GungnirVisualRules.HitCueLife;
                float alpha = GungnirVisualRules.CueAlpha(cue.Age, life);
                Vector3 direction = cue.Direction.LengthSquared() > 0.01f ? Vector3.Normalize(cue.Direction) : Vector3.UnitY;
                Vector3 side = Perpendicular(direction);
                Vector3 up = Vector3.Normalize(Vector3.Cross(direction, side));
                float radius = cue.Kind == 2 ? MathUtils.Lerp(0.16f, 0.62f, cue.Age / life) : MathUtils.Lerp(0.12f, 0.42f, cue.Age / life);
                OrientedRing(cue.Position, direction, radius, cue.Kind == 2 ? 0.045f : 0.032f, new Color(255, cue.Kind == 2 ? 201 : 235, 128) * alpha, cue.Kind == 2 ? 16 : 12);
                Beam(cue.Position - side * radius * 0.8f, cue.Position + side * radius * 0.8f, 0.035f * alpha, new Color(255, 252, 224) * alpha);
                Beam(cue.Position - up * radius * 0.8f, cue.Position + up * radius * 0.8f, 0.035f * alpha, new Color(255, 252, 224) * alpha);
                if (cue.Kind == 2) Glyph(cue.Position + direction * 0.02f, side, up, radius * 0.52f, 6, alpha * 0.9f);
            }
        }

        private void DrawJudgment(Vector3 center, float age)
        {
            float sky = GungnirSkyRules.SkyBlend(age);
            float edict = MathUtils.Saturate(age / 0.9f);
            float impactAge = age - GungnirSkyRules.ImpactTime;
            float groundAlpha = age < GungnirSkyRules.ImpactTime ? edict : sky * MathUtils.Saturate((4.6f - impactAge) / 3.2f);

            // Ground runes light from the centre outward during the edict, then answer the impact.
            Seal(center, 7f, age * 0.12f, groundAlpha * 0.82f, 16, edict);
            Seal(center + Vector3.UnitY * 0.025f, 5.35f, -age * 0.18f, groundAlpha * 0.58f, 12, MathUtils.Saturate((age - 0.16f) * 2.1f));
            Seal(center + Vector3.UnitY * 0.05f, 2.15f, age * 0.28f, groundAlpha * 0.78f, 8, MathUtils.Saturate((age - 0.34f) * 2.2f));
            DrawSkyBackdrop(center, age, sky, edict);
            DrawSkyRift(center, age);
            DrawJudgmentSpear(center, age);
            if (impactAge >= 0f) DrawJudgmentImpact(center, age, impactAge, sky);
        }

        /// <summary>Vertical tear in the canopy that the spear descends through.</summary>
        private void DrawSkyRift(Vector3 center, float age)
        {
            float open = age < GungnirSkyRules.ImpactTime ? MathUtils.Saturate(age / 0.42f) : MathUtils.Saturate((3.4f - age) / 1.9f);
            if (open <= 0.004f) return;
            const float top = 66f, bottom = 1.1f;
            Vector3 mouth = center + Vector3.UnitY * bottom;
            float widen = MathUtils.Lerp(0.4f, 2.6f, open);
            Beam(mouth, center + Vector3.UnitY * top, 0.16f + 0.12f * (1f - open), new Color(255, 250, 228) * (open * 0.55f));
            Beam(mouth, mouth + Vector3.UnitY * 34f, 0.55f * open, Gold(open * 0.12f));
            int jaws = m_quality + 2;
            for (int i = 0; i < jaws; i++)
            {
                float t = (i + 1f) / (jaws + 1f);
                float sway = MathUtils.Sin(age * 1.7f + i * 1.9f) * 0.6f;
                Vector3 a = center + new Vector3(sway * (1f - t), MathUtils.Lerp(bottom, top, t), widen * 0.3f * (1f - t));
                Vector3 b = center + new Vector3(sway + widen * ((i & 1) == 0 ? 1f : -1f), MathUtils.Lerp(bottom, top, t + 0.18f), widen * 0.3f * (1f - t));
                Beam(a, b, 0.05f + 0.03f * (1f - t), Gold(open * (0.34f - 0.05f * i)));
            }
        }

        /// <summary>The giant spear itself: it condenses in the tear, plunges blade-first, then stands buried.</summary>
        private void DrawJudgmentSpear(Vector3 center, float age)
        {
            float alpha = GungnirSkyRules.SpearAlpha(age);
            if (alpha <= 0.004f) return;
            float scale = GungnirSkyRules.SpearScale * GungnirSkyRules.SpearScaleFactor(age);
            float height = GungnirSkyRules.SpearHeight(age);
            bool falling = GungnirSkyRules.SpearFalling(age);
            bool embedded = age >= GungnirSkyRules.ImpactTime;
            if (embedded)
            {
                float settle = MathUtils.Saturate((age - GungnirSkyRules.ImpactTime) / 0.6f);
                height += 0.4f * (1f - settle) + MathUtils.Sin(age * 42f) * 0.05f * (1f - settle) * (1f - settle);
            }
            Vector3 origin = center + Vector3.UnitY * height;
            Color core = (embedded ? new Color(255, 240, 196) : new Color(255, 252, 232)) * alpha;
            if (falling)
            {
                // Speed afterimages: cheaper than a motion-blurred mesh and readable at 30 fps.
                int ghosts = GungnirSkyRules.AfterImageCount(m_quality);
                for (int i = ghosts; i >= 1; i--)
                    Spear(origin + Vector3.UnitY * (i * (2.6f + i * 1.7f)), -Vector3.UnitY, scale * (1f - 0.06f * i), new Color(255, 214, 140) * (alpha * 0.42f / i), false, false);
            }
            Spear(origin, -Vector3.UnitY, scale, core, true, false);
            if (age < GungnirSkyRules.SpearFormEnd)
            {
                // Sky rune light streams into the condensing blade before it is released.
                float form = GungnirSkyRules.SpearScaleFactor(age);
                int converge = GungnirSkyRules.ConvergenceBeams(m_quality);
                for (int i = 0; i < converge; i++)
                {
                    float angle = i * TwoPi / converge + age * (0.08f + i * 0.006f);
                    Vector3 source = center + new Vector3(MathUtils.Cos(angle) * (16f + i % 3 * 3f), 34f + i % 2 * 4f, MathUtils.Sin(angle) * (16f + i % 3 * 3f));
                    Beam(source, origin + Vector3.UnitY * (2.2f - i * 0.12f), 0.04f + 0.014f * (i & 1), Gold(form * 0.3f));
                }
            }
            if (!falling) return;
            float tip = height - GungnirSkyRules.SpearBladeLength * scale;
            for (int i = 0; i < m_tailBands + 1; i++)
                Beam(center + Vector3.UnitY * (GungnirSkyRules.SpearHoverHeight + 9f + i * 3.4f), center + Vector3.UnitY * MathUtils.Max(tip + 1.4f, 1.2f), 0.1f + i * 0.13f, Gold(0.5f / (i + 1)));
            OrientedRing(center + Vector3.UnitY * MathUtils.Max(tip + 2.6f, 0.6f), Vector3.UnitY, 1.2f + (age - GungnirSkyRules.SpearFormEnd) * 7f, 0.05f, new Color(255, 246, 214) * 0.55f, 14);
        }

        private void DrawJudgmentImpact(Vector3 center, float age, float impactAge, float sky)
        {
            float flash = MathUtils.Saturate(1f - impactAge / 0.26f) * (StormhammerOptions.ReducedFlashes ? 0.35f : 1f);
            if (flash > 0f)
            {
                const float reach = 9.5f;
                Beam(center + new Vector3(-reach, 0.3f, 0f), center + new Vector3(reach, 0.3f, 0f), 0.16f + 0.5f * flash, new Color(255, 254, 238) * flash);
                Beam(center + new Vector3(0f, 0.3f, -reach), center + new Vector3(0f, 0.3f, reach), 0.16f + 0.5f * flash, new Color(255, 254, 238) * flash);
                Beam(center + Vector3.UnitY * 0.1f, center + Vector3.UnitY * 24f, 0.06f + 0.22f * flash, Gold(flash * 0.85f));
                OrientedRing(center + Vector3.UnitY * 0.12f, Vector3.UnitY, MathUtils.Lerp(1f, 13f, MathUtils.Saturate(impactAge / 0.5f)), 0.2f, Gold(flash * 0.9f), 24);
            }
            for (int i = 0; i < 3; i++)
            {
                float wave = MathUtils.Saturate((impactAge - i * 0.13f) / (0.76f + i * 0.12f));
                if (wave > 0f && wave < 1f) Ring(center + Vector3.UnitY * (0.08f + i * 0.07f), MathUtils.Lerp(0.45f, 12f - i * 1.7f, EaseOut(wave)), 0.14f - i * 0.025f, Gold((1f - wave) * (0.98f - i * 0.17f)));
            }
            // Embers and dust thrown up along the buried shaft.
            int embers = GungnirSkyRules.EmberCount(m_quality);
            float emberFade = sky * MathUtils.Saturate((6.4f - impactAge) / 3.4f);
            for (int i = 0; i < embers; i++)
            {
                float local = MathUtils.Max(0f, impactAge - i * 0.05f);
                float rise = MathUtils.Saturate(local / (1.8f + (i % 3) * 0.5f));
                float angle = i * TwoPi / embers + local * (0.5f + (i & 1) * 0.2f);
                Vector3 radial = new Vector3(MathUtils.Cos(angle), 0f, MathUtils.Sin(angle));
                Vector3 start = center + radial * (0.6f + rise * 2.4f) + Vector3.UnitY * (0.1f + rise * (6f + i % 4 * 1.8f));
                Beam(start, start + Vector3.UnitY * (0.9f + (i % 3) * 0.5f), 0.05f + 0.02f * (i & 1), Gold(emberFade * (1f - rise) * 0.8f));
            }
            // Glyph fragments drifting outward after the impact.
            int fragments = MathUtils.Min(m_dynamicGlyphs, GungnirSkyRules.SkyRunes(m_quality));
            float fragmentFade = sky * MathUtils.Saturate((8.7f - age) / 4.5f);
            for (int i = 0; i < fragments; i++)
            {
                float local = MathUtils.Max(0f, impactAge - i * 0.07f);
                float drift = MathUtils.Saturate(local / (2.2f + (i % 3) * 0.3f));
                float angle = i * TwoPi / fragments + local * (0.13f + (i & 1) * 0.07f);
                Vector3 radial = new Vector3(MathUtils.Cos(angle), 0f, MathUtils.Sin(angle));
                Glyph(center + radial * MathUtils.Lerp(0.8f, 6.6f, drift) + Vector3.UnitY * (0.6f + drift * (2.4f + i % 3)), Vector3.Cross(radial, Vector3.UnitY), Vector3.UnitY, 0.23f * (1f - drift * 0.28f), i, fragmentFade * (1f - drift * 0.55f));
            }
        }

        private void DrawSkyBackdrop(Vector3 center, float age, float sky, float reveal)
        {
            if (sky <= 0.002f) return;
            int ringLayers = m_quality == 0 ? 1 : 3;
            for (int layer = 0; layer < ringLayers; layer++)
            {
                float layerReveal = MathUtils.Saturate((age - layer * 0.14f) / 0.52f);
                float shatter = age <= 1.4f ? 1f : MathUtils.Saturate((3.15f - age) / 1.75f);
                float rotation = age * (layer == 1 ? -0.34f : 0.22f + layer * 0.08f);
                Seal(center + Vector3.UnitY * (10f + layer * 5.2f), (5.4f - layer * 0.58f) * (age > 1.4f ? 1f + (age - 1.4f) * 0.12f : 1f), rotation, sky * layerReveal * shatter * (0.62f - layer * 0.08f), 12 - layer * 2, layerReveal);
            }

            int clouds = GungnirSkyRules.CloudBands(m_quality);
            for (int i = 0; i < clouds; i++)
            {
                float gather = age < 1.4f ? MathUtils.Lerp(28f, 15f + i * 1.7f, EaseOut(reveal)) : MathUtils.Lerp(15f + i * 1.7f, 31f, MathUtils.Saturate((age - 6.6f) / 2.4f));
                SkyBand(center + Vector3.UnitY * (18f + i * 2.1f), gather, age * (0.035f + i * 0.006f) * ((i & 1) == 0 ? 1f : -1f), 0.16f + i * 0.025f, new Color(i % 2 == 0 ? 183 : 72, i % 2 == 0 ? 144 : 109, i % 2 == 0 ? 70 : 104) * (sky * 0.11f), m_quality == 2 ? 24 : 16);
            }

            int runes = GungnirSkyRules.SkyRunes(m_quality);
            Vector3 viewFlat = new Vector3(m_camera.ViewDirection.X, 0f, m_camera.ViewDirection.Z);
            if (viewFlat.LengthSquared() < 0.01f) viewFlat = Vector3.UnitZ;
            Vector3 right = Vector3.Normalize(Vector3.Cross(viewFlat, Vector3.UnitY));
            for (int i = 0; i < runes; i++)
            {
                float angle = i * TwoPi / runes + age * (0.025f + (i & 1) * 0.012f);
                float radius = 12f + i % 3 * 4f;
                Vector3 p = center + new Vector3(MathUtils.Cos(angle) * radius, 13f + i % 4 * 3.4f + MathUtils.Sin(age * 0.3f + i) * 0.4f, MathUtils.Sin(angle) * radius);
                Glyph(p, right, Vector3.UnitY, 0.34f + (i % 2) * 0.08f, i, sky * (0.16f + 0.12f * MathUtils.Saturate(MathUtils.Sin(age * 0.7f + i) * 0.5f + 0.5f)));
            }

            if (age >= 1.4f)
            {
                int columns = GungnirSkyRules.LightColumns(m_quality);
                float columnFade = sky * MathUtils.Saturate((6.8f - age) / 3.8f);
                for (int i = 0; i < columns; i++)
                {
                    float angle = i * TwoPi / columns + 0.45f;
                    float radius = 8f + (i % 3) * 5f;
                    Vector3 ground = center + new Vector3(MathUtils.Cos(angle) * radius, 0.3f, MathUtils.Sin(angle) * radius);
                    Beam(ground, ground + Vector3.UnitY * (20f + i % 2 * 8f), 0.07f + (i % 2) * 0.035f, new Color(255, 226, 156) * (columnFade * (0.18f + (i % 3) * 0.04f)));
                }
            }
        }

        private void SkyBand(Vector3 center, float radius, float rotation, float width, Color color, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = rotation + i * TwoPi / segments;
                float b = rotation + (i + 1) * TwoPi / segments;
                Vector3 first = center + new Vector3(MathUtils.Cos(a) * radius, MathUtils.Sin(a * 3f) * 0.45f, MathUtils.Sin(a) * radius);
                Vector3 second = center + new Vector3(MathUtils.Cos(b) * radius, MathUtils.Sin(b * 3f) * 0.45f, MathUtils.Sin(b) * radius);
                Beam(first, second, width, color);
            }
        }

        private void Spear(Vector3 position, Vector3 axis, float scale, Color color, bool flight = false, bool returning = false)
        {
            if (GungnirBlock.Mesh == null) return;
            Vector3 forward = Vector3.Cross(axis, Vector3.UnitX);
            if (forward.LengthSquared() < 0.01f) forward = Vector3.Cross(axis, Vector3.UnitZ);
            Matrix matrix = Matrix.CreateWorld(position, Vector3.Normalize(forward), axis);
            m_environment.InWorldMatrix = matrix;
            m_environment.ViewProjectionMatrix = m_camera.ViewProjectionMatrix;
            BlocksManager.DrawMeshBlock(m_renderer, GungnirBlock.Mesh, GungnirBlock.Texture, color, scale, ref matrix, m_environment);
            if (!flight) return;
            if (GungnirBlock.EnergyRidge != null)
                BlocksManager.DrawMeshBlock(m_renderer, GungnirBlock.EnergyRidge, GungnirBlock.Texture, returning ? new Color(255, 222, 141) : new Color(255, 252, 224), scale * 1.012f, ref matrix, m_environment);
            BlockMesh[] runes = GungnirBlock.RuneMeshes;
            int stride = m_quality == 0 ? 6 : m_quality == 1 ? 4 : 2;
            int offset = ((int)(m_runtime.Now * (returning ? 18.0 : 13.0))) % stride;
            for (int i = offset; i < runes.Length; i += stride)
                if (runes[i] != null) BlocksManager.DrawMeshBlock(m_renderer, runes[i], GungnirBlock.Texture, returning ? new Color(255, 209, 112) : new Color(255, 244, 195), scale * 1.016f, ref matrix, m_environment);
        }

        private static Vector3 Perpendicular(Vector3 axis)
        {
            Vector3 result = Vector3.Cross(axis, Vector3.UnitY);
            if (result.LengthSquared() < 0.01f) result = Vector3.Cross(axis, Vector3.UnitX);
            return Vector3.Normalize(result);
        }

        private void OrientedRing(Vector3 p, Vector3 axis, float radius, float width, Color color, int segments)
        {
            if (radius <= width || color.A == 0) return;
            Vector3 x = Perpendicular(axis);
            Vector3 y = Vector3.Normalize(Vector3.Cross(axis, x));
            for (int i = 0; i < segments; i++)
            {
                float a = i * TwoPi / segments, b = (i + 1) * TwoPi / segments;
                Vector3 u = x * MathUtils.Cos(a) + y * MathUtils.Sin(a);
                Vector3 v = x * MathUtils.Cos(b) + y * MathUtils.Sin(b);
                m_lines.QueueQuad(p + u * (radius - width), p + u * (radius + width), p + v * (radius + width), p + v * (radius - width), color);
            }
        }

        private void Seal(Vector3 center, float radius, float rotation, float alpha, int requestedGlyphs, float reveal)
        {
            if (alpha <= 0.003f) return;
            Ring(center, radius, 0.025f, Gold(alpha));
            Ring(center, radius * 0.82f, 0.016f, Gold(alpha * 0.62f));
            int count = MathUtils.Min(requestedGlyphs, m_dynamicGlyphs);
            for (int i = 0; i < count; i++)
            {
                float glyphReveal = MathUtils.Saturate(reveal * count - i);
                if (glyphReveal <= 0f) continue;
                float angle = rotation + i * TwoPi / count;
                Vector3 radial = new Vector3(MathUtils.Cos(angle), 0f, MathUtils.Sin(angle));
                Glyph(center + radial * radius * 0.91f + Vector3.UnitY * 0.018f, Vector3.Cross(radial, Vector3.UnitY), radial, radius * 0.072f, i, alpha * glyphReveal);
            }
        }

        private void Glyph(Vector3 p, Vector3 x, Vector3 y, float size, int glyph, float alpha)
        {
            if (alpha <= 0.003f || size <= 0.002f) return;
            if (x.LengthSquared() < 0.001f || y.LengthSquared() < 0.001f) return;
            x = Vector3.Normalize(x); y = Vector3.Normalize(y);
            int index = glyph & 7;
            float u0 = index / 8f, u1 = (index + 1) / 8f;
            Vector2 uv0 = new Vector2(u0, 1f), uv1 = new Vector2(u1, 1f), uv2 = new Vector2(u1, 0f), uv3 = new Vector2(u0, 0f);
            Vector3 z = Vector3.Cross(x, y);
            if (z.LengthSquared() > 0.001f) p += Vector3.Normalize(z) * 0.008f;
            Color etch = new Color(236, 205, 139, (int)(220f * MathUtils.Saturate(alpha)));
            Color glow = new Color(255, 218, 118, (int)(155f * MathUtils.Saturate(alpha)));
            QueueGlyph(m_runeEtch, p, x, y, size, uv0, uv1, uv2, uv3, etch);
            QueueGlyph(m_runeGlow, p, x, y, size * 1.42f, uv0, uv1, uv2, uv3, glow);
        }

        private static void QueueGlyph(TexturedBatch3D batch, Vector3 p, Vector3 x, Vector3 y, float size, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3, Color color)
        {
            Vector3 right = x * size, up = y * size;
            batch.QueueQuad(p - right - up, p + right - up, p + right + up, p - right + up, uv0, uv1, uv2, uv3, color);
        }

        private void Ring(Vector3 p, float radius, float width, Color color)
        {
            if (radius <= width || color.A == 0) return;
            for (int i = 0; i < m_segments; i++)
            {
                float a = i * TwoPi / m_segments, b = (i + 1) * TwoPi / m_segments;
                Vector3 u = new Vector3(MathUtils.Cos(a), 0f, MathUtils.Sin(a));
                Vector3 v = new Vector3(MathUtils.Cos(b), 0f, MathUtils.Sin(b));
                m_lines.QueueQuad(p + u * (radius - width), p + u * (radius + width), p + v * (radius + width), p + v * (radius - width), color);
            }
        }

        private void Beam(Vector3 a, Vector3 b, float width, Color color)
        {
            Vector3 side = Vector3.Cross(b - a, m_camera.ViewPosition - (a + b) * 0.5f);
            if (side.LengthSquared() < 0.0001f) return;
            side = Vector3.Normalize(side) * width;
            m_lines.QueueQuad(a - side, a + side, b + side, b - side, color);
        }

        private static float EaseOut(float value) { value = MathUtils.Saturate(value); return 1f - (1f - value) * (1f - value); }
        private static float EaseIn(float value) { value = MathUtils.Saturate(value); return value * value * value; }
        private static Color Gold(float opacity) => new Color(255, 216, 127) * MathUtils.Saturate(opacity);
    }
}
