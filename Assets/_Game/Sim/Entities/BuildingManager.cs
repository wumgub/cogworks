using System;
using System.Collections.Generic;

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

        public IEnumerable<Building> All => _byId.Values;

        public Building Get(int id) => _byId.TryGetValue(id, out var b) ? b : null;

        public Building GetAt(Int2 tile)
        {
            int id = _world.GetChunk(World.TileToChunk(tile)).Occupant[World.TileToIndex(tile)];
            return id == 0 ? null : _byId[id];
        }

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
                if (chunk.Occupant[i] != 0) return false;
                if (chunk.Ore[i] != Ore.None) touchesOre = true;
            }
            return !spec.RequiresOre || touchesOre;
        }

        /// <summary>Places a building, or returns null if the spot is invalid.</summary>
        public Building Place(BuildingSpec spec, Int2 origin, Direction direction)
        {
            if (!CanPlace(spec, origin, direction)) return null;

            var building = new Building(_nextId++, spec, origin, direction);
            _byId.Add(building.Id, building);
            SetOccupancy(building, building.Id);
            Placed?.Invoke(building);
            return building;
        }

        public bool Remove(Int2 tile)
        {
            var building = GetAt(tile);
            if (building == null) return false;

            SetOccupancy(building, 0);
            _byId.Remove(building.Id);
            Removed?.Invoke(building);
            return true;
        }

        /// <summary>
        /// Sets facing. Only square footprints can rotate in place, so occupancy never changes here;
        /// non-square buildings are rotated by removing and re-placing.
        /// </summary>
        public bool SetDirection(Int2 tile, Direction direction)
        {
            var building = GetAt(tile);
            if (building == null || !building.Spec.Rotatable) return false;
            if (building.Spec.Width != building.Spec.Height) return false;
            if (building.Direction == direction) return false;

            building.Direction = direction;
            _world.GetChunk(World.TileToChunk(building.Origin)).MarkModified();
            Changed?.Invoke(building);
            return true;
        }

        void SetOccupancy(Building building, int id)
        {
            Int2 size = building.Size;
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
            {
                var tile = new Int2(building.Origin.X + x, building.Origin.Y + y);
                var chunk = _world.GetChunk(World.TileToChunk(tile));
                chunk.Occupant[World.TileToIndex(tile)] = id;
                chunk.MarkModified();
            }
        }

        internal void AddToHash(ref Fnv64 h)
        {
            h.Add(_nextId);
            var ids = new List<int>(_byId.Keys);
            ids.Sort();
            foreach (int id in ids)
            {
                var b = _byId[id];
                h.Add(id);
                h.Add(b.Spec.Id);
                h.Add(b.Origin.Pack());
                h.Add((byte)b.Direction);
            }
        }
    }
}
