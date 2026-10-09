using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    public struct BeltItem
    {
        public ushort Item;
        /// <summary>Distance travelled along this belt in belt units; 0 = entry edge, <see cref="ConveyorLogic.Length"/> = exit edge.</summary>
        public int Pos;
    }

    /// <summary>
    /// A single-lane belt tile. Items keep at least <see cref="Spacing"/> apart (4 per tile) and flow into the
    /// belt in front: straight on at the back of the queue, or from the side into the middle.
    /// </summary>
    public sealed class ConveyorLogic : BuildingLogic
    {
        public const int Length = 256;
        public const int Spacing = 64;
        public const int MidPos = Length / 2;

        /// <summary>Front (highest Pos) first.</summary>
        public readonly List<BeltItem> Items = new List<BeltItem>(4);

        /// <summary>Belt this one feeds, set by <see cref="BeltSystem"/>.</summary>
        internal ConveyorLogic Next;
        /// <summary>True if <see cref="Next"/> runs perpendicular (we side-load into it).</summary>
        internal bool NextIsSide;
        /// <summary>Non-belt building ahead that takes items straight off the end (splitter, underground).</summary>
        internal BuildingLogic NextSink;

        public int Speed => Building.Spec.BeltSpeed;

        int TailPos => Items.Count == 0 ? int.MaxValue : Items[Items.Count - 1].Pos;

        /// <summary>Moves items forward one tick. Called downstream-first so the belt ahead has already moved.</summary>
        internal void Advance()
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var it = Items[i];
                int limit;
                if (i > 0) limit = Items[i - 1].Pos - Spacing;
                else if (Next != null && !NextIsSide)
                {
                    int tail = Next.TailPos;
                    limit = tail == int.MaxValue ? int.MaxValue : Length + tail - Spacing;
                }
                else limit = Next != null || NextSink != null ? Length : Length - 1;

                int pos = Math.Max(it.Pos, Math.Min(it.Pos + Speed, limit));
                if (i == 0 && pos >= Length && (Next != null || NextSink != null))
                {
                    bool moved;
                    if (Next != null) moved = NextIsSide ? Next.TryInsertAt(MidPos, it.Item) : Next.TryAppendTail(pos - Length, it.Item);
                    else if (NextSink.CanAccept(it.Item, Building.Origin))
                    {
                        NextSink.Insert(it.Item, Building.Origin);
                        moved = true;
                    }
                    else moved = false;
                    if (moved)
                    {
                        Items.RemoveAt(0);
                        i--;
                        continue;
                    }
                    pos = Length - 1;
                }
                it.Pos = pos;
                Items[i] = it;
            }
        }

        /// <summary>Puts an item at the entry edge, as if it arrived from the belt behind.</summary>
        public bool TryInsertAtEntry(ushort item) => TryAppendTail(0, item);

        bool TryAppendTail(int pos, ushort item)
        {
            if (pos < 0 || pos > TailPos - Spacing) return false;
            Items.Add(new BeltItem { Item = item, Pos = pos });
            return true;
        }

        /// <summary>Inserts at <paramref name="pos"/> if there's a gap of <see cref="Spacing"/> on both sides.</summary>
        public bool TryInsertAt(int pos, ushort item)
        {
            int index = 0;
            while (index < Items.Count && Items[index].Pos > pos) index++;
            if (index > 0 && Items[index - 1].Pos - pos < Spacing) return false;
            if (index < Items.Count && pos - Items[index].Pos < Spacing) return false;
            Items.Insert(index, new BeltItem { Item = item, Pos = pos });
            return true;
        }

        bool HasGapAt(int pos)
        {
            foreach (var it in Items)
                if (Math.Abs(it.Pos - pos) < Spacing) return false;
            return true;
        }

        public override bool CanAccept(ushort item, Int2 fromTile) => HasGapAt(MidPos);

        public override void Insert(ushort item, Int2 fromTile) => TryInsertAt(MidPos, item);

        /// <summary>Takes the front-most acceptable item, as an inserter grabbing from a passing belt.</summary>
        public override bool TryTake(Func<ushort, bool> accept, out ushort item)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (accept != null && !accept(Items[i].Item)) continue;
                item = Items[i].Item;
                Items.RemoveAt(i);
                return true;
            }
            item = ItemCatalog.None;
            return false;
        }

        public override void CollectContents(List<ItemStack> into)
        {
            foreach (var it in Items) AddContents(into, it.Item, 1);
        }

        internal override void Write(BinaryWriter w)
        {
            w.Write((byte)Items.Count);
            foreach (var it in Items)
            {
                w.Write(it.Item);
                w.Write((short)it.Pos);
            }
        }

        internal override void Read(BinaryReader r)
        {
            Items.Clear();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++) Items.Add(new BeltItem { Item = r.ReadUInt16(), Pos = r.ReadInt16() });
        }

        internal override void AddToHash(ref Fnv64 h)
        {
            h.Add(Items.Count);
            foreach (var it in Items)
            {
                h.Add(it.Item);
                h.Add(it.Pos);
            }
        }
    }
}
