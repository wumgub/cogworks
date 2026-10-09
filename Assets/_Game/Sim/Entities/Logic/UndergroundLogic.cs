using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>
    /// Underground belt entrance. Pairs with the first exit facing the same way up to <see cref="MaxGap"/>
    /// tiles ahead; items travel through at belt speed and come out of the exit onto whatever is in front.
    /// </summary>
    public sealed class UndergroundEntranceLogic : BuildingLogic
    {
        /// <summary>Tiles that can be skipped between entrance and exit.</summary>
        public const int MaxGap = 5;
        const int TicksPerTile = ConveyorLogic.Length / 8;
        const int ItemsPerTile = 4;

        struct Transit
        {
            public ushort Item;
            public int TicksLeft;
        }

        readonly List<Transit> _inTransit = new List<Transit>();
        // Refreshed every tick from the paired exit's distance; belts query acceptance between ticks.
        int _capacity;
        int _travelTicks;

        public override bool AcceptsBeltInput => true;

        /// <summary>Paired exit, or null. Looked up on demand since the tiles ahead can change.</summary>
        public Building FindExit(World world)
        {
            var dir = Building.Direction;
            var tile = Building.Origin;
            for (int i = 0; i <= MaxGap; i++)
            {
                tile += dir.ToOffset();
                var b = world.Buildings.GetAt(tile);
                if (b?.Spec.Kind == BuildingKind.UndergroundExit && b.Direction == dir) return b;
            }
            return null;
        }

        int Distance(Building exit) =>
            System.Math.Abs(exit.Origin.X - Building.Origin.X) + System.Math.Abs(exit.Origin.Y - Building.Origin.Y);

        public override bool CanAccept(ushort item, Int2 fromTile)
        {
            if (_capacity == 0 || _inTransit.Count >= _capacity) return false;
            // Keep items spaced like on a belt.
            return _inTransit.Count == 0 || _inTransit[_inTransit.Count - 1].TicksLeft <= _travelTicks - TicksPerTile / ItemsPerTile;
        }

        public override void Insert(ushort item, Int2 fromTile) =>
            _inTransit.Add(new Transit { Item = item, TicksLeft = _travelTicks });

        public override void Tick(World world)
        {
            var exit = FindExit(world);
            if (exit == null)
            {
                _capacity = 0;
                return;
            }
            int distance = Distance(exit);
            _travelTicks = distance * TicksPerTile;
            _capacity = distance * ItemsPerTile;

            for (int i = 0; i < _inTransit.Count; i++)
            {
                var t = _inTransit[i];
                if (t.TicksLeft > 0) t.TicksLeft--;
                _inTransit[i] = t;
            }
            if (_inTransit.Count > 0 && _inTransit[0].TicksLeft == 0)
            {
                var dir = exit.Direction;
                if (ItemPush.Into(world, exit.Origin + dir.ToOffset(), dir, _inTransit[0].Item, exit.Origin))
                    _inTransit.RemoveAt(0);
            }
        }

        public int InTransit => _inTransit.Count;

        public override void CollectContents(List<ItemStack> into)
        {
            foreach (var t in _inTransit) AddContents(into, t.Item, 1);
        }

        internal override void Write(BinaryWriter w)
        {
            w.Write((ushort)_inTransit.Count);
            foreach (var t in _inTransit)
            {
                w.Write(t.Item);
                w.Write((short)t.TicksLeft);
            }
            w.Write((short)_capacity);
            w.Write((short)_travelTicks);
        }

        internal override void Read(BinaryReader r)
        {
            _inTransit.Clear();
            int n = r.ReadUInt16();
            for (int i = 0; i < n; i++) _inTransit.Add(new Transit { Item = r.ReadUInt16(), TicksLeft = r.ReadInt16() });
            _capacity = r.ReadInt16();
            _travelTicks = r.ReadInt16();
        }

        internal override void AddToHash(ref Fnv64 h)
        {
            foreach (var t in _inTransit)
            {
                h.Add(t.Item);
                h.Add(t.TicksLeft);
            }
            h.Add(_capacity);
            h.Add(_travelTicks);
        }
    }

    /// <summary>Underground belt exit; all the work happens in its entrance.</summary>
    public sealed class UndergroundExitLogic : BuildingLogic { }
}
