using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>Smelts ore (or stone into bricks) while burning fuel. Fuel only burns while smelting.</summary>
    public sealed class FurnaceLogic : BuildingLogic
    {
        // Inserters top machines up to small buffers; players can fill a full stack.
        const int InserterInputCap = 10;
        const int InserterFuelCap = 5;
        const int OutputCap = 100;

        public ItemStack Input;
        public ItemStack Fuel;
        public ItemStack Output;
        public int Progress { get; private set; }
        public int BurnLeft { get; private set; }
        public int BurnTotal { get; private set; }

        public Recipe CurrentRecipe => RecipeCatalog.SmeltingFor(Input.Item);
        public bool Burning => BurnLeft > 0;

        public override void Tick(World world)
        {
            var recipe = CurrentRecipe;
            bool canWork = recipe != null && Input.Count >= recipe.Inputs[0].Count && OutputHasRoom(recipe);
            if (!canWork)
            {
                Progress = 0;
                return;
            }

            if (BurnLeft <= 0)
            {
                if (Fuel.Count <= 0) return;
                BurnTotal = BurnLeft = ItemCatalog.Get(Fuel.Item).FuelTicks;
                Take(ref Fuel, 1);
            }
            BurnLeft--;
            if (++Progress < recipe.Ticks) return;

            Progress = 0;
            Take(ref Input, recipe.Inputs[0].Count);
            Output = new ItemStack(recipe.Output.Item, Output.Count + recipe.Output.Count);
        }

        bool OutputHasRoom(Recipe recipe) =>
            Output.IsEmpty || (Output.Item == recipe.Output.Item && Output.Count + recipe.Output.Count <= OutputCap);

        static bool IsFuel(ushort item) => (ItemCatalog.Get(item)?.FuelTicks ?? 0) > 0;
        static bool IsSmeltable(ushort item) => RecipeCatalog.SmeltingFor(item) != null;

        static void Take(ref ItemStack stack, int n)
        {
            stack.Count -= n;
            if (stack.Count <= 0) stack = default;
        }

        int Room(ItemStack slot, ushort item, int cap) =>
            slot.IsEmpty ? cap : slot.Item == item ? Math.Max(0, cap - slot.Count) : 0;

        public override bool CanAccept(ushort item, Int2 fromTile)
        {
            if (IsFuel(item)) return Room(Fuel, item, InserterFuelCap) > 0;
            if (IsSmeltable(item)) return Room(Input, item, InserterInputCap) > 0;
            return false;
        }

        public override void Insert(ushort item, Int2 fromTile)
        {
            if (IsFuel(item)) Fuel = new ItemStack(item, Fuel.Count + 1);
            else Input = new ItemStack(item, Input.Count + 1);
        }

        public override int InsertFromPlayer(ushort item, int count)
        {
            int cap = ItemCatalog.StackSizeOf(item);
            int n;
            if (IsFuel(item))
            {
                n = Math.Min(count, Room(Fuel, item, cap));
                if (n > 0) Fuel = new ItemStack(item, Fuel.Count + n);
            }
            else if (IsSmeltable(item))
            {
                n = Math.Min(count, Room(Input, item, cap));
                if (n > 0) Input = new ItemStack(item, Input.Count + n);
            }
            else n = 0;
            return n;
        }

        public override bool TryTake(Func<ushort, bool> accept, out ushort item)
        {
            item = Output.Item;
            if (Output.IsEmpty || (accept != null && !accept(item))) return false;
            Take(ref Output, 1);
            return true;
        }

        public override int TakeToPlayer(ushort item, int count)
        {
            int moved = 0;
            moved += TakeFrom(ref Output, item, count - moved);
            moved += TakeFrom(ref Input, item, count - moved);
            moved += TakeFrom(ref Fuel, item, count - moved);
            return moved;
        }

        static int TakeFrom(ref ItemStack slot, ushort item, int want)
        {
            if (want <= 0 || slot.IsEmpty || slot.Item != item) return 0;
            int n = Math.Min(want, slot.Count);
            Take(ref slot, n);
            return n;
        }

        public override void CollectContents(List<ItemStack> into)
        {
            AddContents(into, Input.Item, Input.Count);
            AddContents(into, Fuel.Item, Fuel.Count);
            AddContents(into, Output.Item, Output.Count);
        }

        internal override void Write(BinaryWriter w)
        {
            WriteStack(w, Input);
            WriteStack(w, Fuel);
            WriteStack(w, Output);
            w.Write(Progress);
            w.Write(BurnLeft);
            w.Write(BurnTotal);
        }

        internal override void Read(BinaryReader r)
        {
            Input = ReadStack(r);
            Fuel = ReadStack(r);
            Output = ReadStack(r);
            Progress = r.ReadInt32();
            BurnLeft = r.ReadInt32();
            BurnTotal = r.ReadInt32();
        }

        internal override void AddToHash(ref Fnv64 h)
        {
            h.Add(Input.Item); h.Add(Input.Count);
            h.Add(Fuel.Item); h.Add(Fuel.Count);
            h.Add(Output.Item); h.Add(Output.Count);
            h.Add(Progress);
            h.Add(BurnLeft);
        }

        internal static void WriteStack(BinaryWriter w, ItemStack s)
        {
            w.Write(s.Item);
            w.Write(s.Count);
        }

        internal static ItemStack ReadStack(BinaryReader r) => new ItemStack(r.ReadUInt16(), r.ReadInt32());
    }
}
