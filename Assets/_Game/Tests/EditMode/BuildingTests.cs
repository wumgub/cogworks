using System.Collections.Generic;
using NUnit.Framework;

namespace Cogworks.Sim.Tests
{
    public class BuildingTests
    {
        const uint Seed = 12345;

        // Tiles around spawn are guaranteed dry; search for specific ore/no-ore spots instead of hardcoding.
        static Int2 FindTile(World world, System.Func<Int2, bool> predicate)
        {
            for (int r = 0; r < 40; r++)
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                var t = new Int2(x, y);
                if (predicate(t)) return t;
            }
            Assert.Fail("no matching tile near spawn");
            return default;
        }

        static bool NoOre2x2(World w, Int2 o)
        {
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
                if (w.GetOre(new Int2(o.X + x, o.Y + y), out _) != Ore.None) return false;
            return true;
        }

        [Test]
        public void Place_SetsOccupancyOnWholeFootprint_AndRaisesEvent()
        {
            var world = new World(Seed);
            Building placed = null;
            world.Buildings.Placed += b => placed = b;

            var b = world.Buildings.Place(BuildingCatalog.Assembler, new Int2(-1, -1), Direction.North);

            Assert.IsNotNull(b);
            Assert.AreSame(b, placed);
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
                Assert.AreSame(b, world.Buildings.GetAt(new Int2(x, y)));
            Assert.IsNull(world.Buildings.GetAt(new Int2(2, 0)));
        }

        [Test]
        public void Place_FailsOnOverlap()
        {
            var world = new World(Seed);
            Assert.IsNotNull(world.Buildings.Place(BuildingCatalog.Furnace, new Int2(0, 0), Direction.North));
            Assert.IsNull(world.Buildings.Place(BuildingCatalog.Conveyor, new Int2(1, 1), Direction.North));
            Assert.IsNull(world.Buildings.Place(BuildingCatalog.Furnace, new Int2(-1, -1), Direction.North));
            Assert.AreEqual(1, world.Buildings.Count);
        }

        [Test]
        public void Place_FailsOnWater()
        {
            var world = new World(Seed);
            var water = Int2.Unpack(0);
            bool found = false;
            for (int y = -300; y < 300 && !found; y += 3)
            for (int x = -300; x < 300 && !found; x += 3)
                if (world.GetTerrain(new Int2(x, y)) == TerrainType.Water) { water = new Int2(x, y); found = true; }
            Assert.IsTrue(found, "seed has no water nearby");

            Assert.IsNull(world.Buildings.Place(BuildingCatalog.Chest, water, Direction.North));
        }

        [Test]
        public void Drill_RequiresOre()
        {
            var world = new World(Seed);
            var bare = FindTile(world, t => NoOre2x2(world, t));
            var ore = FindTile(world, t => world.GetOre(t, out _) != Ore.None);

            Assert.IsNull(world.Buildings.Place(BuildingCatalog.Drill, bare, Direction.North));
            Assert.IsNotNull(world.Buildings.Place(BuildingCatalog.Drill, ore, Direction.North));
        }

        [Test]
        public void NonRotatable_RejectsNonNorthFacing()
        {
            var world = new World(Seed);
            Assert.IsNull(world.Buildings.Place(BuildingCatalog.Chest, new Int2(0, 0), Direction.East));
        }

        [Test]
        public void Remove_FromAnyFootprintTile_ClearsOccupancy()
        {
            var world = new World(Seed);
            var b = world.Buildings.Place(BuildingCatalog.Assembler, new Int2(0, 0), Direction.North);
            Building removed = null;
            world.Buildings.Removed += r => removed = r;

            Assert.IsTrue(world.Buildings.Remove(new Int2(2, 2)));

            Assert.AreSame(b, removed);
            Assert.AreEqual(0, world.Buildings.Count);
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                Assert.IsNull(world.Buildings.GetAt(new Int2(x, y)));
            Assert.IsFalse(world.Buildings.Remove(new Int2(0, 0)));
        }

        [Test]
        public void Footprint_SpanningChunkBoundary_Works()
        {
            var world = new World(Seed);
            var origin = new Int2(Chunk.Size - 1, Chunk.Size - 1); // corner where 4 chunks meet
            var b = world.Buildings.Place(BuildingCatalog.Furnace, origin, Direction.North);

            Assert.IsNotNull(b);
            Assert.AreSame(b, world.Buildings.GetAt(new Int2(Chunk.Size, Chunk.Size)));
            Assert.IsTrue(world.Buildings.Remove(new Int2(Chunk.Size, Chunk.Size)));
            Assert.IsNull(world.Buildings.GetAt(origin));
        }

