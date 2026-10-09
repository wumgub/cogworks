using System.Collections.Generic;

namespace Cogworks.Sim
{
    /// <summary>Every building type in the game. Never renumber an Id once shipped.</summary>
    public static class BuildingCatalog
    {
        public static readonly BuildingSpec Conveyor = new BuildingSpec(1, "conveyor", "Conveyor Belt", BuildingKind.Conveyor, 1, 1,
            orientOnDrag: true, solid: false).WithBeltSpeed(8);
        public static readonly BuildingSpec Inserter = new BuildingSpec(2, "inserter", "Inserter", BuildingKind.Inserter, 1, 1);
        public static readonly BuildingSpec Chest = new BuildingSpec(3, "chest", "Iron Chest", BuildingKind.Chest, 1, 1, rotatable: false);
        public static readonly BuildingSpec Drill = new BuildingSpec(4, "drill", "Mechanical Drill", BuildingKind.Drill, 2, 2,
            requiresOre: true).AsConsumer(impact: 4);
        public static readonly BuildingSpec Furnace = new BuildingSpec(5, "furnace", "Stone Furnace", BuildingKind.Furnace, 2, 2, rotatable: false);
        public static readonly BuildingSpec Assembler = new BuildingSpec(6, "assembler", "Mechanical Assembler", BuildingKind.Assembler, 3, 3,
            rotatable: false).AsConsumer(impact: 8);
        public static readonly BuildingSpec Shaft = new BuildingSpec(7, "shaft", "Shaft", BuildingKind.Shaft, 1, 1,
            orientOnDrag: true).AsKinetic(KineticRole.Shaft);
        public static readonly BuildingSpec HandCrank = new BuildingSpec(8, "hand_crank", "Hand Crank", BuildingKind.Generator, 1, 1,
            rotatable: false).AsGenerator(rpm: 32, capacity: 160);
        public static readonly BuildingSpec Gearbox = new BuildingSpec(9, "gearbox", "Gearbox", BuildingKind.Gearbox, 1, 1,
            rotatable: false).AsKinetic(KineticRole.Gearbox);
        public static readonly BuildingSpec WaterWheel = new BuildingSpec(10, "water_wheel", "Water Wheel", BuildingKind.Generator, 1, 1,
            rotatable: false, requiresAdjacentWater: true).AsGenerator(rpm: 32, capacity: 512);
        public static readonly BuildingSpec SteamEngine = new BuildingSpec(11, "steam_engine", "Steam Engine", BuildingKind.Generator, 2, 2,
            rotatable: false).AsGenerator(rpm: 64, capacity: 2048);
        public static readonly BuildingSpec Lab = new BuildingSpec(12, "lab", "Research Lab", BuildingKind.Lab, 2, 2, rotatable: false);
        public static readonly BuildingSpec FastConveyor = new BuildingSpec(13, "fast_conveyor", "Fast Conveyor Belt", BuildingKind.Conveyor, 1, 1,
            orientOnDrag: true, solid: false).WithBeltSpeed(16);
        public static readonly BuildingSpec Splitter = new BuildingSpec(14, "splitter", "Splitter", BuildingKind.Splitter, 1, 1, solid: false);
        public static readonly BuildingSpec UndergroundEntrance = new BuildingSpec(15, "underground_in", "Underground Belt (in)",
            BuildingKind.UndergroundEntrance, 1, 1, solid: false);
        public static readonly BuildingSpec UndergroundExit = new BuildingSpec(16, "underground_out", "Underground Belt (out)",
            BuildingKind.UndergroundExit, 1, 1, solid: false);

        /// <summary>Default hotbar order.</summary>
        public static readonly IReadOnlyList<BuildingSpec> All = new[]
        {
            Conveyor, Inserter, Chest, Drill, Furnace, Assembler, Shaft, Gearbox, HandCrank,
            WaterWheel, SteamEngine, Lab, FastConveyor, Splitter, UndergroundEntrance, UndergroundExit,
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
