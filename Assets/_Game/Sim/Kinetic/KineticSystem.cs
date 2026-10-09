using System.Collections.Generic;

namespace Cogworks.Sim
{
    /// <summary>A connected group of shafts, gearboxes, generators and machines that turn together.</summary>
    public sealed class KineticNetwork
    {
        public readonly List<Building> Members = new List<Building>();
        public readonly List<Building> Generators = new List<Building>();
        public readonly List<Building> Consumers = new List<Building>();

        /// <summary>Speed of the fastest active generator.</summary>
        public int Rpm { get; internal set; }
        /// <summary>Sum of active generators' stress capacity (SU).</summary>
        public int Capacity { get; internal set; }
        /// <summary>Sum of consumers' stress impact × RPM (SU).</summary>
        public int Usage { get; internal set; }

        /// <summary>Usage exceeds capacity: the whole network stops.</summary>
        public bool Overstressed => Usage > Capacity;

        /// <summary>The speed machines actually run at.</summary>
        public int EffectiveRpm => Overstressed ? 0 : Rpm;
    }

    /// <summary>
    /// Create-style rotational power. Networks are rebuilt only when kinetic buildings change; every tick
    /// each network takes the fastest active generator's RPM, and stalls if its consumers need more
    /// stress than its generators supply.
    /// </summary>
    internal sealed class KineticSystem
    {
        readonly List<KineticNetwork> _networks = new List<KineticNetwork>();
        readonly Dictionary<int, KineticNetwork> _byBuilding = new Dictionary<int, KineticNetwork>();
        bool _dirty = true;

        public IReadOnlyList<KineticNetwork> Networks => _networks;

        public void MarkDirty() => _dirty = true;

        public KineticNetwork NetworkOf(Building b) => b != null && _byBuilding.TryGetValue(b.Id, out var n) ? n : null;

        public int RpmAt(Building b) => NetworkOf(b)?.EffectiveRpm ?? 0;

        public void Tick(World world, List<Building> buildingsById)
        {
            if (_dirty) Rebuild(world, buildingsById);
            foreach (var net in _networks)
            {
                bool hasLoad = net.Consumers.Count > 0;
                int rpm = 0, capacity = 0;
                foreach (var g in net.Generators)
                {
                    var gen = (GeneratorLogic)g.Logic;
                    gen.UpdateGenerator(hasLoad);
                    if (!gen.Active) continue;
                    if (g.Spec.GeneratorRpm > rpm) rpm = g.Spec.GeneratorRpm;
                    capacity += g.Spec.StressCapacity;
                }
                int usage = 0;
                foreach (var c in net.Consumers) usage += c.Spec.StressImpact * rpm;
                net.Rpm = rpm;
                net.Capacity = capacity;
                net.Usage = usage;
            }
        }

        /// <summary>Whether a building passes rotation through the given side of its footprint.</summary>
        static bool ConnectsThrough(Building b, Direction side)
        {
            if (b.Spec.Kinetic != KineticRole.Shaft) return true;
            bool vertical = b.Direction == Direction.North || b.Direction == Direction.South;
            bool sideVertical = side == Direction.North || side == Direction.South;
            return vertical == sideVertical;
        }

        void Rebuild(World world, List<Building> buildingsById)
        {
            _dirty = false;
            _networks.Clear();
            _byBuilding.Clear();

            var kinetic = new List<Building>();
            var index = new Dictionary<int, int>();
            foreach (var b in buildingsById)
            {
                if (b.Spec.Kinetic == KineticRole.None) continue;
                index[b.Id] = kinetic.Count;
                kinetic.Add(b);
            }

            var parent = new int[kinetic.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x)
            {
                while (parent[x] != x) x = parent[x] = parent[parent[x]];
                return x;
            }

            for (int i = 0; i < kinetic.Count; i++)
            {
                var b = kinetic[i];
                foreach (var (tile, side) in b.Neighbours())
                {
                    var other = world.Buildings.GetAt(tile);
                    if (other == null || other == b || !index.TryGetValue(other.Id, out int j)) continue;
                    if (!ConnectsThrough(b, side) || !ConnectsThrough(other, side.Opposite())) continue;
                    int a = Find(i), c = Find(j);
                    // Union toward the lower index so roots (and network order) are deterministic.
                    if (a < c) parent[c] = a;
                    else if (c < a) parent[a] = c;
                }
            }

            var byRoot = new Dictionary<int, KineticNetwork>();
            for (int i = 0; i < kinetic.Count; i++)
            {
                int root = Find(i);
                if (!byRoot.TryGetValue(root, out var net))
                {
                    net = new KineticNetwork();
                    byRoot[root] = net;
                    _networks.Add(net);
                }
                var b = kinetic[i];
                net.Members.Add(b);
                if (b.Spec.Kinetic == KineticRole.Generator) net.Generators.Add(b);
                else if (b.Spec.Kinetic == KineticRole.Consumer) net.Consumers.Add(b);
                _byBuilding[b.Id] = net;
            }
        }
    }
}
