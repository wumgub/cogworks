using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>
    /// Behaviour and state for one placed building. Subclasses override only what they need. All state
    /// that affects the simulation must be written by <see cref="Write"/> and hashed by <see cref="AddToHash"/>.
    /// </summary>
    public abstract class BuildingLogic
    {
        public Building Building { get; internal set; }

        /// <summary>Runs once per tick in the building's update phase (see <see cref="World.Tick"/>).</summary>
        public virtual void Tick(World world) { }

        // ------------------------------------------------------------ item transfer (inserters, drills, belts)

        /// <summary>Whether one <paramref name="item"/> arriving from <paramref name="fromTile"/> fits right now.</summary>
        public virtual bool CanAccept(ushort item, Int2 fromTile) => false;

        /// <summary>Adds one item. Only call after <see cref="CanAccept"/> returned true this tick.</summary>
        public virtual void Insert(ushort item, Int2 fromTile) { }

        /// <summary>Removes one output item that <paramref name="accept"/> allows (null = anything).</summary>
        public virtual bool TryTake(Func<ushort, bool> accept, out ushort item)
        {
            item = ItemCatalog.None;
            return false;
        }

        // ------------------------------------------------------------ player transfer

        /// <summary>Moves up to <paramref name="count"/> from a player into this building. Returns how many moved.</summary>
        public virtual int InsertFromPlayer(ushort item, int count)
        {
            int n = 0;
            while (n < count && CanAccept(item, Building.Origin))
            {
                Insert(item, Building.Origin);
                n++;
            }
            return n;
        }

        /// <summary>Moves up to <paramref name="count"/> of <paramref name="item"/> out to a player. Returns how many moved.</summary>
        public virtual int TakeToPlayer(ushort item, int count)
        {
            int n = 0;
            while (n < count && TryTake(i => i == item, out _)) n++;
            return n;
        }

        /// <summary>Everything stored inside, refunded to the player who removes the building.</summary>
        public virtual void CollectContents(List<ItemStack> into) { }

        // ------------------------------------------------------------ persistence

        internal virtual void Write(BinaryWriter w) { }
        internal virtual void Read(BinaryReader r) { }
        internal virtual void AddToHash(ref Fnv64 h) { }

        internal static BuildingLogic Create(BuildingSpec spec) => spec.Kind switch
        {
            BuildingKind.Conveyor => new ConveyorLogic(),
            BuildingKind.Inserter => new InserterLogic(),
            BuildingKind.Chest => new ChestLogic(),
            BuildingKind.Drill => new DrillLogic(),
            BuildingKind.Furnace => new FurnaceLogic(),
            BuildingKind.Assembler => new AssemblerLogic(),
            BuildingKind.Generator => new GeneratorLogic(),
            BuildingKind.Lab => new LabLogic(),
            _ => new PassiveLogic(),
        };

        protected static void AddContents(List<ItemStack> into, Inventory inv) => into.AddRange(inv.Contents());

        protected static void AddContents(List<ItemStack> into, ushort item, int count)
        {
            if (item != ItemCatalog.None && count > 0) into.Add(new ItemStack(item, count));
        }
    }

    /// <summary>Shafts and gearboxes: they only carry rotation, which the kinetic system handles.</summary>
    public sealed class PassiveLogic : BuildingLogic { }

    public sealed class ChestLogic : BuildingLogic
    {
        public readonly Inventory Inventory = new Inventory(16);

        public override bool CanAccept(ushort item, Int2 fromTile) => Inventory.CanInsert(item);
        public override void Insert(ushort item, Int2 fromTile) => Inventory.Insert(item, 1);
        public override bool TryTake(Func<ushort, bool> accept, out ushort item) => Inventory.TryTakeFirst(accept, out item);
        public override int InsertFromPlayer(ushort item, int count) => Inventory.Insert(item, count);
        public override int TakeToPlayer(ushort item, int count) => Inventory.Remove(item, count);
        public override void CollectContents(List<ItemStack> into) => AddContents(into, Inventory);

        internal override void Write(BinaryWriter w) => Inventory.Write(w);
        internal override void Read(BinaryReader r) => Inventory.Read(r);
        internal override void AddToHash(ref Fnv64 h) => Inventory.AddToHash(ref h);
    }
}
