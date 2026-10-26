using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>Android-first two-button Skywrath V3.2 HUD.</summary>
    internal static class StormhammerSkillHud
    {
        private sealed class SkillButton : CanvasWidget
        {
            public readonly ClickableWidget Clickable;
            public readonly LabelWidget CooldownLabel;
            public readonly RectangleWidget Frame;

            public SkillButton(Texture2D atlas, int icon, string name, float size)
            {
                Size = new Vector2(size, size + 24f);
                Frame = new RectangleWidget
                {
                    Size = new Vector2(size),
                    Subtexture = new Subtexture(atlas, new Vector2(icon * 0.25f, 0f), new Vector2((icon + 1) * 0.25f, 1f)),
                    FillColor = Color.White,
                    OutlineColor = new Color(101, 218, 255, 235),
                    OutlineThickness = size >= 90f ? 3f : 2f
                };
                CooldownLabel = new LabelWidget { Size = new Vector2(size), TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter, FontScale = size >= 90f ? 1.05f : 0.86f, DropShadow = true, Color = Color.White };
                LabelWidget label = new LabelWidget { Size = new Vector2(size, 22f), Text = name, TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter, FontScale = 0.54f, DropShadow = true, Color = new Color(194, 236, 255) };
                Clickable = new ClickableWidget { HorizontalAlignment = WidgetAlignment.Stretch, VerticalAlignment = WidgetAlignment.Stretch, SoundName = "Audio/UI/ButtonClick" };
                Children.Add(Frame);
                Children.Add(CooldownLabel);
                Children.Add(label);
                Children.Add(Clickable);
                CanvasWidget.SetPosition(label, new Vector2(0f, size + 1f));
            }

            public void SetState(float cooldown)
            {
                bool ready = cooldown <= 0.01f;
                IsEnabled = ready;
                Frame.FillColor = ready ? Color.White : new Color(80, 91, 112, 210);
                Frame.OutlineColor = Clickable.IsPressed ? new Color(244, 251, 255) : ready ? new Color(83, 220, 255, 245) : new Color(60, 72, 93, 210);
                CooldownLabel.Text = ready ? string.Empty : MathUtils.Ceiling(cooldown).ToString("0");
                ColorTransform = ready ? Color.White : new Color(158, 169, 188, 225);
            }
        }

        private sealed class HudState
        {
            public CanvasWidget Root;
            public SkillButton Judgment;
            public SkillButton Ultimate;
        }

        private static readonly Dictionary<ComponentGui, HudState> s_states = new Dictionary<ComponentGui, HudState>();

        public static void Update(ComponentGui gui)
        {
            HudState state;
            if (!s_states.TryGetValue(gui, out state))
            {
                state = Create(gui);
                s_states.Add(gui, state);
            }
            ComponentPlayer player = gui.m_componentPlayer;
            bool equipped = SubsystemAwakenedStormhammerBlockBehavior.IsEquipped(player);
            state.Root.IsVisible = equipped && gui.ControlsContainerWidget.IsVisible;
            if (!equipped) return;
            SubsystemAwakenedStormhammerBlockBehavior behavior = player.Project.FindSubsystem<SubsystemAwakenedStormhammerBlockBehavior>(false);
            if (behavior == null) { state.Root.IsVisible = false; return; }
            state.Judgment.SetState(behavior.GetCooldownRemaining(player, StormhammerSkill.ThunderousJudgment));
            state.Ultimate.SetState(behavior.GetCooldownRemaining(player, StormhammerSkill.MyriadTribulation));
            if (state.Judgment.Clickable.IsClicked) behavior.RequestSkill(player, StormhammerSkill.ThunderousJudgment);
            if (state.Ultimate.Clickable.IsClicked) behavior.RequestSkill(player, StormhammerSkill.MyriadTribulation);
        }

        private static HudState Create(ComponentGui gui)
        {
            Texture2D atlas = ContentManager.Get<Texture2D>("Textures/StormhammerAwakenedSkills");
            HudState state = new HudState
            {
                Root = new CanvasWidget { Name = "SCIEPort.SkywrathV3Skills", Size = new Vector2(220f, 250f), HorizontalAlignment = WidgetAlignment.Far, VerticalAlignment = WidgetAlignment.Center, Margin = new Vector2(12f, 0f), IsVisible = false },
                Judgment = new SkillButton(atlas, 1, "雷霆万钧", 78f),
                Ultimate = new SkillButton(atlas, 2, "万象雷劫", 96f)
            };
            state.Root.Children.Add(state.Judgment);
            state.Root.Children.Add(state.Ultimate);
            CanvasWidget.SetPosition(state.Judgment, new Vector2(126f, 148f));
            CanvasWidget.SetPosition(state.Ultimate, new Vector2(64f, 24f));
            gui.ControlsContainerWidget.Children.Add(state.Root);
            return state;
        }
    }
}
