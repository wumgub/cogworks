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

        // Cogs spin at the network's speed (6°/s per rpm, i.e. one turn per minute per rpm, sped up for visibility).
        const float CogDegreesPerRpmSecond = 6f * 4f;
        static readonly Color CogStopped = new Color(0.6f, 0.6f, 0.6f, 0.7f);
        static readonly Color CogOverstressed = new Color(1f, 0.3f, 0.25f, 0.95f);

        readonly Dictionary<int, Transform> _views = new Dictionary<int, Transform>();
        readonly Dictionary<int, SpriteRenderer> _cogs = new Dictionary<int, SpriteRenderer>();
        BuildingManager _buildings;
        Simulation _sim;

        void OnEnable()
        {
            bootstrap.SimChanged += Bind;
            Bind(bootstrap.Sim);
        }

        void OnDisable()
        {
            bootstrap.SimChanged -= Bind;
            Bind(null);
        }

        void Bind(Simulation sim)
        {
            if (_buildings != null)
            {
                _buildings.Placed -= OnPlaced;
                _buildings.Removed -= OnRemoved;
                _buildings.Changed -= OnChanged;
            }
            foreach (var t in _views.Values) Destroy(t.gameObject);
            foreach (var c in _cogs.Values) Destroy(c.gameObject);
            _views.Clear();
            _cogs.Clear();

            _sim = sim;
            _buildings = sim?.World.Buildings;
            if (_buildings == null) return;
            _buildings.Placed += OnPlaced;
            _buildings.Removed += OnRemoved;
            _buildings.Changed += OnChanged;
            foreach (var b in _buildings.All) OnPlaced(b);
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

            if (b.Spec.Kinetic != KineticRole.None && b.Spec.Kinetic != KineticRole.Shaft)
            {
                // Separate object so it doesn't inherit the building's rotation.
                var cog = new GameObject("Cog").AddComponent<SpriteRenderer>();
                cog.transform.SetParent(transform, false);
                cog.transform.localPosition = go.transform.localPosition;
                cog.transform.localScale = Vector3.one * (b.Spec.Width > 1 ? 0.75f : 0.55f);
                cog.sprite = PlaceholderArt.CogSprite();
                cog.sortingOrder = SortingOrder + 1;
                _cogs.Add(b.Id, cog);
            }
        }

        void LateUpdate()
        {
            if (_sim == null) return;
            var kinetics = _sim.World.Kinetics;
            float dt = Time.deltaTime;
            foreach (var pair in _cogs)
            {
                var b = _buildings.Get(pair.Key);
                var net = kinetics.NetworkOf(b);
                int rpm = net?.EffectiveRpm ?? 0;
                var cog = pair.Value;
                cog.color = net != null && net.Overstressed ? CogOverstressed : rpm == 0 ? CogStopped : Color.white;
                if (rpm != 0) cog.transform.Rotate(0f, 0f, -rpm * CogDegreesPerRpmSecond * dt);
            }
        }

        void OnRemoved(Building b)
        {
            if (_views.Remove(b.Id, out var t)) Destroy(t.gameObject);
            if (_cogs.Remove(b.Id, out var cog)) Destroy(cog.gameObject);
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
