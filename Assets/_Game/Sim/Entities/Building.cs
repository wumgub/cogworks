namespace Cogworks.Sim
{
    /// <summary>A placed building instance. <see cref="Origin"/> is the bottom-left tile of the footprint.</summary>
    public sealed class Building
    {
        public readonly int Id;
        public readonly BuildingSpec Spec;
        public readonly Int2 Origin;
        public Direction Direction { get; internal set; }

        internal Building(int id, BuildingSpec spec, Int2 origin, Direction direction)
        {
            Id = id;
            Spec = spec;
            Origin = origin;
            Direction = direction;
        }

        public Int2 Size => Spec.SizeFor(Direction);

        public bool Contains(Int2 tile)
        {
            Int2 size = Size;
            return tile.X >= Origin.X && tile.X < Origin.X + size.X
                && tile.Y >= Origin.Y && tile.Y < Origin.Y + size.Y;
        }
    }
}
