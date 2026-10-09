using Cogworks.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cogworks.View
{
    /// <summary>
    /// Top-right minimap centred on the camera: terrain, ore, buildings, every player, and the current view
    /// outline. Redrawn a few times a second into a small texture; M cycles the zoom.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MinimapView : MonoBehaviour
    {
        const int Size = 128;          // texture pixels per side
        const float RefreshSeconds = 0.5f;
        static readonly int[] TilesPerPixel = { 1, 2, 4 };

        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] Camera worldCamera;

        static readonly Color32 Unknown = new Color32(20, 16, 14, 255);
        static readonly Color32 ViewOutline = new Color32(255, 240, 200, 255);
        static readonly Color32[] PlayerDots =
        {
            new Color32(255, 150, 60, 255), new Color32(90, 170, 255, 255),
            new Color32(120, 230, 110, 255), new Color32(220, 130, 255, 255),
        };

        Texture2D _texture;
        Color32[] _pixels;
        VisualElement _root, _image;
        Label _coords;
        float _nextRefresh;
        int _zoom;

        void Start()
        {
            var doc = GetComponent<UIDocument>().rootVisualElement;
            _root = doc.Q("minimap");
            _image = doc.Q("minimap-image");
            _coords = doc.Q<Label>("minimap-coords");
            _texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Minimap",
            };
            _pixels = new Color32[Size * Size];
            _image.style.backgroundImage = new StyleBackground(_texture);
        }

        void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        void Update()
        {
            var sim = bootstrap.Sim;
            _root.style.display = sim != null ? DisplayStyle.Flex : DisplayStyle.None;
            if (sim == null) return;

            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame && PlayerController.InputEnabled)
            {
                _zoom = (_zoom + 1) % TilesPerPixel.Length;
                _nextRefresh = 0f;
            }

            Vector3 c = worldCamera.transform.position;
            _coords.text = $"{Mathf.FloorToInt(c.x)}, {Mathf.FloorToInt(c.y)}   ·   M: zoom";
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            Redraw(sim.World, c);
        }

        void Redraw(World world, Vector3 centre)
        {
            int scale = TilesPerPixel[_zoom];
            int cx = Mathf.FloorToInt(centre.x), cy = Mathf.FloorToInt(centre.y);
            int half = Size / 2 * scale;

            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                var tile = new Int2(cx - half + px * scale, cy - half + py * scale);
                _pixels[py * Size + px] = TileColor(world, tile);
            }

            // Camera view outline.
            float h = worldCamera.orthographicSize, w = h * worldCamera.aspect;
            int x0 = Size / 2 - Mathf.RoundToInt(w / scale), x1 = Size / 2 + Mathf.RoundToInt(w / scale);
            int y0 = Size / 2 - Mathf.RoundToInt(h / scale), y1 = Size / 2 + Mathf.RoundToInt(h / scale);
            for (int x = x0; x <= x1; x++)
            {
                Plot(x, y0, ViewOutline);
                Plot(x, y1, ViewOutline);
            }
            for (int y = y0; y <= y1; y++)
            {
                Plot(x0, y, ViewOutline);
                Plot(x1, y, ViewOutline);
            }

            // Players as 3x3 dots in their slot colour.
            foreach (var avatar in FindObjectsByType<PlayerSpriteAnimator>(FindObjectsInactive.Exclude))
            {
                Vector3 p = avatar.transform.position;
                int dx = Mathf.FloorToInt((p.x - cx + half) / scale), dy = Mathf.FloorToInt((p.y - cy + half) / scale);
                var col = PlayerDots[Mathf.Abs(avatar.Slot) % PlayerDots.Length];
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                    Plot(dx + ox, dy + oy, ox == 0 && oy == 0 ? new Color32(255, 255, 255, 255) : col);
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
        }

        void Plot(int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < Size && y < Size) _pixels[y * Size + x] = c;
        }

        static Color32 TileColor(World world, Int2 tile)
        {
            var building = world.Buildings.GetTopAt(tile);
            if (building != null) return BuildingColor(building.Spec);
            var ore = world.GetOre(tile, out _);
            switch (ore)
            {
                case Ore.Iron: return new Color32(165, 185, 215, 255);
                case Ore.Copper: return new Color32(206, 112, 58, 255);
                case Ore.Coal: return new Color32(34, 32, 36, 255);
                case Ore.Stone: return new Color32(196, 180, 148, 255);
            }
            switch (world.GetTerrain(tile))
            {
                case TerrainType.Water: return new Color32(46, 90, 122, 255);
                case TerrainType.Sand: return new Color32(204, 176, 124, 255);
                case TerrainType.Grass: return new Color32(94, 126, 56, 255);
                case TerrainType.Dirt: return new Color32(110, 82, 58, 255);
                case TerrainType.Rock: return new Color32(112, 106, 100, 255);
                default: return Unknown;
            }
        }

        static Color32 BuildingColor(BuildingSpec spec)
        {
            switch (spec.Kind)
            {
                case BuildingKind.Conveyor:
                case BuildingKind.Splitter:
                case BuildingKind.UndergroundEntrance:
                case BuildingKind.UndergroundExit:
                    return new Color32(70, 62, 60, 255);
                case BuildingKind.Inserter:
                    return new Color32(230, 190, 80, 255);
                case BuildingKind.Chest:
                    return new Color32(160, 110, 62, 255);
                case BuildingKind.Shaft:
                case BuildingKind.Gearbox:
                case BuildingKind.Generator:
                    return new Color32(214, 160, 60, 255);
                case BuildingKind.Furnace:
                    return new Color32(236, 120, 50, 255);
                default:
                    return new Color32(176, 182, 190, 255);
            }
        }
    }
}
