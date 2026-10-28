using Engine;
using Engine.Input;
using Game;

namespace SCIEPort.Features.AirVehicles.Hud
{
    /// <summary>Captures one finger through the player's filtered touch stream, independently of the joystick.</summary>
    internal sealed class CollectiveLeverControl : Widget
    {
        private int? m_touchId;
        private float m_grabOffset;
        private bool m_tapped;
        public bool ToggleMode { get; set; }
        public bool IsDragging { get; private set; }
        public float RawValue { get; private set; }
        public float DisplayValue { get; set; }
        public float Travel => 92f;
        public float Center => ActualSize.Y * 0.5f;

        public bool ConsumeTap() { bool value = m_tapped; m_tapped = false; return value; }

        public override void UpdateCeases()
        {
            m_touchId = null;
            IsDragging = false;
            RawValue = 0f;
            m_tapped = false;
            base.UpdateCeases();
        }

        public override void Update()
        {
            if (!IsVisibleGlobal || !IsEnabledGlobal) { UpdateCeases(); return; }
            bool found = false;
            foreach (TouchLocation touch in Input.TouchLocations)
            {
                Vector2 point = ScreenToWidget(touch.Position);
                if (!m_touchId.HasValue && touch.State == TouchLocationState.Pressed &&
                    HitTestGlobal(touch.Position) == this &&
                    (ToggleMode || System.Math.Abs(point.Y - (Center - DisplayValue * Travel)) <= 36f))
                {
                    m_touchId = touch.Id;
                    m_grabOffset = point.Y - (Center - DisplayValue * Travel);
                }
                if (m_touchId != touch.Id) continue;
                found = true;
                bool outside = point.X < -48f || point.X > ActualSize.X + 48f ||
                    point.Y < -48f || point.Y > ActualSize.Y + 48f;
                if (touch.State == TouchLocationState.Released || outside)
                {
                    if (ToggleMode && !outside && touch.State == TouchLocationState.Released &&
                        HitTestGlobal(touch.Position) == this) m_tapped = true;
                    m_touchId = null;
                    IsDragging = false;
                    RawValue = 0f;
                }
                else
                {
                    IsDragging = true;
                    RawValue = HelicopterFlightInput.Clamp((Center - point.Y + m_grabOffset) / Travel, -1f, 1f);
                }
            }
            if (!found) { m_touchId = null; IsDragging = false; RawValue = 0f; }
        }
    }
}
