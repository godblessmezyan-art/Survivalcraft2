using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using GameEntitySystem;

namespace SCIEPort.Features.FourSaints
{
    /// <summary>Persistent solid torso/leg/foot attachments; no gameplay or network state.</summary>
    internal static class FourSaintsArmorRenderer
    {
        private sealed class Armor
        {
            internal readonly string ModelPath;
            internal readonly int ClothingIndex;
            internal readonly ClothingSlot Slot;
            internal readonly bool Paired;
            internal BlockMesh[] Meshes;
            internal BlockMesh Icon;
            internal bool Failed;

            internal Armor(string modelPath, int clothingIndex, ClothingSlot slot, bool paired)
            {
                ModelPath = modelPath;
                ClothingIndex = clothingIndex;
                Slot = slot;
                Paired = paired;
            }
        }

        private sealed class Binding
        {
            // Body, Leg1, Leg2. Built once per actual native model, never per animation frame.
            internal readonly int[] Indices = new int[3];
            internal readonly Matrix[] Corrections = new Matrix[3];

            internal Binding(Model model)
            {
                ModelBone[] bones = { model.FindBone("Body"), model.FindBone("Leg1"), model.FindBone("Leg2") };
                for (int i = 0; i < bones.Length; i++)
                {
                    ModelBone bone = bones[i];
                    Indices[i] = bone != null ? bone.Index : -1;
                    if (bone == null) continue;
                    Matrix basis = BlockMesh.GetBoneAbsoluteTransform(bone);
                    basis.Translation = Vector3.Zero;
                    // Authored around the audited male mesh in meters. Female root is 0.95x;
                    // retain its shorter body/legs so the soles still meet the ground.
                    // Female leg widths are approximately the male width, so retain X for legs.
                    float scale = basis.Right.Length() / 0.02413f;
                    Matrix fit = Matrix.CreateScale(i == 0 ? scale : 1f, scale, scale);
                    Corrections[i] = fit * Matrix.Invert(basis);
                }
            }
        }

        private static readonly Armor[] s_armors =
        {
            new Armor("Models/XuanwuArmorV3", FourSaintsRules.XuanwuIndex, ClothingSlot.Torso, false),
            new Armor("Models/BaihuLegArmorV3", FourSaintsRules.BaihuIndex, ClothingSlot.Legs, true),
            new Armor("Models/ZhuqueBootsV3", FourSaintsRules.ZhuqueIndex, ClothingSlot.Feet, true)
        };
        private static readonly Dictionary<Model, Binding> s_bindings = new Dictionary<Model, Binding>();
        private static readonly PrimitivesRenderer3D s_renderer = new PrimitivesRenderer3D();
        private static readonly DrawBlockEnvironmentData s_environment = new DrawBlockEnvironmentData();
        private static Texture2D s_texture;
        internal static bool PreviewEnabled = true;
        internal static bool Diagnostics;

        private static int BoneSlot(string name)
        {
            return name == "Body" ? 0 : name == "Leg1" ? 1 : name == "Leg2" ? 2 : -1;
        }

        private static bool Ensure(Armor armor)
        {
            if (armor.Meshes != null) return true;
            if (armor.Failed) return false;
            try
            {
                Model model = ContentManager.Get<Model>(armor.ModelPath);
                if (s_texture == null) s_texture = ContentManager.Get<Texture2D>("Textures/FourSaintsArmorV3");
                BlockMesh[] meshes = { new BlockMesh(), new BlockMesh(), new BlockMesh() };
                BlockMesh icon = new BlockMesh();
                // Stable, recognisable icons use all pieces of this garment, with equal visual size.
                Matrix iconTransform;
                if (!armor.Paired) iconTransform = Matrix.CreateTranslation(0f, -1.0445f, 0.0485f) * Matrix.CreateScale(1.05f);
                else if (armor.ClothingIndex == FourSaintsRules.BaihuIndex)
                    iconTransform = Matrix.CreateTranslation(0f, 0.3285f, 0.017f) * Matrix.CreateScale(1.21f);
                else iconTransform = Matrix.CreateTranslation(0f, 0.5955f, 0.0295f) * Matrix.CreateScale(1.60f);
                foreach (ModelMesh mesh in model.Meshes)
                {
                    int slot = BoneSlot(mesh.ParentBone.Name);
                    if (slot < 0 || armor.Paired == (slot == 0))
                        throw new InvalidOperationException("Unexpected armor attachment bone: " + mesh.ParentBone.Name);
                    Matrix bind = BlockMesh.GetBoneAbsoluteTransform(mesh.ParentBone);
                    foreach (ModelMeshPart part in mesh.MeshParts)
                    {
                        meshes[slot].AppendModelMeshPart(part, bind, false, false, false, false, Color.White);
                        icon.AppendModelMeshPart(part, bind * iconTransform, false, false, false, false, Color.White);
                    }
                }
                if (armor.Paired)
                {
                    if (meshes[1].Indices.Count == 0 || meshes[2].Indices.Count == 0)
                        throw new InvalidOperationException("Both leg attachment meshes are required.");
                }
                else if (meshes[0].Indices.Count == 0) throw new InvalidOperationException("Body attachment mesh is empty.");
                armor.Icon = icon;
                armor.Meshes = meshes;
                Log.Information("[SCIEPort] Four Saints V3 solid armor loaded: " + armor.ModelPath);
                return true;
            }
            catch (Exception exception)
            {
                // Log once per asset per world; leave other garments and native clothing operational.
                armor.Failed = true;
                Log.Error("[SCIEPort] V3 armor unavailable, native clothing retained: " + armor.ModelPath + ": " + exception);
                return false;
            }
        }

