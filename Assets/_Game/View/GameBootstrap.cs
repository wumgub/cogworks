using System;
using Cogworks.Net;
using Cogworks.Sim;
using UnityEngine;

namespace Cogworks.View
{
    /// <summary>
    /// Scene-wide access point for the running game. Holds the active <see cref="ISession"/> (single-player,
    /// host or client), advances it each frame, and tells views when the sim they draw has changed.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        public ISession Session { get; private set; }

        /// <summary>Null while no game is running (main menu) or a client is waiting for its snapshot.</summary>
        public Simulation Sim => Session?.Sim;

        public ICommandSink Commands => Session?.Commands;
        public byte LocalPlayerId => Session?.LocalPlayerId ?? 0;

        /// <summary>Fraction of the way to the next tick, for interpolating visuals.</summary>
        public float TickAlpha => Session?.TickAlpha ?? 0f;

        /// <summary>Freezes the sim (single-player pause menu). Multiplayer games never pause.</summary>
        public bool Paused { get; set; }

        /// <summary>Raised with the new sim (or null when the game ends). Views rebuild from scratch.</summary>
        public event Action<Simulation> SimChanged;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Begin(ISession session)
        {
            End();
            Session = session;
            Paused = false;
            session.SimReplaced += OnSimReplaced;
            if (session.Sim != null) SimChanged?.Invoke(session.Sim);
        }

        public void End()
        {
            if (Session == null) return;
            Session.SimReplaced -= OnSimReplaced;
            Session = null;
            SimChanged?.Invoke(null);
        }

        void OnSimReplaced(Simulation sim) => SimChanged?.Invoke(sim);

        void Update()
        {
            if (!Paused) Session?.Advance(Time.deltaTime);
        }
    }
}
