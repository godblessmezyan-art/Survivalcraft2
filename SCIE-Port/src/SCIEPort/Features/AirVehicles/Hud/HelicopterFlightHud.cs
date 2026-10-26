using System;
using System.Globalization;
using Engine;
using Game;

namespace SCIEPort.Features.AirVehicles.Hud
{
    /// <summary>Driver cockpit overlay. Input and telemetry only, with no entity or network writes.</summary>
    internal sealed class HelicopterFlightHud
    {
        private static readonly Color Cyan = new Color(77, 218, 226, 255);
        private static readonly Color Muted = new Color(139, 163, 177, 255);
        private readonly CanvasWidget m_layer = new CanvasWidget();
        private readonly CanvasWidget m_panel = new CanvasWidget();
        private readonly CanvasWidget m_instruments = new CanvasWidget();
        private readonly CanvasWidget m_grip = new CanvasWidget();
        private readonly CollectiveLeverControl m_lever = new CollectiveLeverControl();
        private readonly CollectiveLeverControl m_hover = new CollectiveLeverControl { ToggleMode = true };
        private LabelWidget m_alt, m_vs, m_spd, m_state, m_command, m_hoverLabel;
        private BevelledRectangleWidget m_gripFace, m_hoverFace;
        private ContainerWidget m_container;
        private bool m_built, m_failed, m_telemetryReady;
        private float m_textTimer, m_altitude, m_vertical, m_speed;
        private Vector2 m_lastSurface;
        private float m_lastScale, m_lastX, m_lastY;
        public float HudScale { get; set; } = 1f;
        public float HudX { get; set; } = 0.97f;
        public float HudY { get; set; } = 0.48f;
        public float RawValue => m_container != null ? m_lever.RawValue : 0f;
        public bool IsDragging => m_container != null && m_lever.IsDragging;
        public bool ConsumeHoverToggle() => m_container != null && m_hover.ConsumeTap();

        public bool Attach(ContainerWidget container)
        {
            if (m_failed || container == null) return false;
            if (ReferenceEquals(container, m_container)) return true;
            Detach();
            try
            {
                if (!m_built) Build();
                container.AddChildren(m_layer);
                m_container = container;
                m_layer.IsVisible = true;
                return true;
            }
            catch (Exception exception)
            {
                m_failed = true;
                Log.Error("[SCIEPort] Collective HUD unavailable: " + exception);
                Detach();
                return false;
            }
        }

        public void Detach()
        {
            m_lever.UpdateCeases(); m_hover.UpdateCeases();
            m_layer.IsVisible = false;
            m_container?.RemoveChildren(m_layer);
            m_container = null;
            m_lastSurface = Vector2.Zero;
            m_telemetryReady = false;
        }
        public void Dispose() { Detach(); m_layer.Dispose(); }

        private void Build()
        {
            m_layer.IsHitTestVisible = false;
            m_layer.HorizontalAlignment = WidgetAlignment.Stretch;
            m_layer.VerticalAlignment = WidgetAlignment.Stretch;
            m_panel.IsHitTestVisible = m_instruments.IsHitTestVisible = m_grip.IsHitTestVisible = false;
            m_panel.Size = new Vector2(138f, 380f);
            m_instruments.Size = new Vector2(504f, 76f);
            m_layer.AddChildren(m_panel); m_layer.AddChildren(m_instruments);
            Plate(m_panel, 0, 0, 138, 380, new Color(12, 20, 27, 222), 10);
            Plate(m_panel, 10, 12, 3, 22, Cyan, 0);
            Text(m_panel, "COLLECTIVE", 21, 12, 108, 22, 0.55f, Cyan);
            Text(m_panel, "VERTICAL CONTROL", 14, 35, 116, 16, 0.35f, Muted);
            Plate(m_panel, 61, 66, 16, 238, new Color(3, 8, 13, 240), 6);
            Plate(m_panel, 67, 72, 3, 226, new Color(48, 70, 82, 255), 0);
            for (int i = -4; i <= 4; i++)
            {
                float y = 186f + i * 23f;
                Color color = i == 0 ? Cyan : new Color(78, 105, 119, 230);
                Plate(m_panel, i == 0 ? 20 : 31, y, i == 0 ? 30 : 19, 2, color, 0);
                Plate(m_panel, 87, y, i == 0 ? 30 : 19, 2, color, 0);
            }
            Text(m_panel, "+", 17, 62, 28, 25, 0.65f, Muted);
            Text(m_panel, "-", 17, 278, 28, 25, 0.65f, Muted);
            m_grip.Size = new Vector2(92, 48);
            m_gripFace = Plate(m_grip, 0, 0, 92, 48, new Color(47, 65, 76, 255), 7);
            Plate(m_grip, 8, 9, 76, 3, new Color(110, 139, 151, 255), 0);
            Plate(m_grip, 8, 23, 76, 3, Cyan, 0);
            Plate(m_grip, 8, 36, 76, 2, new Color(22, 35, 44, 255), 0);
            Place(m_panel, m_grip, 23, 162);
            m_command = Text(m_panel, "0%", 42, 307, 64, 20, 0.5f, Cyan);
            m_hoverFace = Plate(m_panel, 10, 331, 118, 39, new Color(23, 63, 71, 255), 5);
            m_hoverLabel = Text(m_panel, "HOVER ON", 18, 339, 106, 23, 0.47f, Cyan);
            m_lever.DesiredSize = new Vector2(118, 252);
            Place(m_panel, m_lever, 10, 60);
            m_hover.DesiredSize = new Vector2(118, 44);
            Place(m_panel, m_hover, 10, 328);
            Plate(m_instruments, 0, 0, 504, 76, new Color(12, 20, 27, 216), 8);
            Plate(m_instruments, 12, 9, 480, 2, new Color(45, 106, 120, 255), 0);
            string[] titles = { "ALT / M", "VS / M/S", "SPD / M/S", "FLIGHT" };
            for (int i = 0; i < titles.Length; i++)
            {
                Text(m_instruments, titles[i], 18 + i * 122, 17, 113, 18, 0.4f, Muted);
                if (i > 0) Plate(m_instruments, 6 + i * 122, 22, 1, 39, new Color(47, 65, 76, 255), 0);
            }
            m_alt = Text(m_instruments, "0.0", 18, 36, 113, 31, 0.8f, Color.White);
            m_vs = Text(m_instruments, "0.0", 140, 36, 113, 31, 0.8f, Cyan);
            m_spd = Text(m_instruments, "0.0", 262, 36, 113, 31, 0.8f, Color.White);
            m_state = Text(m_instruments, "GROUND", 384, 41, 112, 24, 0.48f, Cyan);
            m_built = true;
        }

