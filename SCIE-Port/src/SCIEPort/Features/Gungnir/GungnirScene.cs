using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using SCIEPort.Features.Stormhammer;

namespace SCIEPort.Features.Gungnir
{
    // All major effects use connected ribbon geometry and the real spear mesh, never glow balls.
    internal sealed class GungnirScene
    {
        private readonly SubsystemGungnir m_runtime;
        private readonly PrimitivesRenderer3D m_renderer = new PrimitivesRenderer3D();
        private readonly DrawBlockEnvironmentData m_environment;
        private readonly List<Slash> m_slashes = new List<Slash>();
        private FlatBatch3D m_batch;
        private Camera m_camera;
        private int m_segments;
        private struct Slash { internal Vector3 Start, Direction; internal float Age; }
        internal GungnirScene(SubsystemGungnir runtime, SubsystemTerrain terrain)
        {
            m_runtime = runtime; m_environment = new DrawBlockEnvironmentData { SubsystemTerrain = terrain, Light = 15 };
        }
        internal void Stab(Vector3 start, Vector3 direction)
        {
            if (m_slashes.Count == 24) m_slashes.RemoveAt(0);
            m_slashes.Add(new Slash { Start = start, Direction = direction });
        }
        internal void Update(float dt)
        {
            for (int i = m_slashes.Count - 1; i >= 0; i--)
            { var s = m_slashes[i]; s.Age += dt; if (s.Age > 0.2f) m_slashes.RemoveAt(i); else m_slashes[i] = s; }
        }
        internal void Draw(Camera camera)
        {
            m_camera = camera;
            m_segments = StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 32 : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 48 : 64;
            m_batch = m_renderer.FlatBatch(0, DepthStencilState.DepthRead, RasterizerState.CullNoneScissor, BlendState.Additive);
            foreach (var s in m_runtime.States.Values)
            {
                if (s.Player == null || !s.Player.IsAddedToProject) continue;
                Vector3 foot = s.Player.ComponentBody.Position + Vector3.UnitY * 0.04f;
                if (Vector3.DistanceSquared(foot, camera.ViewPosition) > 14400) continue;
                float remaining = (float)(s.BuffEnd - m_runtime.Now);
                float time = (float)m_runtime.Now;
                if (s.Equipped && remaining > 0)
                {
                    float fade = MathUtils.Saturate(remaining / 2);
                    Seal(foot, 1.05f, time * 0.4f, fade * 0.65f);
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = time * 0.7f + i * 6.283185307f / 6;
                        Vector3 radial = new Vector3(MathUtils.Cos(angle), 0, MathUtils.Sin(angle));
                        Rune(foot + radial * 0.68f + Vector3.UnitY * (0.65f + i % 2 * 0.45f), Vector3.Cross(radial, Vector3.UnitY), Vector3.UnitY, 0.22f, Gold(fade));
                    }
                    Vector3 velocity = s.Player.ComponentBody.Velocity;
                    if (velocity.LengthSquared() > 4) Beam(foot + Vector3.UnitY * 0.8f, foot + Vector3.UnitY * 0.8f - velocity * 0.12f, 0.13f, Gold(fade * 0.3f));
                    if (velocity.Y > 1) Ring(foot - Vector3.UnitY * 0.1f, 0.5f + (time % 0.35f) * 2, 0.03f, Gold(0.6f * fade));
                }
                if (s.Phase != 0)
                {
                    Vector3 direction = s.Direction.LengthSquared() > 0.1f ? Vector3.Normalize(s.Direction) : Vector3.UnitY;
                    Spear(s.Position, direction, 1, Color.White);
                    Beam(s.Position - direction * 0.9f, s.Position - direction * (s.Phase == 2 ? 4.5f : 3.2f), 0.09f, Gold(0.8f));
                    Beam(s.Position - direction, s.Position - direction * 4, 0.025f, new Color(255, 252, 226));
                }
                float age = (float)(m_runtime.Now - s.CastStart);
                if (age >= 0 && age < 3.8f) Judgment(s.Target, age);
            }
            foreach (var s in m_slashes)
            {
                float fade = 1 - s.Age / 0.2f;
                Beam(s.Start + s.Direction * 0.5f, s.Start + s.Direction * 3.5f, 0.04f * fade, Gold(fade));
                Beam(s.Start + s.Direction, s.Start + s.Direction * 3.5f, 0.12f * fade, Gold(0.2f * fade));
            }
            m_renderer.Flush(camera.ViewProjectionMatrix);
        }
        private void Judgment(Vector3 center, float age)
        {
            float fade = age < 1.4f ? MathUtils.Saturate(age * 4) : MathUtils.Saturate((3.8f - age) / 1.8f);
            Seal(center, 7, age * 0.15f, fade);
            for (int i = 0; i < 3; i++) Seal(center + Vector3.UnitY * (3 + i * 3), 3.8f - i * 0.6f, age * (i % 2 == 0 ? 0.5f : -0.5f), fade * 0.75f);
            if (age < 1.4f)
            {
                float fall = MathUtils.Saturate((age - 1.2f) / 0.2f);
                Vector3 position = center + Vector3.UnitY * (18 - 13 * fall);
                Spear(position, -Vector3.UnitY, 3.4f, Gold(MathUtils.Saturate(age * 2)));
                if (fall > 0) Beam(center + Vector3.UnitY * 20, position, 0.25f, Gold(0.9f));
            }
            else
            {
                float t = age - 1.4f;
                for (int i = 0; i < 3; i++) Ring(center + Vector3.UnitY * (0.1f + i * 0.16f), MathUtils.Min(10, t * (10 - i * 2)), 0.09f, Gold(fade * MathUtils.Saturate(1 - t / 1.2f)));
                for (int i = 0; i < 12; i++)
                {
                    float a = i * 6.283185307f / 12;
                    Vector3 radial = new Vector3(MathUtils.Cos(a), 0, MathUtils.Sin(a));
                    Beam(center + radial * 0.4f, center + radial * 4 + Vector3.Cross(radial, Vector3.UnitY) * 0.4f, 0.03f, Gold(fade));
                    Rune(center + radial * (1 + t) + Vector3.UnitY * (1 + i % 3 + t), Vector3.UnitX, Vector3.UnitY, 0.15f, Gold(fade));
                }
            }
        }
        private void Spear(Vector3 position, Vector3 axis, float scale, Color color)
        {
            if (GungnirBlock.Mesh == null) return;
            Vector3 forward = Vector3.Cross(axis, Vector3.UnitX);
            if (forward.LengthSquared() < 0.01f) forward = Vector3.Cross(axis, Vector3.UnitZ);
            Matrix matrix = Matrix.CreateWorld(position, Vector3.Normalize(forward), axis);
            m_environment.InWorldMatrix = matrix; m_environment.ViewProjectionMatrix = m_camera.ViewProjectionMatrix;
            BlocksManager.DrawMeshBlock(m_renderer, GungnirBlock.Mesh, GungnirBlock.Texture, color, scale, ref matrix, m_environment);
        }
        private static Color Gold(float opacity) => new Color(255, 216, 127) * MathUtils.Saturate(opacity);
        private void Seal(Vector3 center, float radius, float rotation, float alpha)
        {
            Ring(center, radius, 0.025f, Gold(alpha)); Ring(center, radius * 0.82f, 0.018f, Gold(alpha * 0.7f));
            int count = radius > 2 ? 16 : 8;
            for (int i = 0; i < count; i++)
            {
                float a = rotation + i * 6.283185307f / count;
                Vector3 radial = new Vector3(MathUtils.Cos(a), 0, MathUtils.Sin(a));
                Rune(center + radial * radius * 0.91f, Vector3.Cross(radial, Vector3.UnitY), radial, radius * 0.065f, Gold(alpha));
            }
        }
        private void Rune(Vector3 p, Vector3 x, Vector3 y, float size, Color color)
        {
            Beam(p - y * size, p + y * size, 0.018f, color);
            Beam(p + y * size, p + x * size, 0.018f, color);
            Beam(p + x * size, p, 0.018f, color);
            Beam(p, p - x * size + y * size * 0.4f, 0.018f, color);
        }
        private void Ring(Vector3 p, float radius, float width, Color color)
        {
            for (int i = 0; i < m_segments; i++)
            {
                float a = i * 6.283185307f / m_segments, b = (i + 1) * 6.283185307f / m_segments;
                Vector3 u = new Vector3(MathUtils.Cos(a), 0, MathUtils.Sin(a)), v = new Vector3(MathUtils.Cos(b), 0, MathUtils.Sin(b));
                m_batch.QueueQuad(p + u * (radius - width), p + u * (radius + width), p + v * (radius + width), p + v * (radius - width), color);
            }
        }
        private void Beam(Vector3 a, Vector3 b, float width, Color color)
        {
            Vector3 side = Vector3.Cross(b - a, m_camera.ViewPosition - (a + b) * 0.5f);
            if (side.LengthSquared() < 0.0001f) return;
            side = Vector3.Normalize(side) * width;
            m_batch.QueueQuad(a - side, a + side, b + side, b - side, color);
        }
    }
}
