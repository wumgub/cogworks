using System;
using Cogworks.Sim;

namespace Cogworks.Net
{
    /// <summary>
    /// Drives a simulation for one game mode (single-player, host, or client). Presentation code calls
    /// <see cref="Advance"/> every frame and reads <see cref="Sim"/>; it never steps the sim itself.
    /// </summary>
    public interface ISession
    {
        /// <summary>Null on a client until the first snapshot arrives.</summary>
        Simulation Sim { get; }

        ICommandSink Commands { get; }

        /// <summary>This peer's player slot (0 = host or single-player).</summary>
        byte LocalPlayerId { get; }

        /// <summary>Raised when <see cref="Sim"/> is replaced (client first snapshot or desync resync).</summary>
        event Action<Simulation> SimReplaced;

        /// <summary>Advances by wall-clock seconds; steps zero or more ticks.</summary>
        void Advance(double deltaSeconds);

        /// <summary>Fraction of the way to the next tick, for interpolating visuals.</summary>
        float TickAlpha { get; }
    }

    /// <summary>Fixed-timestep accumulator shared by the sessions.</summary>
    internal struct TickClock
    {
        public const double TickLength = 1.0 / Simulation.TicksPerSecond;
        /// <summary>Caps catch-up work after a hitch so the game doesn't spiral.</summary>
        public const int MaxTicksPerAdvance = 5;

        public double Accumulator;

        public float Alpha => (float)(Accumulator / TickLength);
    }

    /// <summary>Single-player: steps on the clock and applies commands locally.</summary>
    public sealed class LocalSession : ISession
    {
        TickClock _clock;

        public LocalSession(Simulation sim)
        {
            Sim = sim;
            Commands = new LocalCommandSink(sim);
            if (sim.World.Players.Get(0) == null) sim.Schedule(sim.Tick, new PlayerJoinCommand(0));
        }

        public Simulation Sim { get; }
        public ICommandSink Commands { get; }
        public byte LocalPlayerId => 0;
        public float TickAlpha => _clock.Alpha;

#pragma warning disable 67 // never raised: a local session keeps one sim for its lifetime
        public event Action<Simulation> SimReplaced;
#pragma warning restore 67

        public void Advance(double deltaSeconds)
        {
            _clock.Accumulator += deltaSeconds;
            int steps = 0;
            while (_clock.Accumulator >= TickClock.TickLength && steps < TickClock.MaxTicksPerAdvance)
            {
                Sim.Step();
                _clock.Accumulator -= TickClock.TickLength;
                steps++;
            }
            if (steps == TickClock.MaxTicksPerAdvance) _clock.Accumulator = 0;
        }
    }
}