        internal static bool DrawIcon(int clothingIndex, PrimitivesRenderer3D renderer, Color color,
            float size, ref Matrix matrix, DrawBlockEnvironmentData environment)
        {
            for (int i = 0; i < s_armors.Length; i++)
            {
                Armor armor = s_armors[i];
                if (armor.ClothingIndex != clothingIndex) continue;
                if (!Ensure(armor)) return false;
                BlocksManager.DrawMeshBlock(renderer, armor.Icon, s_texture, color, size, ref matrix, environment);
                return true;
            }
            return false;
        }

        internal static void Draw(ComponentModel component, Camera camera)
        {
            if (!PreviewEnabled && !Diagnostics) return;
            ComponentHumanModel human = component as ComponentHumanModel;
            if (human == null || !human.IsVisibleForCamera || (human.Opacity ?? 1f) < 0.99f) return;
            ComponentPlayer player = human.Entity.FindComponent<ComponentPlayer>(false);
            if (player == null || player.ComponentHealth.Health <= 0f) return;
            Matrix[] transforms = human.AbsoluteBoneTransformsForCamera;
            if (human.m_headBone == null || transforms == null || human.m_headBone.Index >= transforms.Length) return;
            Matrix headWorld = transforms[human.m_headBone.Index] * camera.InvertedViewMatrix;
            float distance = Vector3.DistanceSquared(headWorld.Translation, camera.ViewPosition);
            // Keep the local first-person view clear while leaving every remote character unaffected.
            if (distance < 0.45f * 0.45f || distance > 48f * 48f) return;

            Binding binding = null;
            bool queued = false;
            int diagnosticBones = 0;
            for (int a = 0; a < s_armors.Length; a++)
            {
                Armor armor = s_armors[a];
                if (!SubsystemFourSaints.Wears(player, armor.Slot, armor.ClothingIndex)) continue;
                if (PreviewEnabled && !Ensure(armor)) continue;
                if (binding == null && !s_bindings.TryGetValue(human.Model, out binding))
                {
                    binding = new Binding(human.Model);
                    s_bindings.Add(human.Model, binding);
                }
                if (!queued)
                {
                    SubsystemTerrain terrain = player.Project.FindSubsystem<SubsystemTerrain>(true);
                    Vector3 position = headWorld.Translation;
                    s_environment.SubsystemTerrain = terrain;
                    s_environment.Light = terrain.Terrain.GetCellLight(Terrain.ToCell(position.X),
                        MathUtils.Clamp(Terrain.ToCell(position.Y), 0, 255), Terrain.ToCell(position.Z));
                    s_environment.ViewProjectionMatrix = null; // projection applied exactly once at Flush
                }
                int start = armor.Paired ? 1 : 0, end = armor.Paired ? 3 : 1;
                for (int b = start; b < end; b++)
                {
                    int index = binding.Indices[b];
                    if (index < 0 || index >= transforms.Length) continue;
                    Matrix world = binding.Corrections[b] * transforms[index] * camera.InvertedViewMatrix;
                    if (PreviewEnabled)
                    {
                        s_environment.InWorldMatrix = world;
                        BlocksManager.DrawMeshBlock(s_renderer, armor.Meshes[b], s_texture, Color.White, 1f, ref world, s_environment);
                    }
                    if (Diagnostics && (diagnosticBones & (1 << b)) == 0)
                    {
                        FlatBatch3D axes = s_renderer.FlatBatch(1, DepthStencilState.DepthRead, RasterizerState.CullNone, BlendState.AlphaBlend);
                        Vector3 origin = world.Translation;
                        axes.QueueLine(origin, origin + world.Right * 0.22f, Color.Red);
                        axes.QueueLine(origin, origin + world.Up * 0.22f, Color.Green);
                        axes.QueueLine(origin, origin - world.Forward * 0.22f, Color.Blue);
                        diagnosticBones |= 1 << b;
                    }
                    queued = true;
                }
            }
            // All five animated pieces share one opaque material batch, flushed once per wearer/camera.
            if (queued) s_renderer.Flush(camera.ViewProjectionMatrix);
        }

        internal static void QueuePreview(PrimitivesRenderer3D renderer, ComponentPlayer player,
            Model model, Matrix[] transforms, Matrix placement, Color color, DrawBlockEnvironmentData environment)
        {
            if (!PreviewEnabled) return;
            Binding binding = null;
            for (int a = 0; a < s_armors.Length; a++)
            {
                Armor armor = s_armors[a];
                if (!SubsystemFourSaints.Wears(player, armor.Slot, armor.ClothingIndex) || !Ensure(armor)) continue;
                if (binding == null && !s_bindings.TryGetValue(model, out binding))
                {
                    binding = new Binding(model);
                    s_bindings.Add(model, binding);
                }
                int start = armor.Paired ? 1 : 0, end = armor.Paired ? 3 : 1;
                for (int b = start; b < end; b++)
                {
                    int index = binding.Indices[b];
                    if (index < 0 || index >= transforms.Length) continue;
                    Matrix world = binding.Corrections[b] * transforms[index] * placement;
                    environment.InWorldMatrix = world;
                    BlocksManager.DrawMeshBlock(renderer, armor.Meshes[b], s_texture, color, 1f, ref world, environment);
                }
            }
        }

        internal static void Reset(Project project)
        {
            s_environment.SubsystemTerrain = null;
            s_bindings.Clear();
            s_texture = null;
            foreach (Armor armor in s_armors)
            {
                armor.Meshes = null;
                armor.Icon = null;
                armor.Failed = false;
            }
        }
    }
}