        public void Refresh(float dt, float inputValue, float command, float altitude,
            Vector3 velocity, bool hover, HelicopterControlMode mode, float rotorSpeed)
        {
            if (m_container == null) return;
            Vector2 surface = m_container.ActualSize;
            if (surface.X <= 0f || surface.Y <= 0f) return;
            // GUI coordinates already include UIScale. Scale only for available space and user preference.
            float scale = Math.Min(HelicopterFlightInput.Clamp(HudScale, 0.7f, 1.4f),
                Math.Min(surface.Y * 0.69f / 380f, surface.X * 0.7f / 504f));
            if (surface != m_lastSurface || scale != m_lastScale || HudX != m_lastX || HudY != m_lastY)
            {
                m_lever.UpdateCeases(); m_hover.UpdateCeases();
                m_panel.LayoutTransform = Matrix.CreateScale(scale, scale, 1f);
                m_instruments.LayoutTransform = Matrix.CreateScale(scale, scale, 1f);
                float x = HelicopterFlightInput.Clamp((surface.X - 80f) * HudX - 138f * scale, surface.X * 0.5f, surface.X - 80f - 138f * scale);
                float y = HelicopterFlightInput.Clamp(surface.Y * HudY - 190f * scale, 90f * scale + 12f, surface.Y * 0.82f - 380f * scale);
                CanvasWidget.SetPosition(m_panel, new Vector2(x, y));
                CanvasWidget.SetPosition(m_instruments, new Vector2((surface.X - 504f * scale) * 0.5f, 12f));
                m_lastSurface = surface; m_lastScale = scale; m_lastX = HudX; m_lastY = HudY;
            }
            m_lever.DisplayValue = inputValue;
            CanvasWidget.SetPosition(m_grip, new Vector2(23f, 162f - inputValue * m_lever.Travel));
            m_gripFace.CenterColor = IsDragging ? new Color(55, 109, 123, 255) : new Color(47, 65, 76, 255);
            float blend = m_telemetryReady ? 1f - (float)Math.Exp(-6f * dt) : 1f;
            m_altitude += (altitude - m_altitude) * blend;
            m_vertical += (velocity.Y - m_vertical) * blend;
            float speed = (float)Math.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z);
            m_speed += (speed - m_speed) * blend;
            m_telemetryReady = true;
            m_textTimer -= dt;
            if (m_textTimer > 0f) return;
            m_textTimer = 0.1f;
            m_alt.Text = m_altitude.ToString("0.0", CultureInfo.InvariantCulture);
            m_vs.Text = m_vertical.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
            m_spd.Text = m_speed.ToString("0.0", CultureInfo.InvariantCulture);
            m_command.Text = (command * 100f).ToString("+0;-0;0", CultureInfo.InvariantCulture) + "%";
            m_hoverLabel.Text = hover ? "HOVER ON" : "HOVER OFF";
            m_hoverFace.CenterColor = hover ? new Color(23, 63, 71, 255) : new Color(33, 42, 50, 255);
            m_state.Text = rotorSpeed < 0.99f ? "SPOOL UP" :
                mode == HelicopterControlMode.Grounded ? "GROUND" :
                mode == HelicopterControlMode.Manual ? "MANUAL" :
                mode == HelicopterControlMode.Recovering ? "CAPTURE" :
                mode == HelicopterControlMode.AltitudeHold ? "ALT HOLD" : "ASSIST";
        }

        private static void Place(CanvasWidget parent, Widget child, float x, float y)
        {
            parent.AddChildren(child);
            CanvasWidget.SetPosition(child, new Vector2(x, y));
        }
        private static BevelledRectangleWidget Plate(CanvasWidget parent, float x, float y,
            float width, float height, Color color, float radius)
        {
            var plate = new BevelledRectangleWidget
            {
                Size = new Vector2(width, height), DesiredSize = new Vector2(width, height),
                CenterColor = color, BevelColor = new Color(83, 108, 121, (int)color.A),
                BevelSize = radius > 0 ? 1f : 0f, RoundingRadius = radius, RoundingCount = 4,
                AmbientLight = 0.8f, DirectionalLight = 0.2f, ShadowSize = 0f, IsHitTestVisible = false
            };
            Place(parent, plate, x, y); return plate;
        }
        private static LabelWidget Text(CanvasWidget parent, string text, float x, float y,
            float width, float height, float scale, Color color)
        {
            var label = new LabelWidget
            {
                Text = text, Size = new Vector2(width, height), FontScale = scale,
                Color = color, IsHitTestVisible = false, DropShadow = false
            };
            Place(parent, label, x, y); return label;
        }
    }
}
