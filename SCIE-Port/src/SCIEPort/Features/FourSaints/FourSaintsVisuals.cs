using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;
using SCIEPort.Features.Stormhammer;

namespace SCIEPort.Features.FourSaints
{
    /// <summary>Bone-following, visual-only feedback derived from existing authoritative Four Saints state.</summary>
    internal static class FourSaintsVisuals
    {
        private sealed class Avatar
        {
            internal Project Project;
            internal int LastFrame;
        }

        private static readonly Dictionary<ComponentModel, Avatar> s_avatars = new Dictionary<ComponentModel, Avatar>();
        private static readonly List<ComponentModel> s_remove = new List<ComponentModel>();
        private static PrimitivesRenderer3D s_renderer;
        private static TexturedBatch3D s_etch;
        private static TexturedBatch3D s_glow;

        internal static void TouchAvatar(ComponentModel model, Camera camera)
        {
            ComponentHumanModel human = model as ComponentHumanModel;
            ComponentPlayer player = model?.Entity?.FindComponent<ComponentPlayer>(false);
            SubsystemFourSaints runtime = player?.Project.FindSubsystem<SubsystemFourSaints>(false);
            float qinglong, xuanwu, baihu, zhuque;
            if (human == null || player == null || player.ComponentHealth.Health <= 0f || runtime == null
                || !runtime.TryGetVisualState(player, out qinglong, out xuanwu, out baihu, out zhuque))
            {
                if (model != null) s_avatars.Remove(model);
                return;
            }

            int quality = Quality();
            float visibleDistance = quality == 0 ? 32f : 48f;
            if (Vector3.DistanceSquared(player.ComponentBody.Position, camera.ViewPosition) > visibleDistance * visibleDistance)
            {
                s_avatars.Remove(model);
                return;
            }

            EnsureRenderer();
            Avatar avatar;
            if (!s_avatars.TryGetValue(model, out avatar))
            {
                avatar = new Avatar { Project = model.Project };
                s_avatars.Add(model, avatar);
            }
            avatar.LastFrame = Time.FrameIndex;

            Matrix head = BoneWorld(human, human.m_headBone, camera, player.ComponentBody.BoundingBox.Center() + Vector3.UnitY * 0.65f);
            Matrix body = BoneWorld(human, human.m_bodyBone, camera, player.ComponentBody.BoundingBox.Center());
            Matrix leg1 = BoneWorld(human, human.m_leg1Bone, camera, player.ComponentBody.Position + Vector3.UnitY * 0.45f);
            Matrix leg2 = BoneWorld(human, human.m_leg2Bone, camera, player.ComponentBody.Position + Vector3.UnitY * 0.45f);
            Vector3 viewRight = SafeNormalize(Vector3.Cross(camera.ViewDirection, Vector3.UnitY), Vector3.UnitX);
            Vector3 viewUp = SafeNormalize(Vector3.Cross(viewRight, camera.ViewDirection), Vector3.UnitY);
            double now = Time.RealTime;

            if (qinglong > 0f)
            {
                float pulse = qinglong * (0.82f + 0.18f * MathUtils.Sin((float)now * 8f));
                Glyph(head.Translation + head.Up * 0.12f - head.Forward * 0.19f, viewRight, viewUp, 0.22f, 0, new Color(103, 226, 199, (int)(205 * pulse)));
                if (quality > 0)
                    Glyph(head.Translation + head.Up * (0.28f + 0.08f * (1f - qinglong)), viewRight, viewUp, 0.11f, 0, new Color(103, 226, 199, (int)(120 * pulse)));
                if (quality > 1)
                {
                    Glyph(head.Translation - head.Right * 0.22f + head.Up * 0.08f, viewRight, viewUp, 0.07f, 0, new Color(103, 226, 199, (int)(85 * pulse)));
                    Glyph(head.Translation + head.Right * 0.22f + head.Up * 0.08f, viewRight, viewUp, 0.07f, 0, new Color(103, 226, 199, (int)(85 * pulse)));
                }
            }
            if (xuanwu > 0f)
            {
                float expand = 1f + 0.22f * (1f - xuanwu);
                Glyph(body.Translation - body.Forward * 0.24f + body.Up * 0.08f, viewRight, viewUp, 0.30f * expand, 1, new Color(219, 177, 99, (int)(220 * xuanwu)));
                if (quality > 1)
                    Glyph(body.Translation - body.Forward * 0.26f + body.Up * 0.08f, viewRight, viewUp, 0.36f * expand, 1, new Color(219, 177, 99, (int)(70 * xuanwu)));
            }
            if (baihu > 0f)
            {
                float flow = 0.72f + 0.20f * MathUtils.Sin((float)now * 6.4f + player.Entity.EntityId * 0.17f);
                byte alpha = (byte)(MathUtils.Saturate(flow) * 145f);
                Glyph(leg1.Translation - leg1.Forward * 0.11f, viewRight, viewUp, 0.17f, 2, new Color(235, 245, 244, (int)alpha));
                Glyph(leg2.Translation - leg2.Forward * 0.11f, viewRight, viewUp, 0.17f, 2, new Color(235, 245, 244, (int)alpha));
                if (quality > 0)
                {
                    float drift = (float)MathUtils.Remainder(now * 0.9 + player.Entity.EntityId * 0.07, 1.0);
                    Vector3 centre = (leg1.Translation + leg2.Translation) * 0.5f;
                    Glyph(centre - body.Right * (0.28f + 0.12f * drift) + body.Up * drift, viewRight, viewUp, 0.10f, 2, new Color(220, 237, 239, (int)(100 * (1f - drift))));
                }
            }
            if (zhuque > 0f)
            {
                float bloom = 0.86f + 0.14f * MathUtils.Sin((float)now * 9f);
                Glyph(body.Translation - body.Forward * 0.28f + body.Up * 0.10f, viewRight, viewUp, 0.38f * bloom, 3, new Color(255, 171, 66, (int)(225 * zhuque)));
                if (quality > 0)
                {
                    Vector3 feet = (leg1.Translation + leg2.Translation) * 0.5f - body.Up * 0.25f;
                    Glyph(feet - body.Right * 0.22f, viewRight, viewUp, 0.14f, 3, new Color(255, 199, 91, (int)(150 * zhuque)));
                    Glyph(feet + body.Right * 0.22f, -viewRight, viewUp, 0.14f, 3, new Color(255, 199, 91, (int)(150 * zhuque)));
                }
            }

            s_renderer.Flush(camera.ViewProjectionMatrix);
            if ((Time.FrameIndex & 127) == 0) Prune();
        }

