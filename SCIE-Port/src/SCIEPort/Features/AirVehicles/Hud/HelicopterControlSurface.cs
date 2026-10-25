using System;

namespace SCIEPort.Features.AirVehicles.Hud
{
    /// <summary>
    /// The resolved vertical flight command for the local helicopter pilot. This is the
    /// single value the on-screen buttons and the driver-view channel are arbitrated into,
    /// so the physics never reads raw touch state directly.
    /// </summary>
    public enum HelicopterVerticalIntent
    {
        /// <summary>Hold altitude: neutralize vertical velocity with rotor lift (hover assist).</summary>
        Hover,
        /// <summary>Sustained climb toward the ascent ceiling.</summary>
        Climb,
        /// <summary>Sustained dive toward the descent ceiling.</summary>
        Dive
    }

    /// <summary>An absolute button rectangle on the HUD surface in logical pixels.</summary>
    public struct HelicopterButtonRect
    {
        /// <summary>Left edge in surface pixels.</summary>
        public float X;
        /// <summary>Top edge in surface pixels.</summary>
        public float Y;
        /// <summary>Square edge length in surface pixels.</summary>
        public float Size;

        /// <summary>Initializes a square button rectangle at the given left/top and edge length.</summary>
        public HelicopterButtonRect(float x, float y, float size)
        {
            X = x;
            Y = y;
            Size = size;
        }

        /// <summary>Right edge.</summary>
        public float Right { get { return X + Size; } }

        /// <summary>Bottom edge.</summary>
        public float Bottom { get { return Y + Size; } }
    }

    /// <summary>The two vertical-control buttons resolved to concrete positions for one frame.</summary>
    public struct HelicopterButtonLayout
    {
        /// <summary>The climb (▲) button.</summary>
        public HelicopterButtonRect Ascent;
        /// <summary>The dive (▼) button.</summary>
        public HelicopterButtonRect Descent;
    }

    /// <summary>
    /// Pure, engine-independent logic backing the mobile helicopter flight HUD. It owns the
    /// intent arbitration (button precedence over the view channel, simultaneous-press
    /// cancel), the visibility predicate and the responsive button layout math. Everything
    /// here is deterministic and unit-testable; the widget layer only calls into these
    /// functions and never re-implements the decisions.
    /// </summary>
    public static class HelicopterControlSurface
    {
        /// <summary>Button square as a fraction of the surface's smaller dimension.</summary>
        public const float ButtonSizeFactor = 0.155f;
        /// <summary>Floor so buttons stay reachable on very small or high-density screens.</summary>
        public const float MinButtonSize = 96f;
        /// <summary>Ceiling so buttons never dominate large tablets.</summary>
        public const float MaxButtonSize = 190f;
        /// <summary>Vertical gap between the two stacked buttons, as a fraction of the surface height.</summary>
        public const float VerticalGapFactor = 0.03f;
        /// <summary>Distance from the right edge to the buttons, as a fraction of the surface width.</summary>
        public const float RightMarginFactor = 0.045f;
        /// <summary>Distance from the bottom edge (above the hotbar) to the lower button, as a fraction of the surface height.</summary>
        public const float BottomMarginFactor = 0.06f;
        /// <summary>Below this effective UI scale the layout stops shrinking so targets stay valid.</summary>
        public const float MinimumUiScale = 0.7f;

        /// <summary>
        /// Normalizes the caller-supplied UI scale so a mis-reported zero or negative value
        /// never collapses the layout. Values at or below <see cref="MinimumUiScale"/> clamp up.
        /// </summary>
        public static float ClampUiScale(float uiScale)
        {
            if (!(uiScale > 0f)) return 1f;
            return uiScale < MinimumUiScale ? MinimumUiScale : uiScale;
        }

