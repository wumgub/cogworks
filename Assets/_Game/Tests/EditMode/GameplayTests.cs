using System;
using NUnit.Framework;

namespace Cogworks.Sim.Tests
{
    public class GameplayTests
    {
        const uint Seed = 12345;

        static Simulation Sandbox() => new Simulation(Seed, sandbox: true);

        static void Run(Simulation sim, int ticks)
        {
            for (int i = 0; i < ticks; i++) sim.Step();
        }

        /// <summary>Nearest tile to spawn matching <paramref name="ok"/>.</summary>
        static Int2 Find(Func<Int2, bool> ok, int radius = 60)
        {
            for (int r = 0; r < radius; r++)
            for (int y = -r; y <= r; y++)
            for (int x = -r; x <= r; x++)
            {
                if (Math.Max(Math.Abs(x), Math.Abs(y)) != r) continue;
                var t = new Int2(x, y);
                if (ok(t)) return t;
            }
            Assert.Fail("no suitable spot");
            return default;
        }

        /// <summary>A clear, ore-free, dry rectangle to build test contraptions in.</summary>
        static Int2 ClearArea(World w, int width, int height) => Find(o =>
        {
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var t = new Int2(o.X + x, o.Y + y);
                if (w.GetTerrain(t) == TerrainType.Water || w.GetOre(t, out _) != Ore.None || w.Buildings.GetAt(t) != null) return false;
            }
            return true;
        });

        static Building Place(World w, BuildingSpec spec, Int2 at, Direction dir = Direction.North)
        {
            var b = w.Buildings.Place(spec, at, dir);
            Assert.IsNotNull(b, $"could not place {spec.Key} at {at}");
            return b;
        }

        static ushort IronOre => ItemCatalog.IronOre.Id;

        // ------------------------------------------------------------------ belts

        [Test]
        public void Belt_MovesItemsToTheEnd_AndCompressesFourPerTile()
        {
            var sim = Sandbox();
            var w = sim.World;
            var o = ClearArea(w, 6, 1);
            var belts = new ConveyorLogic[6];
            for (int i = 0; i < 6; i++) belts[i] = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, new Int2(o.X + i, o.Y), Direction.East).Logic;

            // Feed the first tile whenever it has room, for long enough to fill the line.
            for (int t = 0; t < 2000; t++)
            {
                belts[0].TryInsertAt(0, IronOre); // at the entry edge, like a belt feeding in
                sim.Step();
            }

