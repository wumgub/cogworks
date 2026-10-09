using System.Collections.Generic;

namespace Cogworks.Sim
{
    /// <summary>Where a recipe can be made.</summary>
    public enum CraftStation : byte
    {
        /// <summary>Furnaces only (smelting).</summary>
        Furnace,
        /// <summary>Assemblers, and also by hand from the player inventory.</summary>
        Assembler,
    }

    public sealed class Recipe
    {
        public readonly ushort Id;
        public readonly string Key;
        public readonly CraftStation Station;
        public readonly ItemStack[] Inputs;
        public readonly ItemStack Output;
        /// <summary>Ticks to craft at base speed (hand crafting, or a machine at 32 rpm).</summary>
        public readonly int Ticks;

        public Recipe(ushort id, string key, CraftStation station, int ticks, ItemStack output, params ItemStack[] inputs)
        {
            Id = id;
            Key = key;
            Station = station;
            Ticks = ticks;
            Output = output;
            Inputs = inputs;
        }

        public string DisplayName => ItemCatalog.NameOf(Output.Item) + (Output.Count > 1 ? $" x{Output.Count}" : "");

        public int InputCount(ushort item)
        {
            foreach (var s in Inputs)
                if (s.Item == item) return s.Count;
            return 0;
        }
    }

    public static class RecipeCatalog
    {
        static ItemStack S(ItemSpec item, int count = 1) => new ItemStack(item.Id, count);
        static ItemStack B(BuildingSpec building, int count = 1) => new ItemStack(building.ItemId, count);

        const int Second = Simulation.TicksPerSecond;

        // Smelting
        public static readonly Recipe IronPlate = new Recipe(1, "iron_plate", CraftStation.Furnace, Second * 16 / 5,
            S(ItemCatalog.IronPlate), S(ItemCatalog.IronOre));
        public static readonly Recipe CopperPlate = new Recipe(2, "copper_plate", CraftStation.Furnace, Second * 16 / 5,
            S(ItemCatalog.CopperPlate), S(ItemCatalog.CopperOre));
        public static readonly Recipe StoneBrick = new Recipe(3, "stone_brick", CraftStation.Furnace, Second * 16 / 5,
            S(ItemCatalog.StoneBrick), S(ItemCatalog.Stone, 2));

        // Intermediates
        public static readonly Recipe IronGear = new Recipe(10, "iron_gear", CraftStation.Assembler, Second / 2,
            S(ItemCatalog.IronGear), S(ItemCatalog.IronPlate, 2));
        public static readonly Recipe CopperWire = new Recipe(11, "copper_wire", CraftStation.Assembler, Second / 2,
            S(ItemCatalog.CopperWire, 2), S(ItemCatalog.CopperPlate));
        public static readonly Recipe Circuit = new Recipe(12, "circuit", CraftStation.Assembler, Second,
            S(ItemCatalog.Circuit), S(ItemCatalog.IronPlate), S(ItemCatalog.CopperWire, 3));
        public static readonly Recipe RedScience = new Recipe(13, "science_red", CraftStation.Assembler, Second * 5,
            S(ItemCatalog.RedScience), S(ItemCatalog.CopperPlate), S(ItemCatalog.IronGear));
        public static readonly Recipe GreenScience = new Recipe(14, "science_green", CraftStation.Assembler, Second * 6,
            S(ItemCatalog.GreenScience), B(BuildingCatalog.Inserter), B(BuildingCatalog.Conveyor));

