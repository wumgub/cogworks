using System;
using System.Collections.Generic;
using Cogworks.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cogworks.View
{
    /// <summary>
    /// World input: hotbar tools, placement preview, rotate, drag-place and drag-remove, plus (with no tool)
    /// clicking buildings, hand-mining ore, cranking and pipette. Never touches the world directly; it only
    /// reads it for previews and submits commands.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class BuildController : MonoBehaviour
    {
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] Camera worldCamera;
        [SerializeField] HotbarView hotbar;
        [SerializeField] PlayerController player;

        /// <summary>Buildings on number keys 1-9, 0. Everything else is picked from the inventory.</summary>
        public static readonly BuildingSpec[] HotbarSlots =
        {
            BuildingCatalog.Conveyor, BuildingCatalog.Inserter, BuildingCatalog.Chest, BuildingCatalog.Drill,
            BuildingCatalog.Furnace, BuildingCatalog.Assembler, BuildingCatalog.Shaft, BuildingCatalog.Gearbox,
            BuildingCatalog.HandCrank, BuildingCatalog.WaterWheel,
        };

        static readonly Color GhostValid = new Color(0.55f, 1f, 0.55f, 0.65f);
        static readonly Color GhostInvalid = new Color(1f, 0.35f, 0.35f, 0.65f);
        const int GhostSortingOrder = 30;
        // Client-side pacing for hand mining; the sim enforces its own cooldown.
        const float MineInterval = 0.35f;

        SpriteRenderer _ghost;
        bool _placing;
        Int2? _lastPlaceTile;
        // Last piece this drag submitted. It may not have run yet, so it can't be looked up in the world.
        Int2? _lastPlacedAt;
        Direction _lastPlacedDir;
        bool _removing;
        Int2? _lastRemoveTile;
        bool _mining;
        float _nextMineTime;
        float _nextOutOfItemMessage;

        public BuildingSpec Selected { get; private set; }
        public Direction Direction { get; private set; } = Direction.North;

        /// <summary>Building under the cursor (null if none or over UI).</summary>
        public Building Hovered { get; private set; }

        /// <summary>Tile under the cursor.</summary>
        public Int2 CursorTile { get; private set; }

        /// <summary>True while hand-mining is in progress.</summary>
        public bool IsMining => _mining;

        public event Action SelectionChanged;
        /// <summary>Raised when a building is clicked with no tool selected (opens its panel).</summary>
        public event Action<Building> BuildingClicked;
        /// <summary>Short feedback for the player (e.g. "Out of Conveyor Belt").</summary>
        public event Action<string> Message;

        /// <summary>False while a menu or panel has focus; world input is ignored and the ghost hidden.</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>Frame on which Esc was used to cancel a tool, so menus don't also open on it.</summary>
        public int EscapeConsumedFrame { get; private set; } = -1;

        /// <summary>The local avatar, used to stop you building on yourself. Set when it spawns.</summary>
        public void SetPlayer(PlayerController localPlayer) => player = localPlayer;

        void Awake()
        {
            var ghostGo = new GameObject("BuildGhost");
            ghostGo.transform.SetParent(transform, false);
            _ghost = ghostGo.AddComponent<SpriteRenderer>();
            _ghost.sortingOrder = GhostSortingOrder;
            _ghost.enabled = false;
        }

        /// <summary>Selects a building tool; selecting the active one again (or null) clears it.</summary>
        public void Select(BuildingSpec spec)
        {
            Selected = spec == Selected ? null : spec;
            if (Selected != null && !Selected.Rotatable) Direction = Direction.North;
            SelectionChanged?.Invoke();
        }

        public void SelectSlot(int slot)
        {
            if (slot >= 0 && slot < HotbarSlots.Length) Select(HotbarSlots[slot]);
        }

        /// <summary>How many of this building the local player can place (int.MaxValue in sandbox).</summary>
        public int Available(BuildingSpec spec)
        {
            var sim = bootstrap.Sim;
            if (sim == null) return 0;
            if (sim.World.Sandbox) return int.MaxValue;
            return bootstrap.LocalPlayer?.Inventory.Count(spec.ItemId) ?? 0;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null || bootstrap.Sim == null || !InputEnabled)
            {
                _ghost.enabled = false;
                _placing = _removing = _mining = false;
                Hovered = null;
                return;
            }

            Vector2 screen = mouse.position.ReadValue();
            bool overUi = hotbar != null && hotbar.IsPointerOverUI(screen);
            Vector3 w = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
            var cursor = new Int2(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y));
            CursorTile = cursor;
            Hovered = overUi ? null : bootstrap.Sim.World.Buildings.GetTopAt(cursor);

            HandleKeys(kb, cursor);
            if (Selected != null) HandlePlacing(mouse, cursor, overUi);
            else HandleInteract(mouse, cursor, overUi);
            HandleRemoving(mouse, cursor, overUi);
            UpdateGhost(cursor, overUi);
        }

        void HandleKeys(Keyboard kb, Int2 cursor)
        {
            for (int i = 0; i < HotbarSlots.Length; i++)
            {
                var key = i == 9 ? Key.Digit0 : Key.Digit1 + i;
                if (kb[key].wasPressedThisFrame) SelectSlot(i);
            }

            if (kb.escapeKey.wasPressedThisFrame && Selected != null)
            {
                Select(null);
                EscapeConsumedFrame = Time.frameCount;
            }

            if (kb.qKey.wasPressedThisFrame)
            {
                // Q clears the tool, or with no tool picks up the hovered building's type (pipette).
                if (Selected != null) Select(null);
                else if (Hovered != null) Select(Hovered.Spec);
            }

            if (kb.eKey.wasPressedThisFrame && Hovered?.Spec == BuildingCatalog.HandCrank)
            {
                bootstrap.Commands.Submit(new CrankCommand(Hovered.Origin));
                SoundFx.Instance?.Play(Sfx.Crank);
            }

            if (kb.rKey.wasPressedThisFrame)
            {
                bool reverse = kb.shiftKey.isPressed;
                if (Selected != null)
                {
                    if (Selected.Rotatable) Direction = reverse ? Direction.RotateCcw() : Direction.RotateCw();
                }
                else if (Hovered != null && Hovered.Spec.Rotatable)
                {
                    var next = reverse ? Hovered.Direction.RotateCcw() : Hovered.Direction.RotateCw();
                    bootstrap.Commands.Submit(new SetBuildingDirectionCommand(cursor, next));
                }
            }
        }

        /// <summary>No tool: click a building to open it, hold on bare ore to mine it by hand.</summary>
        void HandleInteract(Mouse mouse, Int2 cursor, bool overUi)
        {
            _placing = false;
            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                if (Hovered != null) BuildingClicked?.Invoke(Hovered);
                else _mining = true;
            }
            if (!mouse.leftButton.isPressed) _mining = false;
            if (!_mining || Hovered != null) return;

            if (bootstrap.Sim.World.GetOre(cursor, out int amount) == Ore.None || amount <= 0) return;
            if (Time.time < _nextMineTime) return;
            _nextMineTime = Time.time + MineInterval;
            bootstrap.Commands.Submit(new MineTileCommand(cursor));
            SoundFx.Instance?.Play(Sfx.Mine, 0.6f);
        }

        void HandlePlacing(Mouse mouse, Int2 cursor, bool overUi)
        {
            _mining = false;
            var spec = Selected;
            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                _placing = true;
                _lastPlaceTile = null;
                _lastPlacedAt = null;
            }
            if (!mouse.leftButton.isPressed) _placing = false;
            if (!_placing || _lastPlaceTile == cursor) return;

            if (_lastPlaceTile is Int2 last && spec.Width == 1 && spec.Height == 1)
            {
                // Fill every tile between the last and current cursor so fast drags leave no gaps.
                foreach (var step in StepsBetween(last, cursor))
                {
                    if (spec.OrientOnDrag && DirectionExt.FromStep(step.to - step.from) is Direction d)
                    {
                        Direction = d;
                        // Turn the previous piece too, so a corner bends the line instead of leaving a stub.
                        // Commands run in submission order, so this lands after that piece's placement.
                        if (_lastPlacedAt == step.from && _lastPlacedDir != d)
                            bootstrap.Commands.Submit(new SetBuildingDirectionCommand(step.from, d));
                    }
                    TryPlace(spec, step.to);
                }
            }
            else
            {
                TryPlace(spec, cursor);
            }
            _lastPlaceTile = cursor;
        }

        void TryPlace(BuildingSpec spec, Int2 cursor)
        {
            Int2 origin = OriginFor(spec, cursor, Direction);
            // Pre-check to avoid flooding the sink; the command re-validates when it runs.
            if (!CanPlaceLocally(spec, origin)) return;
            if (Available(spec) <= 0)
            {
                if (Time.time >= _nextOutOfItemMessage)
                {
                    Message?.Invoke($"Out of {spec.DisplayName}. Craft more (Tab).");
                    SoundFx.Instance?.Play(Sfx.Error, 0.5f);
                    _nextOutOfItemMessage = Time.time + 2f;
                }
                return;
            }
            bootstrap.Commands.Submit(new PlaceBuildingCommand(spec.Id, origin, Direction));
            _lastPlacedAt = cursor;
            _lastPlacedDir = Direction;
        }

        void HandleRemoving(Mouse mouse, Int2 cursor, bool overUi)
        {
            if (mouse.rightButton.wasPressedThisFrame && !overUi)
            {
                _removing = true;
                _lastRemoveTile = null;
            }
            if (!mouse.rightButton.isPressed) _removing = false;
            if (!_removing || _lastRemoveTile == cursor) return;

            if (bootstrap.Sim.World.Buildings.GetTopAt(cursor) != null)
                bootstrap.Commands.Submit(new RemoveBuildingCommand(cursor));
            _lastRemoveTile = cursor;
        }

        void UpdateGhost(Int2 cursor, bool overUi)
        {
            var spec = Selected;
            _ghost.enabled = spec != null && !overUi;
            if (!_ghost.enabled) return;

            Int2 origin = OriginFor(spec, cursor, Direction);
            _ghost.sprite = GameArt.BuildingSprite(spec);
            _ghost.color = CanPlaceLocally(spec, origin) && Available(spec) > 0 ? GhostValid : GhostInvalid;
            BuildingsView.Pose(_ghost.transform, spec, origin, Direction);
        }

        /// <summary>
        /// Sim rules plus "not on top of yourself". Avatars aren't part of the deterministic sim, so the
        /// player check is client-side only; <see cref="PlayerController"/> lets you walk out if built over.
        /// </summary>
        bool CanPlaceLocally(BuildingSpec spec, Int2 origin)
        {
            if (!bootstrap.Sim.World.Buildings.CanPlace(spec, origin, Direction)) return false;
            return !spec.Solid || player == null || !player.Overlaps(origin, spec.SizeFor(Direction));
        }

        /// <summary>Centers the footprint on the cursor (bottom-left of center for even sizes).</summary>
        static Int2 OriginFor(BuildingSpec spec, Int2 cursor, Direction dir)
        {
            Int2 size = spec.SizeFor(dir);
            return new Int2(cursor.X - (size.X - 1) / 2, cursor.Y - (size.Y - 1) / 2);
        }

        /// <summary>Orthogonal unit steps from <paramref name="a"/> to <paramref name="b"/>, horizontal first.</summary>
        static IEnumerable<(Int2 from, Int2 to)> StepsBetween(Int2 a, Int2 b)
        {
            var p = a;
            while (p.X != b.X)
            {
                var next = new Int2(p.X + Math.Sign(b.X - p.X), p.Y);
                yield return (p, next);
                p = next;
            }
            while (p.Y != b.Y)
            {
                var next = new Int2(p.X, p.Y + Math.Sign(b.Y - p.Y));
                yield return (p, next);
                p = next;
            }
        }
    }
}