        /// <summary>
        /// Computes the square button edge length from the surface dimensions and UI scale.
        /// The smaller screen axis drives the base size so portrait and landscape phones both
        /// get reachable targets; the result is clamped to the min/max band and scaled.
        /// </summary>
        public static float ButtonSize(float surfaceWidth, float surfaceHeight, float uiScale)
        {
            float safeScale = ClampUiScale(uiScale);
            float baseSize = Math.Min(Math.Max(surfaceWidth, 0f), Math.Max(surfaceHeight, 0f)) * ButtonSizeFactor;
            float clamped = baseSize < MinButtonSize ? MinButtonSize : (baseSize > MaxButtonSize ? MaxButtonSize : baseSize);
            return clamped * safeScale;
        }

        /// <summary>
        /// Places the climb button above the dive button in the lower-right corner, clear of
        /// the native left movement joystick and the bottom hotbar. The stack is anchored to
        /// the bottom-right so both thumbs keep their natural zones: movement stays left,
        /// vertical control stays right.
        /// </summary>
        public static HelicopterButtonLayout ComputeLayout(float surfaceWidth, float surfaceHeight, float uiScale)
        {
            float safeWidth = Math.Max(surfaceWidth, 0f);
            float safeHeight = Math.Max(surfaceHeight, 0f);
            float size = ButtonSize(safeWidth, safeHeight, uiScale);
            float gap = safeHeight * VerticalGapFactor;
            float rightMargin = safeWidth * RightMarginFactor;
            float bottomMargin = safeHeight * BottomMarginFactor;

            float x = safeWidth - rightMargin - size;
            if (x < 0f) x = 0f;

            float descentY = safeHeight - bottomMargin - size;
            if (descentY < 0f) descentY = 0f;
            float ascentY = descentY - gap - size;
            if (ascentY < 0f) ascentY = 0f;

            return new HelicopterButtonLayout
            {
                Ascent = new HelicopterButtonRect(x, ascentY, size),
                Descent = new HelicopterButtonRect(x, descentY, size)
            };
        }

        /// <summary>
        /// Shows the vertical HUD only for the local, authorized player seated in the driver
        /// position while the aircraft still exists. Passengers, remote replicas and dead or
        /// despawned vehicles never get the buttons, which keeps input authority singular.
        /// </summary>
        public static bool ShouldShowControls(bool isLocalMainPlayer, bool isDriverSeat, bool vehicleAlive)
        {
            return isLocalMainPlayer && isDriverSeat && vehicleAlive;
        }

        /// <summary>
        /// Arbitrates every vertical input source into one intent. The dedicated buttons take
        /// precedence over the ambient view-pitch channel; the jump/sneak keys map onto the
        /// same button lanes for keyboard play. Pressing climb and dive together cancels to a
        /// hover rather than fighting, and a released pair also settles to hover so the
        /// aircraft holds altitude smoothly.
        /// </summary>
        public static HelicopterVerticalIntent ResolveIntent(
            bool buttonClimb,
            bool buttonDive,
            bool viewClimb,
            bool viewDive,
            bool jumpClimb,
            bool sneakDive)
        {
            bool climb = buttonClimb || jumpClimb;
            bool dive = buttonDive || sneakDive;

            // A deliberate button/keyboard press fully overrides the view channel.
            if (climb && dive) return HelicopterVerticalIntent.Hover;
            if (climb) return HelicopterVerticalIntent.Climb;
            if (dive) return HelicopterVerticalIntent.Dive;

            // No explicit press: fall back to the driver's view elevation.
            bool viewUp = viewClimb && !viewDive;
            bool viewDown = viewDive && !viewClimb;
            if (viewUp) return HelicopterVerticalIntent.Climb;
            if (viewDown) return HelicopterVerticalIntent.Dive;

            return HelicopterVerticalIntent.Hover;
        }

        /// <summary>
        /// Maps a resolved intent onto the existing <see cref="HelicopterFlightRules.GetVerticalTarget"/> ascend/
        /// descend booleans so the flight physics is reused unchanged. Hover and climb both
        /// require a live pilot; without a pilot the caller still passes hasPilot=false and the
        /// rules produce the bounded auto-landing target.
        /// </summary>
        public static void ToVerticalFlags(HelicopterVerticalIntent intent, out bool ascend, out bool descend)
        {
            ascend = intent == HelicopterVerticalIntent.Climb;
            descend = intent == HelicopterVerticalIntent.Dive;
        }
    }
}
