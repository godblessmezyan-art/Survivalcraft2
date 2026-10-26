using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.Stormhammer
{
    /// <summary>Android-first three-button arc HUD, visible only for Skywrath V2.</summary>
    internal static class StormhammerSkillHud
    {
        private sealed class SkillButton : CanvasWidget
        {
            public readonly ClickableWidget Clickable;
            public readonly LabelWidget CooldownLabel;
            public readonly LabelWidget NameLabel;
            public readonly RectangleWidget ChargeBar;
            public readonly RectangleWidget Frame;
            public bool WasPressed;
            public double PressStart;

            public SkillButton(Texture2D atlas, int icon, string name, float size)
            {
                Size = new Vector2(size, size + 24f);
                RectangleWidget iconWidget = new RectangleWidget
                {
                    Size = new Vector2(size),
                    Subtexture = new Subtexture(atlas, new Vector2(icon * 0.25f, 0f), new Vector2((icon + 1) * 0.25f, 1f)),
                    FillColor = Color.White,
                    OutlineColor = new Color(93, 199, 255, 220),
                    OutlineThickness = 2f
                };
                Frame = iconWidget;
                CooldownLabel = new LabelWidget
                {
                    Size = new Vector2(size),
                    TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter,
                    FontScale = size >= 84f ? 0.92f : 0.78f,
                    DropShadow = true,
                    Color = Color.White
                };
                NameLabel = new LabelWidget
                {
                    Size = new Vector2(size, 22f),
                    Text = name,
                    TextAnchor = TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter,
                    FontScale = 0.52f,
                    DropShadow = true,
                    Color = new Color(188, 232, 255)
                };
                ChargeBar = new RectangleWidget
                {
                    Size = new Vector2(0f, 5f),
                    FillColor = new Color(67, 224, 255, 240),
                    OutlineColor = Color.Transparent,
                    OutlineThickness = 0f
                };
                Clickable = new ClickableWidget
                {
                    HorizontalAlignment = WidgetAlignment.Stretch,
                    VerticalAlignment = WidgetAlignment.Stretch,
                    SoundName = "Audio/UI/ButtonClick"
                };
                Children.Add(iconWidget);
                Children.Add(CooldownLabel);
                Children.Add(NameLabel);
                Children.Add(ChargeBar);
                Children.Add(Clickable);
                CanvasWidget.SetPosition(NameLabel, new Vector2(0f, size + 1f));
                CanvasWidget.SetPosition(ChargeBar, new Vector2(0f, size - 6f));
            }

            public void SetState(float cooldown, float fullCooldown, float charge)
            {
                bool ready = cooldown <= 0.01f;
                IsEnabled = ready;
                Frame.FillColor = ready ? Color.White : new Color(92, 104, 124, 210);
                Frame.OutlineColor = Clickable.IsPressed ? new Color(235, 248, 255) : ready ? new Color(93, 199, 255, 230) : new Color(65, 78, 98, 210);
                CooldownLabel.Text = ready ? string.Empty : MathUtils.Ceiling(cooldown).ToString("0");
                CooldownLabel.Color = ready ? Color.White : new Color(215, 227, 241);
                ChargeBar.Size = new Vector2(Size.X * MathUtils.Saturate(charge), 5f);
                ColorTransform = ready ? Color.White : new Color(165, 176, 192, 225);
            }
        }

        private sealed class HudState
        {
            public ComponentGui Gui;
            public CanvasWidget Root;
            public SkillButton Lance;
            public SkillButton Domain;
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
            bool equipped = player != null && player.IsAddedToProject && Terrain.ExtractContents(player.ComponentMiner.ActiveBlockValue) == StormhammerAwakenedBlock.Index;
            state.Root.IsVisible = equipped && gui.ControlsContainerWidget.IsVisible;
            if (!equipped)
            {
                ResetPress(state.Lance);
                return;
            }

            SubsystemAwakenedStormhammerBlockBehavior behavior = player.Project.FindSubsystem<SubsystemAwakenedStormhammerBlockBehavior>(false);
            if (behavior == null)
            {
                state.Root.IsVisible = false;
                return;
            }

            double now = Time.RealTime;
            float charge = state.Lance.Clickable.IsPressed ? MathUtils.Saturate((float)(now - state.Lance.PressStart) / 2.2f) : 0f;
            state.Lance.SetState(behavior.GetCooldownRemaining(player, StormhammerSkill.ThunderLance), 9f, charge);
            state.Domain.SetState(behavior.GetCooldownRemaining(player, StormhammerSkill.ThunderDomain), 18f, 0f);
            state.Ultimate.SetState(behavior.GetCooldownRemaining(player, StormhammerSkill.MyriadTribulation), 60f, 0f);

            if (state.Lance.Clickable.IsPressed && !state.Lance.WasPressed)
            {
                state.Lance.PressStart = now;
            }
            if (!state.Lance.Clickable.IsPressed && state.Lance.WasPressed)
            {
                float held = (float)(now - state.Lance.PressStart);
                if (held >= 0.35f)
                {
                    behavior.RequestSkill(player, StormhammerSkill.ThunderLance, held);
                }
                else
                {
                    gui.DisplaySmallMessage("雷狱天矛需长按蓄力。", Color.Cyan, false, false);
                }
            }
            state.Lance.WasPressed = state.Lance.Clickable.IsPressed;

            if (state.Domain.Clickable.IsClicked)
            {
                behavior.RequestSkill(player, StormhammerSkill.ThunderDomain, 0f);
            }
            if (state.Ultimate.Clickable.IsClicked)
            {
                behavior.RequestSkill(player, StormhammerSkill.MyriadTribulation, 0f);
            }
        }

        private static HudState Create(ComponentGui gui)
        {
            Texture2D atlas = ContentManager.Get<Texture2D>("Textures/StormhammerAwakenedSkills");
            HudState state = new HudState
            {
                Gui = gui,
                Root = new CanvasWidget
                {
                    Name = "SCIEPort.SkywrathV2Skills",
                    Size = new Vector2(230f, 326f),
                    HorizontalAlignment = WidgetAlignment.Far,
                    VerticalAlignment = WidgetAlignment.Center,
                    Margin = new Vector2(12f, 0f),
                    IsVisible = false
                },
                Lance = new SkillButton(atlas, 0, "雷狱天矛", 72f),
                Domain = new SkillButton(atlas, 1, "雷霆王域", 72f),
                Ultimate = new SkillButton(atlas, 2, "万象雷劫", 88f)
            };
            state.Root.Children.Add(state.Lance);
            state.Root.Children.Add(state.Domain);
            state.Root.Children.Add(state.Ultimate);
            CanvasWidget.SetPosition(state.Lance, new Vector2(146f, 220f));
            CanvasWidget.SetPosition(state.Domain, new Vector2(116f, 118f));
            CanvasWidget.SetPosition(state.Ultimate, new Vector2(58f, 8f));
            gui.ControlsContainerWidget.Children.Add(state.Root);
            return state;
        }

        private static void ResetPress(SkillButton button)
        {
            button.WasPressed = false;
            button.PressStart = 0.0;
        }
    }
}
