using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Gungnir
{
    internal static class GungnirHud
    {
        private sealed class Hud
        {
            internal CanvasWidget Root;
            internal readonly ClickableWidget[] Buttons = new ClickableWidget[2];
            internal readonly LabelWidget[] Labels = new LabelWidget[2];
        }
        private static readonly Dictionary<ComponentGui, Hud> s_huds = new Dictionary<ComponentGui, Hud>();
        internal static void Clear()
        {
            foreach (var entry in s_huds) entry.Key.ControlsContainerWidget.Children.Remove(entry.Value.Root);
            s_huds.Clear();
        }
        internal static void Update(ComponentGui gui)
        {
            var p = gui.m_componentPlayer;
            Hud h;
            if (!s_huds.TryGetValue(gui, out h))
            {
                if (!SubsystemGungnir.Equipped(p)) return;
                h = Create(gui); s_huds.Add(gui, h);
            }
            h.Root.IsVisible = SubsystemGungnir.Equipped(p) && gui.ControlsContainerWidget.IsVisible;
            if (!h.Root.IsVisible) return;
            var runtime = p.Project.FindSubsystem<SubsystemGungnir>(false);
            if (runtime == null) { h.Root.IsVisible = false; return; }
            var s = runtime.Get(p);
            for (int i = 0; i < 2; i++)
            {
                float cooldown = (float)((i == 0 ? s.BuffReady : s.JudgmentReady) - runtime.Now);
                float remaining = (float)(s.BuffEnd - runtime.Now);
                h.Labels[i].Text = i == 0 && remaining > 0 ? "神佑 " + MathUtils.Ceiling(remaining) + "s" : cooldown > 0 ? MathUtils.Ceiling(cooldown) + "s" : "就绪";
                h.Buttons[i].IsEnabled = cooldown <= 0;
                if (h.Buttons[i].IsClicked) runtime.Request(p, (byte)(i + 1), p.GameWidget.ActiveCamera.ViewDirection);
            }
        }
        private static Hud Create(ComponentGui gui)
        {
            var h = new Hud { Root = new CanvasWidget { Name = "SCIEPort.Gungnir.TwoSkills", Size = new Vector2(202, 116), HorizontalAlignment = WidgetAlignment.Center, VerticalAlignment = WidgetAlignment.Near, Margin = new Vector2(0, 56) } };
            var texture = ContentManager.Get<Texture2D>("Textures/GungnirSkills");
            for (int i = 0; i < 2; i++)
            {
                float size = i == 0 ? 72 : 88;
                var button = new CanvasWidget { Size = new Vector2(size, size + 22) };
                button.Children.Add(new RectangleWidget { Size = new Vector2(size), Subtexture = new Subtexture(texture, new Vector2(i * 0.5f, 0), new Vector2((i + 1) * 0.5f, 1)), FillColor = Color.White, OutlineColor = new Color(188, 150, 70), OutlineThickness = 2 });
                h.Labels[i] = new LabelWidget { Size = new Vector2(size), TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter, FontScale = 0.65f, DropShadow = true, Color = Color.White };
                button.Children.Add(h.Labels[i]);
                var name = new LabelWidget { Text = i == 0 ? "卢恩神佑" : "永恒裁决", Size = new Vector2(size, 22), FontScale = 0.6f, Color = new Color(240, 215, 162), TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter };
                button.Children.Add(name); CanvasWidget.SetPosition(name, new Vector2(0, size));
                h.Buttons[i] = new ClickableWidget { HorizontalAlignment = WidgetAlignment.Stretch, VerticalAlignment = WidgetAlignment.Stretch, SoundName = "Audio/UI/ButtonClick" };
                button.Children.Add(h.Buttons[i]); h.Root.Children.Add(button);
                CanvasWidget.SetPosition(button, new Vector2(i * 104, i == 0 ? 8 : 0));
            }
            gui.ControlsContainerWidget.Children.Add(h.Root); return h;
        }
    }
}

