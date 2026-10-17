using System;

namespace BuilderAssistant.Core
{
    /// <summary>
    /// An integer cell coordinate, used both for blueprint-relative offsets
    /// and for absolute world cells. Independent of any game assembly so the
    /// building logic stays unit-testable without a running Survivalcraft.
    /// </summary>
    internal readonly struct CellPos : IEquatable<CellPos>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;

        public CellPos(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public CellPos Add(CellPos other)
        {
            return new CellPos(X + other.X, Y + other.Y, Z + other.Z);
        }

        public bool Equals(CellPos other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is CellPos other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + X;
                hash = (hash * 31) + Y;
                hash = (hash * 31) + Z;
                return hash;
            }
        }

        public static bool operator ==(CellPos left, CellPos right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CellPos left, CellPos right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return "(" + X + "," + Y + "," + Z + ")";
        }
    }
}
