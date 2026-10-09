using Cogworks.Sim;
using UnityEngine;

namespace Cogworks.View
{
    /// <summary>Creates the simulation and steps it at a fixed tick rate, decoupled from frame rate.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] uint seed = 12345;
        [Tooltip("Caps catch-up work after a hitch so the game doesn't spiral.")]
        [SerializeField] int maxTicksPerFrame = 5;

        float _accumulator;

        public Simulation Sim { get; private set; }

        /// <summary>Where input sends commands. Swapped for a network sink in multiplayer.</summary>
        public ICommandSink Commands { get; private set; }

        /// <summary>Fraction of the way to the next tick, for interpolating visuals.</summary>
        public float TickAlpha => _accumulator * Simulation.TicksPerSecond;

        void Awake()
        {
            Sim = new Simulation(seed);
            Commands = new LocalCommandSink(Sim);
        }

        void Update()
        {
            const float tickLength = 1f / Simulation.TicksPerSecond;
            _accumulator += Time.deltaTime;

            int steps = 0;
            while (_accumulator >= tickLength && steps < maxTicksPerFrame)
            {
                Sim.Step();
                _accumulator -= tickLength;
                steps++;
            }
            if (steps == maxTicksPerFrame) _accumulator = 0f;
        }
    }
}
