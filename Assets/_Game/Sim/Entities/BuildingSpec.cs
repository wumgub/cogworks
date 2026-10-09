namespace Cogworks.Sim
{
    /// <summary>What a building does; picks its <see cref="BuildingLogic"/>.</summary>
    public enum BuildingKind : byte
    {
        Conveyor,
        Inserter,
        Chest,
        Drill,
        Furnace,
        Assembler,
        Shaft,
        Gearbox,
        Generator,
        Lab,
        Splitter,
        UndergroundEntrance,
        UndergroundExit,
    }

    /// <summary>How a building takes part in rotational-power networks.</summary>
    public enum KineticRole : byte
    {
        None,
        /// <summary>Connects only along its axis (facing North/South = vertical, East/West = horizontal).</summary>
        Shaft,
        /// <summary>Connects on all sides; no stress.</summary>
        Gearbox,
        Generator,
        Consumer,
    }

    /// <summary>Static definition of a building type. Ids are stable: they go into saves and network messages.</summary>
    public sealed class BuildingSpec
    {
        public readonly ushort Id;
        public readonly string Key;
        public readonly string DisplayName;
        public readonly BuildingKind Kind;
        public readonly int Width;
        public readonly int Height;
        public readonly bool Rotatable;
        /// <summary>At least one footprint tile must contain ore.</summary>
        public readonly bool RequiresOre;
        /// <summary>At least one tile next to the footprint must be water.</summary>
        public readonly bool RequiresAdjacentWater;
        /// <summary>When drag-placing, face the direction of the drag (conveyors, shafts).</summary>
        public readonly bool OrientOnDrag;
        /// <summary>Blocks player movement. Conveyors are walkable so players can step over them.</summary>
        public readonly bool Solid;
        /// <summary>
        /// Lives on the overhead layer (shafts): can share a tile with a belt underneath, but never with
        /// a machine.
        /// </summary>
        public readonly bool Overhead;

        public KineticRole Kinetic { get; private set; }
        /// <summary>Generators: rotation speed supplied.</summary>
        public int GeneratorRpm { get; private set; }
        /// <summary>Generators: stress capacity supplied (SU).</summary>
        public int StressCapacity { get; private set; }
        /// <summary>Consumers: stress used per RPM (SU/rpm).</summary>
        public int StressImpact { get; private set; }
        /// <summary>Conveyors: movement per tick in belt units (256 = one tile).</summary>
        public int BeltSpeed { get; private set; }

        public BuildingSpec(ushort id, string key, string displayName, BuildingKind kind, int width, int height,
            bool rotatable = true, bool requiresOre = false, bool orientOnDrag = false, bool solid = true,
            bool requiresAdjacentWater = false, bool overhead = false)
        {
            Id = id;
            Key = key;
            DisplayName = displayName;
            Kind = kind;
            Width = width;
            Height = height;
            Rotatable = rotatable;
            RequiresOre = requiresOre;
            OrientOnDrag = orientOnDrag;
            Solid = solid;
            RequiresAdjacentWater = requiresAdjacentWater;
            Overhead = overhead;
        }

        /// <summary>Belts, splitters and undergrounds: the only ground buildings allowed under an overhead shaft.</summary>
        public bool IsBeltFamily => Kind == BuildingKind.Conveyor || Kind == BuildingKind.Splitter
            || Kind == BuildingKind.UndergroundEntrance || Kind == BuildingKind.UndergroundExit;

        /// <summary>The item that places this building.</summary>
        public ushort ItemId => (ushort)(100 + Id);

        internal BuildingSpec AsGenerator(int rpm, int capacity)
        {
            Kinetic = KineticRole.Generator;
            GeneratorRpm = rpm;
            StressCapacity = capacity;
            return this;
        }

        internal BuildingSpec AsConsumer(int impact)
        {
            Kinetic = KineticRole.Consumer;
            StressImpact = impact;
            return this;
        }

        internal BuildingSpec AsKinetic(KineticRole role)
        {
            Kinetic = role;
            return this;
        }

        internal BuildingSpec WithBeltSpeed(int speed)
        {
            BeltSpeed = speed;
            return this;
        }

        /// <summary>Footprint size after rotation (width/height swap when facing East or West).</summary>
        public Int2 SizeFor(Direction d) =>
            d == Direction.East || d == Direction.West ? new Int2(Height, Width) : new Int2(Width, Height);
    }
}
