using System.Collections.Generic;
using Cogworks.Sim;
using UnityEngine;

namespace Cogworks.View
{
    /// <summary>Item icons, building glyphs and small UI sprites, all generated as 16x16 pixel art.</summary>
    public static partial class GameArt
    {
        internal static readonly Color32 IronOreColor = new Color32(165, 185, 215, 255);
        internal static readonly Color32 CopperOreColor = new Color32(216, 122, 58, 255);
        internal static readonly Color32 CoalColor = new Color32(40, 40, 46, 255);
        internal static readonly Color32 StoneColor = new Color32(196, 176, 136, 255);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        static readonly Dictionary<ushort, Sprite> ItemSprites = new Dictionary<ushort, Sprite>();
        static Sprite _cog;
        static Sprite _pixel;

        /// <summary>Icon for any item. Building items reuse the building's art.</summary>
        public static Sprite ItemSprite(ushort itemId)
        {
            if (ItemSprites.TryGetValue(itemId, out var cached)) return cached;
            var spec = ItemCatalog.Get(itemId);
            Sprite sprite;
            if (spec?.Places != null) sprite = BuildingSprite(spec.Places);
            else if (spec != null && ArtLibrary.First("item_" + spec.Key) is Sprite authored) sprite = authored;
            else
            {
                var px = new Color32[PixelsPerUnit * PixelsPerUnit];
                DrawItem(px, spec?.Key ?? "");
                sprite = MakeSprite(px, spec?.Key ?? "unknown");
            }
            ItemSprites[itemId] = sprite;
            return sprite;
        }

        /// <summary>A cog drawn over kinetic buildings; rotated to show the network's speed.</summary>
        public static Sprite CogSprite()
        {
            if (_cog != null) return _cog;
            var px = new Color32[PixelsPerUnit * PixelsPerUnit];
            DrawGear(px, PixelsPerUnit, 8, 8, new Color32(235, 225, 200, 255), hole: true);
            _cog = MakeSprite(px, "Cog");
            return _cog;
        }

        /// <summary>1x1 white pixel for bars and tints.</summary>
        public static Sprite PixelSprite()
        {
            if (_pixel != null) return _pixel;
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            return _pixel;
        }

        static void DrawItem(Color32[] px, string key)
        {
            switch (key)
            {
                case "iron_ore": DrawNuggets(px, IronOreColor); break;
                case "copper_ore": DrawNuggets(px, CopperOreColor); break;
                case "coal": DrawNuggets(px, CoalColor, outline: new Color32(90, 90, 100, 255)); break;
                case "stone": DrawNuggets(px, StoneColor); break;
                case "iron_plate": DrawPlate(px, new Color32(185, 195, 210, 255)); break;
                case "copper_plate": DrawPlate(px, new Color32(220, 130, 70, 255)); break;
                case "stone_brick": DrawBricks(px); break;
                case "iron_gear": DrawGear(px, PixelsPerUnit, 8, 8, new Color32(175, 185, 200, 255), hole: true); break;
                case "copper_wire": DrawCoil(px); break;
                case "circuit": DrawCircuit(px); break;
                case "science_red": DrawFlask(px, new Color32(220, 60, 60, 255)); break;
                case "science_green": DrawFlask(px, new Color32(80, 200, 90, 255)); break;
                default: Fill(px, 4, 4, 8, 8, new Color32(255, 0, 255, 255)); break;
            }
        }

        // ------------------------------------------------------------ building glyphs

