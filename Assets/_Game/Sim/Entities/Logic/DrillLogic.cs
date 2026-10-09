using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>
    /// Mines the ore under its footprint while its kinetic network turns, and pushes each item into the
    /// building in front. Speed scales with RPM; at 32 rpm it mines one item every 2 seconds.
    /// </summary>
    public sealed class DrillLogic : BuildingLogic
    {
        public const int BaseRpm = 32;
        public const int WorkPerItem = BaseRpm * Simulation.TicksPerSecond * 2;

        public int Progress { get; private set; }
        /// <summary>Mined item waiting for space in front (drill stalls until it's gone).</summary>
        public ushort Output { get; private set; }
        int _cursor;

        // Status for the UI, not part of sim state.
        public int LastRpm { get; private set; }
        public bool OutOfOre { get; private set; }

        public override void Tick(World world)
        {
            if (Output != ItemCatalog.None && !TryPushOutput(world)) return;

            LastRpm = world.Kinetics.RpmAt(Building);
            OutOfOre = !HasOre(world);
            if (LastRpm <= 0 || OutOfOre) return;

            Progress += LastRpm;
            if (Progress < WorkPerItem) return;
            Progress -= WorkPerItem;
            Output = MineOne(world);
            TryPushOutput(world);
        }

        bool TryPushOutput(World world)
        {
            Int2 front = Building.FrontTile();
            var target = world.Buildings.GetAt(front)?.Logic;
            if (target == null || !target.CanAccept(Output, Building.Origin)) return false;
            target.Insert(Output, Building.Origin);
            Output = ItemCatalog.None;
            return true;
        }

        bool HasOre(World world)
        {
            foreach (var tile in Building.Footprint())
            {
                world.GetOre(tile, out int amount);
                if (amount > 0) return true;
            }
            return false;
        }

        /// <summary>Takes one unit from the next ore tile in round-robin order, so patches deplete evenly.</summary>
        ushort MineOne(World world)
        {
            var tiles = new List<Int2>(Building.Footprint());
            for (int n = 0; n < tiles.Count; n++)
            {
                int i = (_cursor + n) % tiles.Count;
                var ore = world.MineOre(tiles[i]);
                if (ore == Ore.None) continue;
                _cursor = (i + 1) % tiles.Count;
                return ItemCatalog.FromOre(ore).Id;
            }
            return ItemCatalog.None;
        }

        public override bool TryTake(Func<ushort, bool> accept, out ushort item)
        {
            item = Output;
            if (Output == ItemCatalog.None || (accept != null && !accept(Output))) return false;
            Output = ItemCatalog.None;
            return true;
        }

        public override void CollectContents(List<ItemStack> into) => AddContents(into, Output, 1);

        internal override void Write(BinaryWriter w)
        {
            w.Write(Progress);
            w.Write(Output);
            w.Write((byte)_cursor);
        }

        internal override void Read(BinaryReader r)
        {
            Progress = r.ReadInt32();
            Output = r.ReadUInt16();
            _cursor = r.ReadByte();
        }

        internal override void AddToHash(ref Fnv64 h)
        {
            h.Add(Progress);
            h.Add(Output);
            h.Add(_cursor);
        }
    }
}
