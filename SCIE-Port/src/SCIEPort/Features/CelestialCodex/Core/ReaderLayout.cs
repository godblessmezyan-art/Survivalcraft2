using System;

namespace SCIEPort.Features.CelestialCodex.Core
{
    internal sealed class ReaderLayout
    {
        internal readonly float Width, Height, ContentHeight, ButtonWidth;
        internal ReaderLayout(float viewportWidth, float viewportHeight)
        {
            Width = Math.Max(1, Math.Min(720, viewportWidth - 2 * Math.Max(16, viewportWidth * .04f)));
            Height = Math.Max(1, Math.Min(720, viewportHeight - 2 * Math.Max(16, viewportHeight * .04f)));
            ContentHeight = Math.Max(1, Height - 164);
            ButtonWidth = Math.Max(1, (Width - 40) / 3);
        }
    }
}
