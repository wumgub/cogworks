using System.Collections.Generic;

namespace Cogworks.Sim
{
    /// <summary>A research project. Labs consume <see cref="Cost"/> science packs per pack type to finish it.</summary>
    public sealed class Tech
    {
        public readonly ushort Id;
        public readonly string Key;
        public readonly string DisplayName;
        public readonly string Description;
        public readonly ItemStack[] Cost;
        public readonly Tech[] Prerequisites;
        public readonly Recipe[] Unlocks;

        public Tech(ushort id, string key, string displayName, string description, ItemStack[] cost, Tech[] prerequisites, params Recipe[] unlocks)
        {
            Id = id;
            Key = key;
            DisplayName = displayName;
            Description = description;
            Cost = cost;
            Prerequisites = prerequisites;
            Unlocks = unlocks;
        }
    }

    public static class TechCatalog
    {
        static ItemStack[] Red(int n) => new[] { new ItemStack(ItemCatalog.RedScience.Id, n) };
        static ItemStack[] RedGreen(int r, int g) => new[] { new ItemStack(ItemCatalog.RedScience.Id, r), new ItemStack(ItemCatalog.GreenScience.Id, g) };
        static readonly Tech[] None = new Tech[0];

        public static readonly Tech Automation = new Tech(1, "automation", "Automation",
            "Mechanical assemblers craft for you when given rotational power.",
            Red(10), None, RecipeCatalog.Assembler);
        // Id 2 was "Hydropower"; water wheels are now available from the start so early power isn't all cranking.
        public static readonly Tech Logistics = new Tech(6, "logistics", "Logistics",
            "Splitters share a belt between outputs; underground belts tunnel up to 5 tiles under anything.",
            Red(20), None, RecipeCatalog.Splitter, RecipeCatalog.UndergroundEntrance, RecipeCatalog.UndergroundExit);
        public static readonly Tech SteamPower = new Tech(3, "steam_power", "Steam Power",
            "Coal-fired engines spin twice as fast as water wheels, with four times the stress capacity.",
            Red(30), new[] { Automation }, RecipeCatalog.SteamEngine);
        public static readonly Tech LogisticScience = new Tech(4, "logistic_science", "Logistic Science",
            "Unlocks green science packs for advanced research.",
            Red(30), new[] { Automation }, RecipeCatalog.GreenScience);
        public static readonly Tech Logistics2 = new Tech(5, "logistics_2", "Logistics 2",
            "Fast conveyor belts move items twice as fast.",
            RedGreen(40, 20), new[] { LogisticScience, Logistics }, RecipeCatalog.FastConveyor);

        public static readonly IReadOnlyList<Tech> All = new[] { Automation, Logistics, SteamPower, LogisticScience, Logistics2 };

        static readonly Dictionary<ushort, Tech> ById = BuildIndex();
        static readonly HashSet<ushort> LockedRecipes = BuildLocked();

        public static Tech Get(ushort id) => ById.TryGetValue(id, out var t) ? t : null;

        /// <summary>True if some tech has to be researched before this recipe can be made.</summary>
        public static bool IsLockedByDefault(Recipe recipe) => LockedRecipes.Contains(recipe.Id);

        static Dictionary<ushort, Tech> BuildIndex()
        {
            var map = new Dictionary<ushort, Tech>();
            foreach (var t in All) map.Add(t.Id, t);
            return map;
        }

        static HashSet<ushort> BuildLocked()
        {
            var set = new HashSet<ushort>();
            foreach (var t in All)
            foreach (var r in t.Unlocks)
                set.Add(r.Id);
            return set;
        }
    }
}
