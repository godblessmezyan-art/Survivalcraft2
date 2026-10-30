using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>Touch-first HUD anchored to the target build's real native Look control.</summary>
    internal static class GungnirHud
    {
        /// <summary>Atlas cells: 0 Rune Blessing, 1 Fate Severance, 2 Eternal Judgment.</summary>
        private const int IconCount = 3;

        private sealed class SkillButton
        {
            internal CanvasWidget Visual;
            internal CanvasWidget HitTarget;
            internal RectangleWidget Icon;
            internal RectangleWidget ReadyGlow;
            internal RectangleWidget Energy;
            internal LabelWidget Cooldown;
            internal ClickableWidget Clickable;
            internal float Size;
            internal Color Accent;
            internal bool Ultimate;
        }

        private sealed class Hud
        {
            internal CanvasWidget Root;
            internal readonly SkillButton[] Skills = new SkillButton[3];
            internal Vector2 LastLookMin = new Vector2(float.MinValue);
            internal Vector2 LastLookMax = new Vector2(float.MinValue);
            internal Vector2 LastHotbarMin = new Vector2(float.MinValue);
            internal Vector2 LastHotbarMax = new Vector2(float.MinValue);
            internal Vector2 LastRootSize = new Vector2(float.MinValue);
            internal bool LastLeftHanded;
        }

        private static readonly Dictionary<ComponentGui, Hud> s_huds = new Dictionary<ComponentGui, Hud>();

        internal static void Clear()
        {
            foreach (var entry in s_huds)
            {
                if (entry.Key?.ControlsContainerWidget != null && entry.Value.Root.ParentWidget == entry.Key.ControlsContainerWidget)
                    entry.Key.ControlsContainerWidget.Children.Remove(entry.Value.Root);
            }
            s_huds.Clear();
        }

        internal static void Update(ComponentGui gui)
        {
            ComponentPlayer player = gui?.m_componentPlayer;
            bool visible = SubsystemGungnir.Equipped(player) && gui.ControlsContainerWidget != null && gui.ControlsContainerWidget.IsVisible;
            Hud hud;
            if (!s_huds.TryGetValue(gui, out hud))
            {
                if (!visible) return;
                hud = Create(gui);
                s_huds.Add(gui, hud);
            }
            hud.Root.IsVisible = visible;
            if (!visible) return;

            SubsystemGungnir runtime = player.Project.FindSubsystem<SubsystemGungnir>(false);
            if (runtime == null) { hud.Root.IsVisible = false; return; }
            Layout(gui, hud);
            SubsystemGungnir.State state = runtime.Get(player);
            double now = runtime.Now;
            SetState(hud.Skills[0], (float)(state.BuffReady - now), (float)(state.BuffEnd - now), 15f, (float)(now - state.BuffVisualStart));
            float fatalismAge = (float)(now - state.FatalismStart);
            SetState(hud.Skills[1], (float)(state.FatalismReady - now), (float)(GungnirRules.FatalismEnd - fatalismAge), GungnirRules.FatalismEnd, fatalismAge);
            SetState(hud.Skills[2], (float)(state.JudgmentReady - now), 0f, 0f, (float)(now - state.CastStart));
            for (int i = 0; i < hud.Skills.Length; i++)
            {
                // Explicit mapping, never index+1: Rune Blessing=1, Fate Severance=5, Eternal Judgment=2.
                if (hud.Skills[i].Clickable.IsClicked)
                    runtime.Request(player, GungnirRules.HudCommand(i), player.GameWidget.ActiveCamera.ViewDirection);
            }
        }

        private static void SetState(SkillButton button, float cooldown, float duration, float durationTotal, float feedbackAge)
        {
            cooldown = MathUtils.Max(0f, cooldown);
            duration = MathUtils.Max(0f, duration);
            bool ready = cooldown <= 0.01f;
            bool active = duration > 0.01f || feedbackAge >= 0f && feedbackAge < 0.32f;
            bool pressed = button.Clickable.IsPressed;
            float pulse = 0.5f + 0.5f * MathUtils.Sin((float)Time.RealTime * 2.35f + (button.Ultimate ? 1.1f : 0f));
            button.Clickable.IsEnabled = ready;
            button.Cooldown.Text = duration > 0f ? MathUtils.Ceiling(duration).ToString("0") : ready ? string.Empty : MathUtils.Ceiling(cooldown).ToString("0");
            button.Icon.FillColor = pressed || active ? new Color(255, 249, 218) : ready ? Color.White : new Color(105, 110, 120, 205);
            button.ReadyGlow.FillColor = ready ? button.Accent * (pressed ? 0.42f : 0.08f + pulse * (button.Ultimate ? 0.18f : 0.12f)) : Color.Transparent;
            button.Cooldown.Color = active ? new Color(255, 246, 204) : Color.White;
            float energy = durationTotal > 0f && duration > 0f ? MathUtils.Saturate(duration / durationTotal) : ready ? 1f : 0f;
            button.Energy.Size = new Vector2((button.Size - 10f) * energy, 3f);
            button.Energy.FillColor = active ? new Color(255, 248, 205, 245) : ready ? button.Accent * 0.9f : new Color(92, 88, 78, 190);
        }

        private static void Layout(ComponentGui gui, Hud hud)
        {
            ContainerWidget controls = gui.ControlsContainerWidget;
            Vector2 size = controls.ActualSize;
            if (size.X < 1f || size.Y < 1f) return;
            hud.Root.Size = size;
            GungnirHudLayout.Rect look = ToLocalRect(controls, gui.LookWidget);
            GungnirHudLayout.Rect hotbar = ToLocalRect(controls, gui.ShortInventoryWidget);
            bool changed = Vector2.DistanceSquared(hud.LastLookMin, new Vector2(look.X, look.Y)) > 0.01f
                || Vector2.DistanceSquared(hud.LastLookMax, new Vector2(look.Right, look.Bottom)) > 0.01f
                || Vector2.DistanceSquared(hud.LastHotbarMin, new Vector2(hotbar.X, hotbar.Y)) > 0.01f
                || Vector2.DistanceSquared(hud.LastHotbarMax, new Vector2(hotbar.Right, hotbar.Bottom)) > 0.01f
                || Vector2.DistanceSquared(hud.LastRootSize, size) > 0.01f
                || hud.LastLeftHanded != SettingsManager.LeftHandedLayout;
            GungnirHudLayout.Result placement = GungnirHudLayout.Place(size.X, size.Y, look, hotbar, SettingsManager.LeftHandedLayout);
            if (changed)
            {
                AvoidNativeControls(gui, hud, size, ref placement);
                CanvasWidget.SetPosition(hud.Skills[0].Visual, new Vector2(placement.Blessing.X, placement.Blessing.Y));
                CanvasWidget.SetPosition(hud.Skills[1].Visual, new Vector2(placement.Fatalism.X, placement.Fatalism.Y));
                CanvasWidget.SetPosition(hud.Skills[2].Visual, new Vector2(placement.Judgment.X, placement.Judgment.Y));
                bool lookOverlap = placement.Blessing.Intersects(look) || placement.Fatalism.Intersects(look) || placement.Judgment.Intersects(look);
                bool hotbarOverlap = placement.Blessing.Intersects(hotbar) || placement.Fatalism.Intersects(hotbar) || placement.Judgment.Intersects(hotbar);
                bool nativeOverlap = OverlapsNativeControls(gui, hud, placement.Blessing) || OverlapsNativeControls(gui, hud, placement.Fatalism) || OverlapsNativeControls(gui, hud, placement.Judgment);
                Log.Information(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[SCIEPort:GungnirHUD] viewport={0:0.#}x{1:0.#} look=({2:0.#},{3:0.#},{4:0.#},{5:0.#}) blessing=({6:0.#},{7:0.#},72,72) fatalism=({8:0.#},{9:0.#},72,72) judgment=({15:0.#},{16:0.#},88,88) mirrored={10} fallback={11} overlapLook={12} overlapHotbar={13} overlapNative={14} hitTarget=iconOnly",
                    size.X, size.Y, look.X, look.Y, look.Width, look.Height, placement.Blessing.X, placement.Blessing.Y,
                    placement.Fatalism.X, placement.Fatalism.Y, placement.Mirrored, placement.UsedFallback, lookOverlap, hotbarOverlap, nativeOverlap,
                    placement.Judgment.X, placement.Judgment.Y));
                hud.LastLookMin = new Vector2(look.X, look.Y); hud.LastLookMax = new Vector2(look.Right, look.Bottom);
                hud.LastHotbarMin = new Vector2(hotbar.X, hotbar.Y); hud.LastHotbarMax = new Vector2(hotbar.Right, hotbar.Bottom);
                hud.LastRootSize = size; hud.LastLeftHanded = SettingsManager.LeftHandedLayout;
            }
        }

        private static void AvoidNativeControls(ComponentGui gui, Hud hud, Vector2 viewport, ref GungnirHudLayout.Result placement)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                if (!OverlapsNativeControls(gui, hud, placement.Blessing) && !OverlapsNativeControls(gui, hud, placement.Judgment)) return;
                placement.UsedFallback = true;
                float top = MathUtils.Min(placement.Blessing.Y, placement.Judgment.Y);
                if (top >= 24f)
                {
                    placement.Blessing.Y -= 16f; placement.Judgment.Y -= 16f;
                }
                else
                {
                    float shift = placement.Mirrored ? 16f : -16f;
                    placement.Blessing.X = MathUtils.Clamp(placement.Blessing.X + shift, 8f, viewport.X - placement.Blessing.Width - 8f);
                    placement.Judgment.X = MathUtils.Clamp(placement.Judgment.X + shift, 8f, viewport.X - placement.Judgment.Width - 8f);
                }
            }
        }

        private static bool OverlapsNativeControls(ComponentGui gui, Hud hud, GungnirHudLayout.Rect rect)
        {
            ContainerWidget controls = gui.ControlsContainerWidget;
            foreach (Widget widget in controls.AllChildren)
            {
                if (widget == hud.Root || widget.IsChildWidgetOf(hud.Root) || !widget.IsVisibleGlobal || !widget.IsHitTestVisible) continue;
                // DragHost and other full-screen routing layers are not visible native controls;
                // only concrete touch/button/slot surfaces participate in collision avoidance.
                if (!(widget is ButtonWidget) && !(widget is TouchInputWidget) && !(widget is MoveRoseWidget) && !(widget is InventorySlotWidget)) continue;
                GungnirHudLayout.Rect native = ToLocalRect(controls, widget);
                if (native.IsValid && rect.Intersects(native)) return true;
            }
            return false;
        }

        private static GungnirHudLayout.Rect ToLocalRect(ContainerWidget controls, Widget widget)
        {
            if (widget == null || !widget.IsVisibleGlobal || widget.ActualSize.X < 1f || widget.ActualSize.Y < 1f) return default(GungnirHudLayout.Rect);
            Vector2 min = controls.ScreenToWidget(widget.GlobalBounds.Min);
            Vector2 max = controls.ScreenToWidget(widget.GlobalBounds.Max);
            return new GungnirHudLayout.Rect { X = MathUtils.Min(min.X, max.X), Y = MathUtils.Min(min.Y, max.Y), Width = MathUtils.Abs(max.X - min.X), Height = MathUtils.Abs(max.Y - min.Y) };
        }

        private static Hud Create(ComponentGui gui)
        {
            Texture2D atlas = ContentManager.Get<Texture2D>("Textures/GungnirSkills");
            var hud = new Hud
            {
                Root = new CanvasWidget
                {
                    Name = "SCIEPort.Gungnir.NativeAnchoredSkills",
                    HorizontalAlignment = WidgetAlignment.Near,
                    VerticalAlignment = WidgetAlignment.Near,
                    IsHitTestVisible = false,
                    IsVisible = false
                }
            };
            hud.Skills[0] = CreateButton(atlas, 0, GungnirHudLayout.BlessingSize, new Color(111, 226, 214), false);
            hud.Skills[1] = CreateButton(atlas, 1, GungnirHudLayout.FatalismSize, new Color(238, 216, 158), false);
            hud.Skills[2] = CreateButton(atlas, 2, GungnirHudLayout.JudgmentSize, new Color(245, 226, 178), true);
            hud.Root.Children.Add(hud.Skills[0].Visual);
            hud.Root.Children.Add(hud.Skills[1].Visual);
            hud.Root.Children.Add(hud.Skills[2].Visual);
            gui.ControlsContainerWidget.Children.Add(hud.Root);
            return hud;
        }

        private static SkillButton CreateButton(Texture2D atlas, int icon, float size, Color accent, bool ultimate)
        {
            var skill = new SkillButton { Size = size, Accent = accent, Ultimate = ultimate };
            skill.Visual = new CanvasWidget { Size = new Vector2(size), IsHitTestVisible = false };
            skill.Icon = new RectangleWidget
            {
                Size = new Vector2(size),
                Subtexture = new Subtexture(atlas, new Vector2(icon * (1f / IconCount), 0f), new Vector2((icon + 1) * (1f / IconCount), 1f)),
                FillColor = Color.White,
                OutlineColor = Color.Transparent,
                OutlineThickness = 0f,
                IsHitTestVisible = false
            };
            skill.ReadyGlow = new RectangleWidget
            {
                Size = new Vector2(size),
                Subtexture = skill.Icon.Subtexture,
                FillColor = Color.Transparent,
                OutlineColor = Color.Transparent,
                OutlineThickness = 0f,
                IsHitTestVisible = false
            };
            skill.Cooldown = new LabelWidget { Size = new Vector2(size), TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter, FontScale = size >= 80f ? 0.92f : 0.78f, DropShadow = true, Color = Color.White, IsHitTestVisible = false };
            skill.Energy = new RectangleWidget { Size = new Vector2(size - 10f, 3f), FillColor = new Color(224, 184, 91), OutlineColor = Color.Transparent, OutlineThickness = 0f, IsHitTestVisible = false };
            skill.HitTarget = new CanvasWidget { Size = new Vector2(size), IsHitTestVisible = false };
            skill.Clickable = new ClickableWidget { HorizontalAlignment = WidgetAlignment.Stretch, VerticalAlignment = WidgetAlignment.Stretch, SoundName = "Audio/UI/ButtonClick" };
            skill.HitTarget.Children.Add(skill.Clickable);
            skill.Visual.Children.Add(skill.Icon);
            skill.Visual.Children.Add(skill.ReadyGlow);
            skill.Visual.Children.Add(skill.Energy);
            skill.Visual.Children.Add(skill.Cooldown);
            skill.Visual.Children.Add(skill.HitTarget);
            CanvasWidget.SetPosition(skill.Energy, new Vector2(5f, size - 5f));
            return skill;
        }
    }
}
