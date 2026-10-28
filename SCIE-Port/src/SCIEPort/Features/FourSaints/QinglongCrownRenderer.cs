using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.FourSaints
{
    /// <summary>Persistent, opaque head attachment; independent of skill pulses and package 245.</summary>
    internal static class QinglongCrownRenderer
    {
        private static BlockMesh s_mesh;
        private static BlockMesh s_icon;
        private static Texture2D s_texture;
        private static bool s_failed;
        private static readonly PrimitivesRenderer3D s_renderer = new PrimitivesRenderer3D();
        private static readonly DrawBlockEnvironmentData s_environment = new DrawBlockEnvironmentData();
        private static readonly Dictionary<Model, Matrix> s_bindCorrections = new Dictionary<Model, Matrix>();

        // The owner requested an enabled candidate package for personal device acceptance.
        // Diagnostics can be enabled independently; this default does not imply device validation.
        internal static bool PreviewEnabled = true;
        internal static bool Diagnostics;

        private static bool EnsureResources()
        {
            if (s_mesh != null) return true;
            if (s_failed) return false;
            try
            {
                Model model = ContentManager.Get<Model>("Models/QinglongCrown");
                Texture2D texture = ContentManager.Get<Texture2D>("Textures/QinglongCrown");
                BlockMesh mesh = new BlockMesh();
                BlockMesh icon = new BlockMesh();
                Matrix centreIcon = Matrix.CreateTranslation(0f, -0.35f, 0f);
                foreach (ModelMesh part in model.Meshes)
                {
                    Matrix bind = BlockMesh.GetBoneAbsoluteTransform(part.ParentBone);
                    foreach (ModelMeshPart meshPart in part.MeshParts)
                    {
                        mesh.AppendModelMeshPart(meshPart, bind, false, false, false, false, Color.White);
                        icon.AppendModelMeshPart(meshPart, bind * centreIcon, false, false, false, false, Color.White);
                    }
                }
                // Publish the cache only after all assets successfully load. ContentManager owns GPU assets.
                s_texture = texture;
                s_icon = icon;
                s_mesh = mesh;
                Log.Information("[SCIEPort] Qinglong V3 crown loaded (680 triangles, persistent Head attachment).");
                return true;
            }
            catch (Exception exception)
            {
                s_failed = true;
                Log.Error("[SCIEPort] Qinglong V3 crown unavailable; native clothing retained: " + exception);
                return false;
            }
        }

        internal static bool DrawIcon(PrimitivesRenderer3D renderer, Color color, float size,
            ref Matrix matrix, DrawBlockEnvironmentData environment)
        {
            if (!EnsureResources()) return false;
            BlocksManager.DrawMeshBlock(renderer, s_icon, s_texture, color, size * 1.7f, ref matrix, environment);
            return true;
        }

        internal static void QueuePreview(PrimitivesRenderer3D renderer, ComponentPlayer player,
            Model model, Matrix[] transforms, Matrix placement, Color color, DrawBlockEnvironmentData environment)
        {
            if (!PreviewEnabled || !SubsystemFourSaints.Wears(player, ClothingSlot.Head, FourSaintsRules.QinglongIndex)
                || !EnsureResources()) return;
            ModelBone head = model.FindBone("Head");
            if (head.Index < 0 || head.Index >= transforms.Length) return;
            Matrix correction;
            if (!s_bindCorrections.TryGetValue(model, out correction))
            {
                Matrix basis = BlockMesh.GetBoneAbsoluteTransform(head);
                basis.Translation = Vector3.Zero;
                correction = Matrix.Invert(basis);
                s_bindCorrections.Add(model, correction);
            }
            Matrix world = correction * transforms[head.Index] * placement;
            environment.InWorldMatrix = world;
            BlocksManager.DrawMeshBlock(renderer, s_mesh, s_texture, color, 1f, ref world, environment);
        }

        internal static void Draw(ComponentModel component, Camera camera)
        {
            if (!PreviewEnabled && !Diagnostics) return;
            ComponentHumanModel human = component as ComponentHumanModel;
            if (human == null || !human.IsVisibleForCamera || (human.Opacity ?? 1f) < 0.99f) return;
            ComponentPlayer player = human.Entity.FindComponent<ComponentPlayer>(false);
            if (player == null || player.ComponentHealth.Health <= 0f ||
                !SubsystemFourSaints.Wears(player, ClothingSlot.Head, FourSaintsRules.QinglongIndex)) return;
            Matrix[] transforms = human.AbsoluteBoneTransformsForCamera;
            ModelBone head = human.m_headBone;
            if (head == null || transforms == null || head.Index < 0 || head.Index >= transforms.Length) return;

            Matrix headWorld = transforms[head.Index] * camera.InvertedViewMatrix;
            float distance = Vector3.DistanceSquared(headWorld.Translation, camera.ViewPosition);
            // Hide only the camera-near head, including first person. Other players remain visible.
            if (distance < 0.45f * 0.45f || distance > 48f * 48f) return;
            Matrix correction;
            if (!s_bindCorrections.TryGetValue(human.Model, out correction))
            {
                // Native heads are Z-up modelling units, NOT Y-up meters. Retain the complete
                // animated matrix, and cancel the bind basis once instead of normalizing its axes.
                Matrix basis = BlockMesh.GetBoneAbsoluteTransform(head);
                basis.Translation = Vector3.Zero;
                correction = Matrix.Invert(basis);
                s_bindCorrections.Add(human.Model, correction);
                if (Diagnostics) Log.Information("[SCIEPort] Qinglong Head bind correction: " + correction);
            }
            Matrix world = correction * headWorld;
            if (Diagnostics)
            {
                FlatBatch3D axes = s_renderer.FlatBatch(1, DepthStencilState.DepthRead,
                    RasterizerState.CullNone, BlendState.AlphaBlend);
                Vector3 origin = world.Translation;
                axes.QueueLine(origin, origin + world.Right * 0.25f, Color.Red);
                axes.QueueLine(origin, origin + world.Up * 0.35f, Color.Green);
                axes.QueueLine(origin, origin - world.Forward * 0.25f, Color.Blue);
            }
            if (PreviewEnabled && EnsureResources())
            {
                SubsystemTerrain terrain = player.Project.FindSubsystem<SubsystemTerrain>(true);
                s_environment.SubsystemTerrain = terrain;
                Vector3 position = headWorld.Translation;
                s_environment.Light = terrain.Terrain.GetCellLight(Terrain.ToCell(position.X),
                    MathUtils.Clamp(Terrain.ToCell(position.Y), 0, 255), Terrain.ToCell(position.Z));
                s_environment.InWorldMatrix = world;
                // DrawMeshBlock already applies an environment ViewProjectionMatrix on the CPU.
                // Keep it null here: Flush applies camera projection exactly once.
                s_environment.ViewProjectionMatrix = null;
                BlocksManager.DrawMeshBlock(s_renderer, s_mesh, s_texture, Color.White, 1f, ref world, s_environment);
            }
            s_renderer.Flush(camera.ViewProjectionMatrix);
        }

        internal static void Reset(Project project)
        {
            // No per-player state or transforms survive world changes. Shared content remains owned by ContentManager.
            s_environment.SubsystemTerrain = null;
            s_bindCorrections.Clear();
            s_mesh = null;
            s_icon = null;
            s_texture = null;
            s_failed = false;
        }
    }
}
