using NUnit.Framework;

namespace Cogworks.Sim.Tests
{
    public class WorldGenTests
    {
        static readonly Int2[] SampleChunks =
        {
            new Int2(0, 0), new Int2(-1, -1), new Int2(5, -3), new Int2(-40, 17), new Int2(1000, 1000),
        };

        [Test]
        public void SameSeed_GeneratesIdenticalChunks_RegardlessOfOrder()
        {
            var a = new World(12345);
            var b = new World(12345);
            for (int i = SampleChunks.Length - 1; i >= 0; i--) b.GetChunk(SampleChunks[i]);

            foreach (var coord in SampleChunks)
            {
                var ca = a.GetChunk(coord);
                var cb = b.GetChunk(coord);
                CollectionAssert.AreEqual(ca.Terrain, cb.Terrain, $"terrain {coord}");
                CollectionAssert.AreEqual(ca.Ore, cb.Ore, $"ore {coord}");
                CollectionAssert.AreEqual(ca.OreAmount, cb.OreAmount, $"amount {coord}");
            }
        }

        [Test]
        public void DifferentSeeds_GenerateDifferentTerrain()
        {
            var a = new World(1).GetChunk(new Int2(3, 3));
            var b = new World(2).GetChunk(new Int2(3, 3));
            CollectionAssert.AreNotEqual(a.Terrain, b.Terrain);
        }

        [TestCase(1u)]
        [TestCase(12345u)]
        [TestCase(987654321u)]
        public void SpawnArea_IsWalkable(uint seed)
        {
            var world = new World(seed);
            for (int y = -12; y <= 12; y++)
            for (int x = -12; x <= 12; x++)
                Assert.IsTrue(TerrainInfo.IsWalkable(world.GetTerrain(new Int2(x, y))), $"seed {seed} tile ({x},{y})");
        }

        [Test]
        public void Region_ContainsEveryTerrainAndOre()
        {
            var world = new World(12345);
            var terrainSeen = new bool[5];
            var oreSeen = new bool[5];
            for (int y = -256; y < 256; y += 2)
            for (int x = -256; x < 256; x += 2)
            {
                var tile = new Int2(x, y);
                terrainSeen[(int)world.GetTerrain(tile)] = true;
                oreSeen[(int)world.GetOre(tile, out _)] = true;
            }
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(terrainSeen[i], $"terrain {(TerrainType)i} never generated");
                Assert.IsTrue(oreSeen[i], $"ore {(Ore)i} never generated");
            }
        }

        [TestCase(1u)]
        [TestCase(12345u)]
        [TestCase(987654321u)]
        public void EveryOre_HasStarterPatchNearSpawn(uint seed)
        {
            var world = new World(seed);
            var seen = new bool[5];
            for (int y = -36; y <= 36; y++)
            for (int x = -36; x <= 36; x++)
                seen[(int)world.GetOre(new Int2(x, y), out _)] = true;
            for (int o = 1; o < 5; o++)
                Assert.IsTrue(seen[o], $"seed {seed}: no {(Ore)o} near spawn");
        }

        [Test]
        public void OreTiles_HaveAmount_AndOthersDoNot()
        {
            var chunk = new World(12345).GetChunk(new Int2(2, -2));
            for (int i = 0; i < Chunk.Area; i++)
                Assert.AreEqual(chunk.Ore[i] != Ore.None, chunk.OreAmount[i] > 0);
        }
    }

    public class CoreTests
    {
        [TestCase(-1, -1, -1, -1, 31, 31)]
        [TestCase(0, 0, 0, 0, 0, 0)]
        [TestCase(31, 32, 0, 1, 31, 0)]
        [TestCase(-32, -33, -1, -2, 0, 31)]
        public void TileToChunk_HandlesNegatives(int tx, int ty, int cx, int cy, int lx, int ly)
        {
            var tile = new Int2(tx, ty);
            Assert.AreEqual(new Int2(cx, cy), World.TileToChunk(tile));
            Assert.AreEqual(Chunk.Index(lx, ly), World.TileToIndex(tile));
        }

        [Test]
        public void Int2_PackRoundTrips()
        {
            foreach (var v in new[] { new Int2(0, 0), new Int2(-1, 5), new Int2(int.MaxValue, int.MinValue) })
                Assert.AreEqual(v, Int2.Unpack(v.Pack()));
        }

        [Test]
        public void Noise_StaysInRange()
        {
            for (int y = -300; y < 300; y += 7)
            for (int x = -300; x < 300; x += 7)
            {
                int n = Noise.Fractal(x, y, 64, 4, 42);
                Assert.That(n, Is.InRange(0, Noise.Max));
            }
        }

        [Test]
        public void Rng_IsDeterministic()
        {
            var a = new Rng(99);
            var b = new Rng(99);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.Range(-50, 50), b.Range(-50, 50));
        }

        [Test]
        public void Simulation_HashMatchesAcrossIdenticalRuns()
        {
            var a = new Simulation(7);
            var b = new Simulation(7);
            for (int i = 0; i < 600; i++)
            {
                a.Step();
                b.Step();
            }
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
            Assert.AreEqual(600, a.Tick);
        }
    }
}
