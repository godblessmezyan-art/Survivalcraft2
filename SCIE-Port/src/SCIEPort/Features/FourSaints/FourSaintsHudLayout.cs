using System;

namespace SCIEPort.Features.FourSaints
{
    internal static class FourSaintsHudLayout
    {
        internal const float Size = 76f;
        internal struct Rect
        {
            internal float X, Y, Width, Height;
            internal float Right => X + Width;
            internal float Bottom => Y + Height;
            internal bool IsValid => Width > 1f && Height > 1f;
            internal bool Intersects(Rect other) => other.IsValid && other.X < Right && other.Right > X && other.Y < Bottom && other.Bottom > Y;
        }

        internal static Rect Place(float width, float height, Rect look, Rect hotbar, bool leftHanded)
        {
            bool valid = look.IsValid;
            bool mirrored = valid && (leftHanded || look.X < width * .5f);
            float x = valid ? (mirrored ? look.Right + 10f : look.X - Size - 170f) : width - Size - 250f;
            float y = valid ? look.Y + (look.Height - Size) * .5f : height - Size - 26f;
            var result = new Rect { X = x, Y = y, Width = Size, Height = Size };
            result.X = Math.Max(8f, Math.Min(width - Size - 8f, result.X));
            result.Y = Math.Max(8f, Math.Min(height - Size - 8f, result.Y));
            if (result.Intersects(hotbar)) result.Y = Math.Max(8f, hotbar.Y - Size - 10f);
            return result;
        }
    }
}
