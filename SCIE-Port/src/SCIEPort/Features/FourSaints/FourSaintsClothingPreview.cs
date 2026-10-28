using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.FourSaints
{
    /// <summary>Attaches solid armor to the clothing page's separate, rotatable UI mannequin.</summary>
    internal static class FourSaintsClothingPreview
    {
        internal static void Update(ComponentGui gui)
        {
            ClothingWidget clothing = gui?.ModalPanelWidget as ClothingWidget;
            if (clothing == null || clothing.m_componentPlayer == null) return;
            // All Four Saints pieces are outer garments. Preserve the native inner/skin preview.
            PlayerModelWidget host = clothing.m_outerClothingModelWidget;
            ModelWidget original = host?.m_modelWidget;
            if (original == null || original is ArmorModelWidget) return;
            // Do not overwrite another mod's custom preview implementation.
            if (original.GetType() != typeof(ModelWidget)) return;
            ArmorModelWidget replacement = new ArmorModelWidget(clothing.m_componentPlayer)
            {
                Name = original.Name,
                Size = original.Size,
                Color = original.Color,
                ColorTransform = original.ColorTransform,
                UseAlphaThreshold = original.UseAlphaThreshold,
                IsPerspective = original.IsPerspective,
                OrthographicFrustumSize = original.OrthographicFrustumSize,
                ViewPosition = original.ViewPosition,
                ViewTarget = original.ViewTarget,
                ViewFov = original.ViewFov,
                ModelMatrix = original.ModelMatrix,
                AutoRotationVector = original.AutoRotationVector,
                TextureOverride = original.TextureOverride,
                HorizontalAlignment = original.HorizontalAlignment,
                VerticalAlignment = original.VerticalAlignment,
                Margin = original.Margin,
                LayoutTransform = original.LayoutTransform,
                RenderTransform = original.RenderTransform,
                IsVisible = original.IsVisible,
                IsEnabled = original.IsEnabled,
                Model = original.Model
            };
            if (original.m_boneTransforms != null && replacement.m_boneTransforms != null)
                for (int i = 0; i < original.m_boneTransforms.Length; i++)
                    replacement.SetBoneTransform(i, original.GetBoneTransform(i));
            int childIndex = host.Children.IndexOf(original);
            host.Children.Remove(original);
            host.Children.Insert(childIndex, replacement);
            // PlayerModelWidget keeps writing rotations, animation, textures and camera settings here.
            host.m_modelWidget = replacement;
        }

        private sealed class ArmorModelWidget : ModelWidget
        {
            private readonly ComponentPlayer m_player;
            private readonly PrimitivesRenderer3D m_renderer = new PrimitivesRenderer3D();
            private readonly DrawBlockEnvironmentData m_environment = new DrawBlockEnvironmentData { Light = 15 };

            internal ArmorModelWidget(ComponentPlayer player)
            {
                m_player = player;
            }

            public override void Draw(DrawContext dc)
            {
                base.Draw(dc);
                if (Model == null || m_absoluteBoneTransforms == null || m_player == null) return;
                // base.Draw has just configured the native mannequin camera, UI rectangle,
                // DPI/global transform and screen projection. Reuse that exact projection.
                LitShader shader = UseAlphaThreshold ? m_shaderAlpha : m_shader;
                m_environment.ViewProjectionMatrix = shader.Transforms.View * shader.Transforms.Projection;
                float time = (float)Time.RealTime + (float)(GetHashCode() % 1000) / 100f;
                Matrix autoRotation = AutoRotationVector.LengthSquared() > 0f
                    ? Matrix.CreateFromAxisAngle(Vector3.Normalize(AutoRotationVector), AutoRotationVector.Length() * time)
                    : Matrix.Identity;
                Matrix placement = ModelMatrix * autoRotation;
                Color color = Color * GlobalColorTransform;
                QinglongCrownRenderer.QueuePreview(m_renderer, m_player, Model,
                    m_absoluteBoneTransforms, placement, color, m_environment);
                FourSaintsArmorRenderer.QueuePreview(m_renderer, m_player, Model,
                    m_absoluteBoneTransforms, placement, color, m_environment);
                // DrawMeshBlock applies the UI projection above on the CPU. Do not project again
                // with a world camera; depth and scissor remain the native mannequin's states.
                m_renderer.Flush(Matrix.Identity);
            }
        }
    }
}
