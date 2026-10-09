using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>
    /// Source of rotation. Hand cranks turn for a few seconds after a player cranks them, water wheels
    /// always turn, steam engines burn coal while their network has something to drive.
    /// </summary>
    public sealed class GeneratorLogic : BuildingLogic
    {
        /// <summary>How long one crank keeps a hand crank turning.</summary>
        public const int CrankTicks = Simulation.TicksPerSecond * 5;
        const int InserterFuelCap = 10;

        public int CrankLeft { get; private set; }
        public ItemStack Fuel;
        public int BurnLeft { get; private set; }

        /// <summary>Supplying rotation this tick (set by the kinetic system each tick).</summary>
        public bool Active { get; private set; }

        bool IsCrank => Building.Spec == BuildingCatalog.HandCrank;
        bool IsSteam => Building.Spec == BuildingCatalog.SteamEngine;

        public void Crank() => CrankLeft = CrankTicks;

        /// <summary>Called by the kinetic system once per tick, before the network's speed is computed.</summary>
        internal void UpdateGenerator(bool networkHasLoad)
        {
            if (IsCrank)
            {
                Active = CrankLeft > 0;
                if (CrankLeft > 0) CrankLeft--;
            }
            else if (IsSteam)
            {
                if (networkHasLoad && BurnLeft <= 0 && Fuel.Count > 0)
                {
                    BurnLeft = ItemCatalog.Get(Fuel.Item).FuelTicks;
                    Fuel.Count--;
                    if (Fuel.Count <= 0) Fuel = default;
                }
                Active = BurnLeft > 0;
                if (networkHasLoad && BurnLeft > 0) BurnLeft--;
            }
            else
            {
                Active = true;
            }
        }

        static bool IsFuel(ushort item) => (ItemCatalog.Get(item)?.FuelTicks ?? 0) > 0;

        public override bool CanAccept(ushort item, Int2 fromTile) =>
            IsSteam && IsFuel(item) && (Fuel.IsEmpty || (Fuel.Item == item && Fuel.Count < InserterFuelCap));

        public override void Insert(ushort item, Int2 fromTile) => Fuel = new ItemStack(item, Fuel.Count + 1);

        public override int InsertFromPlayer(ushort item, int count)
        {
            if (!IsSteam || !IsFuel(item) || (!Fuel.IsEmpty && Fuel.Item != item)) return 0;
            int n = Math.Min(count, ItemCatalog.StackSizeOf(item) - Fuel.Count);
            if (n > 0) Fuel = new ItemStack(item, Fuel.Count + n);
            return Math.Max(0, n);
        }

        public override int TakeToPlayer(ushort item, int count)
        {
            if (Fuel.IsEmpty || Fuel.Item != item) return 0;
            int n = Math.Min(count, Fuel.Count);
            Fuel.Count -= n;
            if (Fuel.Count <= 0) Fuel = default;
            return n;
        }

        public override void CollectContents(List<ItemStack> into) => AddContents(into, Fuel.Item, Fuel.Count);

        internal override void Write(BinaryWriter w)
        {
            w.Write(CrankLeft);
            FurnaceLogic.WriteStack(w, Fuel);
            w.Write(BurnLeft);
        }

        internal override void Read(BinaryReader r)
        {
            CrankLeft = r.ReadInt32();
            Fuel = FurnaceLogic.ReadStack(r);
            BurnLeft = r.ReadInt32();
        }

        internal override void AddToHash(ref Fnv64 h)
        {
            h.Add(CrankLeft);
            h.Add(Fuel.Item);
            h.Add(Fuel.Count);
            h.Add(BurnLeft);
        }
    }
}
