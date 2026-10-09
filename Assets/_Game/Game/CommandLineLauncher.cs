using System;
using System.Collections.Generic;
using System.IO;
using Cogworks.Sim;
using Cogworks.View;
using UnityEngine;

namespace Cogworks.Game
{
    /// <summary>
    /// Starts a game straight from command-line flags, skipping the menu. Used for dedicated testing:
    /// <code>
    ///   -cw-mode single|host|client   -cw-ip 127.0.0.1   -cw-port 7777   -cw-seed 123
    ///   -cw-autotest 60               # issue random build commands for N seconds, then quit
    ///   -cw-log path.txt              # write "tick hash" lines every hash interval
    /// </code>
    /// Comparing the logs from a host and its clients proves they ran identical simulations.
    /// </summary>
    public sealed class CommandLineLauncher : MonoBehaviour
    {
        [SerializeField] NetworkGame game;
        [SerializeField] GameBootstrap bootstrap;

        float _autotestSeconds;
        float _startTime;
        Rng _rng;
        StreamWriter _log;
        Simulation _loggedSim;
        bool _active;

        void Start()
        {
            var args = ParseArgs(Environment.GetCommandLineArgs());
            if (!args.TryGetValue("-cw-mode", out string mode)) return;
            _active = true;

            uint seed = args.TryGetValue("-cw-seed", out string s) && uint.TryParse(s, out uint parsed) ? parsed : 12345u;
            ushort port = args.TryGetValue("-cw-port", out string p) && ushort.TryParse(p, out ushort pp) ? pp : NetworkGame.DefaultPort;
            string ip = args.TryGetValue("-cw-ip", out string i) ? i : "127.0.0.1";
            if (args.TryGetValue("-cw-autotest", out string a)) float.TryParse(a, out _autotestSeconds);
            if (args.TryGetValue("-cw-log", out string logPath)) _log = new StreamWriter(logPath) { AutoFlush = true };
            _rng = new Rng(seed ^ (ulong)Environment.TickCount);
            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            switch (mode)
            {
                case "host": game.HostLan(new Simulation(seed), port); break;
                case "client": game.JoinLan(ip, port); break;
                default: game.StartSinglePlayer(new Simulation(seed)); break;
            }
            _startTime = Time.realtimeSinceStartup;
            Log($"# mode={mode} seed={seed} started");
        }

        void Update()
        {
            if (!_active) return;
            var sim = bootstrap.Sim;
            if (sim != _loggedSim)
            {
                if (_loggedSim != null) _loggedSim.Stepped -= OnStepped;
                _loggedSim = sim;
                if (sim != null)
                {
                    sim.Stepped += OnStepped;
                    Log($"# sim ready at tick {sim.Tick} as player {bootstrap.LocalPlayerId}");
                }
            }

            if (_autotestSeconds <= 0) return;
            float elapsed = Time.realtimeSinceStartup - _startTime;
            if (sim != null && elapsed < _autotestSeconds && _rng.Range(0, 4) == 0) bootstrap.Commands.Submit(RandomCommand());

            // Linger after the test so clients finish receiving ticks before the host goes away.
            if (elapsed > _autotestSeconds + (game.Mode == GameMode.Host ? 8f : 5f))
            {
                Log($"# done at tick {sim?.Tick} buildings={sim?.World.Buildings.Count}");
                _log?.Dispose();
                _log = null;
                _active = false;
                game.Leave();
                Application.Quit();
            }
        }

        void OnStepped(long tick)
        {
            if (tick % Net.Protocol.HashInterval == 0) Log($"{tick} {_loggedSim.ComputeHash():x16}");
        }

        Command RandomCommand()
        {
            var tile = new Int2(_rng.Range(-15, 15), _rng.Range(-15, 15));
            var dir = (Direction)_rng.Range(0, 4);
            if (_rng.Range(0, 4) == 0) return new RemoveBuildingCommand(tile);
            var spec = BuildingCatalog.All[_rng.Range(0, BuildingCatalog.All.Count)];
            return new PlaceBuildingCommand(spec.Id, tile, spec.Rotatable ? dir : Direction.North);
        }

        void Log(string line)
        {
            _log?.WriteLine(line);
            Debug.Log("[Cogworks] " + line);
        }

        static Dictionary<string, string> ParseArgs(string[] argv)
        {
            var map = new Dictionary<string, string>();
            for (int i = 0; i < argv.Length; i++)
            {
                if (!argv[i].StartsWith("-cw-")) continue;
                map[argv[i]] = i + 1 < argv.Length && !argv[i + 1].StartsWith("-") ? argv[i + 1] : "";
            }
            return map;
        }
    }
}
