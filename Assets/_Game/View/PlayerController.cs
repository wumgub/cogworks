using Cogworks.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cogworks.View
{
    /// <summary>
    /// WASD movement against sim terrain and solid buildings, plus riding conveyor belts. Avatars live
    /// outside the deterministic sim and are synced as plain networked transforms.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] GameBootstrap bootstrap;
        [Tooltip("Tiles per second.")]
        [SerializeField] float speed = 6f;
        [Tooltip("Half-size of the collision box, in tiles.")]
        [SerializeField] float radius = 0.3f;
        [Tooltip("How strongly a belt pulls its rider toward its centre line (per second).")]
        [SerializeField] float beltCentering = 4f;

        /// <summary>Only the local player's avatar reads input; remote avatars are moved by the network.</summary>
        public bool IsLocal { get; set; } = true;

        /// <summary>False while a menu has focus. Applies to every avatar on this machine.</summary>
        public static bool InputEnabled { get; set; } = true;

        void Awake()
        {
            GetComponent<SpriteRenderer>().sprite = GameArt.PlayerSprite();
            if (bootstrap == null) bootstrap = GameBootstrap.Instance;
        }

        void Update()
        {
            if (!IsLocal || bootstrap.Sim == null) return;

            Vector2 walk = InputEnabled ? ReadInput() * speed : Vector2.zero;
            // Belts keep carrying you while a menu is open (multiplayer doesn't pause), but not when paused.
            Vector2 belt = bootstrap.Paused ? Vector2.zero : BeltVelocity();
            Vector2 delta = (walk + belt) * Time.deltaTime;
            if (delta == Vector2.zero) return;

            // Move each axis separately so the player slides along shorelines instead of sticking.
            TryMove(new Vector2(delta.x, 0f));
            TryMove(new Vector2(0f, delta.y));
        }

        static Vector2 ReadInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            var input = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            return input.normalized;
        }

        /// <summary>
        /// Velocity from the belt under the player's centre: its item speed along its direction, plus a pull
        /// toward the belt's centre line so riders stay on track and follow corners.
        /// </summary>
        Vector2 BeltVelocity()
        {
            Vector2 p = transform.position;
            var tile = new Int2(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
            if (!(bootstrap.Sim.World.Buildings.GetAt(tile)?.Logic is ConveyorLogic belt)) return Vector2.zero;

            Int2 o = belt.Building.Direction.ToOffset();
            var dir = new Vector2(o.X, o.Y);
            float tilesPerSecond = belt.Speed * Simulation.TicksPerSecond / (float)ConveyorLogic.Length;
            Vector2 toCentre = new Vector2(tile.X + 0.5f, tile.Y + 0.5f) - p;
            Vector2 sideways = toCentre - dir * Vector2.Dot(toCentre, dir);
            return dir * tilesPerSecond + sideways * beltCentering;
        }

        void TryMove(Vector2 delta)
        {
            Vector2 current = transform.position;
            Vector2 next = current + delta;
            if (CanOccupy(current, next)) transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        /// <summary>True if the player's tile bounds overlap the given footprint (used to block placement on yourself).</summary>
        public bool Overlaps(Int2 origin, Int2 size)
        {
            RectInt r = TileBounds(transform.position);
            return r.xMin < origin.X + size.X && origin.X < r.xMax
                && r.yMin < origin.Y + size.Y && origin.Y < r.yMax;
        }

        // Blocks entering water or solid buildings. Tiles the player already overlaps are exempt, so someone
        // who gets built over (e.g. by another player) can still walk out.
        bool CanOccupy(Vector2 from, Vector2 to)
        {
            RectInt current = TileBounds(from);
            RectInt next = TileBounds(to);
            for (int y = next.yMin; y < next.yMax; y++)
            for (int x = next.xMin; x < next.xMax; x++)
            {
                if (current.Contains(new Vector2Int(x, y))) continue;
                if (IsBlocked(new Int2(x, y))) return false;
            }
            return true;
        }

        RectInt TileBounds(Vector2 p)
        {
            int x0 = Mathf.FloorToInt(p.x - radius);
            int x1 = Mathf.FloorToInt(p.x + radius);
            int y0 = Mathf.FloorToInt(p.y - radius);
            int y1 = Mathf.FloorToInt(p.y + radius);
            return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        bool IsBlocked(Int2 tile)
        {
            var world = bootstrap.Sim.World;
            if (!TerrainInfo.IsWalkable(world.GetTerrain(tile))) return true;
            var building = world.Buildings.GetAt(tile);
            return building != null && building.Spec.Solid;
        }
    }
}
