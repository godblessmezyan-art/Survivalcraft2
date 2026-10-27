using System;

namespace SCIEPort.Features.Gungnir
{
    /// <summary>Pure logical-coordinate placement used by the live HUD and offline layout checks.</summary>
    internal static class GungnirHudLayout
    {
        internal const float BlessingSize = 72f;
        internal const float JudgmentSize = 88f;
        internal const float NativeGap = 10f;
        internal const float DiagonalX = 60f;
        internal const float DiagonalY = 96f;
        internal struct Rect
        {
            internal float X, Y, Width, Height;
            internal float Right => X + Width;
            internal float Bottom => Y + Height;
            internal bool IsValid => Width > 1f && Height > 1f;
            internal bool Intersects(Rect other) => other.X < Right - 0.01f && other.Right > X + 0.01f && other.Y < Bottom - 0.01f && other.Bottom > Y + 0.01f;
        }

        internal struct Result
        {
            internal Rect Blessing, Judgment;
            internal bool Mirrored, UsedFallback;
        }

        internal static Result Place(float width, float height, Rect look, Rect hotbar, bool leftHanded)
        {
            bool validLook = look.IsValid && look.X >= -1f && look.Y >= -1f && look.Right <= width + 1f && look.Bottom <= height + 1f;
            bool mirrored = validLook && (leftHanded || look.X < width * 0.5f);
            float blessingY = validLook ? look.Y + (look.Height - BlessingSize) * 0.56f : height - BlessingSize - 24f;
            float blessingX;
            if (validLook && !mirrored)
            {
                // Leave enough room for the upper-right 88-unit Judgment badge without entering Look.
                blessingX = look.X - NativeGap - JudgmentSize - DiagonalX;
            }
            else if (validLook)
            {
                blessingX = look.Right + NativeGap;
            }
            else
            {
                blessingX = width - BlessingSize - 24f - JudgmentSize - NativeGap;
            }

            var result = new Result
            {
                Mirrored = mirrored,
                UsedFallback = !validLook,
                Blessing = new Rect { X = blessingX, Y = blessingY, Width = BlessingSize, Height = BlessingSize }
            };
            result.Judgment = new Rect
            {
                X = result.Blessing.X + DiagonalX,
                Y = result.Blessing.Y - DiagonalY,
                Width = JudgmentSize,
                Height = JudgmentSize
            };

            float minX = Math.Min(result.Blessing.X, result.Judgment.X);
            float maxX = Math.Max(result.Blessing.Right, result.Judgment.Right);
            float minY = Math.Min(result.Blessing.Y, result.Judgment.Y);
            float maxY = Math.Max(result.Blessing.Bottom, result.Judgment.Bottom);
            float dx = minX < 8f ? 8f - minX : maxX > width - 8f ? width - 8f - maxX : 0f;
            float dy = minY < 8f ? 8f - minY : maxY > height - 8f ? height - 8f - maxY : 0f;
            result.Blessing.X += dx; result.Judgment.X += dx;
            result.Blessing.Y += dy; result.Judgment.Y += dy;

            // The native hotbar is authoritative. If a narrow/split viewport causes contact,
            // lift the pair as one unit so their diagonal relationship remains unchanged.
            if (hotbar.IsValid && (result.Blessing.Intersects(hotbar) || result.Judgment.Intersects(hotbar)))
            {
                float lift = Math.Max(result.Blessing.Bottom, result.Judgment.Bottom) - hotbar.Y + NativeGap;
                result.Blessing.Y -= lift; result.Judgment.Y -= lift;
                result.UsedFallback = true;
            }
            return result;
        }
    }
}
