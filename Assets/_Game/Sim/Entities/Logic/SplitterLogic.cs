using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>
    /// Takes items from the belt behind it and deals them out round-robin to the front, left and right,
    /// skipping any side that's blocked or missing.
    /// </summary>
    public sealed class SplitterLogic : BuildingLogic
    {
        const int Capacity = 2;

        readonly List<ushort> _buffer = new List<ushort>(Capacity);
        int _nextOutput;

        public int Buffered => _buffer.Count;

        public override bool AcceptsBeltInput => true;

        public override bool CanAccept(ushort item, Int2 fromTile) => _buffer.Count < Capacity;

        public override void Insert(ushort item, Int2 fromTile) => _buffer.Add(item);

        public override void Tick(World world)
        {
            if (_buffer.Count == 0) return;
            var facing = Building.Direction;
            // Front, left, right.
            var outputs = new[] { facing, facing.RotateCcw(), facing.RotateCw() };
            for (int n = 0; n < outputs.Length; n++)
            {
                int i = (_nextOutput + n) % outputs.Length;
                var dir = outputs[i];
                if (!ItemPush.Into(world, Building.Origin + dir.ToOffset(), dir, _buffer[0], Building.Origin)) continue;
                _buffer.RemoveAt(0);
                _nextOutput = (i + 1) % outputs.Length;
                return;
            }
        }

        public override void CollectContents(List<ItemStack> into)
        {
            foreach (ushort item in _buffer) AddContents(into, item, 1);
        }

        internal override void Write(BinaryWriter w)
        {
            w.Write((byte)_buffer.Count);
            foreach (ushort item in _buffer) w.Write(item);
            w.Write((byte)_nextOutput);
        }

        internal override void Read(BinaryReader r)
        {
            _buffer.Clear();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++) _buffer.Add(r.ReadUInt16());
            _nextOutput = r.ReadByte();
        }

        internal override void AddToHash(ref Fnv64 h)
        {
            h.Add(_buffer.Count);
            foreach (ushort item in _buffer) h.Add(item);
            h.Add(_nextOutput);
        }
    }
}
