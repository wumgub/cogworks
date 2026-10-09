using System.Collections.Generic;

namespace Cogworks.Sim
{
    /// <summary>Static definition of an item type. Ids are stable (saves, network).</summary>
    public sealed class ItemSpec
    {
        public readonly ushort Id;
        public readonly string Key;
        public readonly string DisplayName;
        public readonly int StackSize;
        /// <summary>Building this item places, if any.</summary>
        public BuildingSpec Places { get; internal set; }
        /// <summary>Burn time in ticks when used as fuel; 0 if not a fuel.</summary>
        public readonly int FuelTicks;

        public ItemSpec(ushort id, string key, string displayName, int stackSize = 50, int fuelTicks = 0)
        {
            Id = id;
            Key = key;
            DisplayName = displayName;
            StackSize = stackSize;
            FuelTicks = fuelTicks;
        }
    }

    public static class ItemCatalog
    {
        public const ushort None = 0;

        // Raw resources
        public static readonly ItemSpec IronOre = new ItemSpec(1, "iron_ore", "Iron Ore");
        public static readonly ItemSpec CopperOre = new ItemSpec(2, "copper_ore", "Copper Ore");
        public static readonly ItemSpec Coal = new ItemSpec(3, "coal", "Coal", fuelTicks: 960);
        public static readonly ItemSpec Stone = new ItemSpec(4, "stone", "Stone");
        // Intermediates
        public static readonly ItemSpec IronPlate = new ItemSpec(5, "iron_plate", "Iron Plate", 100);
        public static readonly ItemSpec CopperPlate = new ItemSpec(6, "copper_plate", "Copper Plate", 100);
        public static readonly ItemSpec StoneBrick = new ItemSpec(7, "stone_brick", "Stone Brick", 100);
        public static readonly ItemSpec IronGear = new ItemSpec(8, "iron_gear", "Iron Gear", 100);
        public static readonly ItemSpec CopperWire = new ItemSpec(9, "copper_wire", "Copper Wire", 200);
        public static readonly ItemSpec Circuit = new ItemSpec(10, "circuit", "Circuit", 100);
        public static readonly ItemSpec RedScience = new ItemSpec(11, "science_red", "Automation Science", 100);
        public static readonly ItemSpec GreenScience = new ItemSpec(12, "science_green", "Logistic Science", 100);

        // Building items are 100 + building id (see BuildingSpec.ItemId) so the mapping is obvious in saves.

        public static readonly IReadOnlyList<ItemSpec> All;
        static readonly Dictionary<ushort, ItemSpec> ById = new Dictionary<ushort, ItemSpec>();

        static ItemCatalog()
        {
            var list = new List<ItemSpec>
            {
                IronOre, CopperOre, Coal, Stone, IronPlate, CopperPlate, StoneBrick,
                IronGear, CopperWire, Circuit, RedScience, GreenScience,
            };
            foreach (var b in BuildingCatalog.All)
            {
                list.Add(new ItemSpec(b.ItemId, b.Key, b.DisplayName) { Places = b });
            }
            foreach (var item in list) ById.Add(item.Id, item); // throws on duplicate ids
            All = list;
        }

        public static ItemSpec Get(ushort id) => ById.TryGetValue(id, out var spec) ? spec : null;

        public static string NameOf(ushort id) => Get(id)?.DisplayName ?? "?";

        public static int StackSizeOf(ushort id) => Get(id)?.StackSize ?? 50;

        /// <summary>The item a mined tile of this ore yields.</summary>
        public static ItemSpec FromOre(Ore ore) => ore switch
        {
            Ore.Iron => IronOre,
            Ore.Copper => CopperOre,
            Ore.Coal => Coal,
            Ore.Stone => Stone,
            _ => null,
        };

        public static ItemSpec ForBuilding(BuildingSpec spec) => Get(spec.ItemId);
    }
}
