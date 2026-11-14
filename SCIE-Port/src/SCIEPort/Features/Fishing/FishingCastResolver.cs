using System;

namespace SCIEPort.Features.Fishing
{
    // Pure bounded geometry; the game adapter supplies decoded cells, never packed values.
    internal struct FishingPoint
    {
        internal float X, Y, Z;
        internal FishingPoint(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    internal struct FishingCell
    {
        // 0 air, 1 exposed water, 2 obstruction, 3 unavailable terrain.
        internal byte Kind;
        internal float Height;
        internal FishingCell(byte kind, float height = 0f) { Kind = kind; Height = height; }
    }

    internal struct FishingLanding
    {
        internal int X, Y, Z;
        internal FishingPoint Position;
    }

    internal enum FishingCastFailure { None, NoWater, Blocked, Unavailable, Invalid }

    internal static class FishingCastResolver
    {
        internal const int MaxVisitedCells = 64;

        internal static FishingCastFailure Resolve(FishingPoint origin, FishingPoint direction,
            Func<int, int, int, FishingCell> read, out FishingLanding landing)
        {
            FishingCastFailure result = Trace(origin, direction, FishingRules.MaxCastDistance, read, out landing);
            // Small downward assistance for level touch aim, never through the first obstruction.
            if (result != FishingCastFailure.NoWater) return result;
            double length = Math.Sqrt((double)direction.X * direction.X + (double)direction.Y * direction.Y + (double)direction.Z * direction.Z);
            direction = new FishingPoint((float)(direction.X / length), (float)(direction.Y / length), (float)(direction.Z / length));
            if (direction.Y > 0.15f || direction.Y < -0.5f) return result;
            for (int i = 1; i <= 2; i++)
            {
                FishingPoint assisted = new FishingPoint(direction.X, direction.Y - 0.15f * i, direction.Z);
                result = Trace(origin, assisted, FishingRules.MaxCastDistance, read, out landing);
                if (result != FishingCastFailure.NoWater) return result;
            }
            return result;
        }

        internal static FishingCastFailure Trace(FishingPoint origin, FishingPoint direction, float range,
            Func<int, int, int, FishingCell> read, out FishingLanding landing)
        {
            landing = default(FishingLanding);
            if (!Finite(origin.X) || !Finite(origin.Y) || !Finite(origin.Z) ||
                !Finite(direction.X) || !Finite(direction.Y) || !Finite(direction.Z) ||
                Math.Abs(origin.X) > 10000000f || Math.Abs(origin.Z) > 10000000f ||
                !Finite(range) || range <= 0f || range > FishingRules.MaxCatchDistance) return FishingCastFailure.Invalid;
            double length = Math.Sqrt((double)direction.X * direction.X + (double)direction.Y * direction.Y + (double)direction.Z * direction.Z);
            if (length < 0.000001) return FishingCastFailure.Invalid;
            double dx = direction.X / length, dy = direction.Y / length, dz = direction.Z / length;
            int x = (int)Math.Floor(origin.X), y = (int)Math.Floor(origin.Y), z = (int)Math.Floor(origin.Z);
            int sx = Math.Sign(dx), sy = Math.Sign(dy), sz = Math.Sign(dz);
            double tx = Boundary(origin.X, x, dx), ty = Boundary(origin.Y, y, dy), tz = Boundary(origin.Z, z, dz);
            double stepx = dx == 0 ? double.PositiveInfinity : Math.Abs(1 / dx);
            double stepy = dy == 0 ? double.PositiveInfinity : Math.Abs(1 / dy);
            double stepz = dz == 0 ? double.PositiveInfinity : Math.Abs(1 / dz);
            double enter = 0;
            for (int visited = 0; visited < MaxVisitedCells && enter <= range; visited++)
            {
                double exit = Math.Min(range, Math.Min(tx, Math.Min(ty, tz)));
                FishingCell cell = read(x, y, z);
                if (cell.Kind == 3) return FishingCastFailure.Unavailable;
                if (cell.Kind == 2) return FishingCastFailure.Blocked;
                if (cell.Kind == 1)
                {
                    if (cell.Height <= 0 || cell.Height > 1 || !Finite(cell.Height)) return FishingCastFailure.Blocked;
                    if (origin.Y < y + cell.Height && enter == 0) return FishingCastFailure.Blocked;
                    if (dy < -0.000001)
                    {
                        double hit = (y + cell.Height - origin.Y) / dy;
                        if (hit >= enter - 0.00001 && hit <= exit + 0.00001 && hit <= range)
                        {
                            landing = new FishingLanding { X = x, Y = y, Z = z,
                                Position = new FishingPoint((float)(origin.X + dx * hit), y + cell.Height, (float)(origin.Z + dz * hit)) };
                            return FishingCastFailure.None;
                        }
                    }
                    // Water sides cannot be used as a surface or as a path to hidden water.
                    if (origin.Y + dy * enter < y + cell.Height - 0.00001) return FishingCastFailure.Blocked;
                }
                if (exit >= range) return FishingCastFailure.NoWater;
                enter = exit;
                // Visit axes separately at ties: conservative occlusion at voxel corners.
                if (tx <= ty && tx <= tz) { x += sx; tx += stepx; }
                else if (ty <= tz) { y += sy; ty += stepy; }
                else { z += sz; tz += stepz; }
            }
            return FishingCastFailure.NoWater;
        }

        private static double Boundary(float p, int cell, double d)
            => d == 0 ? double.PositiveInfinity : ((d > 0 ? cell + 1d : cell) - p) / d;
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
