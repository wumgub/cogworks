namespace Cogworks.Sim
{
    /// <summary>
    /// Where input sends commands. Single-player schedules them locally; in multiplayer (milestone 3)
    /// a network sink forwards them to the host, which picks the tick and broadcasts.
    /// </summary>
    public interface ICommandSink
    {
        void Submit(Command command);
    }

    public sealed class LocalCommandSink : ICommandSink
    {
        readonly Simulation _sim;

        public LocalCommandSink(Simulation sim)
        {
            _sim = sim;
        }

        public void Submit(Command command) => _sim.Schedule(_sim.Tick, command);
    }
}
