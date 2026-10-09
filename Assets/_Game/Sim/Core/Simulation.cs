namespace Cogworks.Sim
{
    /// <summary>
    /// Owns the world and advances it in fixed ticks. Presentation code calls <see cref="Step"/>
    /// at <see cref="TicksPerSecond"/>; nothing in here reads wall-clock time.
    /// </summary>
    public sealed class Simulation
    {
        public const int TicksPerSecond = 60;

        public World World { get; }
        public long Tick { get; private set; }

        public Simulation(uint seed)
        {
            World = new World(seed);
        }

        public void Step()
        {
            // Milestone 2: apply commands scheduled for this tick, in order, before updating entities.
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
