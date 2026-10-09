using System;
using System.Collections.Generic;
using System.IO;

namespace Cogworks.Sim
{
    /// <summary>Team-wide research progress. Progress on a tech is kept if you switch away and back.</summary>
    public sealed class ResearchState
    {
        readonly SortedSet<ushort> _completed = new SortedSet<ushort>();
        readonly SortedDictionary<ushort, int[]> _progress = new SortedDictionary<ushort, int[]>();

        public Tech Current { get; private set; }

        public event Action<Tech> Completed;

        public bool IsDone(Tech t) => _completed.Contains(t.Id);

        public bool IsAvailable(Tech t)
        {
            if (IsDone(t)) return false;
            foreach (var p in t.Prerequisites)
                if (!IsDone(p)) return false;
            return true;
        }

        public bool IsUnlocked(Recipe recipe)
        {
            if (!TechCatalog.IsLockedByDefault(recipe)) return true;
            foreach (ushort id in _completed)
                if (Array.IndexOf(TechCatalog.Get(id).Unlocks, recipe) >= 0) return true;
            return false;
        }

        public int ProgressOf(Tech t, int costIndex) =>
            _progress.TryGetValue(t.Id, out var p) ? p[costIndex] : IsDone(t) ? t.Cost[costIndex].Count : 0;

        public bool SetCurrent(Tech t)
        {
            if (t == Current) return false;
            if (t != null && !IsAvailable(t)) return false;
            Current = t;
            return true;
        }

        /// <summary>True if the current research still needs packs of this type.</summary>
        public bool NeedsMore(ushort pack)
        {
            if (Current == null) return false;
            for (int i = 0; i < Current.Cost.Length; i++)
                if (Current.Cost[i].Item == pack && ProgressOf(Current, i) < Current.Cost[i].Count) return true;
            return false;
        }

        public void AddProgress(ushort pack)
        {
            if (Current == null) return;
            if (!_progress.TryGetValue(Current.Id, out var p)) _progress[Current.Id] = p = new int[Current.Cost.Length];
            for (int i = 0; i < Current.Cost.Length; i++)
                if (Current.Cost[i].Item == pack && p[i] < Current.Cost[i].Count) p[i]++;

            for (int i = 0; i < Current.Cost.Length; i++)
                if (p[i] < Current.Cost[i].Count) return;

            var done = Current;
            _completed.Add(done.Id);
            _progress.Remove(done.Id);
            Current = null;
            Completed?.Invoke(done);
        }

        internal void Write(BinaryWriter w)
        {
            w.Write((ushort)_completed.Count);
            foreach (ushort id in _completed) w.Write(id);
            w.Write(Current?.Id ?? 0);
            w.Write((ushort)_progress.Count);
            foreach (var pair in _progress)
            {
                w.Write(pair.Key);
                w.Write((byte)pair.Value.Length);
                foreach (int v in pair.Value) w.Write(v);
            }
        }

        internal void Read(BinaryReader r)
        {
            _completed.Clear();
            _progress.Clear();
            int n = r.ReadUInt16();
            for (int i = 0; i < n; i++) _completed.Add(r.ReadUInt16());
            Current = TechCatalog.Get(r.ReadUInt16());
            int m = r.ReadUInt16();
            for (int i = 0; i < m; i++)
            {
                ushort id = r.ReadUInt16();
                var values = new int[r.ReadByte()];
                for (int j = 0; j < values.Length; j++) values[j] = r.ReadInt32();
                _progress[id] = values;
            }
        }

        internal void AddToHash(ref Fnv64 h)
        {
            foreach (ushort id in _completed) h.Add(id);
            h.Add(Current?.Id ?? 0);
            foreach (var pair in _progress)
            {
                h.Add(pair.Key);
                foreach (int v in pair.Value) h.Add(v);
            }
        }
    }
}
