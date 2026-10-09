namespace Cogworks.Sim
{
    /// <summary>Integer helpers. The sim never uses floating point, so results match across machines.</summary>
    public static class IntMath
    {
        /// <summary>Division rounding toward negative infinity (C# '/' rounds toward zero).</summary>
        public static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        /// <summary>Modulo that is always in [0, b) for positive b.</summary>
        public static int FloorMod(int a, int b)
        {
            int m = a % b;
            return m < 0 ? m + b : m;
        }

        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
