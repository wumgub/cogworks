using System.Collections.Generic;
using Cogworks.Sim;
using UnityEngine;

namespace Cogworks.View
{
    /// <summary>
    /// Draws items riding belts and held by inserters, using a pool of sprite renderers. Only belts inside
    /// the camera view are drawn, so cost scales with what's on screen rather than factory size.
    /// </summary>
    public sealed class BeltItemsView : MonoBehaviour
    {
        const int SortingOrder = 12;
        const float ItemScale = 0.5f;

        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] Camera worldCamera;

        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        int _used;

        void LateUpdate()
        {
            _used = 0;
            var sim = bootstrap.Sim;
            if (sim != null)
            {
                Rect view = ViewRect(2f);
                foreach (var b in sim.World.Buildings.All)
                {
                    if (!view.Contains(new Vector2(b.Origin.X, b.Origin.Y))) continue;
                    if (b.Logic is ConveyorLogic belt) DrawBelt(belt);
                    else if (b.Logic is InserterLogic ins && ins.Held != ItemCatalog.None) DrawHeld(ins);
                }
            }
            for (int i = _used; i < _pool.Count; i++)
                if (_pool[i].enabled) _pool[i].enabled = false;
        }

        void DrawBelt(ConveyorLogic belt)
        {
            var b = belt.Building;
            Vector2 center = new Vector2(b.Origin.X + 0.5f, b.Origin.Y + 0.5f);
            Int2 off = b.Direction.ToOffset();
            Vector2 dir = new Vector2(off.X, off.Y);
            foreach (var item in belt.Items)
            {
                float along = item.Pos / (float)ConveyorLogic.Length - 0.5f;
                Draw(item.Item, center + dir * along);
            }
        }

        void DrawHeld(InserterLogic ins)
        {
            var b = ins.Building;
            Int2 back = b.BackTile(), front = b.FrontTile();
            // The arm reaches about halfway into each neighbouring tile.
            Vector2 from = Vector2.Lerp(Center(b.Origin), Center(back), 0.6f);
            Vector2 to = Vector2.Lerp(Center(b.Origin), Center(front), 0.6f);
            Draw(ins.Held, Vector2.Lerp(from, to, ins.ArmPosition));
        }

        static Vector2 Center(Int2 t) => new Vector2(t.X + 0.5f, t.Y + 0.5f);

        void Draw(ushort item, Vector2 at)
        {
            if (_used == _pool.Count)
            {
                var go = new GameObject("Item");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * ItemScale;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SortingOrder;
                _pool.Add(sr);
            }
            var r = _pool[_used++];
            r.sprite = GameArt.ItemSprite(item);
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            if (!r.enabled) r.enabled = true;
        }

        Rect ViewRect(float margin)
        {
            float h = worldCamera.orthographicSize;
            float w = h * worldCamera.aspect;
            Vector3 c = worldCamera.transform.position;
            return new Rect(c.x - w - margin, c.y - h - margin, (w + margin) * 2f, (h + margin) * 2f);
        }
    }
}
