using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Cogworks.View
{
    /// <summary>Follows a target and zooms in whole-pixel steps by changing the pixel-perfect reference resolution.</summary>
    [RequireComponent(typeof(PixelPerfectCamera))]
    public sealed class CameraRig : MonoBehaviour
    {
        // Smaller reference resolution = more zoomed in. All are 16:9 so pixels stay square and crisp.
        static readonly Vector2Int[] ZoomLevels =
        {
            new Vector2Int(320, 180),
            new Vector2Int(480, 270),
            new Vector2Int(640, 360),
            new Vector2Int(960, 540),
            new Vector2Int(1280, 720),
        };

        [SerializeField] Transform target;
        [SerializeField] int zoomIndex = 2;

        PixelPerfectCamera _pixelPerfect;

        void Awake()
        {
            _pixelPerfect = GetComponent<PixelPerfectCamera>();
            ApplyZoom();
        }

        void LateUpdate()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f)
                {
                    zoomIndex = Mathf.Clamp(zoomIndex + (scroll > 0f ? -1 : 1), 0, ZoomLevels.Length - 1);
                    ApplyZoom();
                }
            }

            if (target != null)
            {
                Vector3 p = target.position;
                transform.position = new Vector3(p.x, p.y, transform.position.z);
            }
        }

        public void SetTarget(Transform newTarget) => target = newTarget;

        void ApplyZoom()
        {
            _pixelPerfect.refResolutionX = ZoomLevels[zoomIndex].x;
            _pixelPerfect.refResolutionY = ZoomLevels[zoomIndex].y;
        }
    }
}
