using System.Collections.Generic;
using System.IO;
using Cogworks.Net;
using NUnit.Framework;

namespace Cogworks.Sim.Tests
{
    /// <summary>
    /// In-memory network with per-message latency measured in host ticks. Delivery stays in order per
    /// link, like a reliable sequenced channel.
    /// </summary>
    sealed class FakeNetwork : IHostTransport
    {
        sealed class Link
        {
            public readonly Queue<(int due, byte[] data)> ToClient = new Queue<(int, byte[])>();
            public readonly Queue<(int due, byte[] data)> ToHost = new Queue<(int, byte[])>();
            public ClientSession Client;
            public int Latency;
        }

        readonly Dictionary<ulong, Link> _links = new Dictionary<ulong, Link>();
        public HostSession Host;
        public int Now;
        public long BytesToClients;

        public ClientSession Connect(ulong clientId, int latency)
        {
            var link = new Link { Latency = latency };
            link.Client = new ClientSession(new ClientEnd(this, link));
            _links[clientId] = link;
            Assert.IsTrue(Host.OnClientConnected(clientId));
            return link.Client;
        }

        public void SendToClient(ulong clientId, byte[] message)
        {
            BytesToClients += message.Length;
            var link = _links[clientId];
            link.ToClient.Enqueue((Now + link.Latency, message));
        }

        public void Deliver()
        {
            foreach (var pair in _links)
            {
                var link = pair.Value;
                while (link.ToClient.Count > 0 && link.ToClient.Peek().due <= Now) link.Client.OnMessage(link.ToClient.Dequeue().data);
                while (link.ToHost.Count > 0 && link.ToHost.Peek().due <= Now) Host.OnMessage(pair.Key, link.ToHost.Dequeue().data);
            }
        }

        sealed class ClientEnd : IClientTransport
        {
            readonly FakeNetwork _net;
            readonly Link _link;
            public ClientEnd(FakeNetwork net, Link link) { _net = net; _link = link; }
            public void SendToHost(byte[] message) => _link.ToHost.Enqueue((_net.Now + _link.Latency, message));
        }
    }

    public class NetSessionTests
    {
        static Command RandomCommand(ref Rng rng)
        {
            var tile = new Int2(rng.Range(-12, 12), rng.Range(-12, 12));
            var dir = (Direction)rng.Range(0, 4);
            switch (rng.Range(0, 4))
            {
                case 0: return new RemoveBuildingCommand(tile);
                case 1: return new SetBuildingDirectionCommand(tile, dir);
                default:
                    var spec = BuildingCatalog.All[rng.Range(0, BuildingCatalog.All.Count)];
                    return new PlaceBuildingCommand(spec.Id, tile, spec.Rotatable ? dir : Direction.North);
            }
        }

        /// <summary>One host tick plus network delivery and client catch-up.</summary>
        static void Frame(FakeNetwork net, List<ClientSession> clients)
        {
            net.Host.StepOnce();
            net.Host.Flush();
            net.Now++;
            net.Deliver();
            foreach (var c in clients) c.RunAllBuffered();
        }

        static void Drain(FakeNetwork net, List<ClientSession> clients, int frames)
        {
            for (int i = 0; i < frames; i++) Frame(net, clients);
        }

        /// <summary>Stops the host and lets everything in flight arrive, so all peers reach the same tick.</summary>
        static void Settle(FakeNetwork net, List<ClientSession> clients)
        {
            net.Host.Flush();
            for (int i = 0; i < 64; i++)
            {
                net.Now++;
                net.Deliver();
                foreach (var c in clients) c.RunAllBuffered();
            }
        }

