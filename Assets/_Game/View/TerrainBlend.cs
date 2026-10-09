using System.Collections.Generic;
using Cogworks.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Cogworks.View
{
    /// <summary>
    /// Soft borders between terrain types. Where a tile touches higher-priority terrain, an overlay of that
    /// terrain creeps in from the touching sides and corners with a ragged edge, so grass spills over dirt,
    /// dirt over sand and sand into water instead of meeting in hard squares. Overlays are built at runtime
    /// from the authored terrain art and cached per (terrain, neighbour mask).
    /// </summary>
    public static class TerrainBlend
    {
        /// <summary>Bottom to top. Each terrain can overlay the ones before it.</summary>
        public static readonly TerrainType[] Order =
        {
            TerrainType.Water, TerrainType.Sand, TerrainType.Dirt, TerrainType.Rock, TerrainType.Grass,
        };

        // Neighbour bits: edges then corners.
        const int N = 1, E = 2, S = 4, W = 8, NE = 16, SE = 32, SW = 64, NW = 128;

        static readonly Dictionary<(TerrainType, int), TileBase> Cache = new Dictionary<(TerrainType, int), TileBase>();
        static readonly Dictionary<TerrainType, Color32[]> Source = new Dictionary<TerrainType, Color32[]>();

        public static int Priority(TerrainType t) => System.Array.IndexOf(Order, t);

        /// <summary>
        /// 8-neighbour mask of where <paramref name="over"/> touches the tile. Corners only count when neither
        /// adjacent edge is set (the edges already cover them).
        /// </summary>
        public static int Mask(World world, Int2 at, TerrainType over)
        {
            int m = 0;
            if (world.GetTerrain(new Int2(at.X, at.Y + 1)) == over) m |= N;
            if (world.GetTerrain(new Int2(at.X + 1, at.Y)) == over) m |= E;
            if (world.GetTerrain(new Int2(at.X, at.Y - 1)) == over) m |= S;
            if (world.GetTerrain(new Int2(at.X - 1, at.Y)) == over) m |= W;
            if ((m & (N | E)) == 0 && world.GetTerrain(new Int2(at.X + 1, at.Y + 1)) == over) m |= NE;
            if ((m & (S | E)) == 0 && world.GetTerrain(new Int2(at.X + 1, at.Y - 1)) == over) m |= SE;
            if ((m & (S | W)) == 0 && world.GetTerrain(new Int2(at.X - 1, at.Y - 1)) == over) m |= SW;
            if ((m & (N | W)) == 0 && world.GetTerrain(new Int2(at.X - 1, at.Y + 1)) == over) m |= NW;
            return m;
        }

        public static TileBase Tile(TerrainType over, int mask)
        {
            if (mask == 0) return null;
            if (Cache.TryGetValue((over, mask), out var tile)) return tile;
            var src = SourcePixels(over);
            tile = src == null ? null : Build(over, mask, src);
            Cache[(over, mask)] = tile;
            return tile;
        }

        static Color32[] SourcePixels(TerrainType t)
        {
            if (Source.TryGetValue(t, out var px)) return px;
            var sprite = ArtLibrary.First("terrain_" + t.ToString().ToLowerInvariant());
            px = sprite != null && sprite.texture.isReadable ? sprite.texture.GetPixels32() : null;
            Source[t] = px;
            return px;
        }

        static TileBase Build(TerrainType over, int mask, Color32[] src)
        {
            const int size = GameArt.PixelsPerUnit;
            var covered = new bool[size * size];
            uint salt = (uint)over * 7919u;
            // Texture rows run bottom-up, so y = 0 is the south edge.
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool c = false;
                if ((mask & N) != 0) c |= size - 1 - y < Depth(x, salt + 1);
                if ((mask & S) != 0) c |= y < Depth(x, salt + 2);
                if ((mask & E) != 0) c |= size - 1 - x < Depth(y, salt + 3);
                if ((mask & W) != 0) c |= x < Depth(y, salt + 4);
                if ((mask & NE) != 0) c |= Corner(size - 1 - x, size - 1 - y, salt + 5);
                if ((mask & SE) != 0) c |= Corner(size - 1 - x, y, salt + 6);
                if ((mask & SW) != 0) c |= Corner(x, y, salt + 7);
                if ((mask & NW) != 0) c |= Corner(x, size - 1 - y, salt + 8);
                covered[y * size + x] = c;
            }

            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                if (!covered[i]) continue;
                var col = src[i];
                // Darken the ragged rim so the overlap reads as a slight lip.
                bool rim = !Covered(covered, x + 1, y) || !Covered(covered, x - 1, y)
                    || !Covered(covered, x, y + 1) || !Covered(covered, x, y - 1);
                px[i] = rim ? Shade(col, over == TerrainType.Sand ? 18 : -22) : col;
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"blend_{over}_{mask}",
            };
            tex.SetPixels32(px);
            tex.Apply();
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.None;
            return tile;
        }

        static bool Covered(bool[] covered, int x, int y)
        {
            const int size = GameArt.PixelsPerUnit;
            // Outside the tile counts as covered so tile edges don't get a rim (the neighbour continues it).
            if (x < 0 || y < 0 || x >= size || y >= size) return true;
            return covered[y * size + x];
        }

        /// <summary>Ragged fringe depth along an edge: 3 to 6 pixels, wandering smoothly with position.</summary>
        static int Depth(int along, uint salt)
        {
            // Interpolate between random heights every 4 pixels, then add a little per-pixel jitter.
            int a = (int)(Noise.Hash(along / 4, 0, salt) % 4);
            int b = (int)(Noise.Hash(along / 4 + 1, 0, salt) % 4);
            int t = along % 4;
            int jitter = Noise.Hash(along, 1, salt) % 5 == 0 ? 1 : 0;
            return 3 + (a * (4 - t) + b * t) / 4 + jitter;
        }

        static bool Corner(int dx, int dy, uint salt)
        {
            float r = 4f + Noise.Hash(dx, dy, salt) % 3 * 0.5f;
            return dx * dx + dy * dy < r * r;
        }

        static Color32 Shade(Color32 c, int d) => new Color32(
            (byte)Mathf.Clamp(c.r + d, 0, 255), (byte)Mathf.Clamp(c.g + d, 0, 255), (byte)Mathf.Clamp(c.b + d, 0, 255), 255);
    }
}
