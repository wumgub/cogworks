using System.Collections.Generic;

namespace Cogworks.Sim
{
    /// <summary>A placed building instance. <see cref="Origin"/> is the bottom-left tile of the footprint.</summary>
    public sealed class Building
    {
        public readonly int Id;
        public readonly BuildingSpec Spec;
        public readonly Int2 Origin;
        public Direction Direction { get; internal set; }
        public BuildingLogic Logic { get; }

        internal Building(int id, BuildingSpec spec, Int2 origin, Direction direction)
        {
            Id = id;
            Spec = spec;
            Origin = origin;
            Direction = direction;
            Logic = BuildingLogic.Create(spec);
            Logic.Building = this;
        }

        public Int2 Size => Spec.SizeFor(Direction);

        public bool Contains(Int2 tile)
        {
            Int2 size = Size;
            return tile.X >= Origin.X && tile.X < Origin.X + size.X
                && tile.Y >= Origin.Y && tile.Y < Origin.Y + size.Y;
        }

        /// <summary>Footprint tiles, row by row from the origin.</summary>
        public IEnumerable<Int2> Footprint()
        {
            Int2 size = Size;
            for (int y = 0; y < size.Y; y++)
            for (int x = 0; x < size.X; x++)
                yield return new Int2(Origin.X + x, Origin.Y + y);
        }

        /// <summary>The tile just outside the middle of the facing edge (where outputs go).</summary>
        public Int2 FrontTile() => EdgeTile(Direction);

        /// <summary>The tile just outside the middle of the back edge (where inserters pick up).</summary>
        public Int2 BackTile() => EdgeTile(Direction.Opposite());

        Int2 EdgeTile(Direction side)
        {
            Int2 s = Size;
            return side switch
            {
                Direction.North => new Int2(Origin.X + (s.X - 1) / 2, Origin.Y + s.Y),
                Direction.East => new Int2(Origin.X + s.X, Origin.Y + (s.Y - 1) / 2),
                Direction.South => new Int2(Origin.X + (s.X - 1) / 2, Origin.Y - 1),
                _ => new Int2(Origin.X - 1, Origin.Y + (s.Y - 1) / 2),
            };
        }

        /// <summary>Every tile touching the footprint's edges, with the side it touches (N, E, S, W order).</summary>
        public IEnumerable<(Int2 tile, Direction side)> Neighbours()
        {
            Int2 s = Size;
            for (int x = 0; x < s.X; x++) yield return (new Int2(Origin.X + x, Origin.Y + s.Y), Direction.North);
            for (int y = 0; y < s.Y; y++) yield return (new Int2(Origin.X + s.X, Origin.Y + y), Direction.East);
            for (int x = 0; x < s.X; x++) yield return (new Int2(Origin.X + x, Origin.Y - 1), Direction.South);
            for (int y = 0; y < s.Y; y++) yield return (new Int2(Origin.X - 1, Origin.Y + y), Direction.West);
        }
    }
}