            int total = 0;
            foreach (var b in belts) total += b.Items.Count;
            Assert.AreEqual(24, total, "a full line holds 4 items per tile");
            Assert.AreEqual(ConveyorLogic.Length - 1, belts[5].Items[0].Pos, "front item waits at the end");
        }

        [Test]
        public void Belt_SideLoads_AroundACorner()
        {
            var sim = Sandbox();
            var w = sim.World;
            var o = ClearArea(w, 4, 4);
            // East along the bottom, then the last tile turns North: a perpendicular hand-off.
            var first = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, o, Direction.East).Logic;
            Place(w, BuildingCatalog.Conveyor, new Int2(o.X + 1, o.Y), Direction.East);
            Place(w, BuildingCatalog.Conveyor, new Int2(o.X + 2, o.Y), Direction.North);
            var top = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, new Int2(o.X + 2, o.Y + 1), Direction.North).Logic;

            first.Insert(IronOre, o);
            Run(sim, 200);
            Assert.AreEqual(1, top.Items.Count, "item should have turned the corner and reached the top");
        }

        [Test]
        public void Belt_Loop_KeepsItems_AndIsDeterministic()
        {
            ulong RunLoop()
            {
                var sim = Sandbox();
                var w = sim.World;
                var o = ClearArea(w, 3, 3);
                var a = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, o, Direction.East).Logic;
                Place(w, BuildingCatalog.Conveyor, new Int2(o.X + 1, o.Y), Direction.North);
                Place(w, BuildingCatalog.Conveyor, new Int2(o.X + 1, o.Y + 1), Direction.West);
                Place(w, BuildingCatalog.Conveyor, new Int2(o.X, o.Y + 1), Direction.South);
                a.Insert(IronOre, o);
                a.TryInsertAt(0, ItemCatalog.Coal.Id);
                Run(sim, 1000);
                int items = 0;
                foreach (var b in w.Buildings.All) items += ((ConveyorLogic)b.Logic).Items.Count;
                Assert.AreEqual(2, items, "loops must not lose or duplicate items");
                return sim.ComputeHash();
            }
            Assert.AreEqual(RunLoop(), RunLoop());
        }

        [Test]
        public void Splitter_SharesItemsRoundRobin()
        {
            var sim = Sandbox();
            var w = sim.World;
            var o = ClearArea(w, 3, 3);
            // Splitter in the middle facing North, with belts leading away to the front, left and right.
            var splitter = (SplitterLogic)Place(w, BuildingCatalog.Splitter, new Int2(o.X + 1, o.Y + 1), Direction.North).Logic;
            var front = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, new Int2(o.X + 1, o.Y + 2), Direction.North).Logic;
            var left = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, new Int2(o.X, o.Y + 1), Direction.West).Logic;
            var right = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, new Int2(o.X + 2, o.Y + 1), Direction.East).Logic;

            for (int i = 0; i < 6; i++)
            {
                if (splitter.CanAccept(IronOre, o)) splitter.Insert(IronOre, o);
                Run(sim, 40);
            }
            Assert.AreEqual(2, front.Items.Count);
            Assert.AreEqual(2, left.Items.Count);
            Assert.AreEqual(2, right.Items.Count);
        }

        [Test]
        public void Belt_FeedsIntoSplitter()
        {
            var sim = Sandbox();
            var w = sim.World;
            var o = ClearArea(w, 3, 1);
            var belt = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, o, Direction.East).Logic;
            Place(w, BuildingCatalog.Splitter, new Int2(o.X + 1, o.Y), Direction.East);
            var after = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, new Int2(o.X + 2, o.Y), Direction.East).Logic;
            belt.TryInsertAtEntry(IronOre);
            Run(sim, 120);
            Assert.AreEqual(0, belt.Items.Count);
            Assert.AreEqual(1, after.Items.Count, "item should pass through the splitter onto the belt ahead");
        }

        [Test]
        public void Underground_CarriesItemsUnderObstacles()
        {
            var sim = Sandbox();
            var w = sim.World;
            var o = ClearArea(w, 7, 1);
            var feed = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, o, Direction.East).Logic;
            Place(w, BuildingCatalog.UndergroundEntrance, new Int2(o.X + 1, o.Y), Direction.East);
            Place(w, BuildingCatalog.Chest, new Int2(o.X + 2, o.Y));   // obstacles in between
            Place(w, BuildingCatalog.Chest, new Int2(o.X + 3, o.Y));
            Place(w, BuildingCatalog.UndergroundExit, new Int2(o.X + 4, o.Y), Direction.East);
            var outBelt = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, new Int2(o.X + 5, o.Y), Direction.East).Logic;
            var sink = (ChestLogic)Place(w, BuildingCatalog.Chest, new Int2(o.X + 6, o.Y)).Logic;

            for (int i = 0; i < 3; i++) feed.TryInsertAt(i * ConveyorLogic.Spacing, IronOre);
            Run(sim, 400);
            Assert.AreEqual(0, feed.Items.Count, "entrance should take everything");
            Assert.AreEqual(3, outBelt.Items.Count, "all three should come out of the exit");
            Assert.AreEqual(0, sink.Inventory.Count(IronOre), "belts don't insert into chests by themselves");
        }

        [Test]
        public void Underground_WithoutExit_RefusesItems()
        {
            var sim = Sandbox();
            var w = sim.World;
            var o = ClearArea(w, 2, 1);
            var feed = (ConveyorLogic)Place(w, BuildingCatalog.Conveyor, o, Direction.East).Logic;
            Place(w, BuildingCatalog.UndergroundEntrance, new Int2(o.X + 1, o.Y), Direction.East);
            feed.TryInsertAtEntry(IronOre);
            Run(sim, 100);
            Assert.AreEqual(1, feed.Items.Count);
        }

        // ------------------------------------------------------------------ inserters and chests

        [Test]
        public void Inserter_MovesChestToChest()
        {
            var sim = Sandbox();
            var w = sim.World;
            var o = ClearArea(w, 3, 1);
            var src = (ChestLogic)Place(w, BuildingCatalog.Chest, o).Logic;
            Place(w, BuildingCatalog.Inserter, new Int2(o.X + 1, o.Y), Direction.East);
            var dst = (ChestLogic)Place(w, BuildingCatalog.Chest, new Int2(o.X + 2, o.Y)).Logic;
            src.Inventory.Insert(IronOre, 5);

            Run(sim, InserterLogic.SwingTicks * 2 * 5 + 10);
            Assert.AreEqual(0, src.Inventory.Count(IronOre));
            Assert.AreEqual(5, dst.Inventory.Count(IronOre));
        }

        [Test]
        public void Inserter_OnlyGrabsWhatTheTargetAccepts()
        {
            var sim = Sandbox();
            var w = sim.World;
            var o = ClearArea(w, 4, 2);
            var chest = (ChestLogic)Place(w, BuildingCatalog.Chest, o).Logic;
            Place(w, BuildingCatalog.Inserter, new Int2(o.X + 1, o.Y), Direction.East);
            var furnace = (FurnaceLogic)Place(w, BuildingCatalog.Furnace, new Int2(o.X + 2, o.Y)).Logic;
            chest.Inventory.Insert(ItemCatalog.Circuit.Id, 3);
            chest.Inventory.Insert(IronOre, 2);

            Run(sim, 200);
            Assert.AreEqual(3, chest.Inventory.Count(ItemCatalog.Circuit.Id), "furnaces don't take circuits");
            Assert.AreEqual(2, furnace.Input.Count);
        }

        // ------------------------------------------------------------------ kinetics and drills

        /// <summary>Drill on ore facing North with a chest in front, plus room to the west.</summary>
        static (DrillLogic drill, ChestLogic chest, Int2 origin) DrillSetup(World w)
        {
            var o = Find(t =>
                w.Buildings.CanPlace(BuildingCatalog.Drill, t, Direction.North)
                && w.Buildings.CanPlace(BuildingCatalog.Chest, new Int2(t.X, t.Y + 2), Direction.North)
                && w.Buildings.CanPlace(BuildingCatalog.Drill, new Int2(t.X + 2, t.Y), Direction.North)
                && w.Buildings.CanPlace(BuildingCatalog.Chest, new Int2(t.X + 2, t.Y + 2), Direction.North)
                && w.Buildings.CanPlace(BuildingCatalog.HandCrank, new Int2(t.X - 3, t.Y), Direction.North)
                && w.Buildings.CanPlace(BuildingCatalog.Shaft, new Int2(t.X - 2, t.Y), Direction.East)
                && w.Buildings.CanPlace(BuildingCatalog.Shaft, new Int2(t.X - 1, t.Y), Direction.East));
            var drill = (DrillLogic)Place(w, BuildingCatalog.Drill, o).Logic;
            var chest = (ChestLogic)Place(w, BuildingCatalog.Chest, new Int2(o.X, o.Y + 2)).Logic;
            return (drill, chest, o);
        }

        static int Count(ChestLogic chest)
        {
            int n = 0;
            foreach (var s in chest.Inventory.Contents()) n += s.Count;
            return n;
        }

        [Test]
        public void Drill_WithoutPower_MinesNothing()
        {
            var sim = Sandbox();
            var (_, chest, _) = DrillSetup(sim.World);
            Run(sim, 600);
            Assert.AreEqual(0, Count(chest));
        }

        [Test]
        public void Drill_PoweredByCrankThroughShafts_MinesWhileCranked()
        {
            var sim = Sandbox();
            var w = sim.World;
            var (drill, chest, o) = DrillSetup(w);
            var crankAt = new Int2(o.X - 3, o.Y);
            Place(w, BuildingCatalog.HandCrank, crankAt);
            Place(w, BuildingCatalog.Shaft, new Int2(o.X - 2, o.Y), Direction.East);
            Place(w, BuildingCatalog.Shaft, new Int2(o.X - 1, o.Y), Direction.East);

            sim.Schedule(sim.Tick, new CrankCommand(crankAt));
            Run(sim, GeneratorLogic.CrankTicks);
            Assert.AreEqual(32, drill.LastRpm, "crank should drive the drill at 32 rpm");
            int mined = Count(chest);
            // 5 s at one item per 2 s.
            Assert.That(mined, Is.InRange(2, 3));

            Run(sim, 600);
            Assert.AreEqual(mined, Count(chest), "drill stops when the crank winds down");
        }

        [Test]
        public void Shaft_OnlyConnectsAlongItsAxis()
        {
            var sim = Sandbox();
            var w = sim.World;
            var (drill, _, o) = DrillSetup(w);
            var crankAt = new Int2(o.X - 3, o.Y);
            Place(w, BuildingCatalog.HandCrank, crankAt);
            // Vertical shafts sit in the gap but don't connect sideways.
            Place(w, BuildingCatalog.Shaft, new Int2(o.X - 2, o.Y), Direction.North);
            Place(w, BuildingCatalog.Shaft, new Int2(o.X - 1, o.Y), Direction.North);
            sim.Schedule(sim.Tick, new CrankCommand(crankAt));
            Run(sim, 30);
            Assert.AreEqual(0, drill.LastRpm);
        }

        [Test]
        public void Network_Overstressed_StopsEverything()
        {
            var sim = Sandbox();
            var w = sim.World;
            var (drill, _, o) = DrillSetup(w);
            var second = (DrillLogic)Place(w, BuildingCatalog.Drill, new Int2(o.X + 2, o.Y)).Logic; // touches the first drill
            var crankAt = new Int2(o.X - 1, o.Y);
            Place(w, BuildingCatalog.HandCrank, crankAt);
            sim.Schedule(sim.Tick, new CrankCommand(crankAt));
            Run(sim, 10);

            var net = w.Kinetics.NetworkOf(drill.Building);
            Assert.AreSame(net, w.Kinetics.NetworkOf(second.Building), "adjacent machines share a network");
            Assert.AreEqual(2 * BuildingCatalog.Drill.StressImpact * 32, net.Usage);
            Assert.IsTrue(net.Overstressed, "a hand crank can't drive two drills");
            Assert.AreEqual(0, drill.LastRpm);
        }

        [Test]
        public void SteamEngine_BurnsCoalOnlyWithLoad()
        {
            var sim = Sandbox();
            var w = sim.World;
            var o = ClearArea(w, 2, 2);
            var engine = (GeneratorLogic)Place(w, BuildingCatalog.SteamEngine, o).Logic;
            engine.InsertFromPlayer(ItemCatalog.Coal.Id, 3);
            Run(sim, 120);
            Assert.AreEqual(3, engine.Fuel.Count, "no consumers, no burning");
        }

        [Test]
        public void WaterWheel_RequiresWaterNextToIt()
        {
            var w = Sandbox().World;
            var shore = Find(t => w.GetTerrain(t) != TerrainType.Water && w.GetTerrain(new Int2(t.X + 1, t.Y)) == TerrainType.Water, 400);
            var inland = ClearArea(w, 3, 3);
            Assert.IsTrue(w.Buildings.CanPlace(BuildingCatalog.WaterWheel, shore, Direction.North));
            Assert.IsFalse(w.Buildings.CanPlace(BuildingCatalog.WaterWheel, new Int2(inland.X + 1, inland.Y + 1), Direction.North));
        }

        // ------------------------------------------------------------------ crafting

        [Test]
        public void Furnace_SmeltsWithFuel_FiveSmeltsPerCoal()
        {
            var sim = Sandbox();
            var w = sim.World;
            var furnace = (FurnaceLogic)Place(w, BuildingCatalog.Furnace, ClearArea(w, 2, 2)).Logic;
            furnace.InsertFromPlayer(IronOre, 10);
            furnace.InsertFromPlayer(ItemCatalog.Coal.Id, 1);

            Run(sim, RecipeCatalog.IronPlate.Ticks * 10 + 20);
            Assert.AreEqual(ItemCatalog.IronPlate.Id, furnace.Output.Item);
            Assert.AreEqual(5, furnace.Output.Count, "one coal (960 ticks) smelts five plates (192 ticks each)");
            Assert.AreEqual(5, furnace.Input.Count);
        }

        [Test]
        public void Assembler_CraftsGears_WhenPowered()
        {
            var sim = Sandbox();
            var w = sim.World;
            var shore = Find(t =>
                w.GetTerrain(new Int2(t.X - 1, t.Y)) == TerrainType.Water
                && w.Buildings.CanPlace(BuildingCatalog.WaterWheel, t, Direction.North)
                && w.Buildings.CanPlace(BuildingCatalog.Assembler, new Int2(t.X + 1, t.Y), Direction.North), 400);
            Place(w, BuildingCatalog.WaterWheel, shore);
            var asm = (AssemblerLogic)Place(w, BuildingCatalog.Assembler, new Int2(shore.X + 1, shore.Y)).Logic;
            sim.Schedule(sim.Tick, new SetRecipeCommand(asm.Building.Origin, RecipeCatalog.IronGear.Id));
            sim.Step();
            asm.InsertFromPlayer(ItemCatalog.IronPlate.Id, 10);

            Run(sim, RecipeCatalog.IronGear.Ticks * 5 + 10);
            Assert.AreEqual(32, asm.LastRpm);
            Assert.AreEqual(5, asm.Output.Count);
            Assert.AreEqual(0, asm.Inputs.Count(ItemCatalog.IronPlate.Id));
        }

        [Test]
        public void HandCraft_ConsumesIngredients_AndRespectsResearch()
        {
            var sim = new Simulation(Seed);
            sim.Schedule(0, new PlayerJoinCommand(0));
            sim.Step();
            var player = sim.World.Players.Get(0);
            int plates = player.Inventory.Count(ItemCatalog.IronPlate.Id);

            sim.Schedule(sim.Tick, new HandCraftCommand(RecipeCatalog.IronGear.Id, 3));
            sim.Schedule(sim.Tick, new HandCraftCommand(RecipeCatalog.Assembler.Id, 1)); // locked by Automation
            Run(sim, RecipeCatalog.IronGear.Ticks * 3 + 10);

            Assert.AreEqual(plates - 6, player.Inventory.Count(ItemCatalog.IronPlate.Id));
            Assert.AreEqual(3, player.Inventory.Count(ItemCatalog.IronGear.Id));
            Assert.AreEqual(0, player.Inventory.Count(BuildingCatalog.Assembler.ItemId));
        }

        // ------------------------------------------------------------------ inventory rules

        [Test]
        public void Placing_CostsAnItem_AndRemoving_RefundsBuildingAndContents()
        {
            var sim = new Simulation(Seed);
            sim.Schedule(0, new PlayerJoinCommand(0));
            sim.Step();
            var w = sim.World;
            var player = w.Players.Get(0);
            int chests = player.Inventory.Count(BuildingCatalog.Chest.ItemId);
            var at = ClearArea(w, 1, 1);

            sim.Schedule(sim.Tick, new PlaceBuildingCommand(BuildingCatalog.Chest.Id, at, Direction.North));
            sim.Step();
            Assert.AreEqual(chests - 1, player.Inventory.Count(BuildingCatalog.Chest.ItemId));

            sim.Schedule(sim.Tick, new TransferItemsCommand(at, ItemCatalog.Coal.Id, 4, intoBuilding: true));
            sim.Step();
            int coal = player.Inventory.Count(ItemCatalog.Coal.Id);

            sim.Schedule(sim.Tick, new RemoveBuildingCommand(at));
            sim.Step();
            Assert.IsNull(w.Buildings.GetAt(at));
            Assert.AreEqual(chests, player.Inventory.Count(BuildingCatalog.Chest.ItemId));
            Assert.AreEqual(coal + 4, player.Inventory.Count(ItemCatalog.Coal.Id));
        }

        [Test]
        public void Placing_WithoutTheItem_Fails()
        {
            var sim = new Simulation(Seed);
            sim.Schedule(0, new PlayerJoinCommand(0));
            sim.Step();
            sim.Schedule(sim.Tick, new PlaceBuildingCommand(BuildingCatalog.Lab.Id, ClearArea(sim.World, 2, 2), Direction.North));
            sim.Step();
            Assert.AreEqual(0, sim.World.Buildings.Count);
        }

        [Test]
        public void Removing_IsRefused_WhenInventoryIsFull()
        {
            var sim = new Simulation(Seed);
            sim.Schedule(0, new PlayerJoinCommand(0));
            sim.Step();
            var w = sim.World;
            var at = ClearArea(w, 1, 1);
            sim.Schedule(sim.Tick, new PlaceBuildingCommand(BuildingCatalog.Chest.Id, at, Direction.North));
            sim.Step();
            var inv = w.Players.Get(0).Inventory;
            inv.Remove(BuildingCatalog.Chest.ItemId, 1000); // no partial chest stack to refund into
            while (inv.Insert(ItemCatalog.Stone.Id, 1000) > 0) { }

            sim.Schedule(sim.Tick, new RemoveBuildingCommand(at));
            sim.Step();
            Assert.IsNotNull(w.Buildings.GetAt(at));
        }

        [Test]
        public void MineTile_GivesOre_WithCooldown()
        {
            var sim = new Simulation(Seed);
            sim.Schedule(0, new PlayerJoinCommand(0));
            sim.Step();
            var w = sim.World;
            var ore = Find(t => w.GetOre(t, out _) == Ore.Iron);
            w.GetOre(ore, out int before);

            sim.Schedule(sim.Tick, new MineTileCommand(ore));
            sim.Schedule(sim.Tick, new MineTileCommand(ore)); // same tick: on cooldown
            sim.Step();
            Assert.AreEqual(1, w.Players.Get(0).Inventory.Count(IronOre));
            w.GetOre(ore, out int after);
            Assert.AreEqual(before - 1, after);
        }

        // ------------------------------------------------------------------ research

        [Test]
        public void Lab_ResearchesAutomation_UnlockingAssemblers()
        {
            var sim = Sandbox();
            var w = sim.World;
            var lab = (LabLogic)Place(w, BuildingCatalog.Lab, ClearArea(w, 2, 2)).Logic;
            sim.Schedule(sim.Tick, new SetResearchCommand(TechCatalog.Automation.Id));
            sim.Step();
            Assert.AreSame(TechCatalog.Automation, w.Research.Current);
            Assert.IsFalse(w.Research.IsUnlocked(RecipeCatalog.Assembler));

            lab.InsertFromPlayer(ItemCatalog.RedScience.Id, 10);
            Run(sim, LabLogic.TicksPerPack * 10 + 5);

            Assert.IsTrue(w.Research.IsDone(TechCatalog.Automation));
            Assert.IsTrue(w.Research.IsUnlocked(RecipeCatalog.Assembler));
            Assert.IsNull(w.Research.Current);
        }

        [Test]
        public void Research_RequiresPrerequisites()
        {
            var sim = Sandbox();
            sim.Schedule(0, new SetResearchCommand(TechCatalog.SteamPower.Id));
            sim.Step();
            Assert.IsNull(sim.World.Research.Current, "steam power needs automation first");
        }

        // ------------------------------------------------------------------ persistence

        [Test]
        public void Snapshot_OfRunningFactory_ContinuesIdentically()
        {
            var a = Sandbox();
            var w = a.World;
            var (_, _, o) = DrillSetup(w);
            var crankAt = new Int2(o.X - 1, o.Y);
            Place(w, BuildingCatalog.HandCrank, crankAt);
            var furnace = (FurnaceLogic)Place(w, BuildingCatalog.Furnace, ClearArea(w, 2, 2)).Logic;
            furnace.InsertFromPlayer(IronOre, 20);
            furnace.InsertFromPlayer(ItemCatalog.Coal.Id, 2);
            a.Schedule(a.Tick, new CrankCommand(crankAt));
            Run(a, 250);

            var b = Snapshot.Load(Snapshot.Save(a));
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
            Run(a, 500);
            Run(b, 500);
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
        }
    }
}
