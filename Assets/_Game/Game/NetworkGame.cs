using System;
using System.Threading.Tasks;
using Cogworks.Net;
using Cogworks.Sim;
using Cogworks.View;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using MultiplayerSession = Unity.Services.Multiplayer.ISession;

namespace Cogworks.Game
{
    public enum GameMode
    {
        None,
        SinglePlayer,
        Host,
        Client,
    }

    /// <summary>
    /// Starts and stops games, and bridges the engine-free sessions to Netcode for GameObjects.
    /// All sim traffic goes through one named message on a reliable, ordered, fragmenting channel.
    /// Online play uses Unity Relay through Multiplayer Services sessions (join by code); LAN play
    /// connects directly by IP.
    /// </summary>
    public sealed class NetworkGame : MonoBehaviour, IHostTransport, IClientTransport
    {
        public const ushort DefaultPort = 7777;
        const string Channel = "cogworks";

        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] NetworkManager network;
        [SerializeField] UnityTransport transport;
        [SerializeField] PlayerAvatar avatarPrefab;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] BuildController build;

        HostSession _host;
        ClientSession _client;
        MultiplayerSession _online;
        PlayerAvatar _offlineAvatar;
        bool _handlerRegistered;

        public GameMode Mode { get; private set; }
        public string JoinCode { get; private set; }
        public string Status { get; private set; } = "";
        public bool Busy { get; private set; }

        /// <summary>Connected players including the host. Only the host knows this; 0 elsewhere.</summary>
        public int PlayerCount => _host != null ? _host.PeerCount + 1 : 0;

        public event Action Changed;

        /// <summary>Raised when an online game ends without the player choosing to leave.</summary>
        public event Action<string> Disconnected;

        void Awake()
        {
            PlayerAvatar.LocalSpawned += OnLocalAvatar;
            network.NetworkConfig.ConnectionApproval = true;
            network.ConnectionApprovalCallback = Approve;
            network.OnClientConnectedCallback += OnClientConnected;
            network.OnClientDisconnectCallback += OnClientDisconnected;
            network.OnServerStarted += RegisterHandler;
            network.OnClientStarted += RegisterHandler;
            network.OnServerStopped += _ => _handlerRegistered = false;
            network.OnClientStopped += _ => _handlerRegistered = false;
            // Snapshot parts and tick batches stay under 6 KB, but leave headroom for big command bursts.
            transport.MaxPayloadSize = 64 * 1024;
        }

        void OnDestroy()
        {
            PlayerAvatar.LocalSpawned -= OnLocalAvatar;
        }

        // ---------------------------------------------------------------- starting games

        public void StartSinglePlayer(Simulation sim)
        {
            Leave();
            Mode = GameMode.SinglePlayer;
            bootstrap.Begin(new LocalSession(sim));
            _offlineAvatar = Instantiate(avatarPrefab, SpawnPoint(0), Quaternion.identity);
            _offlineAvatar.SetupOffline();
            SetStatus("");
        }

        public bool HostLan(Simulation sim, ushort port = DefaultPort)
        {
            Leave();
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            BeginHost(sim);
            if (!network.StartHost())
            {
                Fail($"Couldn't host on port {port} (already in use?)");
                return false;
            }
            SetStatus($"Hosting on LAN, port {port}");
            return true;
        }

        public bool JoinLan(string address, ushort port = DefaultPort)
        {
            Leave();
            transport.SetConnectionData(address, port);
            BeginClient();
            if (!network.StartClient())
            {
                Fail($"Couldn't connect to {address}:{port}");
                return false;
            }
            SetStatus($"Connecting to {address}:{port}...");
            return true;
        }

        public async Task HostOnline(Simulation sim)
        {
            Leave();
            Busy = true;
            try
            {
                SetStatus("Signing in to Unity Services...");
                await EnsureSignedIn();
                BeginHost(sim);
                SetStatus("Creating session...");
                var options = new SessionOptions { MaxPlayers = PlayerManager.MaxPlayers, IsPrivate = true }.WithRelayNetwork();
                _online = await MultiplayerService.Instance.CreateSessionAsync(options);
                JoinCode = _online.Code;
                SetStatus($"Hosting online. Join code: {JoinCode}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Fail(OnlineErrorMessage(e));
            }
            finally
            {
                Busy = false;
                Changed?.Invoke();
            }
        }

        public async Task JoinOnline(string code)
        {
            Leave();
            Busy = true;
            try
            {
                SetStatus("Signing in to Unity Services...");
                await EnsureSignedIn();
                BeginClient();
                SetStatus($"Joining {code}...");
                _online = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                JoinCode = _online.Code;
                SetStatus("Connected. Loading world...");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Fail(OnlineErrorMessage(e));
            }
            finally
            {
                Busy = false;
                Changed?.Invoke();
            }
        }

        /// <summary>Ends whatever game is running and returns to the no-game state.</summary>
        public void Leave()
        {
            if (_online != null)
            {
                var online = _online;
                _online = null;
                _ = online.LeaveAsync();
            }
            if (network.IsListening) network.Shutdown();
            if (_offlineAvatar != null) Destroy(_offlineAvatar.gameObject);
            _offlineAvatar = null;
            _host = null;
            _client = null;
            JoinCode = null;
            Mode = GameMode.None;
            bootstrap.End();
            Changed?.Invoke();
        }

        void BeginHost(Simulation sim)
        {
            _host = new HostSession(sim, this);
            _host.DesyncDetected += (clientId, tick) =>
                Debug.LogWarning($"[Cogworks] Desync with client {clientId} at tick {tick}; resending world.");
            Mode = GameMode.Host;
            bootstrap.Begin(_host);
        }

        void BeginClient()
        {
            _client = new ClientSession(this);
            _client.SimReplaced += _ => SetStatus(Mode == GameMode.Client ? "Connected" : Status);
            Mode = GameMode.Client;
            bootstrap.Begin(_client);
        }

        static async Task EnsureSignedIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static string OnlineErrorMessage(Exception e)
        {
            string msg = e.Message;
            if (msg.Contains("project") || msg.Contains("cloud") || msg.Contains("Project ID"))
                return "Online play needs the project linked to Unity Cloud (Edit > Project Settings > Services). LAN still works.";
            return "Online error: " + msg;
        }

        // ---------------------------------------------------------------- Netcode callbacks

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool isSelf = request.ClientNetworkId == NetworkManager.ServerClientId;
            response.Approved = isSelf || (_host != null && _host.HasRoomForPlayer);
            response.Reason = response.Approved ? null : "Game is full (4 players max)";
            response.CreatePlayerObject = response.Approved;
            response.Position = SpawnPoint(isSelf ? 0 : (_host?.PeerCount ?? 0) + 1);
            response.Pending = false;
        }

        void OnClientConnected(ulong clientId)
        {
            if (Mode == GameMode.Host && clientId != NetworkManager.ServerClientId)
            {
                if (!_host.OnClientConnected(clientId)) network.DisconnectClient(clientId, "Game is full");
                Changed?.Invoke();
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (Mode == GameMode.Host)
            {
                _host.OnClientDisconnected(clientId);
                Changed?.Invoke();
            }
            else if (Mode == GameMode.Client && (clientId == network.LocalClientId || !network.IsConnectedClient))
            {
                string reason = string.IsNullOrEmpty(network.DisconnectReason) ? "Lost connection to host" : network.DisconnectReason;
                Leave();
                SetStatus(reason);
                Disconnected?.Invoke(reason);
            }
        }

        void RegisterHandler()
        {
            if (_handlerRegistered) return;
            network.CustomMessagingManager.RegisterNamedMessageHandler(Channel, OnNamedMessage);
            _handlerRegistered = true;
        }

        void OnNamedMessage(ulong senderId, FastBufferReader reader)
        {
            reader.ReadValueSafe(out int length);
            var data = new byte[length];
            reader.ReadBytesSafe(ref data, length);
            if (_host != null) _host.OnMessage(senderId, data);
            else _client?.OnMessage(data);
        }

        public void SendToClient(ulong clientId, byte[] message) => Send(clientId, message);

        public void SendToHost(byte[] message) => Send(NetworkManager.ServerClientId, message);

        void Send(ulong target, byte[] message)
        {
            if (!network.IsListening || network.CustomMessagingManager == null) return;
            using var writer = new FastBufferWriter(message.Length + sizeof(int), Allocator.Temp);
            writer.WriteValueSafe(message.Length);
            writer.WriteBytesSafe(message);
            // One delivery type for everything: ordering is only guaranteed within a channel.
            network.CustomMessagingManager.SendNamedMessage(Channel, target, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        // ---------------------------------------------------------------- helpers

        void OnLocalAvatar(PlayerAvatar avatar)
        {
            cameraRig.SetTarget(avatar.transform);
            build.SetPlayer(avatar.Controller);
        }

        static Vector3 SpawnPoint(int slot) => new Vector3(0.5f + slot * 1.5f, 0.5f, 0f);

        void SetStatus(string status)
        {
            Status = status;
            Changed?.Invoke();
        }

        void Fail(string status)
        {
            Leave();
            SetStatus(status);
        }
    }
}
