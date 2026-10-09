using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>
    /// Per-player sim state (avatar position lives outside the sim). Players stay in the world after
    /// disconnecting so their slot and inventory survive a rejoin.
    /// </summary>
    public sealed class PlayerState
    {
        public const int InventorySlots = 40;
        public const int MaxCraftQueue = 100;
        /// <summary>Minimum ticks between hand-mining actions.</summary>
        public const int MineCooldownTicks = 20;

        public readonly byte Id;
        public readonly Inventory Inventory = new Inventory(InventorySlots);

        /// <summary>Hand-craft queue; ingredients were already taken when each entry was queued.</summary>
        public readonly List<ushort> CraftQueue = new List<ushort>();
        public int CraftProgress { get; private set; }
        // Far enough in the past that the first mine is never on cooldown (and the subtraction can't overflow).
        internal long LastMineTick = -MineCooldownTicks;

        internal PlayerState(byte id)
        {
            Id = id;
        }

        internal void GiveStarterKit()
        {
            Inventory.Insert(ItemCatalog.IronPlate.Id, 20);
            Inventory.Insert(ItemCatalog.Coal.Id, 10);
            Inventory.Insert(BuildingCatalog.Drill.ItemId, 2);
            Inventory.Insert(BuildingCatalog.Furnace.ItemId, 2);
            Inventory.Insert(BuildingCatalog.HandCrank.ItemId, 1);
            Inventory.Insert(BuildingCatalog.Shaft.ItemId, 8);
            Inventory.Insert(BuildingCatalog.Chest.ItemId, 2);
        }

        /// <summary>
        /// Advances the front of the craft queue; finished items wait if the inventory is full.
        /// Returns the recipe that completed this tick, if any.
        /// </summary>
        internal Recipe Tick()
        {
            if (CraftQueue.Count == 0) return null;
            var recipe = RecipeCatalog.Get(CraftQueue[0]);
            if (recipe == null)
            {
                CraftQueue.RemoveAt(0);
                return null;
            }
            if (CraftProgress < recipe.Ticks)
            {
                CraftProgress++;
                return null;
            }
            if (!Inventory.CanInsert(recipe.Output.Item, recipe.Output.Count)) return null;
            Inventory.Insert(recipe.Output.Item, recipe.Output.Count);
            CraftQueue.RemoveAt(0);
            CraftProgress = 0;
            return recipe;
        }

        internal void Write(BinaryWriter w)
        {
            w.Write(Id);
            Inventory.Write(w);
            w.Write((ushort)CraftQueue.Count);
            foreach (ushort r in CraftQueue) w.Write(r);
            w.Write(CraftProgress);
            w.Write(LastMineTick);
        }

        internal static PlayerState Read(BinaryReader r)
        {
            var p = new PlayerState(r.ReadByte());
            p.Inventory.Read(r);
            int n = r.ReadUInt16();
            for (int i = 0; i < n; i++) p.CraftQueue.Add(r.ReadUInt16());
            p.CraftProgress = r.ReadInt32();
            p.LastMineTick = r.ReadInt64();
            return p;
        }

        internal void AddToHash(ref Fnv64 h)
        {
            h.Add(Id);
            Inventory.AddToHash(ref h);
            foreach (ushort r in CraftQueue) h.Add(r);
            h.Add(CraftProgress);
            h.Add(LastMineTick);
        }
    }
}