        /// <summary>Simple per-type marking so buildings are recognisable before real art exists.</summary>
        static void DrawGlyph(Color32[] px, int w, int h, BuildingSpec spec)
        {
            int cx = w / 2, cy = h / 2;
            var dark = new Color32(30, 30, 34, 255);
            switch (spec.Key)
            {
                case "furnace":
                    Fill(px, w, cx - 5, 4, 10, 8, dark, stride: w);
                    Fill(px, w, cx - 3, 5, 6, 3, new Color32(240, 140, 40, 255), stride: w);
                    break;
                case "drill":
                    DrawRing(px, w, cx, cy, 7, 5, new Color32(60, 66, 76, 255));
                    DrawRing(px, w, cx, cy, 3, 0, new Color32(200, 205, 215, 255));
                    break;
                case "assembler":
                    for (int i = 6; i < w - 6; i += 6)
                    {
                        Fill(px, w, i, 6, 1, h - 12, Shade(BuildingColor(spec), -40), stride: w);
                        Fill(px, w, 6, i, w - 12, 1, Shade(BuildingColor(spec), -40), stride: w);
                    }
                    break;
                case "chest":
                    Fill(px, w, 2, cy, w - 4, 1, dark, stride: w);
                    Fill(px, w, cx - 1, cy - 2, 2, 3, new Color32(230, 200, 90, 255), stride: w);
                    break;
                case "gearbox":
                    DrawGear(px, w, cx, cy, new Color32(220, 200, 150, 255), hole: false);
                    break;
                case "hand_crank":
                    DrawRing(px, w, cx, cy, 5, 3, new Color32(200, 170, 110, 255));
                    Fill(px, w, cx, cy, 5, 2, new Color32(230, 210, 160, 255), stride: w);
                    break;
                case "water_wheel":
                    DrawRing(px, w, cx, cy, 6, 5, new Color32(170, 130, 80, 255));
                    Fill(px, w, cx - 6, cy, 13, 1, new Color32(170, 130, 80, 255), stride: w);
                    Fill(px, w, cx, cy - 6, 1, 13, new Color32(170, 130, 80, 255), stride: w);
                    break;
                case "steam_engine":
                    Fill(px, w, w - 9, h - 12, 5, 9, dark, stride: w);
                    DrawRing(px, w, cx - 4, cy - 2, 7, 5, new Color32(150, 150, 160, 255));
                    break;
                case "lab":
                    DrawFlaskAt(px, w, cx - 8, cy - 8, new Color32(90, 160, 230, 255));
                    break;
                case "splitter":
                    // Three yellow pips: front, left, right outputs.
                    var pip = new Color32(240, 200, 60, 255);
                    Fill(px, w, cx - 1, h - 4, 2, 2, pip, stride: w);
                    Fill(px, w, 2, cy - 1, 2, 2, pip, stride: w);
                    Fill(px, w, w - 4, cy - 1, 2, 2, pip, stride: w);
                    break;
                case "underground_in":
                    // Dark tunnel mouth at the far (front) end.
                    Fill(px, w, 2, h - 6, w - 4, 4, new Color32(15, 15, 18, 255), stride: w);
                    break;
                case "underground_out":
                    // Tunnel mouth at the back end.
                    Fill(px, w, 2, 2, w - 4, 4, new Color32(15, 15, 18, 255), stride: w);
                    break;
            }
        }

        // ------------------------------------------------------------ primitives

        static void Set(Color32[] px, int stride, int x, int y, Color32 c)
        {
            int h = px.Length / stride;
            if (x < 0 || y < 0 || x >= stride || y >= h) return;
            px[y * stride + x] = c;
        }

        static void Fill(Color32[] px, int x, int y, int w, int h, Color32 c) => Fill(px, 0, x, y, w, h, c, PixelsPerUnit);

        static void Fill(Color32[] px, int unused, int x, int y, int w, int h, Color32 c, int stride)
        {
            for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                Set(px, stride, i, j, c);
        }

        static void DrawRing(Color32[] px, int stride, int cx, int cy, int outer, int inner, Color32 c)
        {
            for (int y = -outer; y <= outer; y++)
            for (int x = -outer; x <= outer; x++)
            {
                int d = x * x + y * y;
                if (d <= outer * outer && d >= inner * inner) Set(px, stride, cx + x, cy + y, c);
            }
        }

