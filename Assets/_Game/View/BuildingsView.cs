using System.Collections.Generic;
using Cogworks.Sim;
using UnityEngine;

namespace Cogworks.View
{
    /// <summary>
    /// Mirrors sim buildings as animated sprites. Machines animate only while actually working (powered
    /// machines at a speed proportional to their network's RPM), inserter arms swing with their hand,
    /// and badges flag machines with no rotation or an overstressed network.
    /// </summary>
    public sealed class BuildingsView : MonoBehaviour
    {
        public const int SortingOrder = 10;
        const int ArmSortingOrder = SortingOrder + 1;
        const int BadgeSortingOrder = SortingOrder + 3;
        const float BaseRpm = 32f;

        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] Camera worldCamera;

        sealed class View
        {
            public Building Building;
            public SpriteRenderer Body;
            public Sprite[] Frames;
            public float Fps;
            public Transform Arm;
            public SpriteRenderer Badge;
        }

        readonly Dictionary<int, View> _views = new Dictionary<int, View>();
        BuildingManager _buildings;
        Simulation _sim;
        Sprite _noPower, _overstress;

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
            foreach (var v in _views.Values) DestroyView(v);
            _views.Clear();

            _sim = sim;
            _buildings = sim?.World.Buildings;
            if (_buildings == null) return;
            _noPower ??= GameArt.StatusBadge("nopower");
            _overstress ??= GameArt.StatusBadge("overstress");
            _buildings.Placed += OnPlaced;
            _buildings.Removed += OnRemoved;
            _buildings.Changed += OnChanged;
            foreach (var b in _buildings.All) OnPlaced(b);
        }

        void OnPlaced(Building b)
        {
            var go = new GameObject($"{b.Spec.Key}#{b.Id}");
            go.transform.SetParent(transform, false);
            var view = new View
            {
                Building = b,
                Body = go.AddComponent<SpriteRenderer>(),
                Frames = GameArt.BuildingFrames(b.Spec),
                Fps = GameArt.BuildingFps(b.Spec),
            };
            view.Body.sprite = view.Frames[0];
            view.Body.sortingOrder = SortingOrder;
            Pose(go.transform, b.Spec, b.Origin, b.Direction);

            if (b.Spec.Kind == BuildingKind.Inserter && GameArt.InserterArm() is Sprite arm)
            {
                var armGo = new GameObject("Arm");
                armGo.transform.SetParent(go.transform, false);
                var sr = armGo.AddComponent<SpriteRenderer>();
                sr.sprite = arm;
                sr.sortingOrder = ArmSortingOrder;
                view.Arm = armGo.transform;
            }

            bool machine = b.Spec.Kinetic == KineticRole.Consumer || b.Spec.Kinetic == KineticRole.Generator;
            if (machine && _noPower != null)
            {
                // Separate object so the badge stays upright in the footprint's top-right corner.
                var badge = new GameObject("Badge").AddComponent<SpriteRenderer>();
                badge.transform.SetParent(transform, false);
                Int2 size = b.Size;
                badge.transform.localPosition = new Vector3(b.Origin.X + size.X - 0.3f, b.Origin.Y + size.Y - 0.3f, 0f);
                badge.transform.localScale = Vector3.one * 0.6f;
                badge.sortingOrder = BadgeSortingOrder;
                badge.enabled = false;
                view.Badge = badge;
            }
            _views.Add(b.Id, view);
        }

        static void DestroyView(View v)
        {
            Destroy(v.Body.gameObject);
            if (v.Badge != null) Destroy(v.Badge.gameObject);
        }

        void OnRemoved(Building b)
        {
            if (_views.Remove(b.Id, out var v)) DestroyView(v);
        }

        void OnChanged(Building b)
        {
            if (_views.TryGetValue(b.Id, out var v)) Pose(v.Body.transform, b.Spec, b.Origin, b.Direction);
        }

        void LateUpdate()
        {
            if (_sim == null) return;
            var world = _sim.World;
            var kinetics = world.Kinetics;
            Rect view = ViewRect(4f);
            float t = Time.time;
            bool pulse = (t * 2f) % 1f < 0.6f;

            foreach (var v in _views.Values)
            {
                var b = v.Building;
                if (!view.Contains(new Vector2(b.Origin.X, b.Origin.Y))) continue;

                // How fast this building animates right now (0 = idle frame).
                float speed = Activity(b, kinetics);
                if (v.Frames.Length > 1)
                {
                    if (b.Spec.Kind == BuildingKind.Furnace)
                    {
                        // Frame 0 is unlit; 1..N flicker while smelting.
                        v.Body.sprite = speed > 0f ? v.Frames[1 + (int)(t * v.Fps) % (v.Frames.Length - 1)] : v.Frames[0];
                    }
                    else if (speed > 0f)
                    {
                        v.Body.sprite = v.Frames[(int)(t * v.Fps * speed) % v.Frames.Length];
                    }
                }

                if (v.Arm != null)
                    v.Arm.localEulerAngles = new Vector3(0f, 0f, 180f * (1f - ((InserterLogic)b.Logic).ArmPosition));

                if (v.Badge != null)
                {
                    var net = kinetics.NetworkOf(b);
                    bool over = net != null && net.Overstressed;
                    bool unpowered = b.Spec.Kinetic == KineticRole.Consumer && !over && (net == null || net.Rpm == 0);
                    v.Badge.enabled = (over || unpowered) && pulse;
                    v.Badge.sprite = over ? _overstress : _noPower;
                }
            }
        }

        /// <summary>Animation speed multiplier: 0 when idle, 1 at base speed, scaled by RPM for powered parts.</summary>
        static float Activity(Building b, World.KineticView kinetics)
        {
            switch (b.Logic)
            {
                case ConveyorLogic _:
                    return 1f;
                case DrillLogic d:
                    return d.Output == ItemCatalog.None && !d.OutOfOre ? d.LastRpm / BaseRpm : 0f;
                case AssemblerLogic a:
                    return a.Crafting ? a.LastRpm / BaseRpm : 0f;
                case FurnaceLogic f:
                    return f.Progress > 0 ? 1f : 0f;
                case LabLogic l:
                    return l.Working != ItemCatalog.None ? 1f : 0f;
                default:
                    if (b.Spec.Kinetic == KineticRole.None) return 0f;
                    return kinetics.RpmAt(b) / BaseRpm;
            }
        }

        Rect ViewRect(float margin)
        {
            var cam = worldCamera != null ? worldCamera : Camera.main;
            float h = cam.orthographicSize;
            float w = h * cam.aspect;
            Vector3 c = cam.transform.position;
            return new Rect(c.x - w - margin, c.y - h - margin, (w + margin) * 2f, (h + margin) * 2f);
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
