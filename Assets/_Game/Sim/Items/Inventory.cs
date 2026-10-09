using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    public struct ItemStack
    {
        public ushort Item;
        public int Count;

        public ItemStack(ushort item, int count)
        {
            Item = item;
            Count = count;
        }

        public bool IsEmpty => Count <= 0 || Item == ItemCatalog.None;
    }

    /// <summary>
    /// Fixed number of slots, each holding one item type up to its stack size (or <see cref="SlotLimit"/>).
    /// All operations scan slots in index order so results are deterministic.
    /// </summary>
    public sealed class Inventory
    {
        readonly ItemStack[] _slots;

        /// <summary>Per-slot cap overriding stack size (0 = use item stack size). Machines use small caps.</summary>
        public int SlotLimit { get; set; }

        public Inventory(int slotCount, int slotLimit = 0)
        {
            _slots = new ItemStack[slotCount];
            SlotLimit = slotLimit;
        }

        public int SlotCount => _slots.Length;

        public ItemStack this[int slot] => _slots[slot];

        public event Action Changed;

        int Cap(ushort item) => SlotLimit > 0 ? SlotLimit : ItemCatalog.StackSizeOf(item);

        public int Count(ushort item)
        {
            int n = 0;
            foreach (var s in _slots)
                if (s.Item == item) n += s.Count;
            return n;
        }

        public bool IsEmpty
        {
            get
            {
                foreach (var s in _slots)
                    if (!s.IsEmpty) return false;
                return true;
            }
        }

        /// <summary>How many of <paramref name="item"/> would fit right now.</summary>
        public int SpaceFor(ushort item)
        {
            int cap = Cap(item);
            int space = 0;
            foreach (var s in _slots)
            {
                if (s.IsEmpty) space += cap;
                else if (s.Item == item) space += Math.Max(0, cap - s.Count);
            }
            return space;
        }

        public bool CanInsert(ushort item, int count = 1) => SpaceFor(item) >= count;

        /// <summary>Inserts as many as fit (existing stacks first). Returns how many were inserted.</summary>
        public int Insert(ushort item, int count)
        {
            if (item == ItemCatalog.None || count <= 0) return 0;
            int cap = Cap(item);
            int left = count;
            for (int i = 0; i < _slots.Length && left > 0; i++)
            {
                if (_slots[i].Item != item || _slots[i].IsEmpty) continue;
                int add = Math.Min(left, cap - _slots[i].Count);
                if (add <= 0) continue;
                _slots[i].Count += add;
                left -= add;
            }
            for (int i = 0; i < _slots.Length && left > 0; i++)
            {
                if (!_slots[i].IsEmpty) continue;
                int add = Math.Min(left, cap);
                _slots[i] = new ItemStack(item, add);
                left -= add;
            }
            if (left != count) Changed?.Invoke();
            return count - left;
        }

        /// <summary>Removes up to <paramref name="count"/> (last slots first). Returns how many were removed.</summary>
        public int Remove(ushort item, int count)
        {
            int left = count;
            for (int i = _slots.Length - 1; i >= 0 && left > 0; i--)
            {
                if (_slots[i].Item != item || _slots[i].IsEmpty) continue;
                int take = Math.Min(left, _slots[i].Count);
                _slots[i].Count -= take;
                if (_slots[i].Count == 0) _slots[i] = default;
                left -= take;
            }
            if (left != count) Changed?.Invoke();
            return count - left;
        }

        /// <summary>Finds the first item (by slot order) that <paramref name="accept"/> allows, and removes one.</summary>
        public bool TryTakeFirst(Func<ushort, bool> accept, out ushort item)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].IsEmpty || (accept != null && !accept(_slots[i].Item))) continue;
                item = _slots[i].Item;
                Remove(item, 1);
                return true;
            }
            item = ItemCatalog.None;
            return false;
        }

        /// <summary>All non-empty stacks, merged by item, in first-seen order.</summary>
        public List<ItemStack> Contents()
        {
            var list = new List<ItemStack>();
            foreach (var s in _slots)
            {
                if (s.IsEmpty) continue;
                int idx = list.FindIndex(x => x.Item == s.Item);
                if (idx >= 0) list[idx] = new ItemStack(s.Item, list[idx].Count + s.Count);
                else list.Add(s);
            }
            return list;
        }

        /// <summary>Whether every stack in <paramref name="stacks"/> would fit at once.</summary>
        public bool CanInsertAll(List<ItemStack> stacks)
        {
            var copy = new Inventory(_slots.Length, SlotLimit);
            Array.Copy(_slots, copy._slots, _slots.Length);
            foreach (var s in stacks)
                if (copy.Insert(s.Item, s.Count) != s.Count) return false;
            return true;
        }

        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
            Changed?.Invoke();
        }

        internal void Write(BinaryWriter w)
        {
            w.Write((ushort)_slots.Length);
            foreach (var s in _slots)
            {
                w.Write(s.Item);
                w.Write(s.Count);
            }
        }

        internal void Read(BinaryReader r)
        {
            int n = r.ReadUInt16();
            if (n != _slots.Length) throw new InvalidDataException($"Inventory has {_slots.Length} slots, save has {n}");
            for (int i = 0; i < n; i++) _slots[i] = new ItemStack(r.ReadUInt16(), r.ReadInt32());
            Changed?.Invoke();
        }

        internal void AddToHash(ref Fnv64 h)
        {
            foreach (var s in _slots)
            {
                h.Add(s.Item);
                h.Add(s.Count);
            }
        }
    }
}
