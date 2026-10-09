namespace Cogworks.Sim
{
    /// <summary>
    /// Deterministic integer value noise. Output range is [0, 65535].
    /// Uses only integer math so every peer generates identical worlds from a seed.
    /// </summary>
    public static class Noise
    {
        public const int Max = 65535;

        public static uint Hash(int x, int y, uint seed)
        {
            unchecked
            {
                uint h = seed ^ 0x9E3779B9u;
                h ^= (uint)x * 0x27D4EB2Du;
                h = Rotl(h, 15) * 0x85EBCA6Bu;
                h ^= (uint)y * 0x165667B1u;
                h = Rotl(h, 13) * 0xC2B2AE35u;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>Single octave of smoothed value noise with lattice spacing <paramref name="cell"/> tiles.</summary>
        public static int Value(int x, int y, int cell, uint seed)
        {
            int cx = IntMath.FloorDiv(x, cell);
            int cy = IntMath.FloorDiv(y, cell);
            int fx = x - cx * cell;
            int fy = y - cy * cell;

            long a = Hash(cx, cy, seed) & 0xFFFF;
            long b = Hash(cx + 1, cy, seed) & 0xFFFF;
            long c = Hash(cx, cy + 1, seed) & 0xFFFF;
            long d = Hash(cx + 1, cy + 1, seed) & 0xFFFF;

            long tx = Smooth(fx * 65536L / cell);
            long ty = Smooth(fy * 65536L / cell);

            long top = a + (((b - a) * tx) >> 16);
            long bottom = c + (((d - c) * tx) >> 16);
            return (int)(top + (((bottom - top) * ty) >> 16));
        }

        /// <summary>Sum of octaves, each half the cell size and half the weight of the last. Normalized to [0, 65535].</summary>
        public static int Fractal(int x, int y, int cell, int octaves, uint seed)
        {
            long sum = 0;
            long totalWeight = 0;
            long weight = 256;
            for (int i = 0; i < octaves && cell >= 1; i++)
            {
                sum += Value(x, y, cell, unchecked(seed + (uint)i * 0x632BE5ABu)) * weight;
                totalWeight += weight;
                weight >>= 1;
                cell >>= 1;
            }
            return (int)(sum / totalWeight);
        }

        // 3t^2 - 2t^3 in 16.16 fixed point.
        static long Smooth(long t) => (((t * t) >> 16) * (3 * 65536L - 2 * t)) >> 16;

        static uint Rotl(uint v, int r) => (v << r) | (v >> (32 - r));
    }
}
