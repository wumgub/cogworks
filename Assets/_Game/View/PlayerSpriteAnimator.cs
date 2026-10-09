using UnityEngine;

namespace Cogworks.View
{
    /// <summary>
    /// Four-direction walk cycle driven by how the avatar actually moved this frame, so it works the same
    /// for the local player and for remote players moved by the network.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerSpriteAnimator : MonoBehaviour
    {
        const float WalkFps = 8f;
        // Below this speed (tiles/s) the avatar counts as standing still; filters network jitter.
        const float MoveThreshold = 0.5f;

        SpriteRenderer _renderer;
        Vector3 _last;
        string _facing = "down";
        float _walkTime;
        int _slot;

        public int Slot
        {
            get => _slot;
            set
            {
                _slot = value;
                Apply(moving: false);
            }
        }

        void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _last = transform.position;
        }

        void LateUpdate()
        {
            Vector3 delta = transform.position - _last;
            _last = transform.position;
            float speed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
            bool moving = speed > MoveThreshold;
            if (moving)
            {
                _facing = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                    ? (delta.x > 0f ? "right" : "left")
                    : (delta.y > 0f ? "up" : "down");
                _walkTime += Time.deltaTime;
            }
            Apply(moving);
        }

        void Apply(bool moving)
        {
            var frames = GameArt.PlayerFrames(_slot, _facing);
            if (frames == null || _renderer == null) return;
            // Frames: 0 idle, 1 and 2 alternate steps.
            _renderer.sprite = moving ? frames[1 + (int)(_walkTime * WalkFps) % 2] : frames[0];
        }
    }
}
