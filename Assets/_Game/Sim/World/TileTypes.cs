namespace Cogworks.Sim
{
    public enum TerrainType : byte
    {
        Water,
        Sand,
        Grass,
        Dirt,
        Rock,
    }

    public enum Ore : byte
    {
        None,
        Iron,
        Copper,
        Coal,
        Stone,
    }

    public static class TerrainInfo
    {
        public static bool IsWalkable(TerrainType t) => t != TerrainType.Water;
        public static bool IsBuildable(TerrainType t) => t != TerrainType.Water;
    }
}
