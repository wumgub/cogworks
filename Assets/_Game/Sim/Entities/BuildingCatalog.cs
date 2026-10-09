using System.Collections.Generic;

namespace Cogworks.Sim
{
    /// <summary>
    /// Every building type in the game. Behavior arrives in later milestones (logistics, crafting, kinetic);
    /// for now these define footprints and placement rules. Never renumber an Id once shipped.
    /// </summary>
    public static class BuildingCatalog
    {
        public static readonly BuildingSpec Conveyor = new BuildingSpec(1, "conveyor", "Conveyor Belt", 1, 1, orientOnDrag: true, solid: false);
        public static readonly BuildingSpec Inserter = new BuildingSpec(2, "inserter", "Inserter", 1, 1);
        public static readonly BuildingSpec Chest = new BuildingSpec(3, "chest", "Wooden Chest", 1, 1, rotatable: false);
        public static readonly BuildingSpec Drill = new BuildingSpec(4, "drill", "Mechanical Drill", 2, 2, requiresOre: true);
        public static readonly BuildingSpec Furnace = new BuildingSpec(5, "furnace", "Stone Furnace", 2, 2);
        public static readonly BuildingSpec Assembler = new BuildingSpec(6, "assembler", "Assembler", 3, 3);
        public static readonly BuildingSpec Shaft = new BuildingSpec(7, "shaft", "Shaft", 1, 1, orientOnDrag: true);
        public static readonly BuildingSpec HandCrank = new BuildingSpec(8, "hand_crank", "Hand Crank", 1, 1);

        /// <summary>In hotbar order.</summary>
        public static readonly IReadOnlyList<BuildingSpec> All = new[]
        {
            Conveyor, Inserter, Chest, Drill, Furnace, Assembler, Shaft, HandCrank,
        };

        static readonly Dictionary<ushort, BuildingSpec> ById = BuildIndex();

        public static BuildingSpec Get(ushort id) => ById.TryGetValue(id, out var spec) ? spec : null;

        static Dictionary<ushort, BuildingSpec> BuildIndex()
        {
            var map = new Dictionary<ushort, BuildingSpec>();
            foreach (var spec in All) map.Add(spec.Id, spec); // throws on duplicate ids
            return map;
        }
    }
}
