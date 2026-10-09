namespace Cogworks.Sim
{
    /// <summary>Cardinal facing. Values are clockwise quarter-turns from North.</summary>
    public enum Direction : byte
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    public static class DirectionExt
    {
        public static Direction RotateCw(this Direction d) => (Direction)(((int)d + 1) & 3);
        public static Direction RotateCcw(this Direction d) => (Direction)(((int)d + 3) & 3);

        public static Int2 ToOffset(this Direction d) => d switch
        {
            Direction.North => new Int2(0, 1),
            Direction.East => new Int2(1, 0),
            Direction.South => new Int2(0, -1),
            _ => new Int2(-1, 0),
        };

        /// <summary>Direction of a unit step between orthogonally adjacent tiles, or null otherwise.</summary>
        public static Direction? FromStep(Int2 step)
        {
            if (step == new Int2(0, 1)) return Direction.North;
            if (step == new Int2(1, 0)) return Direction.East;
            if (step == new Int2(0, -1)) return Direction.South;
            if (step == new Int2(-1, 0)) return Direction.West;
            return null;
        }
    }
}
