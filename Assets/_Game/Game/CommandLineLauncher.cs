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
    ///   -cw-autotest 60               # issue random commands of every kind for N seconds, then quit
    ///   -cw-sandbox                   # host a sandbox world (free building) so random builds succeed
    ///   -cw-log path.txt              # write "tick hash" lines every hash interval
    ///   -cw-demo                      # single-player sandbox with a small working factory
    ///   -cw-shots dir                 # with -cw-demo: screenshot the world and each panel, then quit
    ///   -cw-menushots dir             # screenshot the main menu, help and settings, then quit
    ///   -cw-demo (with host)          # host the demo factory in a sandbox world
    ///   -cw-wander                    # walk the local avatar around the factory (showcase captures)
    ///   -cw-shots dir (host/client)   # after players join, screenshot this player's view, then quit
    ///   -cw-frames dir                # with -cw-demo: save ~4 s of frames for an animated GIF, then quit
    /// </code>
    /// Comparing the logs from a host and its clients proves they ran identical simulations.
    /// </summary>
    public sealed class CommandLineLauncher : MonoBehaviour
    {
        [SerializeField] NetworkGame game;
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] PanelsController panels;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] MenuController menu;

        float _autotestSeconds;
        float _startTime;
        Rng _rng;
        StreamWriter _log;
        Simulation _loggedSim;
        bool _active;
        bool _wander;
        Vector2 _wanderCentre;
        bool _wanderCentreFound;

        void Start()
        {
            var args = ParseArgs(Environment.GetCommandLineArgs());
            if (args.TryGetValue("-cw-menushots", out string menuShots))
            {
                StartCoroutine(DemoScenario.CaptureMenuShots(menuShots, menu, cameraRig));
                return;
            }
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

            _wander = args.ContainsKey("-cw-wander");
            args.TryGetValue("-cw-shots", out string shotDir);
            switch (mode)
            {
                case "host":
                {
                    bool hostDemo = args.ContainsKey("-cw-demo");
                    var hostSim = new Simulation(seed, sandbox: hostDemo || args.ContainsKey("-cw-sandbox"));
                    if (hostDemo) DemoScenario.Build(hostSim);
                    game.HostLan(hostSim, port);
                    if (shotDir != null) StartCoroutine(DemoScenario.CaptureMultiplayerShots(shotDir, "host", cameraRig, 16f));
                    break;
                }
                case "client":
                    game.JoinLan(ip, port);
                    if (shotDir != null) StartCoroutine(DemoScenario.CaptureMultiplayerShots(shotDir, "client", cameraRig, 13f));
                    break;
                default:
                    bool demo = args.ContainsKey("-cw-demo");
                    var sim = new Simulation(seed, sandbox: demo);
                    Vector2 focus = demo ? DemoScenario.Build(sim) : Vector2.zero;
                    game.StartSinglePlayer(sim);
                    if (demo)
                    {
                        var avatar = FindAnyObjectByType<PlayerAvatar>();
                        if (avatar != null) avatar.transform.position = new Vector3(focus.x, focus.y, 0f);
                    }
                    if (demo && shotDir != null)
                        StartCoroutine(DemoScenario.CaptureShots(shotDir, bootstrap, panels, cameraRig));
                    if (demo && args.TryGetValue("-cw-frames", out string frames))
                        StartCoroutine(DemoScenario.CaptureFrames(frames, cameraRig));
                    break;
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

            if (_wander) Wander(sim);

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

        /// <summary>
        /// Showcase helper: walk the local avatar in a loop near the factory (found from the world, so clients
        /// can do it too), each player on its own path. Movement replicates like normal play.
        /// </summary>
        void Wander(Simulation sim)
        {
            if (sim == null) return;
            PlayerAvatar local = null;
            foreach (var a in FindObjectsByType<PlayerAvatar>(FindObjectsInactive.Exclude))
                if (a.Controller.IsLocal) local = a;
            if (local == null) return;
            if (!_wanderCentreFound)
            {
                foreach (var b in sim.World.Buildings.All)
                {
                    if (b.Spec != BuildingCatalog.Lab) continue;
                    _wanderCentre = new Vector2(b.Origin.X + 1f, b.Origin.Y - 1.5f);
                    _wanderCentreFound = true;
                    break;
                }
                if (!_wanderCentreFound) return;
            }
            int id = bootstrap.LocalPlayerId;
            float t = Time.time * 0.6f + id * 1.7f;
            float r = 2.5f + id * 0.9f;
            var target = _wanderCentre + new Vector2(Mathf.Cos(t) * r * 1.4f, Mathf.Sin(t) * r * 0.8f);
            local.transform.position = Vector3.MoveTowards(local.transform.position, target, 6f * Time.deltaTime);
        }

        void OnStepped(long tick)
        {
            if (tick % Net.Protocol.HashInterval == 0) Log($"{tick} {_loggedSim.ComputeHash():x16}");
        }

        /// <summary>A random mix of every command type, aimed at a small area so they interact.</summary>
        Command RandomCommand()
        {
            var tile = new Int2(_rng.Range(-15, 15), _rng.Range(-15, 15));
            var dir = (Direction)_rng.Range(0, 4);
            var items = ItemCatalog.All;
            var recipes = RecipeCatalog.All;
            switch (_rng.Range(0, 12))
            {
                case 0: return new RemoveBuildingCommand(tile);
                case 1: return new CrankCommand(tile);
                case 2: return new MineTileCommand(tile);
                case 3: return new HandCraftCommand(recipes[_rng.Range(0, recipes.Count)].Id, _rng.Range(1, 4));
                case 4: return new SetRecipeCommand(tile, recipes[_rng.Range(0, recipes.Count)].Id);
                case 5: return new TransferItemsCommand(tile, items[_rng.Range(0, items.Count)].Id, _rng.Range(1, 20), _rng.Range(0, 2) == 0);
                case 6: return new SetResearchCommand((ushort)_rng.Range(0, 6));
                case 7: return new SetBuildingDirectionCommand(tile, dir);
                default:
                    var spec = BuildingCatalog.All[_rng.Range(0, BuildingCatalog.All.Count)];
                    return new PlaceBuildingCommand(spec.Id, tile, spec.Rotatable ? dir : Direction.North);
            }
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