        [Test]
        public void FourPlayers_RandomCommands_StayInSync()
        {
            var net = new FakeNetwork();
            net.Host = new HostSession(new Simulation(777), net);
            var clients = new List<ClientSession>
            {
                net.Connect(1, latency: 2),
                net.Connect(2, latency: 5),
                net.Connect(3, latency: 9),
            };
            int desyncs = 0;
            net.Host.DesyncDetected += (_, __) => desyncs++;

            var rng = new Rng(2024);
            for (int frame = 0; frame < 900; frame++)
            {
                if (rng.Range(0, 3) == 0) net.Host.Commands.Submit(RandomCommand(ref rng));
                foreach (var c in clients)
                    if (c.Sim != null && rng.Range(0, 3) == 0) c.Commands.Submit(RandomCommand(ref rng));
                Frame(net, clients);
            }
            Settle(net, clients);

            Assert.Greater(net.Host.Sim.World.Buildings.Count, 10, "commands should have built something");
            Assert.AreEqual(4, CountPlayers(net.Host.Sim));
            foreach (var c in clients)
            {
                Assert.AreEqual(net.Host.Sim.Tick, c.Sim.Tick);
                Assert.AreEqual(net.Host.Sim.ComputeHash(), c.Sim.ComputeHash());
            }
            Assert.AreEqual(0, desyncs);
        }

        [Test]
        public void Clients_GetDistinctPlayerIds_AndCommandsAreStampedByHost()
        {
            var net = new FakeNetwork();
            net.Host = new HostSession(new Simulation(1), net);
            var a = net.Connect(10, 1);
            var b = net.Connect(11, 1);
            var clients = new List<ClientSession> { a, b };
            Drain(net, clients, 5);

            Assert.AreEqual(1, a.LocalPlayerId);
            Assert.AreEqual(2, b.LocalPlayerId);

            // A client lying about its id and trying a host-only command.
            a.Commands.Submit(new PlaceBuildingCommand(BuildingCatalog.Chest.Id, new Int2(0, 0), Direction.North) { PlayerId = 0 });
            a.Commands.Submit(new PlayerJoinCommand(3));
            Drain(net, clients, 5);
            Settle(net, clients);

            Assert.AreEqual(1, net.Host.Sim.World.Buildings.Count);
            Assert.IsNull(net.Host.Sim.World.Players.Get(3), "client must not be able to add players");
            Assert.AreEqual(net.Host.Sim.ComputeHash(), b.Sim.ComputeHash());
        }

        [Test]
        public void LateJoiner_ReceivesSnapshot_AndMatchesHost()
        {
            var net = new FakeNetwork();
            net.Host = new HostSession(new Simulation(5), net);
            var clients = new List<ClientSession>();
            var rng = new Rng(9);
            for (int i = 0; i < 300; i++)
            {
                net.Host.Commands.Submit(RandomCommand(ref rng));
                Frame(net, clients);
            }
            Assert.Greater(net.Host.Sim.World.Buildings.Count, 0);

            var late = net.Connect(42, latency: 4);
            clients.Add(late);
            Assert.IsNull(late.Sim, "snapshot is still in flight");
            for (int i = 0; i < 200; i++)
            {
                net.Host.Commands.Submit(RandomCommand(ref rng));
                Frame(net, clients);
            }
            Settle(net, clients);

            Assert.IsNotNull(late.Sim);
            Assert.AreEqual(net.Host.Sim.ComputeHash(), late.Sim.ComputeHash());
            Assert.IsNotNull(net.Host.Sim.World.Players.Get(late.LocalPlayerId));
        }

        [Test]
        public void Desync_IsDetected_AndRepairedByResync()
        {
            var net = new FakeNetwork();
            net.Host = new HostSession(new Simulation(3), net);
            var client = net.Connect(7, latency: 1);
            var clients = new List<ClientSession> { client };
            int replaced = 0;
            client.SimReplaced += _ => replaced++;
            Drain(net, clients, 10);
            int replacedAfterJoin = replaced;

            // Corrupt the client: a building the host never placed.
            client.Sim.World.Buildings.Place(BuildingCatalog.Chest, new Int2(5, 5), Direction.North);
            ulong detectedFor = 0;
            net.Host.DesyncDetected += (id, _) => detectedFor = id;

            // Past the post-join resync cooldown (5 s), then let the fresh snapshot land.
            Drain(net, clients, Simulation.TicksPerSecond * 8);
            Settle(net, clients);

            Assert.AreEqual(7UL, detectedFor);
            Assert.Greater(replaced, replacedAfterJoin, "client should have reloaded a snapshot");
            Assert.AreEqual(net.Host.Sim.ComputeHash(), client.Sim.ComputeHash());
        }

