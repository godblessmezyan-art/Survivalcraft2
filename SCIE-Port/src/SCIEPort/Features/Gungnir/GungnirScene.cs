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
                float judgmentAge = (float)(now - state.CastStart);
                if (judgmentAge >= 0f && judgmentAge < 4.4f && Vector3.DistanceSquared(state.Target, camera.ViewPosition) < 22500f)
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
            float warning = MathUtils.Saturate(age / 1.2f);
            float after = MathUtils.Saturate((4.4f - age) / 2.1f);
            float alpha = age < 1.4f ? warning : after;
            // Three grounded layers build from dark etching to a precise white-gold lock.
            Seal(center, 7f, age * 0.12f, alpha * 0.78f, 16, warning);
            Seal(center + Vector3.UnitY * 0.025f, 5.35f, -age * 0.18f, alpha * 0.52f, 12, MathUtils.Saturate((age - 0.18f) * 2f));
            Seal(center + Vector3.UnitY * 0.05f, 2.15f, age * 0.28f, alpha * 0.72f, 8, MathUtils.Saturate((age - 0.36f) * 2f));
            // Sky rings use distinct radii and angular velocity, with individually revealed glyphs.
            for (int layer = 0; layer < 3; layer++)
            {
                float layerReveal = MathUtils.Saturate((age - layer * 0.16f) / 0.55f);
                float rotation = age * (layer == 1 ? -0.48f : 0.31f + layer * 0.11f);
                Seal(center + Vector3.UnitY * (4.2f + layer * 3.3f), 4.5f - layer * 0.62f, rotation, alpha * layerReveal * (0.62f - layer * 0.08f), 12 - layer * 2, layerReveal);
            }

            if (age < 1.4f)
            {
                float fall = EaseIn(MathUtils.Saturate((age - 0.42f) / 0.98f));
                Vector3 position = center + Vector3.UnitY * (25f - 24.45f * fall);
                Spear(position, -Vector3.UnitY, 3.4f, Gold(MathUtils.Saturate(age * 2.2f)));
                for (int i = 0; i < m_tailBands; i++)
                    Beam(center + Vector3.UnitY * (30f + i * 1.4f), position + Vector3.UnitY * (1.4f + i * 0.3f), 0.12f + i * 0.09f, Gold((0.42f - i * 0.08f) * warning));
            }
            else
            {
                float t = age - 1.4f;
                if (t < 0.34f) Spear(center + Vector3.UnitY * 0.55f, -Vector3.UnitY, 3.4f * (1f - t * 0.28f), Gold(1f - t / 0.34f));
                float flash = MathUtils.Saturate(1f - t / 0.18f);
                if (flash > 0f)
                {
                    Beam(center + new Vector3(-5f, 0.22f, 0f), center + new Vector3(5f, 0.22f, 0f), 0.11f + 0.3f * flash, new Color(255, 252, 224) * flash);
                    Beam(center + new Vector3(0f, 0.22f, -5f), center + new Vector3(0f, 0.22f, 5f), 0.11f + 0.3f * flash, new Color(255, 252, 224) * flash);
                }
                for (int i = 0; i < 3; i++)
                {
                    float wave = MathUtils.Saturate((t - i * 0.12f) / (0.72f + i * 0.1f));
                    if (wave > 0f && wave < 1f) Ring(center + Vector3.UnitY * (0.08f + i * 0.07f), MathUtils.Lerp(0.4f, 10f - i * 1.55f, EaseOut(wave)), 0.12f - i * 0.022f, Gold((1f - wave) * (0.95f - i * 0.18f)));
                }
                int fragments = MathUtils.Min(m_dynamicGlyphs, 12);
                for (int i = 0; i < fragments; i++)
                {
                    float start = i * 0.055f;
                    float f = MathUtils.Saturate((t - start) / 1.65f);
                    float angle = i * TwoPi / fragments + t * (0.18f + (i & 1) * 0.09f);
                    Vector3 radial = new Vector3(MathUtils.Cos(angle), 0f, MathUtils.Sin(angle));
                    Glyph(center + radial * MathUtils.Lerp(1.1f, 5.2f, f) + Vector3.UnitY * (0.45f + f * (1.8f + i % 3)), Vector3.Cross(radial, Vector3.UnitY), Vector3.UnitY, 0.22f * (1f - f * 0.35f), i, after * (1f - f));
                }
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
