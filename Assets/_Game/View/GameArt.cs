using System.Collections.Generic;
using Cogworks.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Cogworks.View
{
    /// <summary>
    /// All game sprites. Uses the authored art in <see cref="ArtLibrary"/> (Resources/Art) and falls back
    /// to sprites generated here at runtime for anything the library lacks.
    /// </summary>
    public static partial class GameArt
    {
        public const int PixelsPerUnit = 16;

        // Fast Play mode (domain reload disabled) keeps statics between sessions, but Unity destroys
        // runtime-created tiles/textures when Play stops, so caches must start empty every session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            TerrainTiles.Clear();
            OreTiles.Clear();
            VariantTiles.Clear();
            BuildingFrameCache.Clear();
            BuildingSprites.Clear();
            System.Array.Clear(PlayerSprites, 0, PlayerSprites.Length);
            ResetItemArt();
        }

        static readonly Dictionary<TerrainType, Tile> TerrainTiles = new Dictionary<TerrainType, Tile>();
        static readonly Dictionary<Ore, Tile> OreTiles = new Dictionary<Ore, Tile>();
        static readonly Dictionary<(string, int), TileBase> VariantTiles = new Dictionary<(string, int), TileBase>();
        static readonly Dictionary<ushort, Sprite[]> BuildingFrameCache = new Dictionary<ushort, Sprite[]>();
        static readonly Dictionary<ushort, Sprite> BuildingSprites = new Dictionary<ushort, Sprite>();
        static readonly Sprite[] PlayerSprites = new Sprite[4];

        // One body color per player slot so co-op players can tell each other apart.
        static readonly Color32[] PlayerColors =
        {
            new Color32(230, 140, 50, 255),
            new Color32(70, 170, 230, 255),
            new Color32(120, 200, 90, 255),
            new Color32(200, 110, 210, 255),
        };

        /// <summary>Terrain tile for a world position; authored art picks a stable variant per tile, water animates.</summary>
        public static TileBase TerrainTile(TerrainType terrain, Int2 at)
        {
            string name = "terrain_" + terrain.ToString().ToLowerInvariant();
            var frames = ArtLibrary.Frames(name);
            if (frames == null) return TerrainTile(terrain);
            if (terrain == TerrainType.Water) return AnimatedTileFor(name, frames, ArtLibrary.Fps(name));
            return VariantTile(name, frames, at);
        }

        /// <summary>Ore overlay for a world position, or null for no ore.</summary>
        public static TileBase OreTile(Ore ore, Int2 at)
        {
            if (ore == Ore.None) return null;
            string name = "ore_" + ore.ToString().ToLowerInvariant();
            var frames = ArtLibrary.Frames(name);
            return frames == null ? OreTile(ore) : VariantTile(name, frames, at);
        }

        static TileBase VariantTile(string name, Sprite[] frames, Int2 at)
        {
            int v = (int)(Noise.Hash(at.X, at.Y, 0x7A11u) % (uint)frames.Length);
            if (!VariantTiles.TryGetValue((name, v), out var tile))
            {
                tile = MakeTile(frames[v]);
                VariantTiles[(name, v)] = tile;
            }
            return tile;
        }

        static TileBase AnimatedTileFor(string name, Sprite[] frames, int fps)
        {
            if (VariantTiles.TryGetValue((name, -1), out var cached)) return cached;
            var tile = ScriptableObject.CreateInstance<AnimatedTile>();
            tile.m_AnimatedSprites = frames;
            tile.m_MinSpeed = tile.m_MaxSpeed = Mathf.Max(1, fps);
            tile.m_TileColliderType = Tile.ColliderType.None;
            VariantTiles[(name, -1)] = tile;
            return tile;
        }

        public static Tile TerrainTile(TerrainType terrain)
        {
            if (!TerrainTiles.TryGetValue(terrain, out var tile))
            {
                tile = MakeTile(MakeTerrainSprite(terrain));
                TerrainTiles[terrain] = tile;
            }
            return tile;
        }

        public static Tile OreTile(Ore ore)
        {
            if (ore == Ore.None) return null;
            if (!OreTiles.TryGetValue(ore, out var tile))
            {
                tile = MakeTile(MakeOreSprite(ore));
                OreTiles[ore] = tile;
            }
            return tile;
        }

        /// <summary>Walk frames for a player slot facing "down", "up", "left" or "right" (null without authored art).</summary>
        public static Sprite[] PlayerFrames(int slot, string facing) =>
            ArtLibrary.Frames($"player_{Mathf.Abs(slot) % 4}_{facing}");

        public static Sprite PlayerSprite(int slot = 0)
        {
            var authored = PlayerFrames(slot, "down");
            if (authored != null) return authored[0];
            slot = Mathf.Abs(slot) % PlayerSprites.Length;
            if (PlayerSprites[slot] != null) return PlayerSprites[slot];

            var px = new Color32[PixelsPerUnit * PixelsPerUnit];
            var body = PlayerColors[slot];
            var outline = Shade(body, -110);
            var visor = new Color32(120, 210, 240, 255);
            for (int y = 1; y < 15; y++)
            for (int x = 3; x < 13; x++)
            {
                bool edge = x == 3 || x == 12 || y == 1 || y == 14;
                bool isVisor = y >= 9 && y <= 11 && x >= 5 && x <= 10;
                px[y * PixelsPerUnit + x] = edge ? outline : isVisor ? visor : body;
            }
            PlayerSprites[slot] = MakeSprite(px, "Player" + slot);
            return PlayerSprites[slot];
        }

        /// <summary>
        /// Sprite covering the building's unrotated (North-facing) footprint. Rotatable buildings get an
        /// arrow pointing up, so rotating the transform shows their facing.
        /// </summary>
        /// <summary>Animation frames for a building (a single frame if it doesn't animate).</summary>
        public static Sprite[] BuildingFrames(BuildingSpec spec)
        {
            if (BuildingFrameCache.TryGetValue(spec.Id, out var frames)) return frames;
            frames = ArtLibrary.Frames("building_" + spec.Key) ?? new[] { GeneratedBuildingSprite(spec) };
            BuildingFrameCache[spec.Id] = frames;
            return frames;
        }

        public static int BuildingFps(BuildingSpec spec) => ArtLibrary.Fps("building_" + spec.Key);

        /// <summary>The swinging arm drawn over inserters (null without authored art).</summary>
        public static Sprite InserterArm() => ArtLibrary.First("building_inserter_arm");

        /// <summary>"nopower" or "overstress" badge (null without authored art).</summary>
        public static Sprite StatusBadge(string kind) => ArtLibrary.First("building_status_" + kind);

        public static Sprite BuildingSprite(BuildingSpec spec) => BuildingFrames(spec)[0];

        static Sprite GeneratedBuildingSprite(BuildingSpec spec)
        {
            if (BuildingSprites.TryGetValue(spec.Id, out var cached)) return cached;

            int w = spec.Width * PixelsPerUnit;
            int h = spec.Height * PixelsPerUnit;
            Color32 body = BuildingColor(spec);
            Color32 border = Shade(body, -60);
            Color32 inner = Shade(body, 18);
            Color32 arrow = new Color32(250, 240, 210, 255);

            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
                bool bevel = x == 1 || y == h - 2;
                px[y * w + x] = edge ? border : bevel ? inner : body;
            }

            DrawGlyph(px, w, h, spec);
            if (spec.Rotatable)
            {
                // Chevron centered in the footprint, pointing North.
                int cx = w / 2;
                int top = h / 2 + 3;
                for (int row = 0; row < 4; row++)
                for (int t = 0; t < 2; t++)
                {
                    int y = top - row - t;
                    px[y * w + cx - 1 - row] = arrow;
                    px[y * w + cx + row] = arrow;
                }
            }

            var sprite = MakeSprite(px, w, h, spec.Key);
            BuildingSprites[spec.Id] = sprite;
            return sprite;
        }

        static Color32 BuildingColor(BuildingSpec spec) => spec.Key switch
        {
            "conveyor" => new Color32(70, 70, 78, 255),
            "inserter" => new Color32(200, 170, 50, 255),
            "chest" => new Color32(150, 100, 55, 255),
            "drill" => new Color32(95, 110, 130, 255),
            "furnace" => new Color32(140, 125, 110, 255),
            "assembler" => new Color32(80, 115, 150, 255),
            "shaft" => new Color32(165, 135, 90, 255),
            "hand_crank" => new Color32(120, 85, 60, 255),
            "gearbox" => new Color32(150, 120, 80, 255),
            "water_wheel" => new Color32(110, 80, 50, 255),
            "steam_engine" => new Color32(90, 90, 98, 255),
            "lab" => new Color32(200, 205, 215, 255),
            "fast_conveyor" => new Color32(160, 40, 40, 255),
            "splitter" => new Color32(70, 70, 78, 255),
            "underground_in" => new Color32(55, 55, 62, 255),
            "underground_out" => new Color32(55, 55, 62, 255),
            _ => new Color32(200, 0, 200, 255),
        };

        static Sprite MakeTerrainSprite(TerrainType terrain)
        {
            Color32 baseColor = terrain switch
            {
                TerrainType.Water => new Color32(59, 110, 165, 255),
                TerrainType.Sand => new Color32(216, 196, 138, 255),
                TerrainType.Grass => new Color32(90, 143, 62, 255),
                TerrainType.Dirt => new Color32(138, 106, 69, 255),
                _ => new Color32(110, 110, 114, 255),
            };

            var px = new Color32[PixelsPerUnit * PixelsPerUnit];
            for (int y = 0; y < PixelsPerUnit; y++)
            for (int x = 0; x < PixelsPerUnit; x++)
            {
                // Speckled shading so large areas don't look flat.
                int shade = (int)(Noise.Hash(x, y, (uint)terrain * 7919u) % 3) - 1;
                px[y * PixelsPerUnit + x] = Shade(baseColor, shade * 10);
            }
            return MakeSprite(px, terrain.ToString());
        }

        static Sprite MakeOreSprite(Ore ore)
        {
            Color32 color = ore switch
            {
                Ore.Iron => IronOreColor,
                Ore.Copper => CopperOreColor,
                Ore.Coal => CoalColor,
                _ => StoneColor,
            };

            var px = new Color32[PixelsPerUnit * PixelsPerUnit];
            uint salt = (uint)ore * 104729u;
            for (int n = 0; n < 6; n++)
            {
                // 2x2 nuggets with a dark lower-right edge for depth.
                int nx = 1 + (int)(Noise.Hash(n, 0, salt) % 12);
                int ny = 1 + (int)(Noise.Hash(n, 1, salt) % 12);
                px[ny * PixelsPerUnit + nx] = Shade(color, 25);
                px[ny * PixelsPerUnit + nx + 1] = color;
                px[(ny + 1) * PixelsPerUnit + nx] = color;
                px[(ny + 1) * PixelsPerUnit + nx + 1] = Shade(color, -35);
            }
            return MakeSprite(px, ore.ToString());
        }

        internal static Color32 Shade(Color32 c, int delta) => new Color32(
            (byte)Mathf.Clamp(c.r + delta, 0, 255),
            (byte)Mathf.Clamp(c.g + delta, 0, 255),
            (byte)Mathf.Clamp(c.b + delta, 0, 255),
            c.a);

        static Sprite MakeSprite(Color32[] pixels, string name) => MakeSprite(pixels, PixelsPerUnit, PixelsPerUnit, name);

        internal static Sprite MakeSprite(Color32[] pixels, int width, int height, string name)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.name = name;
            return sprite;
        }

        static Tile MakeTile(Sprite sprite)
        {
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            tile.name = sprite.name;
            return tile;
        }
    }
}
