using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>
    /// Owns the world and advances it in fixed ticks. Presentation code calls <see cref="Step"/>
    /// at <see cref="TicksPerSecond"/>; nothing in here reads wall-clock time.
    /// </summary>
    public sealed class Simulation
    {
        public const int TicksPerSecond = 60;

        // Commands keyed by the tick they run on. Within a tick they run in scheduling order,
        // which in multiplayer is the order the host broadcast them.
        readonly SortedDictionary<long, List<Command>> _scheduled = new SortedDictionary<long, List<Command>>();

        public World World { get; }

        /// <summary>Raised after each tick with the tick number that just ran. Presentation/diagnostics only.</summary>
        public event Action<long> Stepped;

        /// <summary>The tick that the next <see cref="Step"/> will process.</summary>
        public long Tick { get; private set; }

        public Simulation(uint seed, bool sandbox = false)
        {
            World = new World(seed, sandbox);
        }

        public void Schedule(long tick, Command command)
        {
            if (tick < Tick)
                throw new ArgumentOutOfRangeException(nameof(tick), $"Tick {tick} already ran (current {Tick}).");
            if (!_scheduled.TryGetValue(tick, out var list))
            {
                list = new List<Command>();
                _scheduled.Add(tick, list);
            }
            list.Add(command);
        }

        public void Step()
        {
            World.CurrentTick = Tick;
            if (_scheduled.TryGetValue(Tick, out var commands))
            {
                _scheduled.Remove(Tick);
                foreach (var command in commands) command.Apply(World);
            }

            World.Tick();
            Tick++;
            Stepped?.Invoke(Tick - 1);
        }

        /// <summary>Restores tick, world and pending commands from <see cref="Write"/> output.</summary>
        internal static Simulation Read(BinaryReader r)
        {
            uint seed = r.ReadUInt32();
            bool sandbox = r.ReadBoolean();
            var sim = new Simulation(seed, sandbox) { Tick = r.ReadInt64() };
            sim.World.CurrentTick = sim.Tick;
            sim.World.Read(r);
            int tickCount = r.ReadInt32();
            for (int i = 0; i < tickCount; i++)
            {
                long tick = r.ReadInt64();
                int count = r.ReadInt32();
                for (int c = 0; c < count; c++) sim.Schedule(tick, CommandCodec.Read(r));
            }
            return sim;
        }

        internal void Write(BinaryWriter w)
        {
            w.Write(World.Seed);
            w.Write(World.Sandbox);
            w.Write(Tick);
            World.Write(w);
            w.Write(_scheduled.Count);
            foreach (var pair in _scheduled)
            {
                w.Write(pair.Key);
                w.Write(pair.Value.Count);
                foreach (var command in pair.Value) CommandCodec.Write(w, command);
            }
        }

        /// <summary>Fingerprint of tick + world state, compared between peers to detect desyncs.</summary>
        public ulong ComputeHash()
        {
            var h = Fnv64.Create();
            h.Add(Tick);
            World.AddToHash(ref h);
            return h.Value;
        }
    }
}