        static void DrawGear(Color32[] px, int stride, int cx, int cy, Color32 c, bool hole)
        {
            DrawRing(px, stride, cx, cy, 5, hole ? 2 : 0, c);
            var dark = Shade(c, -60);
            // Eight teeth around the rim.
            int[] tx = { 0, 4, 6, 4, 0, -4, -6, -4 };
            int[] ty = { 6, 4, 0, -4, -6, -4, 0, 4 };
            for (int i = 0; i < 8; i++)
            {
                Set(px, stride, cx + tx[i], cy + ty[i], c);
                Set(px, stride, cx + tx[i] + (tx[i] > 0 ? 1 : tx[i] < 0 ? -1 : 0), cy + ty[i] + (ty[i] > 0 ? 1 : ty[i] < 0 ? -1 : 0), dark);
            }
            if (hole) Set(px, stride, cx, cy, Clear);
        }

        static void DrawNuggets(Color32[] px, Color32 c, Color32? outline = null)
        {
            var edge = outline ?? Shade(c, -60);
            (int x, int y, int r)[] lumps = { (5, 6, 3), (10, 5, 2), (9, 10, 3), (4, 11, 2) };
            foreach (var (x, y, r) in lumps)
            {
                DrawRing(px, PixelsPerUnit, x, y, r + 1, r, edge);
                DrawRing(px, PixelsPerUnit, x, y, r, 0, c);
                Set(px, PixelsPerUnit, x - 1, y + 1, Shade(c, 40));
            }
        }

        static void DrawPlate(Color32[] px, Color32 c)
        {
            Fill(px, 2, 4, 12, 8, Shade(c, -60));
            Fill(px, 3, 5, 10, 6, c);
            Fill(px, 3, 10, 10, 1, Shade(c, 35));
        }

        static void DrawBricks(Color32[] px)
        {
            var brick = new Color32(170, 90, 70, 255);
            var mortar = new Color32(90, 60, 50, 255);
            Fill(px, 2, 3, 12, 10, mortar);
            Fill(px, 3, 4, 4, 2, brick); Fill(px, 8, 4, 5, 2, brick);
            Fill(px, 3, 7, 6, 2, brick); Fill(px, 10, 7, 3, 2, brick);
            Fill(px, 3, 10, 4, 2, brick); Fill(px, 8, 10, 5, 2, brick);
        }

        static void DrawCoil(Color32[] px)
        {
            var c = new Color32(225, 140, 70, 255);
            for (int i = 0; i < 4; i++) DrawRing(px, PixelsPerUnit, 4 + i * 3, 8, 3, 2, i % 2 == 0 ? c : Shade(c, -40));
        }

        static void DrawCircuit(Color32[] px)
        {
            Fill(px, 2, 3, 12, 10, new Color32(30, 90, 40, 255));
            Fill(px, 3, 4, 10, 8, new Color32(50, 140, 60, 255));
            Fill(px, 6, 6, 4, 4, new Color32(30, 30, 34, 255));
            var gold = new Color32(230, 200, 90, 255);
            for (int i = 4; i < 12; i += 2)
            {
                Set(px, PixelsPerUnit, i, 4, gold);
                Set(px, PixelsPerUnit, i, 11, gold);
            }
        }

        static void DrawFlask(Color32[] px, Color32 liquid) => DrawFlaskAt(px, PixelsPerUnit, 0, 0, liquid);

        static void DrawFlaskAt(Color32[] px, int stride, int ox, int oy, Color32 liquid)
        {
            var glass = new Color32(220, 230, 240, 255);
            Fill(px, 0, ox + 6, oy + 10, 4, 4, glass, stride);       // neck
            DrawRing(px, stride, ox + 8, oy + 6, 5, 4, glass);         // bulb outline
            DrawRing(px, stride, ox + 8, oy + 6, 4, 0, liquid);        // contents
            Set(px, stride, ox + 6, oy + 8, Shade(liquid, 60));
        }
    }
}
