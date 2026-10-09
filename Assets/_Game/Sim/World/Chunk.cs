using System.IO;

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
        /// <summary>Id of the overhead building (shaft) on each tile, or 0.</summary>
        public readonly int[] Overhead = new int[Area];

        /// <summary>
        /// True once gameplay has changed this chunk (mining, building). Unmodified chunks are a pure
        /// function of the seed, so they're skipped by hashing and saving and can be regenerated freely.
        /// </summary>
        public bool Modified { get; private set; }

        // Cached fingerprint of the tile data; recomputed only after the chunk changes.
        ulong _hash;
        bool _hashDirty = true;

        public Chunk(Int2 coord)
        {
            Coord = coord;
        }

        public static int Index(int localX, int localY) => localY * Size + localX;

        public Int2 Origin => new Int2(Coord.X * Size, Coord.Y * Size);

        /// <summary>Call on every change to tile data (it also invalidates the cached hash).</summary>
        public void MarkModified()
        {
            Modified = true;
            _hashDirty = true;
        }

        internal void Write(BinaryWriter w)
        {
            w.Write(Coord);
            for (int i = 0; i < Area; i++) w.Write((byte)Terrain[i]);
            for (int i = 0; i < Area; i++) w.Write((byte)Ore[i]);
            for (int i = 0; i < Area; i++) w.Write(OreAmount[i]);
            for (int i = 0; i < Area; i++) w.Write(Occupant[i]);
            for (int i = 0; i < Area; i++) w.Write(Overhead[i]);
        }

        internal static Chunk Read(BinaryReader r)
        {
            var c = new Chunk(r.ReadInt2());
            for (int i = 0; i < Area; i++) c.Terrain[i] = (TerrainType)r.ReadByte();
            for (int i = 0; i < Area; i++) c.Ore[i] = (Ore)r.ReadByte();
            for (int i = 0; i < Area; i++) c.OreAmount[i] = r.ReadInt32();
            for (int i = 0; i < Area; i++) c.Occupant[i] = r.ReadInt32();
            for (int i = 0; i < Area; i++) c.Overhead[i] = r.ReadInt32();
            c.MarkModified();
            return c;
        }

        internal void AddToHash(ref Fnv64 h)
        {
            if (_hashDirty)
            {
                var c = Fnv64.Create();
                c.Add(Coord.Pack());
                for (int i = 0; i < Area; i++)
                {
                    c.Add((byte)Terrain[i]);
                    c.Add((byte)Ore[i]);
                    c.Add(OreAmount[i]);
                    c.Add(Occupant[i]);
                    c.Add(Overhead[i]);
                }
                _hash = c.Value;
                _hashDirty = false;
            }
            h.Add((long)_hash);
        }
    }
}
