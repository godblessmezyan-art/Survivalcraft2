using System;
using Engine;
using Game;

namespace SCIEPort.Features.Home
{
    internal static class HomeSeatRules
    {
        internal const int MaxSeats = 64;
        internal const float SittingWidth = .42f;
        internal static bool IsSeat(int id) => id == HomeRules.Sofa || id == HomeRules.Dining;
        internal static int Count(int id) => id == HomeRules.Dining ? 2 : id == HomeRules.Sofa ? 1 : 0;
        internal static bool Finite(Vector3 v) => !float.IsNaN(v.X) && !float.IsInfinity(v.X) && !float.IsNaN(v.Y) && !float.IsInfinity(v.Y) && !float.IsNaN(v.Z) && !float.IsInfinity(v.Z);
        internal static bool Valid(Point3 p) => p.Y > 0 && p.Y < 255 && Math.Abs((long)p.X) <= 1048576 && Math.Abs((long)p.Z) <= 1048576;
        internal static bool Newer(uint value, uint old) => value != old && unchecked((int)(value - old)) > 0;
        internal static Vector3 Position(Point3 root, int value, int seat)
        {
            int id = Terrain.ExtractContents(value), data = Terrain.ExtractData(value), variant = HomeRules.Variant(data);
            float x = variant == 1 || variant == 4 ? .57f : variant == 3 ? .43f : .5f;
            Vector3 local = id == HomeRules.Dining ? new Vector3(seat == 0 ? .33f : 1.67f, .49f, 1f) : new Vector3(x, .54f, .42f);
            return Transform(root, data, local);
        }
        internal static Vector3 Transform(Point3 root, int data, Vector3 local)
            => Vector3.Transform(local - new Vector3(.5f, 0, .5f), Matrix.CreateRotationY(HomeRules.Rotation(data) * MathUtils.PI / 2)) + new Vector3(root.X + .5f, root.Y, root.Z + .5f);
        internal static float Yaw(int value, int seat) => HomeRules.Rotation(Terrain.ExtractData(value)) * MathUtils.PI / 2
            + (Terrain.ExtractContents(value) == HomeRules.Dining ? (seat == 0 ? -MathUtils.PI / 2 : MathUtils.PI / 2) : 0);
        internal static Vector3 SeatedBox(Vector3 original) => new Vector3(Math.Min(original.X, SittingWidth), original.Y, Math.Min(original.Z, SittingWidth));
        internal static float VisualDrop(Vector3 original) => .85f * original.Y / 1.77f;
    }
}
