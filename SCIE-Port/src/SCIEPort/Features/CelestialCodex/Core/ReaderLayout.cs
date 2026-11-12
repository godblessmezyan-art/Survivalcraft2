using System;

namespace SCIEPort.Features.CelestialCodex.Core
{
    internal sealed class ReaderLayout
    {
        internal readonly float Width, Height, ContentHeight, ContentWidth, ContentX, ContentY, FooterY, ButtonWidth;
        internal readonly bool Compact;
        internal ReaderLayout(float viewportWidth, float viewportHeight)
        {
            Width = Math.Max(1, Math.Min(900, viewportWidth - 2 * Math.Max(16, Math.Min(32, viewportWidth * .04f))));
            Height = Math.Max(1, Math.Min(900, viewportHeight - 32));
            Compact = Height < 360;
            ContentWidth = Math.Max(1, Math.Min(720, Width - (Width < 420 ? 32 : 48)));
            ContentX = (Width - ContentWidth) / 2;
            ContentY = Compact ? 80 : 104;
            FooterY = Height - 64;
            ContentHeight = Math.Max(1, FooterY - 16 - ContentY);
            ButtonWidth = Math.Max(1, (ContentWidth - 24) / 4);
        }
    }

    internal sealed class ReaderTheme
    {
        internal readonly int Background, Surface, Ink, Muted, Border, Accent, OnAccent, Pressed;
        private ReaderTheme(int background, int surface, int ink, int muted, int border, int accent, int onAccent, int pressed)
        { Background = background; Surface = surface; Ink = ink; Muted = muted; Border = border; Accent = accent; OnAccent = onAccent; Pressed = pressed; }
        internal static readonly ReaderTheme Paper = new ReaderTheme(0xF5ECDC, 0xFFF8EA, 0x302A24, 0x655647, 0x9B8060, 0x684A2E, 0xFFF8EA, 0xE3CFAB);
        internal static readonly ReaderTheme Night = new ReaderTheme(0x191D24, 0x252B34, 0xEEE9DF, 0xB8B1A6, 0x747E8F, 0xDAC195, 0x191D24, 0x394454);
    }
}
