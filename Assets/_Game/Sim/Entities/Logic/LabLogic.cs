using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>Consumes science packs to advance the current research, one pack every two seconds.</summary>
    public sealed class LabLogic : BuildingLogic
    {
        public const int TicksPerPack = Simulation.TicksPerSecond * 2;

        public readonly Inventory Packs = new Inventory(2, 10);
        public ushort Working { get; private set; }
        public int Progress { get; private set; }

        static bool IsScience(ushort item) => item == ItemCatalog.RedScience.Id || item == ItemCatalog.GreenScience.Id;

        public override void Tick(World world)
        {
            var research = world.Research;
            if (Working == ItemCatalog.None)
            {
                if (!Packs.TryTakeFirst(research.NeedsMore, out ushort pack)) return;
                Working = pack;
                Progress = 0;
            }
            if (++Progress < TicksPerPack) return;
            research.AddProgress(Working);
            Working = ItemCatalog.None;
            Progress = 0;
        }

        public override bool CanAccept(ushort item, Int2 fromTile) => IsScience(item) && Packs.CanInsert(item);
        public override void Insert(ushort item, Int2 fromTile) => Packs.Insert(item, 1);
        public override int InsertFromPlayer(ushort item, int count) => IsScience(item) ? Packs.Insert(item, count) : 0;
        public override int TakeToPlayer(ushort item, int count) => Packs.Remove(item, count);

        public override void CollectContents(List<ItemStack> into)
        {
            AddContents(into, Packs);
            AddContents(into, Working, 1);
        }

        internal override void Write(BinaryWriter w)
        {
            Packs.Write(w);
            w.Write(Working);
            w.Write(Progress);
        }

        internal override void Read(BinaryReader r)
        {
            Packs.Read(r);
            Working = r.ReadUInt16();
            Progress = r.ReadInt32();
        }

        internal override void AddToHash(ref Fnv64 h)
        {
            Packs.AddToHash(ref h);
            h.Add(Working);
            h.Add(Progress);
        }
    }
}
