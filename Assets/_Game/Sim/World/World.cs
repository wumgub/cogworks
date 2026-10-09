using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>Infinite tile world made of lazily generated chunks.</summary>
    public sealed class World
    {
        public readonly uint Seed;

        readonly Dictionary<long, Chunk> _chunks = new Dictionary<long, Chunk>();

        public World(uint seed)
        {
            Seed = seed;
            Buildings = new BuildingManager(this);
            Players = new PlayerManager();
        }

        public BuildingManager Buildings { get; }
        public PlayerManager Players { get; }

        public int LoadedChunkCount => _chunks.Count;

        public static Int2 TileToChunk(Int2 tile) =>
            new Int2(IntMath.FloorDiv(tile.X, Chunk.Size), IntMath.FloorDiv(tile.Y, Chunk.Size));

        public static int TileToIndex(Int2 tile) =>
            Chunk.Index(IntMath.FloorMod(tile.X, Chunk.Size), IntMath.FloorMod(tile.Y, Chunk.Size));

        /// <summary>Returns the chunk, generating it if needed.</summary>
        public Chunk GetChunk(Int2 chunkCoord)
        {
            long key = chunkCoord.Pack();
            if (!_chunks.TryGetValue(key, out var chunk))
            {
                chunk = new Chunk(chunkCoord);
                WorldGen.Generate(chunk, Seed);
                _chunks.Add(key, chunk);
            }
            return chunk;
        }

        public TerrainType GetTerrain(Int2 tile) => GetChunk(TileToChunk(tile)).Terrain[TileToIndex(tile)];

        public Ore GetOre(Int2 tile, out int amount)
        {
            var chunk = GetChunk(TileToChunk(tile));
            int i = TileToIndex(tile);
            amount = chunk.OreAmount[i];
            return chunk.Ore[i];
        }

        /// <summary>
        /// Hashes modified chunks in key order. Which chunks a peer happens to have generated depends on
        /// where its camera has been, so unmodified chunks are left out on purpose.
        /// </summary>
        internal void AddToHash(ref Fnv64 h)
        {
            foreach (long key in ModifiedChunkKeys())
                _chunks[key].AddToHash(ref h);
            Buildings.AddToHash(ref h);
            Players.AddToHash(ref h);
        }

        /// <summary>Writes everything that isn't regenerable from the seed.</summary>
        internal void Write(BinaryWriter w)
        {
            var keys = ModifiedChunkKeys();
            w.Write(keys.Count);
            foreach (long key in keys) _chunks[key].Write(w);
            Buildings.Write(w);
            Players.Write(w);
        }

        internal void Read(BinaryReader r)
        {
            _chunks.Clear();
            int chunkCount = r.ReadInt32();
            for (int i = 0; i < chunkCount; i++)
            {
                var chunk = Chunk.Read(r);
                _chunks.Add(chunk.Coord.Pack(), chunk);
            }
            Buildings.Read(r);
            Players.Read(r);
        }

        List<long> ModifiedChunkKeys()
        {
            var keys = new List<long>();
            foreach (var pair in _chunks)
                if (pair.Value.Modified) keys.Add(pair.Key);
            keys.Sort();
            return keys;
        }
    }
}
