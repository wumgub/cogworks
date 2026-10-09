using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    public sealed class PlayerManager
    {
        public const int MaxPlayers = 4;

        // Indexed by player id; ids are small (0 = host / single-player).
        readonly PlayerState[] _players = new PlayerState[MaxPlayers];
        readonly World _world;

        internal PlayerManager(World world)
        {
            _world = world;
        }

        public event Action<PlayerState> Joined;

        /// <summary>Presentation hook: a player's hand craft finished.</summary>
        public event Action<PlayerState, Recipe> CraftCompleted;

        public PlayerState Get(byte id) => id < MaxPlayers ? _players[id] : null;

        public IEnumerable<PlayerState> All
        {
            get
            {
                foreach (var p in _players)
                    if (p != null) yield return p;
            }
        }

        /// <summary>Adds the player if new. Returns false if the id is invalid or already present.</summary>
        public bool Join(byte id)
        {
            if (id >= MaxPlayers || _players[id] != null) return false;
            _players[id] = new PlayerState(id);
            if (!_world.Sandbox) _players[id].GiveStarterKit();
            Joined?.Invoke(_players[id]);
            return true;
        }

        internal void Tick()
        {
            foreach (var p in _players)
            {
                var done = p?.Tick();
                if (done != null) CraftCompleted?.Invoke(p, done);
            }
        }

        internal void Write(BinaryWriter w)
        {
            byte count = 0;
            foreach (var p in _players) if (p != null) count++;
            w.Write(count);
            foreach (var p in _players) p?.Write(w);
        }

        internal void Read(BinaryReader r)
        {
            Array.Clear(_players, 0, _players.Length);
            int count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var p = PlayerState.Read(r);
                if (p.Id >= MaxPlayers) throw new InvalidDataException($"Player id {p.Id} out of range");
                _players[p.Id] = p;
            }
        }

        internal void AddToHash(ref Fnv64 h)
        {
            foreach (var p in _players)
            {
                if (p == null) h.Add((byte)0xFF);
                else p.AddToHash(ref h);
            }
        }
    }
}