        [Test]
        public void SetDirection_ChangesFacing_AndRaisesChanged()
        {
            var world = new World(Seed);
            var b = world.Buildings.Place(BuildingCatalog.Conveyor, new Int2(0, 0), Direction.North);
            int changes = 0;
            world.Buildings.Changed += _ => changes++;

            Assert.IsTrue(world.Buildings.SetDirection(new Int2(0, 0), Direction.West));
            Assert.AreEqual(Direction.West, b.Direction);
            Assert.IsFalse(world.Buildings.SetDirection(new Int2(0, 0), Direction.West), "no-op when unchanged");
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Ids_AreSequential_AndNotReused()
        {
            var world = new World(Seed);
            var a = world.Buildings.Place(BuildingCatalog.Chest, new Int2(0, 0), Direction.North);
            world.Buildings.Remove(new Int2(0, 0));
            var b = world.Buildings.Place(BuildingCatalog.Chest, new Int2(0, 0), Direction.North);
            Assert.AreEqual(a.Id + 1, b.Id);
        }

        [Test]
        public void Catalog_IdsAreUniqueAndLookupWorks()
        {
            var seen = new HashSet<ushort>();
            foreach (var spec in BuildingCatalog.All)
            {
                Assert.IsTrue(seen.Add(spec.Id), $"duplicate id {spec.Id}");
                Assert.AreSame(spec, BuildingCatalog.Get(spec.Id));
            }
            Assert.IsNull(BuildingCatalog.Get(0));
        }

        [Test]
        public void Conveyors_AreWalkable_OtherBuildingsAreSolid()
        {
            foreach (var spec in BuildingCatalog.All)
                Assert.AreEqual(spec != BuildingCatalog.Conveyor, spec.Solid, spec.Key);
        }
    }

    public class CommandTests
    {
        static Command[] SampleLog() => new Command[]
        {
            new PlaceBuildingCommand(BuildingCatalog.Conveyor.Id, new Int2(0, 0), Direction.East),
            new PlaceBuildingCommand(BuildingCatalog.Conveyor.Id, new Int2(1, 0), Direction.East),
            new PlaceBuildingCommand(BuildingCatalog.Furnace.Id, new Int2(3, 0), Direction.North),
            new SetBuildingDirectionCommand(new Int2(1, 0), Direction.South),
            new RemoveBuildingCommand(new Int2(0, 0)),
            new PlaceBuildingCommand(BuildingCatalog.Chest.Id, new Int2(-5, 4), Direction.North) { PlayerId = 3 },
        };

        [Test]
        public void Commands_RunOnScheduledTick_NotBefore()
        {
            var sim = new Simulation(1);
            sim.Schedule(5, new PlaceBuildingCommand(BuildingCatalog.Chest.Id, new Int2(0, 0), Direction.North));

            for (int i = 0; i < 5; i++) sim.Step();
            Assert.AreEqual(0, sim.World.Buildings.Count, "ran early");
            sim.Step();
            Assert.AreEqual(1, sim.World.Buildings.Count);
        }

        [Test]
        public void Commands_InSameTick_RunInScheduledOrder()
        {
            var sim = new Simulation(1);
            sim.Schedule(0, new PlaceBuildingCommand(BuildingCatalog.Chest.Id, new Int2(0, 0), Direction.North));
            sim.Schedule(0, new RemoveBuildingCommand(new Int2(0, 0)));
            sim.Step();
            Assert.AreEqual(0, sim.World.Buildings.Count, "remove should run after place");
        }

        [Test]
        public void Schedule_InThePast_Throws()
        {
            var sim = new Simulation(1);
            sim.Step();
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                sim.Schedule(0, new RemoveBuildingCommand(new Int2(0, 0))));
        }

        [Test]
        public void LocalSink_AppliesOnNextStep()
        {
            var sim = new Simulation(1);
            new LocalCommandSink(sim).Submit(new PlaceBuildingCommand(BuildingCatalog.Chest.Id, new Int2(0, 0), Direction.North));
            sim.Step();
            Assert.AreEqual(1, sim.World.Buildings.Count);
        }

        [Test]
        public void Codec_RoundTripsEveryCommand()
        {
            foreach (var original in SampleLog())
            {
                var copy = CommandCodec.FromBytes(CommandCodec.ToBytes(original));
                Assert.AreEqual(original.GetType(), copy.GetType());
                Assert.AreEqual(original.PlayerId, copy.PlayerId);
                CollectionAssert.AreEqual(CommandCodec.ToBytes(original), CommandCodec.ToBytes(copy));
            }
        }

        [Test]
        public void SameCommandLog_ProducesSameHash_OnSeparateSims()
        {
            ulong RunLog(bool viaCodec)
            {
                var sim = new Simulation(42);
                var log = SampleLog();
                for (int i = 0; i < log.Length; i++)
                {
                    var cmd = viaCodec ? CommandCodec.FromBytes(CommandCodec.ToBytes(log[i])) : log[i];
                    sim.Schedule(i * 7, cmd);
                }
                for (int t = 0; t < 120; t++) sim.Step();
                return sim.ComputeHash();
            }

            // The codec path is what a remote peer sees.
            Assert.AreEqual(RunLog(false), RunLog(true));
        }

        [Test]
        public void DifferentCommandLogs_ProduceDifferentHashes()
        {
            var a = new Simulation(42);
            var b = new Simulation(42);
            a.Schedule(0, new PlaceBuildingCommand(BuildingCatalog.Conveyor.Id, new Int2(0, 0), Direction.North));
            b.Schedule(0, new PlaceBuildingCommand(BuildingCatalog.Conveyor.Id, new Int2(0, 0), Direction.East));
            a.Step();
            b.Step();
            Assert.AreNotEqual(a.ComputeHash(), b.ComputeHash());
        }

        [Test]
        public void InvalidCommand_IsNoOp_AndDoesNotChangeHash()
        {
            var a = new Simulation(42);
            var b = new Simulation(42);
            b.Schedule(0, new RemoveBuildingCommand(new Int2(10, 10)));
            b.Schedule(0, new PlaceBuildingCommand(9999, new Int2(0, 0), Direction.North));
            a.Step();
            b.Step();
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
        }
    }
}
