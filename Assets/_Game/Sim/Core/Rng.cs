namespace Cogworks.Sim
{
    /// <summary>SplitMix64 PRNG. Deterministic and serializable (the whole state is <see cref="State"/>).</summary>
    public struct Rng
    {
        public ulong State;

        public Rng(ulong seed)
        {
            State = seed;
        }

        public ulong NextULong()
        {
            unchecked
            {
                ulong z = State += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Uniform integer in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            ulong span = (ulong)((long)maxExclusive - min);
            return (int)(min + (long)(NextULong() % span));
        }
    }
}
