using System;
using System.Collections.Generic;
using Cogworks.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cogworks.View
{
    /// <summary>
    /// Build-mode input: hotbar selection, placement preview, rotate, drag-place and drag-remove.
    /// Never touches the world directly; it only reads it for previews and submits commands.
    /// </summary>
    public sealed class BuildController : MonoBehaviour
    {
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] Camera worldCamera;
        [SerializeField] HotbarView hotbar;
        [SerializeField] PlayerController player;

        static readonly Color GhostValid = new Color(0.55f, 1f, 0.55f, 0.65f);
        static readonly Color GhostInvalid = new Color(1f, 0.35f, 0.35f, 0.65f);
        const int GhostSortingOrder = 20;

        SpriteRenderer _ghost;
        bool _placing;
        Int2? _lastPlaceTile;
        // Last piece this drag submitted. It may not have run yet, so it can't be looked up in the world.
        Int2? _lastPlacedAt;
        Direction _lastPlacedDir;
        bool _removing;
        Int2? _lastRemoveTile;

        public int SelectedIndex { get; private set; } = -1;
        public Direction Direction { get; private set; } = Direction.North;
        public BuildingSpec Selected => SelectedIndex >= 0 ? BuildingCatalog.All[SelectedIndex] : null;

        public event Action SelectionChanged;

        void Awake()
        {
            var ghostGo = new GameObject("BuildGhost");
            ghostGo.transform.SetParent(transform, false);
            _ghost = ghostGo.AddComponent<SpriteRenderer>();
            _ghost.sortingOrder = GhostSortingOrder;
            _ghost.enabled = false;
        }

        /// <summary>Selects a hotbar slot; selecting the active slot again clears it.</summary>
        public void Select(int index)
        {
            SelectedIndex = index == SelectedIndex || index < 0 || index >= BuildingCatalog.All.Count ? -1 : index;
            if (Selected != null && !Selected.Rotatable) Direction = Direction.North;
            SelectionChanged?.Invoke();
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            Vector2 screen = mouse.position.ReadValue();
            bool overUi = hotbar != null && hotbar.IsPointerOverUI(screen);
            Vector3 w = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
            var cursor = new Int2(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y));

            HandleKeys(kb, cursor);
            HandlePlacing(mouse, cursor, overUi);
            HandleRemoving(mouse, cursor, overUi);
            UpdateGhost(cursor, overUi);
        }

        void HandleKeys(Keyboard kb, Int2 cursor)
        {
            for (int i = 0; i < BuildingCatalog.All.Count && i < 9; i++)
                if (kb[Key.Digit1 + i].wasPressedThisFrame) Select(i);

            if (kb.qKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            {
                if (Selected != null) Select(-1);
            }

            if (kb.rKey.wasPressedThisFrame)
            {
                bool reverse = kb.shiftKey.isPressed;
                if (Selected != null)
                {
                    if (Selected.Rotatable) Direction = reverse ? Direction.RotateCcw() : Direction.RotateCw();
                }
                else
                {
                    // No tool selected: rotate the building under the cursor.
                    var b = bootstrap.Sim.World.Buildings.GetAt(cursor);
                    if (b != null && b.Spec.Rotatable)
                    {
                        var next = reverse ? b.Direction.RotateCcw() : b.Direction.RotateCw();
                        bootstrap.Commands.Submit(new SetBuildingDirectionCommand(cursor, next));
                    }
                }
            }
        }

        void HandlePlacing(Mouse mouse, Int2 cursor, bool overUi)
        {
            var spec = Selected;
            if (spec == null)
            {
                _placing = false;
                return;
            }

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

            if (bootstrap.Sim.World.Buildings.GetAt(cursor) != null)
                bootstrap.Commands.Submit(new RemoveBuildingCommand(cursor));
            _lastRemoveTile = cursor;
        }

        void UpdateGhost(Int2 cursor, bool overUi)
        {
            var spec = Selected;
            _ghost.enabled = spec != null && !overUi;
            if (!_ghost.enabled) return;

            Int2 origin = OriginFor(spec, cursor, Direction);
            _ghost.sprite = PlaceholderArt.BuildingSprite(spec);
            _ghost.color = CanPlaceLocally(spec, origin) ? GhostValid : GhostInvalid;
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
