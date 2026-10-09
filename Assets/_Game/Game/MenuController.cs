using System;
using Cogworks.Sim;
using Cogworks.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cogworks.Game
{
    /// <summary>Main menu, pause menu, settings, help, session badge, and autosave.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuController : MonoBehaviour
    {
        const float AutosaveSeconds = 300f;

        [SerializeField] NetworkGame game;
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] BuildController build;
        [SerializeField] PanelsController panels;
        [SerializeField] HotbarView hud;

        VisualElement _main, _pause, _settings, _help, _joinCodeBox;
        Label _status, _pauseStatus, _pauseNote, _sessionInfo, _pauseJoinCode;
        TextField _joinCode, _joinAddress;
        Toggle _hostSaved, _sandbox;
        Button _continue, _save, _hostOnline, _joinOnline, _hostLan, _joinLan, _newWorld;
        bool _pauseOpen;
        float _nextAutosave;

        bool OverlayOpen => _settings.style.display == DisplayStyle.Flex || _help.style.display == DisplayStyle.Flex;

        public bool MenuOpen => _main.style.display == DisplayStyle.Flex || _pauseOpen || OverlayOpen;

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _main = root.Q("main-menu");
            _pause = root.Q("pause-menu");
            _settings = root.Q("settings-menu");
            _help = root.Q("help-menu");
            _joinCodeBox = root.Q("join-code-box");
            _status = root.Q<Label>("status");
            _pauseStatus = root.Q<Label>("pause-status");
            _pauseNote = root.Q<Label>("pause-note");
            _pauseJoinCode = root.Q<Label>("pause-join-code");
            _sessionInfo = root.Q<Label>("session-info");
            _joinCode = root.Q<TextField>("join-code");
            _joinAddress = root.Q<TextField>("join-address");
            _hostSaved = root.Q<Toggle>("host-saved");
            _sandbox = root.Q<Toggle>("new-sandbox");
            _joinCode.textEdition.placeholder = "Join code";
            root.Q<Label>("help-text").text = HelpText;

            _newWorld = Wire(root, "new-world", () => game.StartSinglePlayer(NewWorld()));
            _continue = Wire(root, "continue", ContinueSaved);
            _hostOnline = Wire(root, "host-online", () => _ = game.HostOnline(WorldForHosting()));
            _joinOnline = Wire(root, "join-online", () =>
            {
                if (string.IsNullOrWhiteSpace(_joinCode.value)) SetStatus("Enter the host's join code.");
                else _ = game.JoinOnline(_joinCode.value);
            });
            _hostLan = Wire(root, "host-lan", () => game.HostLan(WorldForHosting()));
            _joinLan = Wire(root, "join-lan", JoinLan);
            Wire(root, "quit", Quit);
            Wire(root, "resume", () => SetPause(false));
            _save = Wire(root, "save", () => SaveGame(manual: true));
            Wire(root, "leave", () =>
            {
                SetPause(false);
                game.Leave();
            });
            Wire(root, "copy-code", () =>
            {
                GUIUtility.systemCopyBuffer = game.JoinCode ?? "";
                _pauseStatus.text = "Join code copied.";
            });
            Wire(root, "open-settings", () => ShowOverlay(_settings));
            Wire(root, "pause-settings", () => ShowOverlay(_settings));
            Wire(root, "open-help", () => ShowOverlay(_help));
            Wire(root, "pause-help", () => ShowOverlay(_help));
            Wire(root, "settings-back", () => ShowOverlay(null));
            Wire(root, "help-back", () => ShowOverlay(null));

            var master = root.Q<Slider>("master-volume");
            master.value = GameSettings.MasterVolume;
            master.RegisterValueChangedCallback(e => GameSettings.MasterVolume = e.newValue);
            var sfx = root.Q<Slider>("sfx-volume");
            sfx.value = GameSettings.SfxVolume;
            sfx.RegisterValueChangedCallback(e => GameSettings.SfxVolume = e.newValue);
            var fullscreen = root.Q<Toggle>("fullscreen");
            fullscreen.value = GameSettings.Fullscreen;
            fullscreen.RegisterValueChangedCallback(e => GameSettings.Fullscreen = e.newValue);

            ShowOverlay(null);
            game.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (game != null) game.Changed -= Refresh;
        }

        void Update()
        {
            var kb = Keyboard.current;
            bool canSave = game.Mode == GameMode.SinglePlayer || game.Mode == GameMode.Host;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame && OverlayOpen) ShowOverlay(null);
                else if (kb.escapeKey.wasPressedThisFrame && game.Mode != GameMode.None
                    && build.EscapeConsumedFrame != Time.frameCount && panels.EscapeConsumedFrame != Time.frameCount)
                {
                    SetPause(!_pauseOpen);
                }
                if (kb.f5Key.wasPressedThisFrame && canSave && bootstrap.Sim != null) SaveGame(manual: true);
                if (kb.f1Key.wasPressedThisFrame) ShowOverlay(OverlayOpen ? null : _help);
            }

            if (canSave && bootstrap.Sim != null && Time.unscaledTime >= _nextAutosave)
            {
                if (_nextAutosave > 0f) SaveGame(manual: false);
                _nextAutosave = Time.unscaledTime + AutosaveSeconds;
            }

            bool open = MenuOpen;
            build.InputEnabled = !open && !panels.AnyOpen;
            panels.KeysEnabled = !open;
            PlayerController.InputEnabled = !open;
            _sessionInfo.text = SessionInfo();
            _sessionInfo.style.display = string.IsNullOrEmpty(_sessionInfo.text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void Refresh()
        {
            bool inGame = game.Mode != GameMode.None;
            _main.style.display = inGame ? DisplayStyle.None : DisplayStyle.Flex;
            if (!inGame)
            {
                SetPause(false);
                _nextAutosave = 0f;
            }

            bool hasSave = SaveSystem.HasSave;
            _continue.SetEnabled(hasSave && !game.Busy);
            _hostSaved.SetEnabled(hasSave);
            if (!hasSave) _hostSaved.value = false;
            foreach (var b in new[] { _newWorld, _hostOnline, _joinOnline, _hostLan, _joinLan }) b.SetEnabled(!game.Busy);
            _status.text = game.Status;
        }

        /// <summary>For automated screenshots: "help", "settings" or anything else to close overlays.</summary>
        public void ShowOverlayByName(string name) => ShowOverlay(name == "help" ? _help : name == "settings" ? _settings : null);

        void ShowOverlay(VisualElement overlay)
        {
            _settings.style.display = overlay == _settings ? DisplayStyle.Flex : DisplayStyle.None;
            _help.style.display = overlay == _help ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void SetPause(bool open)
        {
            _pauseOpen = open && game.Mode != GameMode.None;
            _pause.style.display = _pauseOpen ? DisplayStyle.Flex : DisplayStyle.None;
            bool canFreeze = game.Mode == GameMode.SinglePlayer;
            bootstrap.Paused = _pauseOpen && canFreeze;
            _pauseNote.text = canFreeze ? "" : "Multiplayer keeps running while this menu is open.";
            _save.SetEnabled(game.Mode == GameMode.SinglePlayer || game.Mode == GameMode.Host);
            _pauseJoinCode.text = game.JoinCode ?? "";
            _joinCodeBox.style.display = string.IsNullOrEmpty(game.JoinCode) ? DisplayStyle.None : DisplayStyle.Flex;
            _pauseStatus.text = "";
        }

        string SessionInfo()
        {
            switch (game.Mode)
            {
                case GameMode.Host:
                    string where = game.JoinCode != null ? $"Join code: {game.JoinCode}" : "LAN host";
                    return $"{where}  ·  {game.PlayerCount}/{PlayerManager.MaxPlayers} players";
                case GameMode.Client:
                    return bootstrap.Sim == null ? "Loading world..." : $"Connected  ·  player {bootstrap.LocalPlayerId + 1}";
                default:
                    return "";
            }
        }

        Simulation NewWorld() => new Simulation((uint)Environment.TickCount, _sandbox.value);

        Simulation WorldForHosting()
        {
            if (_hostSaved.value && SaveSystem.TryLoad(out var sim, out _)) return sim;
            return NewWorld();
        }

        void ContinueSaved()
        {
            if (SaveSystem.TryLoad(out var sim, out string error)) game.StartSinglePlayer(sim);
            else SetStatus("Couldn't load save: " + error);
        }

        void SaveGame(bool manual)
        {
            try
            {
                SaveSystem.Save(bootstrap.Sim);
                _pauseStatus.text = "Saved.";
                hud.Toast(manual ? "Game saved." : "Autosaved.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _pauseStatus.text = "Save failed: " + e.Message;
                hud.Toast("Save failed: " + e.Message);
            }
        }

        void JoinLan()
        {
            string text = (_joinAddress.value ?? "").Trim();
            ushort port = NetworkGame.DefaultPort;
            int colon = text.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(text.Substring(colon + 1), out ushort parsed))
            {
                port = parsed;
                text = text.Substring(0, colon);
            }
            if (text.Length == 0) SetStatus("Enter the host's IP address.");
            else game.JoinLan(text, port);
        }

        void SetStatus(string text) => _status.text = text;

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static Button Wire(VisualElement root, string name, Action onClick)
        {
            var b = root.Q<Button>(name);
            b.clicked += () =>
            {
                SoundFx.Instance?.Play(Sfx.Click, 0.7f);
                onClick();
            };
            return b;
        }

        const string HelpText =
@"<b>Goal</b>
Build an automated factory: mine ore, smelt it, craft parts, and research better machines. Up to 4 friends can build together.

<b>Moving and building</b>
WASD moves. Scroll zooms. Number keys 1-9 and 0 pick a building; R rotates it (Shift+R the other way); left-click places it and dragging places a line. Right-click removes a building and refunds it, contents included. Q drops the tool, or with no tool picks up the type of the building under the cursor.

<b>Getting started</b>
1. With no tool selected, hold left-click on an ore patch to mine it by hand.
2. Press Tab for your inventory and hand crafting. Stone makes furnaces; furnaces smelt ore into plates when given coal.
3. Click any building to open it, and click items to move them in or out.

<b>Rotational power</b>
Drills and assemblers need rotation, like in Create. Generators make it: a Hand Crank turns for 5 seconds each time you press E on it, a Water Wheel (placed next to water) turns forever, and a Steam Engine burns coal. Shafts carry rotation in a straight line; Gearboxes connect all four sides. Machines next to each other share power too.
Every machine adds stress. If a network needs more stress (SU) than its generators supply, the whole network stops and its cogs turn red. Add generators or split the network.

<b>Logistics</b>
Conveyor belts carry up to 4 items per tile and feed into belts ahead of them, including from the side. Inserters move items from the tile behind them to the tile in front (follow the arrow). Drills push ore into whatever is in front of them.

<b>Research</b>
Craft Automation Science packs, build a Research Lab, put packs in it, and press T to choose a project. Research unlocks assemblers, water wheels, steam engines and fast belts.

<b>Saving</b>
F5 saves, and the game autosaves every 5 minutes. The host's save holds the whole multiplayer world; tick 'Host my saved world' to continue it with friends.

<b>Multiplayer</b>
Host Online gives a join code for friends (needs the project linked to Unity Cloud). Host LAN plus Join LAN works on a local network, or over the internet with port 7777 forwarded.";
    }
}
