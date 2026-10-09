using Cogworks.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cogworks.View
{
    /// <summary>
    /// WASD movement against sim terrain. Avatars live outside the deterministic sim and will be synced
    /// as plain networked transforms in milestone 3.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] GameBootstrap bootstrap;
        [Tooltip("Tiles per second.")]
        [SerializeField] float speed = 6f;
        [Tooltip("Half-size of the collision box, in tiles.")]
        [SerializeField] float radius = 0.3f;

        void Awake()
        {
            GetComponent<SpriteRenderer>().sprite = PlaceholderArt.PlayerSprite();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            var input = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            if (input == Vector2.zero) return;

            Vector2 delta = input.normalized * (speed * Time.deltaTime);
            // Move each axis separately so the player slides along shorelines instead of sticking.
            TryMove(new Vector2(delta.x, 0f));
            TryMove(new Vector2(0f, delta.y));
        }

        void TryMove(Vector2 delta)
        {
            Vector2 next = (Vector2)transform.position + delta;
            if (IsWalkable(next)) transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        bool IsWalkable(Vector2 p)
        {
            var world = bootstrap.Sim.World;
            return Walkable(world, p.x - radius, p.y - radius)
                && Walkable(world, p.x + radius, p.y - radius)
                && Walkable(world, p.x - radius, p.y + radius)
                && Walkable(world, p.x + radius, p.y + radius);
        }

        static bool Walkable(World world, float x, float y) =>
            TerrainInfo.IsWalkable(world.GetTerrain(new Int2(Mathf.FloorToInt(x), Mathf.FloorToInt(y))));
    }
}
