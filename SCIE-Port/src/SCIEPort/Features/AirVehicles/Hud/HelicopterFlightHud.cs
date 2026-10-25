using System;
using Engine;
using Engine.Graphics;
using Game;

namespace SCIEPort.Features.AirVehicles.Hud
{
    /// <summary>
    /// The on-device mobile flight HUD for the two-seat helicopter: two rounded,
    /// semi-transparent vertical-control buttons (climb ▲ above dive ▼) anchored to the
    /// lower-right of the driver's control overlay, leaving the native movement joystick
    /// and hotbar untouched. This is the widget counterpart to the pure
    /// <see cref="HelicopterControlSurface"/> math and is owned by the single local pilot's
    /// <see cref="ComponentHelicopterDrive"/>.
    ///
    /// Every widget is built exactly once and only toggled/positioned afterward, so the
    /// steady frame cost is reading two boolean press flags. Nothing in this type performs
    /// any physics; the drive polls <see cref="IsClimbPressed"/> / <see cref="IsDivePressed"/>
    /// and merges them with the view channel through <see
    /// cref="HelicopterControlSurface.ResolveIntent"/>.
    ///
    /// 7723 caveat: the native widget/touch tree compiles and is public against
    /// x25.01.17, but its live behaviour on the Android client is not yet observed. Every
    /// engine surface touched here is therefore wrapped so that any failure simply leaves the
    /// HUD hidden and the buttons unread (fail-safe): the helicopter remains fully flyable via
    /// the proven view-elevation channel, so a HUD that cannot build never strands the pilot.
    /// </summary>
    public sealed class HelicopterFlightHud
    {
        private const string TextureKey = "Textures/HeliArrow";
        private const float ArrowMinSize = 34f;
        private const float ArrowFraction = 0.5f;
        private const float ChevronUpRotation = 0f;
        private const float ChevronDownRotation = 3.14159265f;

        private static Texture2D s_arrowTexture;

        private readonly FlightButton m_ascent = new FlightButton(ChevronUpRotation);
        private readonly FlightButton m_descent = new FlightButton(ChevronDownRotation);

        private ContainerWidget m_container;
        private bool m_attached;

        /// <summary>True while the climb button reports an active touch (continuous press).</summary>
        public bool IsClimbPressed
        {
            get { return m_attached && m_ascent.IsPressed; }
        }

        /// <summary>True while the dive button reports an active touch (continuous press).</summary>
        public bool IsDivePressed
        {
            get { return m_attached && m_descent.IsPressed; }
        }

        /// <summary>True once the overlay has been built and added to the driver's GUI.</summary>
        public bool IsAttached
        {
            get { return m_attached; }
        }

        /// <summary>
        /// Loads (once) and returns the shared chevron texture, or null if the content is not
        /// available. The same texture is reused for both buttons, flipped via
        /// <see cref="ImageWidget.RotateAngle"/>.
        /// </summary>
        public static Texture2D TryGetArrowTexture()
        {
            if (s_arrowTexture != null)
            {
                return s_arrowTexture;
            }

            try
            {
                s_arrowTexture = ContentManager.Get<Texture2D>(TextureKey);
            }
            catch (Exception exception)
            {
                Log.Error("[SCIEPort] Helicopter HUD arrow texture unavailable: " + exception);
                s_arrowTexture = null;
            }

            return s_arrowTexture;
        }

        /// <summary>
        /// Attaches the HUD to <paramref name="container"/> (the local driver's
        /// <see cref="ComponentGui.ControlsContainerWidget"/>) if not already attached to that
        /// same container. Building into a different container first triggers a clean detach so
        /// a driver change never leaves orphaned widgets on the previous player's GUI. Returns
        /// false (and leaves everything detached) when the widget surfaces are unavailable.
        /// </summary>
        public bool Attach(ContainerWidget container)
        {
            if (container == null)
            {
                Detach();
                return false;
            }

            if (m_attached)
            {
                if (ReferenceEquals(m_container, container))
                {
                    return true;
                }

                Detach();
            }

            try
            {
                Texture2D arrow = TryGetArrowTexture();
                m_ascent.Build(arrow);
                m_descent.Build(arrow);
                container.AddChildren(m_ascent.Root);
                container.AddChildren(m_descent.Root);
                container.AddChildren(m_ascent.Hit);
                container.AddChildren(m_descent.Hit);
                m_container = container;
                m_attached = true;
                SetVisible(false);
                return true;
            }
            catch (Exception exception)
            {
                Log.Error("[SCIEPort] Helicopter HUD attach failed; falling back to view-only control: " + exception);
                Detach();
                return false;
            }
        }

        /// <summary>
        /// Refreshes visibility and absolute layout every frame from the caller-supplied
        /// predicate and the live container size. Layout only mutates widget state when the
        /// computed geometry actually changes, so a held button is never re-measured per frame
        /// (which would break the engine's active-touch tracking).
        /// </summary>
        public void Refresh(bool show, float uiScale)
        {
            if (!m_attached)
            {
                return;
            }

            try
            {
                SetVisible(show);
                if (!show)
                {
                    return;
                }

                Vector2 surface = m_container.ActualSize;
                HelicopterButtonLayout layout = HelicopterControlSurface.ComputeLayout(surface.X, surface.Y, uiScale);
                m_ascent.Apply(m_container, layout.Ascent);
                m_descent.Apply(m_container, layout.Descent);
            }
            catch (Exception exception)
            {
                Log.Error("[SCIEPort] Helicopter HUD refresh failed: " + exception);
                Detach();
            }
        }

