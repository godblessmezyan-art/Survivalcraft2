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

            public SkillButton(Texture2D atlas, int icon, float size)
            {
                // Icon only. The skill name and the cyan outline box were removed on request:
                // on a phone they covered the world and the icons already say which skill it is.
                Size = new Vector2(size, size);
                Frame = new RectangleWidget
                {
                    Size = new Vector2(size),
                    Subtexture = new Subtexture(atlas, new Vector2(icon * 0.25f, 0f), new Vector2((icon + 1) * 0.25f, 1f)),
                    FillColor = Color.White,
                    OutlineColor = Color.Transparent,
                    OutlineThickness = 0f
                };
                CooldownLabel = new LabelWidget { Size = new Vector2(size), TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter, FontScale = size >= 90f ? 1.05f : 0.86f, DropShadow = true, Color = Color.White };
                Clickable = new ClickableWidget { HorizontalAlignment = WidgetAlignment.Stretch, VerticalAlignment = WidgetAlignment.Stretch, SoundName = "Audio/UI/ButtonClick" };
                Children.Add(Frame);
                Children.Add(CooldownLabel);
                Children.Add(Clickable);
            }

            public void SetState(float cooldown)
            {
                bool ready = cooldown <= 0.01f;
                IsEnabled = ready;
                Frame.FillColor = ready ? Color.White : new Color(112, 122, 142, 200);
                Frame.OutlineColor = Color.Transparent;
                Frame.OutlineThickness = 0f;
                CooldownLabel.Text = ready ? string.Empty : MathUtils.Ceiling(cooldown).ToString("0");
                ColorTransform = ready ? Color.White : new Color(170, 180, 198, 225);
            }
        }

        private sealed class HudState
        {
            public CanvasWidget Root;
            public SkillButton Judgment;
            public SkillButton Ultimate;
            public LabelWidget ThunderGodLabel;
            public RectangleWidget ThunderGodBar;
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
            float thunderGod = behavior.GetThunderGodRemaining(player);
            bool active = thunderGod > 0f;
            state.ThunderGodLabel.IsVisible = active;
            state.ThunderGodBar.IsVisible = active;
            if (active)
            {
                state.ThunderGodLabel.Text = "雷神降世 " + MathUtils.Ceiling(thunderGod).ToString("0") + "s";
                state.ThunderGodBar.Size = new Vector2(196f * MathUtils.Saturate(thunderGod / 12f), 4f);
            }
            if (state.Judgment.Clickable.IsClicked) behavior.RequestSkill(player, StormhammerSkill.ThunderousJudgment);
            if (state.Ultimate.Clickable.IsClicked) behavior.RequestSkill(player, StormhammerSkill.MyriadTribulation);
        }

        private static HudState Create(ComponentGui gui)
        {
            Texture2D atlas = ContentManager.Get<Texture2D>("Textures/StormhammerAwakenedSkills");
            HudState state = new HudState
            {
                Root = new CanvasWidget { Name = "SCIEPort.SkywrathV3Skills", Size = new Vector2(220f, 250f), HorizontalAlignment = WidgetAlignment.Far, VerticalAlignment = WidgetAlignment.Center, Margin = new Vector2(12f, 0f), IsVisible = false },
                Judgment = new SkillButton(atlas, 1, 78f),
                Ultimate = new SkillButton(atlas, 2, 96f)
            };
            state.ThunderGodLabel = new LabelWidget
            {
                Size = new Vector2(196f, 20f),
                Text = string.Empty,
                TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter,
                FontScale = 0.62f,
                DropShadow = true,
                Color = new Color(236, 248, 255),
                IsVisible = false
            };
            state.ThunderGodBar = new RectangleWidget
            {
                Size = new Vector2(196f, 4f),
                FillColor = new Color(120, 206, 255, 220),
                OutlineColor = Color.Transparent,
                OutlineThickness = 0f,
                IsVisible = false
            };
            state.Root.Children.Add(state.Judgment);
            state.Root.Children.Add(state.Ultimate);
            state.Root.Children.Add(state.ThunderGodLabel);
            state.Root.Children.Add(state.ThunderGodBar);
            CanvasWidget.SetPosition(state.Judgment, new Vector2(126f, 148f));
            CanvasWidget.SetPosition(state.Ultimate, new Vector2(64f, 24f));
            CanvasWidget.SetPosition(state.ThunderGodLabel, new Vector2(12f, 0f));
            CanvasWidget.SetPosition(state.ThunderGodBar, new Vector2(12f, 20f));
            gui.ControlsContainerWidget.Children.Add(state.Root);
            return state;
        }
    }
}
