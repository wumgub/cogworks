namespace Cogworks.Sim
{
    /// <summary>A square block of tiles. Terrain and ore are generated from the world seed on first access.</summary>
    public sealed class Chunk
    {
        public const int Size = 32;
        public const int Area = Size * Size;

        public readonly Int2 Coord;
        public readonly TerrainType[] Terrain = new TerrainType[Area];
        public readonly Ore[] Ore = new Ore[Area];
        public readonly int[] OreAmount = new int[Area];
        /// <summary>Id of the building covering each tile, or 0.</summary>
        public readonly int[] Occupant = new int[Area];

        /// <summary>
        /// True once gameplay has changed this chunk (mining, building). Unmodified chunks are a pure
        /// function of the seed, so they're skipped by hashing and saving and can be regenerated freely.
        /// </summary>
        public bool Modified { get; private set; }

        public Chunk(Int2 coord)
        {
            Coord = coord;
        }

        public static int Index(int localX, int localY) => localY * Size + localX;

        public Int2 Origin => new Int2(Coord.X * Size, Coord.Y * Size);

        public void MarkModified() => Modified = true;

        internal void AddToHash(ref Fnv64 h)
        {
            h.Add(Coord.Pack());
            for (int i = 0; i < Area; i++)
            {
                h.Add((byte)Terrain[i]);
                h.Add((byte)Ore[i]);
                h.Add(OreAmount[i]);
                h.Add(Occupant[i]);
            }
        }
    }
}