        /// <summary>Hides the buttons without tearing down the built widgets (cheap to reshow).</summary>
        public void Hide()
        {
            SetVisible(false);
        }

        /// <summary>
        /// Removes and disposes every widget and clears press state. Safe to call when detached.
        /// </summary>
        public void Detach()
        {
            m_ascent.Dispose(m_container);
            m_descent.Dispose(m_container);
            m_container = null;
            m_attached = false;
        }

        private void SetVisible(bool visible)
        {
            m_ascent.Visible = visible;
            m_descent.Visible = visible;
        }

        /// <summary>
        /// One composed HUD button: a rounded translucent body, a centered chevron and an
        /// invisible full-size clickable hit surface. The root is a CanvasWidget so the engine
        /// measures each child from its own DesiredSize while we place the whole button through
        /// the parent <see cref="CanvasWidget.SetPosition"/>.
        /// </summary>
        private sealed class FlightButton
        {
            private readonly float m_rotation;
            private readonly CanvasWidget m_root = new CanvasWidget();
            private readonly BevelledRectangleWidget m_body = new BevelledRectangleWidget();
            private readonly ImageWidget m_image = new ImageWidget();
            private readonly ClickableWidget m_hit = new ClickableWidget();

            private float m_lastSize = -1f;
            private float m_lastX = float.NaN;
            private float m_lastY = float.NaN;
            private bool m_lastPressed;

            public FlightButton(float rotation)
            {
                m_rotation = rotation;
                Name = "Heli";
            }

            public string Name { get; set; }

            public Widget Root
            {
                get { return m_root; }
            }

            public Widget Hit
            {
                get { return m_hit; }
            }

            public bool IsPressed
            {
                get { return m_hit.IsPressed; }
            }

            public bool Visible
            {
                get { return m_root.IsVisible; }
                set
                {
                    m_root.IsVisible = value;
                    m_hit.IsVisible = value;
                    m_hit.IsHitTestVisible = value;
                    if (!value)
                    {
                        // Drop any stale held state when the button disappears (seat change,
                        // death, disconnect) so the next driver never inherits a phantom press.
                        m_hit.IsPressed = false;
                        m_lastPressed = false;
                    }
                }
            }

            public void Build(Texture2D arrow)
            {
                m_body.RoundingRadius = 16f;
                m_body.RoundingCount = 6;
                m_body.BevelSize = 1.6f;
                m_body.AmbientLight = 0.5f;
                m_body.DirectionalLight = 0.55f;
                m_body.ShadowSize = 0f;
                m_body.CenterColor = new Color(0.1f, 0.13f, 0.18f, 0.42f);
                m_body.BevelColor = new Color(0.55f, 0.62f, 0.72f, 0.5f);
                m_body.IsHitTestVisible = false;
                m_root.AddChildren(m_body);

                if (arrow != null)
                {
                    m_image.Texture = arrow;
                    m_image.RotateAngle = m_rotation;
                    m_image.ColorTransForm = new Color(0.96f, 0.96f, 0.98f, 0.95f);
                    m_image.IsHitTestVisible = false;
                    m_root.AddChildren(m_image);
                }

                m_hit.SoundName = null;
                m_hit.IsAutoCheckingEnabled = false;
                m_hit.HorizontalAlignment = WidgetAlignment.Near;
                m_hit.VerticalAlignment = WidgetAlignment.Near;
                m_hit.IsVisible = true;
            }

            public void Apply(ContainerWidget parent, HelicopterButtonRect rect)
            {
                float size = rect.Size;
                if (size != m_lastSize)
                {
                    m_root.DesiredSize = new Vector2(size, size);
                    m_body.DesiredSize = new Vector2(size, size);
                    m_body.Size = new Vector2(size, size);
                    m_hit.DesiredSize = new Vector2(size, size);

                    float arrowSize = Math.Max(ArrowMinSize, size * ArrowFraction);
                    m_image.DesiredSize = new Vector2(arrowSize, arrowSize);
                    m_image.Size = new Vector2(arrowSize, arrowSize);
                    float arrowOffset = (size - arrowSize) * 0.5f;
                    m_image.Margin = new Vector2(arrowOffset, arrowOffset);
                    m_lastSize = size;
                }

                if (rect.X != m_lastX || rect.Y != m_lastY)
                {
                    CanvasWidget.SetPosition(m_root, new Vector2(rect.X, rect.Y));
                    CanvasWidget.SetPosition(m_hit, new Vector2(rect.X, rect.Y));
                    m_lastX = rect.X;
                    m_lastY = rect.Y;
                }

                bool pressed = m_hit.IsPressed;
                if (pressed != m_lastPressed)
                {
                    m_lastPressed = pressed;
                    if (pressed)
                    {
                        m_body.CenterColor = new Color(0.18f, 0.34f, 0.5f, 0.68f);
                    }
                    else
                    {
                        m_body.CenterColor = new Color(0.1f, 0.13f, 0.18f, 0.42f);
                    }
                }
            }

            public void Dispose(ContainerWidget parent)
            {
                m_hit.IsPressed = false;
                m_lastPressed = false;
                if (parent != null)
                {
                    parent.RemoveChildren(m_root);
                    parent.RemoveChildren(m_hit);
                }

                m_root.ClearChildren();
                m_root.Dispose();
                m_hit.Dispose();
                m_lastSize = -1f;
                m_lastX = float.NaN;
                m_lastY = float.NaN;
            }
        }
    }
}