        internal static void Reset(Project project)
        {
            QinglongCrownRenderer.Reset(project);
            FourSaintsArmorRenderer.Reset(project);
            s_remove.Clear();
            foreach (KeyValuePair<ComponentModel, Avatar> pair in s_avatars)
                if (pair.Value.Project == project) s_remove.Add(pair.Key);
            foreach (ComponentModel model in s_remove) s_avatars.Remove(model);
            s_remove.Clear();
        }

        private static void EnsureRenderer()
        {
            if (s_renderer != null) return;
            Texture2D texture = ContentManager.Get<Texture2D>("Textures/FourSaintsEffects");
            s_renderer = new PrimitivesRenderer3D();
            s_etch = s_renderer.TexturedBatch(texture, false, 0, DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.AlphaBlend, SamplerState.LinearClamp);
            s_glow = s_renderer.TexturedBatch(texture, false, 1, DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.Additive, SamplerState.LinearClamp);
        }

        private static int Quality()
        {
            return StormhammerOptions.Quality == StormhammerEffectQuality.Low ? 0 : StormhammerOptions.Quality == StormhammerEffectQuality.Medium ? 1 : 2;
        }

        private static Matrix BoneWorld(ComponentHumanModel human, ModelBone bone, Camera camera, Vector3 fallback)
        {
            Matrix[] transforms = human.AbsoluteBoneTransformsForCamera;
            if (bone == null || transforms == null || bone.Index < 0 || bone.Index >= transforms.Length)
                return Matrix.CreateTranslation(fallback);
            Matrix result = transforms[bone.Index] * camera.InvertedViewMatrix;
            result.Right = SafeNormalize(result.Right, Vector3.UnitX);
            result.Up = SafeNormalize(result.Up, Vector3.UnitY);
            result.Forward = SafeNormalize(result.Forward, Vector3.UnitZ);
            return result;
        }

        private static Vector3 SafeNormalize(Vector3 value, Vector3 fallback)
        {
            return value.LengthSquared() > 0.0001f ? Vector3.Normalize(value) : fallback;
        }

        private static void Glyph(Vector3 position, Vector3 right, Vector3 up, float size, int index, Color color)
        {
            float u0 = index * 0.25f, u1 = u0 + 0.25f;
            Vector3 x = right * size, y = up * size;
            Vector2 uv0 = new Vector2(u0, 1f), uv1 = new Vector2(u1, 1f), uv2 = new Vector2(u1, 0f), uv3 = new Vector2(u0, 0f);
            s_etch.QueueQuad(position - x - y, position + x - y, position + x + y, position - x + y, uv0, uv1, uv2, uv3, color);
            Color glow = new Color(color.R, color.G, color.B, (byte)(color.A / 3));
            x *= 1.15f; y *= 1.15f;
            s_glow.QueueQuad(position - x - y, position + x - y, position + x + y, position - x + y, uv0, uv1, uv2, uv3, glow);
        }

        private static void Prune()
        {
            s_remove.Clear();
            foreach (KeyValuePair<ComponentModel, Avatar> pair in s_avatars)
                if (pair.Key == null || pair.Key.Entity == null || Time.FrameIndex - pair.Value.LastFrame > 180) s_remove.Add(pair.Key);
            foreach (ComponentModel model in s_remove) s_avatars.Remove(model);
            s_remove.Clear();
        }
    }
}
