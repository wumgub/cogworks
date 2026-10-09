using System;
using System.Collections.Generic;
using System.IO;
using Cogworks.Sim;

namespace Cogworks.Net
{
    /// <summary>
    /// Authoritative session. Collects commands from the local player and from clients, assigns each to
    /// the next tick, runs the sim, and broadcasts every tick's commands so clients run identical ticks.
    /// </summary>
    public sealed class HostSession : ISession
    {
        // How long to keep our own hashes for comparing against late client reports.
        const int HashHistoryTicks = Protocol.HashInterval * 20;
        // Don't resend snapshots to the same client more often than this.
        const int ResyncCooldownTicks = Simulation.TicksPerSecond * 5;

        sealed class Peer
        {
            public ulong ClientId;
            public byte PlayerId;
            public long LastResyncTick = long.MinValue;
        }

        readonly IHostTransport _transport;
        readonly Dictionary<ulong, Peer> _peers = new Dictionary<ulong, Peer>();
        readonly List<Command> _pending = new List<Command>();
        readonly List<TickBundle> _unsent = new List<TickBundle>();
        readonly Dictionary<long, ulong> _hashes = new Dictionary<long, ulong>();
        TickClock _clock;
        int _snapshotId;

        /// <summary>Raised when a client's hash disagrees with ours (args: client id, tick).</summary>
        public event Action<ulong, long> DesyncDetected;

#pragma warning disable 67 // never raised: the host's sim is authoritative and never replaced
        public event Action<Simulation> SimReplaced;
#pragma warning restore 67

        public HostSession(Simulation sim, IHostTransport transport)
        {
            Sim = sim;
            _transport = transport;
            Commands = new Sink(this);
            // The host is always player 0.
            if (sim.World.Players.Get(0) == null) Enqueue(new PlayerJoinCommand(0));
        }

        public Simulation Sim { get; }
        public ICommandSink Commands { get; }
        public byte LocalPlayerId => 0;
        public float TickAlpha => _clock.Alpha;
        public int PeerCount => _peers.Count;

        public bool HasRoomForPlayer => _peers.Count < PlayerManager.MaxPlayers - 1;

        /// <summary>Call when a client connects. Returns false if the game is full.</summary>
        public bool OnClientConnected(ulong clientId)
        {
            if (_peers.ContainsKey(clientId)) return true;
            byte playerId = FreePlayerId();
            if (playerId == 0) return false;

            // Send already-run ticks to existing peers first; the new peer gets them via the snapshot.
            Flush();
            var peer = new Peer { ClientId = clientId, PlayerId = playerId };
            _peers.Add(clientId, peer);
            // Queued before the snapshot is taken, so it runs in a tick the client will receive.
            if (Sim.World.Players.Get(playerId) == null) Enqueue(new PlayerJoinCommand(playerId));
            SendSnapshot(peer);
            return true;
        }

        public void OnClientDisconnected(ulong clientId) => _peers.Remove(clientId);

        public byte? PlayerIdOf(ulong clientId) => _peers.TryGetValue(clientId, out var p) ? p.PlayerId : (byte?)null;

        public void OnMessage(ulong clientId, byte[] message)
        {
            if (!_peers.TryGetValue(clientId, out var peer) || message.Length == 0) return;
            using var r = new BinaryReader(new MemoryStream(message));
            switch ((MessageType)r.ReadByte())
            {
                case MessageType.Command:
                    var command = CommandCodec.Read(r);
                    // Clients can't issue host-only commands or speak for another player.
                    if (command is PlayerJoinCommand) return;
                    command.PlayerId = peer.PlayerId;
                    Enqueue(command);
                    break;
                case MessageType.HashReport:
                    CheckHash(peer, r.ReadInt64(), r.ReadUInt64());
                    break;
            }
        }

        public void Advance(double deltaSeconds)
        {
            _clock.Accumulator += deltaSeconds;
            int steps = 0;
            while (_clock.Accumulator >= TickClock.TickLength && steps < TickClock.MaxTicksPerAdvance)
            {
                StepOnce();
                _clock.Accumulator -= TickClock.TickLength;
                steps++;
            }
            if (steps == TickClock.MaxTicksPerAdvance) _clock.Accumulator = 0;
            Flush();
        }

        /// <summary>Runs exactly one tick (tests and headless hosts use this directly).</summary>
        public void StepOnce()
        {
            long tick = Sim.Tick;
            var commands = new List<Command>(_pending);
            _pending.Clear();
            foreach (var c in commands) Sim.Schedule(tick, c);
            Sim.Step();
            _unsent.Add(new TickBundle(tick, commands));

            if (tick % Protocol.HashInterval == 0)
            {
                _hashes[tick] = Sim.ComputeHash();
                _hashes.Remove(tick - HashHistoryTicks);
            }
        }

        /// <summary>Sends ticks run since the last flush to every client.</summary>
        public void Flush()
        {
            if (_unsent.Count == 0) return;
            if (_peers.Count > 0)
            {
                var messages = Protocol.EncodeTickBatches(_unsent);
                foreach (var peer in _peers.Values)
                foreach (var m in messages)
                    _transport.SendToClient(peer.ClientId, m);
            }
            _unsent.Clear();
        }

        void Enqueue(Command command) => _pending.Add(command);

        void SendSnapshot(Peer peer)
        {
            // Unflushed ticks are already inside the snapshot; flush so the peer's tick stream
            // continues exactly from the snapshot tick (it discards anything older).
            Flush();
            var parts = Protocol.EncodeSnapshot(++_snapshotId, peer.PlayerId, Snapshot.Save(Sim));
            foreach (var part in parts) _transport.SendToClient(peer.ClientId, part);
            peer.LastResyncTick = Sim.Tick;
        }

        void CheckHash(Peer peer, long tick, ulong hash)
        {
            if (!_hashes.TryGetValue(tick, out ulong ours) || ours == hash) return;
            DesyncDetected?.Invoke(peer.ClientId, tick);
            if (Sim.Tick - peer.LastResyncTick >= ResyncCooldownTicks) SendSnapshot(peer);
        }

        byte FreePlayerId()
        {
            for (byte id = 1; id < PlayerManager.MaxPlayers; id++)
            {
                bool taken = false;
                foreach (var p in _peers.Values)
                    if (p.PlayerId == id) taken = true;
                if (!taken) return id;
            }
            return 0;
        }

        sealed class Sink : ICommandSink
        {
            readonly HostSession _host;
            public Sink(HostSession host) => _host = host;

            public void Submit(Command command)
            {
                command.PlayerId = 0;
                _host.Enqueue(command);
            }
        }
    }
}
