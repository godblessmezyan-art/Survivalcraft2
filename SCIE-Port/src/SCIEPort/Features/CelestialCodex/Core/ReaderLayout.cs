using System;

namespace SCIEPort.Features.CelestialCodex.Core
{
    internal sealed class ReaderLayout
    {
        internal readonly float Width, Height, ContentHeight, ContentWidth, ContentX, ContentY, FooterY, ButtonWidth;
        internal readonly bool Compact;
        internal ReaderLayout(float viewportWidth, float viewportHeight, bool reading = false)
        {
            Width = Math.Max(1, Math.Min(900, viewportWidth - 2 * Math.Max(16, Math.Min(32, viewportWidth * .04f))));
            Height = Math.Max(1, Math.Min(900, viewportHeight - 32));
            Compact = Height < 380;
            ContentWidth = Math.Max(1, Math.Min(660, Width - (Width < 420 ? 48 : 80)));
            ContentX = (Width - ContentWidth) / 2;
            ContentY = Compact ? 80 : reading ? 104 : 132;
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
        internal static readonly ReaderTheme Paper = new ReaderTheme(0xFFF4DC, 0xFBF4E5, 0x3E352B, 0x6B5A47, 0x8C7050, 0x4B614B, 0xFFF8E9, 0xE8D8B9);
        internal static readonly ReaderTheme Night = new ReaderTheme(0x24291F, 0x30372D, 0xF4E9CF, 0xC3B79C, 0xB09D78, 0xCDBD94, 0x272E25, 0x424B3D);
    }
}
