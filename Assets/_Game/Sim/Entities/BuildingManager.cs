using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>
    /// Owns all placed buildings and the per-tile occupancy stored in chunks. Ids are allocated
    /// sequentially, so peers applying the same commands assign the same ids.
    /// </summary>
    public sealed class BuildingManager
    {
        readonly World _world;
        readonly Dictionary<int, Building> _byId = new Dictionary<int, Building>();
        // Kept sorted by id: new ids are always the largest, so placement appends.
        readonly List<Building> _ordered = new List<Building>();
        int _nextId = 1;

        /// <summary>Presentation hooks. Raised while a tick runs; never mutate the sim from a handler.</summary>
        public event Action<Building> Placed;
        public event Action<Building> Removed;
        public event Action<Building> Changed;

        internal BuildingManager(World world)
        {
            _world = world;
        }

        public int Count => _byId.Count;

        /// <summary>Buildings in id order, which is also the order the sim updates them in.</summary>
        public IReadOnlyList<Building> All => _ordered;

        internal List<Building> Ordered => _ordered;

        public Building Get(int id) => _byId.TryGetValue(id, out var b) ? b : null;

        /// <summary>The ground-layer building on a tile (everything except overhead shafts).</summary>
        public Building GetAt(Int2 tile)
        {
            int id = _world.GetChunk(World.TileToChunk(tile)).Occupant[World.TileToIndex(tile)];
            return id == 0 ? null : _byId[id];
        }

        /// <summary>The overhead building (shaft) on a tile, if any.</summary>
        public Building GetOverheadAt(Int2 tile)
        {
            int id = _world.GetChunk(World.TileToChunk(tile)).Overhead[World.TileToIndex(tile)];
            return id == 0 ? null : _byId[id];
        }

        /// <summary>What a player points at: the overhead building if there is one, else the ground one.</summary>
        public Building GetTopAt(Int2 tile) => GetOverheadAt(tile) ?? GetAt(tile);

        public bool CanPlace(BuildingSpec spec, Int2 origin, Direction direction)
        {
            if (spec == null) return false;
            if (!spec.Rotatable && direction != Direction.North) return false;

            Int2 size = spec.SizeFor(direction);
            bool touchesOre = false;
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
            {
                var tile = new Int2(origin.X + x, origin.Y + y);
                var chunk = _world.GetChunk(World.TileToChunk(tile));
                int i = World.TileToIndex(tile);
                if (!TerrainInfo.IsBuildable(chunk.Terrain[i])) return false;
                if (spec.Overhead)
                {
                    // Over empty ground or a belt, never through a machine.
                    if (chunk.Overhead[i] != 0) return false;
                    if (chunk.Occupant[i] != 0 && !_byId[chunk.Occupant[i]].Spec.IsBeltFamily) return false;
                }
                else
                {
                    if (chunk.Occupant[i] != 0) return false;
                    // Only belts can go under a shaft.
                    if (chunk.Overhead[i] != 0 && !spec.IsBeltFamily) return false;
                }
                if (chunk.Ore[i] != Ore.None) touchesOre = true;
            }
            if (spec.RequiresOre && !touchesOre) return false;
            if (spec.RequiresAdjacentWater && !TouchesWater(origin, size)) return false;
            return true;
        }

        bool TouchesWater(Int2 origin, Int2 size)
        {
            for (int x = -1; x <= size.X; x++)
            for (int y = -1; y <= size.Y; y++)
            {
                bool inside = x >= 0 && x < size.X && y >= 0 && y < size.Y;
                bool corner = (x == -1 || x == size.X) && (y == -1 || y == size.Y);
                if (inside || corner) continue;
                if (_world.GetTerrain(new Int2(origin.X + x, origin.Y + y)) == TerrainType.Water) return true;
            }
            return false;
        }

        /// <summary>Places a building, or returns null if the spot is invalid.</summary>
        public Building Place(BuildingSpec spec, Int2 origin, Direction direction)
        {
            if (!CanPlace(spec, origin, direction)) return null;

            var building = new Building(_nextId++, spec, origin, direction);
            _byId.Add(building.Id, building);
            _ordered.Add(building);
            SetOccupancy(building, building.Id);
            _world.OnBuildingsChanged(building);
            Placed?.Invoke(building);
            return building;
        }

        public bool Remove(Int2 tile) => Remove(tile, null);

        /// <summary>
        /// Removes the topmost building on <paramref name="tile"/> (an overhead shaft before the belt under it),
        /// adding its contents to <paramref name="contents"/>.
        /// </summary>
        internal bool Remove(Int2 tile, List<ItemStack> contents)
        {
            var building = GetTopAt(tile);
            if (building == null) return false;

            building.Logic.CollectContents(contents ?? new List<ItemStack>());
            SetOccupancy(building, 0);
            _byId.Remove(building.Id);
            _ordered.RemoveAt(IndexOf(building.Id));
            _world.OnBuildingsChanged(building);
            Removed?.Invoke(building);
            return true;
        }

        int IndexOf(int id)
        {
            int lo = 0, hi = _ordered.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                int midId = _ordered[mid].Id;
                if (midId == id) return mid;
                if (midId < id) lo = mid + 1;
                else hi = mid - 1;
            }
            throw new InvalidOperationException($"Building {id} not in order list");
        }

        /// <summary>
        /// Sets facing. Only square footprints can rotate in place, so occupancy never changes here;
        /// non-square buildings are rotated by removing and re-placing.
        /// </summary>
        public bool SetDirection(Int2 tile, Direction direction)
        {
            var building = GetTopAt(tile);
            if (building == null || !building.Spec.Rotatable) return false;
            if (building.Spec.Width != building.Spec.Height) return false;
            if (building.Direction == direction) return false;

            building.Direction = direction;
            _world.GetChunk(World.TileToChunk(building.Origin)).MarkModified();
            _world.OnBuildingsChanged(building);
            Changed?.Invoke(building);
            return true;
        }

        void SetOccupancy(Building building, int id)
        {
            foreach (var tile in building.Footprint())
            {
                var chunk = _world.GetChunk(World.TileToChunk(tile));
                (building.Spec.Overhead ? chunk.Overhead : chunk.Occupant)[World.TileToIndex(tile)] = id;
                chunk.MarkModified();
            }
        }

        internal void Write(BinaryWriter w)
        {
            w.Write(_nextId);
            w.Write(_ordered.Count);
            foreach (var b in _ordered)
            {
                w.Write(b.Id);
                w.Write(b.Spec.Id);
                w.Write(b.Origin);
                w.Write((byte)b.Direction);
                b.Logic.Write(w);
            }
        }

        /// <summary>Replaces all buildings. Chunk occupancy is restored by the chunks themselves.</summary>
        internal void Read(BinaryReader r)
        {
            _byId.Clear();
            _ordered.Clear();
            _nextId = r.ReadInt32();
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int id = r.ReadInt32();
                ushort specId = r.ReadUInt16();
                var spec = BuildingCatalog.Get(specId) ?? throw new InvalidDataException($"Unknown building type {specId}");
                var b = new Building(id, spec, r.ReadInt2(), (Direction)(r.ReadByte() & 3));
                b.Logic.Read(r);
                _byId.Add(id, b);
                _ordered.Add(b);
            }
            _ordered.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        internal void AddToHash(ref Fnv64 h)
        {
            h.Add(_nextId);
            foreach (var b in _ordered)
            {
                h.Add(b.Id);
                h.Add(b.Spec.Id);
                h.Add(b.Origin.Pack());
                h.Add((byte)b.Direction);
                b.Logic.AddToHash(ref h);
            }
        }
    }
}
