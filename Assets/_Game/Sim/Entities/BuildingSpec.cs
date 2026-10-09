namespace Cogworks.Sim
{
    /// <summary>Static definition of a building type. Ids are stable: they go into saves and network messages.</summary>
    public sealed class BuildingSpec
    {
        public readonly ushort Id;
        public readonly string Key;
        public readonly string DisplayName;
        public readonly int Width;
        public readonly int Height;
        public readonly bool Rotatable;
        /// <summary>At least one footprint tile must contain ore.</summary>
        public readonly bool RequiresOre;
        /// <summary>When drag-placing, face the direction of the drag (conveyors, shafts).</summary>
        public readonly bool OrientOnDrag;
        /// <summary>Blocks player movement. Conveyors are walkable so players can ride/step over them.</summary>
        public readonly bool Solid;

        public BuildingSpec(ushort id, string key, string displayName, int width, int height,
            bool rotatable = true, bool requiresOre = false, bool orientOnDrag = false, bool solid = true)
        {
            Id = id;
            Key = key;
            DisplayName = displayName;
            Width = width;
            Height = height;
            Rotatable = rotatable;
            RequiresOre = requiresOre;
            OrientOnDrag = orientOnDrag;
            Solid = solid;
        }

        /// <summary>Footprint size after rotation (width/height swap when facing East or West).</summary>
        public Int2 SizeFor(Direction d) =>
            d == Direction.East || d == Direction.West ? new Int2(Height, Width) : new Int2(Width, Height);
    }
}
