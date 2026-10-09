using System;

namespace Cogworks.Sim
{
    /// <summary>Integer 2D coordinate used for tiles and chunks.</summary>
    public readonly struct Int2 : IEquatable<Int2>
    {
        public readonly int X;
        public readonly int Y;

        public Int2(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Int2 operator +(Int2 a, Int2 b) => new Int2(a.X + b.X, a.Y + b.Y);
        public static Int2 operator -(Int2 a, Int2 b) => new Int2(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Int2 a, Int2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Int2 a, Int2 b) => !(a == b);

        public bool Equals(Int2 other) => this == other;
        public override bool Equals(object obj) => obj is Int2 other && this == other;
        public override int GetHashCode() => unchecked(X * 73856093 ^ Y * 19349663);
        public override string ToString() => $"({X}, {Y})";

        /// <summary>Packs into a single sortable 64-bit key.</summary>
        public long Pack() => ((long)X << 32) | (uint)Y;

        public static Int2 Unpack(long key) => new Int2((int)(key >> 32), (int)(key & 0xFFFFFFFF));
    }
}
