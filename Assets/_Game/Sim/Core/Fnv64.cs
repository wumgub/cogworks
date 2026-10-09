namespace Cogworks.Sim
{
    /// <summary>FNV-1a 64-bit hasher used to fingerprint world state for desync detection.</summary>
    public struct Fnv64
    {
        const ulong Offset = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        public ulong Value;

        public static Fnv64 Create() => new Fnv64 { Value = Offset };

        public void Add(byte b)
        {
            unchecked
            {
                Value ^= b;
                Value *= Prime;
            }
        }

        public void Add(int v)
        {
            Add((byte)v);
            Add((byte)(v >> 8));
            Add((byte)(v >> 16));
            Add((byte)(v >> 24));
        }

        public void Add(long v)
        {
            Add((int)v);
            Add((int)(v >> 32));
        }
    }
}
