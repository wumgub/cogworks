using System.Collections.Generic;
using Cogworks.Sim;
using UnityEngine;

namespace Cogworks.View
{
    /// <summary>Mirrors sim buildings as sprites. One GameObject per building is fine until belts arrive in bulk.</summary>
    public sealed class BuildingsView : MonoBehaviour
    {
        public const int SortingOrder = 5;

        [SerializeField] GameBootstrap bootstrap;

        readonly Dictionary<int, Transform> _views = new Dictionary<int, Transform>();
        BuildingManager _buildings;

        void Start()
        {
            _buildings = bootstrap.Sim.World.Buildings;
            _buildings.Placed += OnPlaced;
            _buildings.Removed += OnRemoved;
            _buildings.Changed += OnChanged;
            foreach (var b in _buildings.All) OnPlaced(b);
        }

        void OnDestroy()
        {
            if (_buildings == null) return;
            _buildings.Placed -= OnPlaced;
            _buildings.Removed -= OnRemoved;
            _buildings.Changed -= OnChanged;
        }

        void OnPlaced(Building b)
        {
            var go = new GameObject($"{b.Spec.Key}#{b.Id}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.BuildingSprite(b.Spec);
            sr.sortingOrder = SortingOrder;
            Pose(go.transform, b.Spec, b.Origin, b.Direction);
            _views.Add(b.Id, go.transform);
        }

        void OnRemoved(Building b)
        {
            if (_views.Remove(b.Id, out var t)) Destroy(t.gameObject);
        }

        void OnChanged(Building b)
        {
            if (_views.TryGetValue(b.Id, out var t)) Pose(t, b.Spec, b.Origin, b.Direction);
        }

        /// <summary>Positions a North-facing sprite over a footprint and rotates it to face <paramref name="dir"/>.</summary>
        public static void Pose(Transform t, BuildingSpec spec, Int2 origin, Direction dir)
        {
            Int2 size = spec.SizeFor(dir);
            t.localPosition = new Vector3(origin.X + size.X * 0.5f, origin.Y + size.Y * 0.5f, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, -90f * (int)dir);
        }
    }
}
