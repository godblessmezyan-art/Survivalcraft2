using System.Collections.Generic;

namespace SCIEPort.Features.OmniscientEye
{
    /// <summary>Logical-coordinate placement, shared by the runtime and offline checks.</summary>
    public static class EyeLayout
    {
        /// <summary>Axis-aligned UI bounds in the controls container.</summary>
        public struct Rect
        {
            /// <summary>Position and size.</summary>
            public float X, Y, Width, Height;
            /// <summary>Tests with an eight-unit safety margin.</summary>
            public bool Overlaps(Rect other) => X < other.X + other.Width + 8 && X + Width + 8 > other.X && Y < other.Y + other.Height + 8 && Y + Height + 8 > other.Y;
        }

        /// <summary>Finds an upper-centre slot; yields no HUD if important controls fill the area.</summary>
        public static Rect? Place(float width, float height, IEnumerable<Rect> obstacles)
        {
            float w = System.Math.Min(340, width - 316);
            const float h = 118;
            if (w < 248 || height < h + 32) return null;
            foreach (float y in new[] { 8f, 64f, 128f })
            {
                if (y + h > height - 80) continue;
                var candidate = new Rect { X = (width - w) / 2, Y = y, Width = w, Height = h };
                bool blocked = false;
                foreach (Rect obstacle in obstacles)
                    if (obstacle.Width > 0 && obstacle.Height > 0 && candidate.Overlaps(obstacle)) { blocked = true; break; }
                if (!blocked) return candidate;
            }
            return null;
        }
    }
}
