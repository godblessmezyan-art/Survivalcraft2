using Engine;

namespace SCIEPort.Features.MountainShelter
{
    internal static class ShelterLayout
    {
        internal const int LightSource = 6;
        internal const int StructureCount = 12;
        internal const int CellCount = 16;
        internal const float FloorHeight = .18f;
        internal const float RoofBottom = .14f;

        internal static bool DoorPart(int part) => part == 0 || part == 1 || part == 4 || part == 5;
        internal static bool LampPart(int part) => part == 6 || part == 7;

        internal static int Encode(int part, int rotation, bool closed = false, bool lit = false)
            => 1 + (part << 4) + rotation + (closed ? 4 : 0) + (lit ? 8 : 0);

        internal static bool Decode(int data, out int part, out int rotation, out bool closed, out bool lit)
        {
            int bits = data - 1;
            part = bits >> 4;
            rotation = bits & 3;
            closed = (bits & 4) != 0;
            lit = (bits & 8) != 0;
            return part >= 0 && part < CellCount && (!closed || DoorPart(part)) && (!lit || LampPart(part));
        }

        private static Point3 Offset(int rotation, int x, int y, int z)
        {
            switch (rotation)
            {
                case 1: return new Point3(-z, y, x);
                case 2: return new Point3(-x, y, -z);
                case 3: return new Point3(z, y, -x);
                default: return new Point3(x, y, z);
            }
        }

        internal static Point3 Offset(int part, int rotation)
            => Offset(rotation, part & 1, part < StructureCount ? part / 4 : -1, (part & 2) >> 1);

        internal static Point3 Entrance(Point3 root, int rotation, int lane)
            => root + Offset(rotation, lane, 0, -1);

        internal static int Facing(Vector3 forward)
            => MathUtils.Abs(forward.X) > MathUtils.Abs(forward.Z)
                ? (forward.X < 0 ? 1 : 3)
                : (forward.Z < 0 ? 2 : 0);

        internal static Vector3 Local(Vector3 position, Point3 root, int rotation)
        {
            float x = position.X - root.X - .5f, z = position.Z - root.Z - .5f;
            float y = position.Y - root.Y;
            switch (rotation)
            {
                case 1: return new Vector3(.5f + z, y, .5f - x);
                case 2: return new Vector3(.5f - x, y, .5f - z);
                case 3: return new Vector3(.5f - z, y, .5f + x);
                default: return new Vector3(.5f + x, y, .5f + z);
            }
        }

        internal static Vector3 RotateCell(Vector3 p, int rotation)
        {
            switch (rotation)
            {
                case 1: return new Vector3(1f - p.Z, p.Y, p.X);
                case 2: return new Vector3(1f - p.X, p.Y, 1f - p.Z);
                case 3: return new Vector3(p.Z, p.Y, 1f - p.X);
                default: return p;
            }
        }

        internal static bool ValidRoot(Point3 point)
            => point.X > -1048576 && point.X < 1048576 && point.Z > -1048576 && point.Z < 1048576
                && point.Y >= 2 && point.Y <= 252;
    }
}
