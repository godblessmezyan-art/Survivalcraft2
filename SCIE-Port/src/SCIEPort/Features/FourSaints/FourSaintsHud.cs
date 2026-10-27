using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.FourSaints
{
    /// <summary>Touch-first Baihu skill HUD anchored to the target build's native Look region.</summary>
    internal static class FourSaintsHud
    {
        private sealed class Hud
        {
            internal CanvasWidget Root;
            internal CanvasWidget Visual;
            internal RectangleWidget Icon, Glow, Meter;
            internal LabelWidget Time;
            internal ClickableWidget Click;
            internal Vector2 LastSize = new Vector2(-1f);
            internal Vector2 LastLookMin = new Vector2(-1f), LastLookMax = new Vector2(-1f);
            internal Vector2 LastHotbarMin = new Vector2(-1f), LastHotbarMax = new Vector2(-1f);
            internal bool LastLeftHanded;
        }
        private static readonly Dictionary<ComponentGui, Hud> s_huds = new Dictionary<ComponentGui, Hud>();

        internal static void Update(ComponentGui gui)
        {
            ComponentPlayer player = gui?.m_componentPlayer;
            bool visible = player != null && SubsystemFourSaints.Wears(player, ClothingSlot.Legs, FourSaintsRules.BaihuIndex)
                && gui.ControlsContainerWidget != null && gui.ControlsContainerWidget.IsVisible && player.ComponentHealth.Health > 0f;
            Hud hud;
            if (!s_huds.TryGetValue(gui, out hud))
            {
                if (!visible) return;
                hud = Create(gui); s_huds.Add(gui, hud);
            }
            hud.Root.IsVisible = visible;
            if (!visible) return;
            SubsystemFourSaints runtime = player.Project.FindSubsystem<SubsystemFourSaints>(false);
            if (runtime == null) { hud.Root.IsVisible = false; return; }
            Layout(gui, hud);
            bool active = runtime.IsAwakened(player);
            bool locked = runtime.IsActionLocked(player);
            // Snapshot deadlines are intentionally exposed only through bounded display helpers.
            float activeRemaining, cooldownRemaining;
            runtime.GetBaihuTimes(player, out activeRemaining, out cooldownRemaining);
            bool ready = cooldownRemaining <= .01f && !locked;
            float pulse = .5f + .5f * MathUtils.Sin((float)Time.RealTime * 2.4f);
            hud.Click.IsEnabled = ready;
            hud.Time.Text = activeRemaining > 0f ? MathUtils.Ceiling(activeRemaining).ToString("0") : ready ? string.Empty : MathUtils.Ceiling(cooldownRemaining).ToString("0");
            hud.Icon.FillColor = active || hud.Click.IsPressed ? Color.White : ready ? new Color(238, 239, 235) : new Color(100, 104, 108, 210);
            hud.Glow.FillColor = ready ? new Color(198, 157, 72) * (.07f + .13f * pulse) : Color.Transparent;
            float ratio = activeRemaining > 0f ? activeRemaining / 5f : ready ? 1f : 0f;
            hud.Meter.Size = new Vector2(66f * MathUtils.Saturate(ratio), 3f);
            if (hud.Click.IsClicked) runtime.RequestAwaken(player);
        }

        private static Hud Create(ComponentGui gui)
        {
            Texture2D texture = ContentManager.Get<Texture2D>("Textures/FourSaintsBaihu");
            var hud = new Hud();
            hud.Root = new CanvasWidget { Name = "SCIEPort.FourSaints.Baihu", IsHitTestVisible = false, IsVisible = false };
            hud.Visual = new CanvasWidget { Size = new Vector2(76f), IsHitTestVisible = false };
            hud.Icon = new RectangleWidget { Size = new Vector2(76f), Subtexture = new Subtexture(texture, Vector2.Zero, Vector2.One), FillColor = Color.White, OutlineColor = Color.Transparent, IsHitTestVisible = false };
            hud.Glow = new RectangleWidget { Size = new Vector2(76f), Subtexture = hud.Icon.Subtexture, FillColor = Color.Transparent, OutlineColor = Color.Transparent, IsHitTestVisible = false };
            hud.Time = new LabelWidget { Size = new Vector2(76f), TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter, FontScale = .8f, DropShadow = true, Color = Color.White, IsHitTestVisible = false };
            hud.Meter = new RectangleWidget { Size = new Vector2(66f, 3f), FillColor = new Color(225, 192, 112), OutlineColor = Color.Transparent, IsHitTestVisible = false };
            var hit = new CanvasWidget { Size = new Vector2(76f), IsHitTestVisible = false };
            hud.Click = new ClickableWidget { HorizontalAlignment = WidgetAlignment.Stretch, VerticalAlignment = WidgetAlignment.Stretch, SoundName = "Audio/UI/ButtonClick" };
            hit.Children.Add(hud.Click); hud.Visual.Children.Add(hud.Icon); hud.Visual.Children.Add(hud.Glow); hud.Visual.Children.Add(hud.Time); hud.Visual.Children.Add(hud.Meter); hud.Visual.Children.Add(hit);
            CanvasWidget.SetPosition(hud.Meter, new Vector2(5f, 68f)); hud.Root.Children.Add(hud.Visual); gui.ControlsContainerWidget.Children.Add(hud.Root);
            return hud;
        }

        private static void Layout(ComponentGui gui, Hud hud)
        {
            ContainerWidget controls = gui.ControlsContainerWidget;
            Vector2 size = controls.ActualSize;
            if (size.X < 1f || size.Y < 1f) return;
            FourSaintsHudLayout.Rect look = Rect(controls, gui.LookWidget);
            FourSaintsHudLayout.Rect hotbar = Rect(controls, gui.ShortInventoryWidget);
            if (Vector2.DistanceSquared(hud.LastSize, size) < .01f
                && Vector2.DistanceSquared(hud.LastLookMin, new Vector2(look.X, look.Y)) < .01f
                && Vector2.DistanceSquared(hud.LastLookMax, new Vector2(look.Right, look.Bottom)) < .01f
                && Vector2.DistanceSquared(hud.LastHotbarMin, new Vector2(hotbar.X, hotbar.Y)) < .01f
                && Vector2.DistanceSquared(hud.LastHotbarMax, new Vector2(hotbar.Right, hotbar.Bottom)) < .01f
                && hud.LastLeftHanded == SettingsManager.LeftHandedLayout) return;
            FourSaintsHudLayout.Rect placement = FourSaintsHudLayout.Place(size.X, size.Y, look, hotbar, SettingsManager.LeftHandedLayout);
            for (int attempt = 0; attempt < 24 && OverlapsNative(controls, hud, placement); attempt++)
            {
                if (placement.Y >= 92f) placement.Y -= 14f;
                else placement.X = MathUtils.Clamp(placement.X - (SettingsManager.LeftHandedLayout ? -14f : 14f), 8f, size.X - placement.Width - 8f);
            }
            CanvasWidget.SetPosition(hud.Visual, new Vector2(placement.X, placement.Y));
            Log.Information(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[SCIEPort:FourSaintsHUD] viewport={0:0.#}x{1:0.#} look=({2:0.#},{3:0.#},{4:0.#},{5:0.#}) baihu=({6:0.#},{7:0.#},76,76) overlapLook={8} overlapHotbar={9}",
                size.X, size.Y, look.X, look.Y, look.Width, look.Height, placement.X, placement.Y, placement.Intersects(look), placement.Intersects(hotbar)));
            hud.LastSize = size; hud.LastLookMin = new Vector2(look.X, look.Y); hud.LastLookMax = new Vector2(look.Right, look.Bottom);
            hud.LastHotbarMin = new Vector2(hotbar.X, hotbar.Y); hud.LastHotbarMax = new Vector2(hotbar.Right, hotbar.Bottom); hud.LastLeftHanded = SettingsManager.LeftHandedLayout;
        }

        private static bool OverlapsNative(ContainerWidget controls, Hud hud, FourSaintsHudLayout.Rect rect)
        {
            foreach (Widget widget in controls.AllChildren)
            {
                if (widget == hud.Root || widget.IsChildWidgetOf(hud.Root) || !widget.IsVisibleGlobal || !widget.IsHitTestVisible) continue;
                if (!(widget is ButtonWidget) && !(widget is TouchInputWidget) && !(widget is MoveRoseWidget) && !(widget is InventorySlotWidget)) continue;
                if (rect.Intersects(Rect(controls, widget))) return true;
            }
            return false;
        }

        private static FourSaintsHudLayout.Rect Rect(ContainerWidget root, Widget widget)
        {
            if (widget == null || !widget.IsVisibleGlobal || widget.ActualSize.X < 1f) return default(FourSaintsHudLayout.Rect);
            Vector2 min = root.ScreenToWidget(widget.GlobalBounds.Min), max = root.ScreenToWidget(widget.GlobalBounds.Max);
            return new FourSaintsHudLayout.Rect { X = MathUtils.Min(min.X, max.X), Y = MathUtils.Min(min.Y, max.Y), Width = MathUtils.Abs(max.X - min.X), Height = MathUtils.Abs(max.Y - min.Y) };
        }

        internal static void Clear()
        {
            foreach (KeyValuePair<ComponentGui, Hud> pair in s_huds)
                if (pair.Key?.ControlsContainerWidget != null && pair.Value.Root.ParentWidget == pair.Key.ControlsContainerWidget) pair.Key.ControlsContainerWidget.Children.Remove(pair.Value.Root);
            s_huds.Clear();
        }
    }
}