        [Test]
        public void GameFull_RejectsFifthPlayer()
        {
            var net = new FakeNetwork();
            net.Host = new HostSession(new Simulation(1), net);
            net.Connect(1, 1);
            net.Connect(2, 1);
            net.Connect(3, 1);
            Assert.IsFalse(net.Host.HasRoomForPlayer);
            Assert.IsFalse(net.Host.OnClientConnected(4));
        }

        [Test]
        public void SlotIsReused_AfterDisconnect()
        {
            var net = new FakeNetwork();
            net.Host = new HostSession(new Simulation(1), net);
            net.Connect(1, 1);
            net.Host.OnClientDisconnected(1);
            var again = net.Connect(2, 1);
            Drain(net, new List<ClientSession> { again }, 5);
            Assert.AreEqual(1, again.LocalPlayerId);
        }

        [Test]
        public void LargeCommandBurst_IsSplitIntoBoundedMessages()
        {
            var bundles = new List<TickBundle>();
            for (int t = 0; t < 50; t++)
            {
                var cmds = new List<Command>();
                for (int i = 0; i < 40; i++) cmds.Add(new RemoveBuildingCommand(new Int2(i, t)));
                bundles.Add(new TickBundle(t, cmds));
            }
            var messages = Protocol.EncodeTickBatches(bundles);
            Assert.Greater(messages.Count, 1);

            var decoded = new List<TickBundle>();
            foreach (var m in messages)
            {
                Assert.LessOrEqual(m.Length, Protocol.MaxBatchBytes + 1024);
                using var r = new BinaryReader(new MemoryStream(m));
                Assert.AreEqual((byte)MessageType.TickBatch, r.ReadByte());
                decoded.AddRange(Protocol.DecodeTickBatch(r));
            }
            Assert.AreEqual(50, decoded.Count);
            for (int t = 0; t < 50; t++) Assert.AreEqual(t, decoded[t].Tick);
        }

        static int CountPlayers(Simulation sim)
        {
            int n = 0;
            foreach (var _ in sim.World.Players.All) n++;
            return n;
        }
    }

    public class SnapshotTests
    {
        [Test]
        public void RoundTrip_PreservesHash_AndContinuesIdentically()
        {
            var a = new Simulation(31337);
            var rng = new Rng(1);
            for (int i = 0; i < 200; i++)
            {
                var tile = new Int2(rng.Range(-20, 20), rng.Range(-20, 20));
                a.Schedule(a.Tick, new PlaceBuildingCommand(BuildingCatalog.All[rng.Range(0, 8)].Id, tile, Direction.North));
                a.Step();
            }
            a.Schedule(a.Tick + 3, new RemoveBuildingCommand(new Int2(0, 0)));

            var b = Snapshot.Load(Snapshot.Save(a));
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
            Assert.AreEqual(a.World.Buildings.Count, b.World.Buildings.Count);

            for (int i = 0; i < 10; i++)
            {
                a.Step();
                b.Step();
            }
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash(), "pending commands must survive the round trip");
        }

        [Test]
        public void Loaded_Buildings_AreQueryableByTile()
        {
            var a = new Simulation(1);
            a.World.Buildings.Place(BuildingCatalog.Assembler, new Int2(10, 10), Direction.North);
            var b = Snapshot.Load(Snapshot.Save(a));
            var found = b.World.Buildings.GetAt(new Int2(12, 12));
            Assert.IsNotNull(found);
            Assert.AreEqual(BuildingCatalog.Assembler, found.Spec);
        }

        [Test]
        public void Garbage_IsRejected()
        {
            Assert.Throws<InvalidDataException>(() => Snapshot.Load(new byte[] { 0x1f, 0x8b, 8, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0 }));
        }
    }
}
