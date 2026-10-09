using System;
using Cogworks.Sim;
using Cogworks.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Cogworks.Game
{
    /// <summary>Main menu, pause menu and the session info badge.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MenuController : MonoBehaviour
    {
        [SerializeField] NetworkGame game;
        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] BuildController build;

        VisualElement _main, _pause;
        Label _status, _pauseStatus, _pauseNote, _sessionInfo;
        TextField _joinCode, _joinAddress;
        Toggle _hostSaved;
        Button _continue, _save, _hostOnline, _joinOnline, _hostLan, _joinLan, _newWorld;
        bool _pauseOpen;

        public bool MenuOpen => _main.style.display == DisplayStyle.Flex || _pauseOpen;

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _main = root.Q("main-menu");
            _pause = root.Q("pause-menu");
            _status = root.Q<Label>("status");
            _pauseStatus = root.Q<Label>("pause-status");
            _pauseNote = root.Q<Label>("pause-note");
            _sessionInfo = root.Q<Label>("session-info");
            _joinCode = root.Q<TextField>("join-code");
            _joinAddress = root.Q<TextField>("join-address");
            _hostSaved = root.Q<Toggle>("host-saved");
            _joinCode.textEdition.placeholder = "Join code";

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
            _save = Wire(root, "save", SaveGame);
            Wire(root, "leave", () =>
            {
                SetPause(false);
                game.Leave();
            });

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
            if (kb != null && kb.escapeKey.wasPressedThisFrame && game.Mode != GameMode.None
                && build.EscapeConsumedFrame != Time.frameCount)
            {
                SetPause(!_pauseOpen);
            }

            bool open = MenuOpen;
            build.InputEnabled = !open;
            PlayerController.InputEnabled = !open;
            _sessionInfo.text = SessionInfo();
            _sessionInfo.style.display = string.IsNullOrEmpty(_sessionInfo.text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void Refresh()
        {
            bool inGame = game.Mode != GameMode.None;
            _main.style.display = inGame ? DisplayStyle.None : DisplayStyle.Flex;
            if (!inGame) SetPause(false);

            bool hasSave = SaveSystem.HasSave;
            _continue.SetEnabled(hasSave && !game.Busy);
            _hostSaved.SetEnabled(hasSave);
            if (!hasSave) _hostSaved.value = false;
            foreach (var b in new[] { _newWorld, _hostOnline, _joinOnline, _hostLan, _joinLan }) b.SetEnabled(!game.Busy);
            _status.text = game.Status;
        }

        void SetPause(bool open)
        {
            _pauseOpen = open && game.Mode != GameMode.None;
            _pause.style.display = _pauseOpen ? DisplayStyle.Flex : DisplayStyle.None;
            bool canFreeze = game.Mode == GameMode.SinglePlayer;
            bootstrap.Paused = _pauseOpen && canFreeze;
            _pauseNote.text = canFreeze ? "" : "Multiplayer keeps running while this menu is open.";
            _save.SetEnabled(game.Mode == GameMode.SinglePlayer || game.Mode == GameMode.Host);
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

        static Simulation NewWorld() => new Simulation((uint)Environment.TickCount);

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

        void SaveGame()
        {
            try
            {
                SaveSystem.Save(bootstrap.Sim);
                _pauseStatus.text = "Saved.";
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _pauseStatus.text = "Save failed: " + e.Message;
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
            b.clicked += onClick;
            return b;
        }
    }
}
