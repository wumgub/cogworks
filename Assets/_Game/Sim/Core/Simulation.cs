using System;
using System.Collections.Generic;

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

        /// <summary>The tick that the next <see cref="Step"/> will process.</summary>
        public long Tick { get; private set; }

        public Simulation(uint seed)
        {
            World = new World(seed);
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
            if (_scheduled.TryGetValue(Tick, out var commands))
            {
                _scheduled.Remove(Tick);
                foreach (var command in commands) command.Apply(World);
            }

            // Later milestones: update entities (drills, belts, machines, kinetic networks) here.
            Tick++;
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