        // Buildings
        public static readonly Recipe Conveyor = new Recipe(20, "conveyor", CraftStation.Assembler, Second / 2,
            B(BuildingCatalog.Conveyor, 2), S(ItemCatalog.IronGear), S(ItemCatalog.IronPlate));
        public static readonly Recipe Inserter = new Recipe(21, "inserter", CraftStation.Assembler, Second / 2,
            B(BuildingCatalog.Inserter), S(ItemCatalog.Circuit), S(ItemCatalog.IronGear), S(ItemCatalog.IronPlate));
        public static readonly Recipe Chest = new Recipe(22, "chest", CraftStation.Assembler, Second / 2,
            B(BuildingCatalog.Chest), S(ItemCatalog.IronPlate, 4));
        public static readonly Recipe Drill = new Recipe(23, "drill", CraftStation.Assembler, Second * 2,
            B(BuildingCatalog.Drill), S(ItemCatalog.IronGear, 3), S(ItemCatalog.IronPlate, 6), S(ItemCatalog.StoneBrick, 2));
        public static readonly Recipe Furnace = new Recipe(24, "furnace", CraftStation.Assembler, Second / 2,
            B(BuildingCatalog.Furnace), S(ItemCatalog.Stone, 5));
        public static readonly Recipe Assembler = new Recipe(25, "assembler", CraftStation.Assembler, Second / 2,
            B(BuildingCatalog.Assembler), S(ItemCatalog.Circuit, 3), S(ItemCatalog.IronGear, 5), S(ItemCatalog.IronPlate, 9));
        public static readonly Recipe Shaft = new Recipe(26, "shaft", CraftStation.Assembler, Second / 2,
            B(BuildingCatalog.Shaft, 4), S(ItemCatalog.IronPlate));
        public static readonly Recipe Gearbox = new Recipe(27, "gearbox", CraftStation.Assembler, Second / 2,
            B(BuildingCatalog.Gearbox), S(ItemCatalog.IronGear, 4), S(ItemCatalog.IronPlate, 2));
        public static readonly Recipe HandCrank = new Recipe(28, "hand_crank", CraftStation.Assembler, Second / 2,
            B(BuildingCatalog.HandCrank), S(ItemCatalog.IronPlate, 2), S(ItemCatalog.IronGear));
        public static readonly Recipe WaterWheel = new Recipe(29, "water_wheel", CraftStation.Assembler, Second,
            B(BuildingCatalog.WaterWheel), S(ItemCatalog.IronPlate, 6), S(ItemCatalog.IronGear, 4), S(ItemCatalog.StoneBrick, 4));
        public static readonly Recipe SteamEngine = new Recipe(30, "steam_engine", CraftStation.Assembler, Second * 2,
            B(BuildingCatalog.SteamEngine), S(ItemCatalog.IronPlate, 10), S(ItemCatalog.IronGear, 8), S(ItemCatalog.StoneBrick, 5), S(ItemCatalog.Circuit, 2));
        public static readonly Recipe Lab = new Recipe(31, "lab", CraftStation.Assembler, Second * 2,
            B(BuildingCatalog.Lab), S(ItemCatalog.Circuit, 10), S(ItemCatalog.IronGear, 10), B(BuildingCatalog.Conveyor, 4));
        public static readonly Recipe FastConveyor = new Recipe(32, "fast_conveyor", CraftStation.Assembler, Second / 2,
            B(BuildingCatalog.FastConveyor), B(BuildingCatalog.Conveyor), S(ItemCatalog.IronGear, 5));

        public static readonly IReadOnlyList<Recipe> All = new[]
        {
            IronPlate, CopperPlate, StoneBrick,
            IronGear, CopperWire, Circuit, RedScience, GreenScience,
            Conveyor, Inserter, Chest, Drill, Furnace, Assembler, Shaft, Gearbox, HandCrank,
            WaterWheel, SteamEngine, Lab, FastConveyor,
        };

        static readonly Dictionary<ushort, Recipe> ById = BuildIndex();

        public static Recipe Get(ushort id) => ById.TryGetValue(id, out var r) ? r : null;

        /// <summary>The smelting recipe a furnace uses for this input item, or null.</summary>
        public static Recipe SmeltingFor(ushort input)
        {
            foreach (var r in All)
                if (r.Station == CraftStation.Furnace && r.Inputs[0].Item == input) return r;
            return null;
        }

        static Dictionary<ushort, Recipe> BuildIndex()
        {
            var map = new Dictionary<ushort, Recipe>();
            foreach (var r in All) map.Add(r.Id, r);
            return map;
        }
    }
}
