using System.Collections.Generic;

namespace Cogworks.Sim
{
    /// <summary>
    /// Links belts to the belt they feed and updates them downstream-first, so a compressed line moves as one
    /// instead of opening a gap per tile each tick. Links are rebuilt only when belts change.
    /// </summary>
    internal sealed class BeltSystem
    {
        readonly List<ConveyorLogic> _order = new List<ConveyorLogic>();
        bool _dirty = true;

        public void MarkDirty() => _dirty = true;

        public void Tick(World world, List<Building> buildingsById)
        {
            if (_dirty) Rebuild(world, buildingsById);
            foreach (var belt in _order) belt.Advance();
        }

        void Rebuild(World world, List<Building> buildingsById)
        {
            _dirty = false;
            _order.Clear();
            var belts = new List<ConveyorLogic>();
            foreach (var b in buildingsById)
                if (b.Logic is ConveyorLogic c) belts.Add(c);

            var feeders = new Dictionary<ConveyorLogic, List<ConveyorLogic>>();
            foreach (var c in belts)
            {
                c.Next = null;
                c.NextIsSide = false;
                var dir = c.Building.Direction;
                var ahead = world.Buildings.GetAt(c.Building.Origin + dir.ToOffset());
                if (ahead?.Logic is ConveyorLogic n && n.Building.Direction != dir.Opposite())
                {
                    c.Next = n;
                    c.NextIsSide = n.Building.Direction != dir;
                    if (!feeders.TryGetValue(n, out var list)) feeders[n] = list = new List<ConveyorLogic>();
                    list.Add(c);
                }
            }

            // Breadth-first from line ends back up the feeders: smaller depth = further downstream.
            var depth = new Dictionary<ConveyorLogic, int>();
            var queue = new Queue<ConveyorLogic>();
            foreach (var c in belts)
            {
                if (c.Next != null) continue;
                depth[c] = 0;
                queue.Enqueue(c);
            }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (!feeders.TryGetValue(c, out var list)) continue;
                foreach (var f in list)
                {
                    if (depth.ContainsKey(f)) continue;
                    depth[f] = depth[c] + 1;
                    queue.Enqueue(f);
                }
            }

            // Belts in or feeding a loop never reach an end; they go last, in id order.
            belts.Sort((a, b) =>
            {
                int da = depth.TryGetValue(a, out int x) ? x : int.MaxValue;
                int db = depth.TryGetValue(b, out int y) ? y : int.MaxValue;
                return da != db ? da.CompareTo(db) : a.Building.Id.CompareTo(b.Building.Id);
            });
            _order.AddRange(belts);
        }
    }
}
