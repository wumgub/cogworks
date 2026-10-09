using System.Collections.Generic;
using Cogworks.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Cogworks.View
{
    /// <summary>Streams sim chunks into tilemaps around a focus point.</summary>
    public sealed class WorldView : MonoBehaviour
    {
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] Tilemap terrainMap;
        [SerializeField] Tilemap oreMap;
        [Tooltip("One border overlay layer per entry in TerrainBlend.Order (index 0, water, is unused).")]
        [SerializeField] Tilemap[] blendMaps;
        [SerializeField] Transform focus;
        [Tooltip("Chunks within this many chunks of the focus are drawn.")]
        [SerializeField] int loadRadius = 2;
        [Tooltip("Drawn chunks farther than this are cleared. Larger than loadRadius to avoid thrashing at edges.")]
        [SerializeField] int unloadRadius = 4;

        readonly HashSet<Int2> _shown = new HashSet<Int2>();
        readonly List<Int2> _toHide = new List<Int2>();
        readonly Vector3Int[] _positions = new Vector3Int[Chunk.Area];
        readonly TileBase[] _terrainTiles = new TileBase[Chunk.Area];
        readonly TileBase[] _oreTiles = new TileBase[Chunk.Area];
        readonly TileBase[][] _blendTiles = CreateBlendBuffers();
        World _world;

        void LateUpdate()
        {
            var world = bootstrap.Sim?.World;
            if (world != _world)
            {
                // New game, resync, or back to menu: everything drawn belongs to the old world.
                ClearAll();
                if (_world != null) _world.TileChanged -= OnTileChanged;
                _world = world;
                if (_world != null) _world.TileChanged += OnTileChanged;
            }
            if (world == null) return;
            Vector3 p = focus.position;
            Int2 center = World.TileToChunk(new Int2(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)));

            for (int dy = -loadRadius; dy <= loadRadius; dy++)
            for (int dx = -loadRadius; dx <= loadRadius; dx++)
            {
                var coord = new Int2(center.X + dx, center.Y + dy);
                if (_shown.Add(coord)) Show(world.GetChunk(coord));
            }

            _toHide.Clear();
            foreach (var coord in _shown)
            {
                if (Mathf.Abs(coord.X - center.X) > unloadRadius || Mathf.Abs(coord.Y - center.Y) > unloadRadius)
                    _toHide.Add(coord);
            }
            foreach (var coord in _toHide)
            {
                _shown.Remove(coord);
                Hide(coord);
            }
        }

        void OnTileChanged(Int2 tile)
        {
            if (!_shown.Contains(World.TileToChunk(tile))) return;
            var pos = new Vector3Int(tile.X, tile.Y, 0);
            terrainMap.SetTile(pos, GameArt.TerrainTile(_world.GetTerrain(tile), tile));
            oreMap.SetTile(pos, GameArt.OreTile(_world.GetOre(tile, out _), tile));
        }

        void ClearAll()
        {
            terrainMap.ClearAllTiles();
            oreMap.ClearAllTiles();
            if (blendMaps != null)
                foreach (var map in blendMaps)
                    if (map != null) map.ClearAllTiles();
            _shown.Clear();
        }

        void Show(Chunk chunk)
        {
            FillPositions(chunk.Coord);
            Int2 origin = chunk.Origin;
            for (int i = 0; i < Chunk.Area; i++)
            {
                var at = new Int2(origin.X + i % Chunk.Size, origin.Y + i / Chunk.Size);
                _terrainTiles[i] = GameArt.TerrainTile(chunk.Terrain[i], at);
                _oreTiles[i] = GameArt.OreTile(chunk.Ore[i], at);
            }
            terrainMap.SetTiles(_positions, _terrainTiles);
            oreMap.SetTiles(_positions, _oreTiles);
            ShowBlends(chunk);
        }

        static TileBase[][] CreateBlendBuffers()
        {
            var buffers = new TileBase[TerrainBlend.Order.Length][];
            for (int i = 0; i < buffers.Length; i++) buffers[i] = new TileBase[Chunk.Area];
            return buffers;
        }

        /// <summary>For each tile, overlays every higher-priority terrain that touches it.</summary>
        void ShowBlends(Chunk chunk)
        {
            if (blendMaps == null || blendMaps.Length < TerrainBlend.Order.Length) return;
            Int2 origin = chunk.Origin;
            for (int layer = 1; layer < TerrainBlend.Order.Length; layer++)
                System.Array.Clear(_blendTiles[layer], 0, Chunk.Area);

            for (int i = 0; i < Chunk.Area; i++)
            {
                var at = new Int2(origin.X + i % Chunk.Size, origin.Y + i / Chunk.Size);
                int own = TerrainBlend.Priority(chunk.Terrain[i]);
                for (int layer = own + 1; layer < TerrainBlend.Order.Length; layer++)
                {
                    var over = TerrainBlend.Order[layer];
                    _blendTiles[layer][i] = TerrainBlend.Tile(over, TerrainBlend.Mask(_world, at, over));
                }
            }
            for (int layer = 1; layer < TerrainBlend.Order.Length; layer++)
                blendMaps[layer].SetTiles(_positions, _blendTiles[layer]);
        }

        void Hide(Int2 coord)
        {
            FillPositions(coord);
            System.Array.Clear(_terrainTiles, 0, Chunk.Area);
            terrainMap.SetTiles(_positions, _terrainTiles);
            oreMap.SetTiles(_positions, _terrainTiles);
            if (blendMaps != null)
                foreach (var map in blendMaps)
                    if (map != null) map.SetTiles(_positions, _terrainTiles);
        }

        void FillPositions(Int2 coord)
        {
            int ox = coord.X * Chunk.Size;
            int oy = coord.Y * Chunk.Size;
            for (int y = 0; y < Chunk.Size; y++)
            for (int x = 0; x < Chunk.Size; x++)
                _positions[Chunk.Index(x, y)] = new Vector3Int(ox + x, oy + y, 0);
        }
    }
}
