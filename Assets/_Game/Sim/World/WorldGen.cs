namespace Cogworks.Sim
{
    /// <summary>Fills chunks with terrain and ore. Output depends only on (seed, chunk coord).</summary>
    public static class WorldGen
    {
        // Elevation bands (noise is 0..65535, centered around 32768).
        const int WaterBelow = 25000;
        const int SandBelow = 27000;
        const int RockAbove = 42000;
        const int GrassAboveMoisture = 31000;

        // Radius around the origin that is guaranteed dry, so players never spawn in water.
        const int SpawnRadius = 40;

        const int OreThreshold = 51000;
        // Ore noise lattice spacing in tiles; larger spreads patches farther apart.
        const int OreCell = 48;
        const int OreBaseAmount = 400;

        static readonly uint[] OreSalts = { 0, 0xA1B2C3D4u, 0x5E6F7081u, 0x92A3B4C5u, 0xD6E7F809u };

        // One guaranteed patch per ore near spawn so every seed has a playable start.
        // Offsets are rotated/mirrored per seed; index = ore - 1.
        static readonly Int2[] StarterOffsets = { new Int2(24, 8), new Int2(-8, 24), new Int2(-24, -8), new Int2(8, -24) };
        const int StarterRadiusSq = 30;
        const int StarterBaseAmount = 800;

        public static void Generate(Chunk chunk, uint seed)
        {
            uint elevationSeed = seed;
            uint moistureSeed = seed ^ 0x3C6EF372u;
            Int2 origin = chunk.Origin;

            for (int ly = 0; ly < Chunk.Size; ly++)
            for (int lx = 0; lx < Chunk.Size; lx++)
            {
                int wx = origin.X + lx;
                int wy = origin.Y + ly;
                int i = Chunk.Index(lx, ly);

                int elevation = Noise.Fractal(wx, wy, 64, 4, elevationSeed);
                int distSq = wx * wx + wy * wy;
                if (distSq < SpawnRadius * SpawnRadius)
                    elevation = IntMath.Max(elevation, SandBelow + (SpawnRadius * SpawnRadius - distSq) * 4);

                TerrainType terrain;
                if (elevation < WaterBelow) terrain = TerrainType.Water;
                else if (elevation < SandBelow) terrain = TerrainType.Sand;
                else if (elevation > RockAbove) terrain = TerrainType.Rock;
                else terrain = Noise.Fractal(wx, wy, 48, 3, moistureSeed) > GrassAboveMoisture ? TerrainType.Grass : TerrainType.Dirt;

                chunk.Terrain[i] = terrain;
                chunk.Ore[i] = Ore.None;
                chunk.OreAmount[i] = 0;
                if (terrain == TerrainType.Water) continue;

                // Each ore has its own noise field; the strongest one above threshold wins the tile.
                int bestMargin = 0;
                for (int o = 1; o < OreSalts.Length; o++)
                {
                    int n = Noise.Fractal(wx, wy, OreCell, 3, seed ^ OreSalts[o]);
                    int margin = n - OreThreshold;
                    if (margin > bestMargin)
                    {
                        bestMargin = margin;
                        chunk.Ore[i] = (Ore)o;
                    }
                }
                if (chunk.Ore[i] != Ore.None)
                    chunk.OreAmount[i] = OreBaseAmount + bestMargin / 4;

                ApplyStarterPatches(chunk, i, wx, wy, seed);
            }
        }

        static void ApplyStarterPatches(Chunk chunk, int i, int wx, int wy, uint seed)
        {
            // Cheap reject: starter patches all sit within ~32 tiles of the origin.
            if (wx < -40 || wx > 40 || wy < -40 || wy > 40) return;

            int quarterTurns = (int)(seed & 3);
            bool mirror = (seed & 4) != 0;
            for (int o = 0; o < StarterOffsets.Length; o++)
            {
                Int2 c = Orient(StarterOffsets[o], quarterTurns, mirror);
                int dx = wx - c.X;
                int dy = wy - c.Y;
                int distSq = dx * dx + dy * dy;
                // Per-tile jitter on the radius gives a ragged, natural-looking edge.
                int radiusSq = StarterRadiusSq + (int)(Noise.Hash(wx, wy, seed ^ 0x51ED270Bu) % 12);
                if (distSq >= radiusSq) continue;

                chunk.Ore[i] = (Ore)(o + 1);
                chunk.OreAmount[i] = StarterBaseAmount + (radiusSq - distSq) * 20;
                return;
            }
        }

        static Int2 Orient(Int2 v, int quarterTurns, bool mirror)
        {
            int x = mirror ? -v.X : v.X;
            int y = v.Y;
            for (int r = 0; r < quarterTurns; r++)
            {
                int t = x;
                x = -y;
                y = t;
            }
            return new Int2(x, y);
        }
    }
}
