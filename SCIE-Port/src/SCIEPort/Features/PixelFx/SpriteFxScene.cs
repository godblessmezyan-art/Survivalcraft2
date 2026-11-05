using System;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.PixelFx
{
    /// <summary>One shared native batch, depth-tested billboards; owns no gameplay or content textures.</summary>
    internal sealed class SpriteFxScene
    {
        internal readonly SpriteFxPlayer Player = new SpriteFxPlayer();
        private TexturedBatch3D m_batch;
        private int m_width, m_height;
        internal int LastQuadCount { get; private set; }

        internal void Load(PrimitivesRenderer3D renderer, string textureName)
        {
            try
            {
                Texture2D texture = ContentManager.Get<Texture2D>(textureName);
                Attach(renderer, texture);
            }
            catch (Exception e)
            {
                m_batch = null;
                Log.Warning("[SCIEPort PixelFX] Sprite layer disabled: " + e);
            }
        }

        // Also used by the real Engine GPU fixture, bypassing only ContentManager setup.
        internal void Attach(PrimitivesRenderer3D renderer, Texture2D texture)
        {
            m_width = texture.Width; m_height = texture.Height;
            m_batch = renderer.TexturedBatch(texture, false, 0, DepthStencilState.DepthRead,
                RasterizerState.CullNone, BlendState.Additive, SamplerState.PointClamp);
        }

        internal void Play(SpriteFxClip clip, Vector3 anchor, float size, double now, int quality, float life = 0)
        {
            if (m_batch != null && clip != null && clip.AtlasWidth == m_width && clip.AtlasHeight == m_height)
                Player.Play(clip, anchor.X, anchor.Y, anchor.Z, size, now, quality, life);
        }

        internal void Clear()
        {
            Player.Clear(); m_batch = null; m_width = 0; m_height = 0; LastQuadCount = 0;
            // ContentManager owns the atlas. Never dispose its shared texture here.
        }

        internal void Queue(Vector3 eye, Vector3 direction, double now, int quality, bool reducedFlashes)
        {
            LastQuadCount = 0;
            if (m_batch == null || direction.LengthSquared() < 0.0001f) return;
            direction = Vector3.Normalize(direction);
            Vector3 right = Vector3.Cross(direction, Vector3.UnitY);
            if (right.LengthSquared() < 0.0001f) right = Vector3.UnitX;
            else right = Vector3.Normalize(right);
            Vector3 screenUp = Vector3.Normalize(Vector3.Cross(right, direction));
            SpriteFxBudget budget = SpriteFxBudget.ForQuality(quality);
            // Render high-priority impacts first. Bounded scan (4 * 32), no sort/allocations.
            for (int priority = 3; priority >= 0; priority--)
            for (int i = 0; i < SpriteFxPlayer.Capacity; i++)
            {
                SpriteFxInstance item = Player.At(i);
                if (item.Clip == null || item.Clip.Priority != priority) continue;
                int frame = SpriteFxPlayer.Frame(item, now, budget.SampleFps);
                if (frame < 0) continue;
                Vector3 anchor = new Vector3(item.X, item.Y, item.Z);
                Vector3 delta = anchor - eye;
                float distance = delta.Length(), depth = Vector3.Dot(delta, direction);
                if (distance > budget.Distance || distance < 0.65f || depth <= 0.2f) continue;
                if (LastQuadCount >= budget.Quads) return;
                // Avoid a giant billboard covering the phone screen when the caster stands inside it.
                float width = MathUtils.Min(item.Size, distance * 0.8f);
                Vector3 up = item.Clip.Facing == SpriteFxFacing.Vertical ? Vector3.UnitY : screenUp;
                Vector3 p0 = anchor - right * (width * item.Clip.PivotX) + up * (width * item.Clip.PivotY);
                Vector3 p1 = p0 + right * width, p2 = p1 - up * width, p3 = p0 - up * width;
                float u0, v0, u1, v1; item.Clip.Uv(frame, out u0, out v0, out u1, out v1);
                float fade = MathUtils.Saturate((budget.Distance - distance) / 8f) * MathUtils.Saturate((distance - 0.65f) / 0.85f);
                float tail = (float)((now - item.Birth) / item.Life);
                fade *= MathUtils.Saturate((1f - tail) / 0.18f) * (reducedFlashes ? 0.25f : 0.72f);
                // Native Additive is SourceAlpha/One; tint alpha attenuates both core and soft glow.
                Color tint = new Color(255, 255, 255, (int)(255f * fade));
                m_batch.QueueQuad(p0, p1, p2, p3, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), tint);
                LastQuadCount++;
            }
        }
    }
}
