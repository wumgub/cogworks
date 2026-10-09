using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>Infinite tile world made of lazily generated chunks, plus everything built on it.</summary>
    public sealed class World
    {
        public readonly uint Seed;

        /// <summary>Sandbox worlds place buildings for free and need no inventory.</summary>
        public readonly bool Sandbox;

        readonly Dictionary<long, Chunk> _chunks = new Dictionary<long, Chunk>();
        readonly BeltSystem _belts = new BeltSystem();
        readonly KineticSystem _kinetics = new KineticSystem();

        public World(uint seed, bool sandbox = false)
        {
            Seed = seed;
            Sandbox = sandbox;
            Buildings = new BuildingManager(this);
            Players = new PlayerManager(this);
            Research = new ResearchState();
        }

        public BuildingManager Buildings { get; }
        public PlayerManager Players { get; }
        public ResearchState Research { get; }

        /// <summary>Rotational power networks (read-only view for UI).</summary>
        public KineticView Kinetics => new KineticView(_kinetics);

        /// <summary>The tick currently being simulated (set by the simulation before commands run).</summary>
        public long CurrentTick { get; internal set; }

        /// <summary>Raised when a tile's terrain or ore changes (e.g. an ore tile is mined out).</summary>
        public event Action<Int2> TileChanged;

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

        /// <summary>Removes one unit of ore from the tile. Returns the ore type, or None if there was none.</summary>
        internal Ore MineOre(Int2 tile)
        {
            var chunk = GetChunk(TileToChunk(tile));
            int i = TileToIndex(tile);
            var ore = chunk.Ore[i];
            if (ore == Ore.None || chunk.OreAmount[i] <= 0) return Ore.None;
            chunk.MarkModified();
            if (--chunk.OreAmount[i] == 0)
            {
                chunk.Ore[i] = Ore.None;
                TileChanged?.Invoke(tile);
            }
            return ore;
        }

        internal void OnBuildingsChanged(Building b)
        {
            if (b.Spec.Kind == BuildingKind.Conveyor) _belts.MarkDirty();
            if (b.Spec.Kinetic != KineticRole.None) _kinetics.MarkDirty();
        }

        /// <summary>
        /// One tick of world simulation, after commands. Order matters for determinism and feel:
        /// power first (machines read this tick's RPM), then machines, then inserters, then belts.
        /// </summary>
        internal void Tick()
        {
            var ordered = Buildings.Ordered;
            _kinetics.Tick(this, ordered);
            foreach (var b in ordered)
            {
                switch (b.Spec.Kind)
                {
                    case BuildingKind.Drill:
                    case BuildingKind.Furnace:
                    case BuildingKind.Assembler:
                    case BuildingKind.Lab:
                        b.Logic.Tick(this);
                        break;
                }
            }
            foreach (var b in ordered)
                if (b.Spec.Kind == BuildingKind.Inserter) b.Logic.Tick(this);
            _belts.Tick(this, ordered);
            Players.Tick();
        }

        /// <summary>
        /// Hashes modified chunks in key order. Which chunks a peer happens to have generated depends on
        /// where its camera has been, so unmodified chunks are left out on purpose.
        /// </summary>
        internal void AddToHash(ref Fnv64 h)
        {
            h.Add(Sandbox ? 1 : 0);
            foreach (long key in ModifiedChunkKeys())
                _chunks[key].AddToHash(ref h);
            Buildings.AddToHash(ref h);
            Players.AddToHash(ref h);
            Research.AddToHash(ref h);
        }

        /// <summary>Writes everything that isn't regenerable from the seed (seed and sandbox flag are written by the sim).</summary>
        internal void Write(BinaryWriter w)
        {
            var keys = ModifiedChunkKeys();
            w.Write(keys.Count);
            foreach (long key in keys) _chunks[key].Write(w);
            Buildings.Write(w);
            Players.Write(w);
            Research.Write(w);
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
            Research.Read(r);
            _belts.MarkDirty();
            _kinetics.MarkDirty();
        }

        List<long> ModifiedChunkKeys()
        {
            var keys = new List<long>();
            foreach (var pair in _chunks)
                if (pair.Value.Modified) keys.Add(pair.Key);
            keys.Sort();
            return keys;
        }

        /// <summary>Read-only access to rotational power state for presentation.</summary>
        public readonly struct KineticView
        {
            readonly KineticSystem _system;
            internal KineticView(KineticSystem system) => _system = system;

            public KineticNetwork NetworkOf(Building b) => _system.NetworkOf(b);
            public int RpmAt(Building b) => _system.RpmAt(b);
            public IReadOnlyList<KineticNetwork> Networks => _system.Networks;
        }
    }
}
