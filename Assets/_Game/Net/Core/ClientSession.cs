using System;
using System.Collections.Generic;
using System.IO;
using Cogworks.Sim;

namespace Cogworks.Net
{
    /// <summary>
    /// Non-authoritative session. Loads the host's snapshot, then runs each tick only once the host's
    /// commands for it have arrived, so it always matches the host. Its own commands go to the host.
    /// </summary>
    public sealed class ClientSession : ISession
    {
        // Ticks of buffered input above which we step faster to catch up with the host.
        const int CatchUpThreshold = 8;

        readonly IClientTransport _transport;
        readonly Queue<TickBundle> _bundles = new Queue<TickBundle>();
        readonly Dictionary<int, byte[][]> _snapshotParts = new Dictionary<int, byte[][]>();
        TickClock _clock;

        public ClientSession(IClientTransport transport)
        {
            _transport = transport;
            Commands = new Sink(this);
        }

        public Simulation Sim { get; private set; }
        public ICommandSink Commands { get; }
        public byte LocalPlayerId { get; private set; }
        public float TickAlpha => _clock.Alpha;

        /// <summary>Ticks received from the host but not yet run.</summary>
        public int BufferedTicks => _bundles.Count;

        public event Action<Simulation> SimReplaced;

        public void OnMessage(byte[] message)
        {
            if (message.Length == 0) return;
            using var r = new BinaryReader(new MemoryStream(message));
            switch ((MessageType)r.ReadByte())
            {
                case MessageType.TickBatch:
                    foreach (var bundle in Protocol.DecodeTickBatch(r))
                        if (Sim == null || bundle.Tick >= Sim.Tick) _bundles.Enqueue(bundle);
                    break;
                case MessageType.SnapshotPart:
                    OnSnapshotPart(r);
                    break;
            }
        }

        public void Advance(double deltaSeconds)
        {
            if (Sim == null) return;
            _clock.Accumulator += deltaSeconds;

            int steps = 0;
            while (steps < TickClock.MaxTicksPerAdvance && _bundles.Count > 0)
            {
                bool due = _clock.Accumulator >= TickClock.TickLength;
                bool behind = _bundles.Count > CatchUpThreshold;
                if (!due && !behind) break;

                RunNext();
                if (due) _clock.Accumulator -= TickClock.TickLength;
                steps++;
            }

            // Starved: don't bank time we couldn't spend, or we'd lurch forward when input arrives.
            if (_bundles.Count == 0 && _clock.Accumulator > TickClock.TickLength)
                _clock.Accumulator = TickClock.TickLength;
        }

        /// <summary>Runs every buffered tick immediately (tests and headless clients).</summary>
        public void RunAllBuffered()
        {
            while (Sim != null && _bundles.Count > 0) RunNext();
        }

        void RunNext()
        {
            var bundle = _bundles.Dequeue();
            if (bundle.Tick != Sim.Tick)
                throw new InvalidOperationException($"Tick stream gap: expected {Sim.Tick}, got {bundle.Tick}");

            foreach (var c in bundle.Commands) Sim.Schedule(bundle.Tick, c);
            Sim.Step();

            if (bundle.Tick % Protocol.HashInterval == 0)
                _transport.SendToHost(Protocol.EncodeHash(bundle.Tick, Sim.ComputeHash()));
        }

        void OnSnapshotPart(BinaryReader r)
        {
            int id = r.ReadInt32();
            byte playerId = r.ReadByte();
            int index = r.ReadUInt16();
            int count = r.ReadUInt16();
            byte[] data = r.ReadBytes(r.ReadInt32());

            if (!_snapshotParts.TryGetValue(id, out var parts))
            {
                parts = new byte[count][];
                _snapshotParts[id] = parts;
            }
            parts[index] = data;
            foreach (var p in parts)
                if (p == null) return;

            _snapshotParts.Remove(id);
            using var full = new MemoryStream();
            foreach (var p in parts) full.Write(p, 0, p.Length);

            Sim = Snapshot.Load(full.ToArray());
            LocalPlayerId = playerId;
            _clock.Accumulator = 0;

            // Drop ticks the snapshot already includes; later ones continue the stream.
            int keep = _bundles.Count;
            for (int i = 0; i < keep; i++)
            {
                var b = _bundles.Dequeue();
                if (b.Tick >= Sim.Tick) _bundles.Enqueue(b);
            }
            SimReplaced?.Invoke(Sim);
        }

        sealed class Sink : ICommandSink
        {
            readonly ClientSession _client;
            public Sink(ClientSession client) => _client = client;

            public void Submit(Command command)
            {
                command.PlayerId = _client.LocalPlayerId;
                _client._transport.SendToHost(Protocol.EncodeCommand(command));
            }
        }
    }
}
