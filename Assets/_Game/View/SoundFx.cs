using System.Collections.Generic;
using Cogworks.Sim;
using UnityEngine;

namespace Cogworks.View
{
    public enum Sfx
    {
        Place,
        Remove,
        Mine,
        Craft,
        Crank,
        Click,
        Error,
        Research,
    }

    /// <summary>
    /// Sound effects synthesized at startup (no audio assets needed), played from a small pool of sources.
    /// Listens to sim events, so remote players' actions near the camera are heard too.
    /// </summary>
    public sealed class SoundFx : MonoBehaviour
    {
        const int SampleRate = 44100;
        const int Voices = 8;
        // World sounds farther than this from the camera are skipped.
        const float HearingRange = 24f;

        public static SoundFx Instance { get; private set; }

        [SerializeField] GameBootstrap bootstrap;
        [SerializeField] BuildController build;

        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        readonly AudioSource[] _sources = new AudioSource[Voices];
        int _next;
        Simulation _sim;
        float _lastPlaceTime;

        void Awake()
        {
            Instance = this;
            GameSettings.Apply();
            for (int i = 0; i < Voices; i++)
            {
                _sources[i] = gameObject.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
            }
            _clips[Sfx.Place] = Thunk(150f, 0.09f);
            _clips[Sfx.Remove] = Sweep(700f, 250f, 0.08f, 0.5f);
            _clips[Sfx.Mine] = Noise(0.05f, 0.35f);
            _clips[Sfx.Craft] = Chime(new[] { 660f, 990f }, 0.18f);
            _clips[Sfx.Crank] = Ratchet(0.25f);
            _clips[Sfx.Click] = Sweep(1200f, 1000f, 0.025f, 0.3f);
            _clips[Sfx.Error] = Square(140f, 0.15f, 0.25f);
            _clips[Sfx.Research] = Chime(new[] { 523f, 659f, 784f, 1047f }, 0.6f);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Bind(null);
        }

        void Update()
        {
            if (bootstrap.Sim != _sim) Bind(bootstrap.Sim);
        }

        void Bind(Simulation sim)
        {
            if (_sim != null)
            {
                _sim.World.Buildings.Placed -= OnPlaced;
                _sim.World.Buildings.Removed -= OnRemoved;
                _sim.World.Research.Completed -= OnResearch;
                _sim.World.Players.CraftCompleted -= OnCrafted;
                _sim.World.TileChanged -= OnTileChanged;
            }
            _sim = sim;
            if (_sim == null) return;
            _sim.World.Buildings.Placed += OnPlaced;
            _sim.World.Buildings.Removed += OnRemoved;
            _sim.World.Research.Completed += OnResearch;
            _sim.World.Players.CraftCompleted += OnCrafted;
            _sim.World.TileChanged += OnTileChanged;
        }

        void OnPlaced(Building b)
        {
            // Dragging a belt places many pieces at once; one thunk per burst is plenty.
            if (Time.unscaledTime - _lastPlaceTime < 0.05f) return;
            _lastPlaceTime = Time.unscaledTime;
            PlayAt(Sfx.Place, b.Origin);
        }

        void OnRemoved(Building b) => PlayAt(Sfx.Remove, b.Origin);
        void OnResearch(Tech t) => Play(Sfx.Research);
        void OnTileChanged(Int2 tile) => PlayAt(Sfx.Mine, tile);

        void OnCrafted(PlayerState p, Recipe r)
        {
            if (p.Id == bootstrap.LocalPlayerId) Play(Sfx.Craft, 0.6f);
        }

        void PlayAt(Sfx sfx, Int2 tile)
        {
            var cam = Camera.main;
            if (cam != null && Vector2.Distance(cam.transform.position, new Vector2(tile.X, tile.Y)) > HearingRange) return;
            Play(sfx);
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            if (!_clips.TryGetValue(sfx, out var clip)) return;
            var src = _sources[_next];
            _next = (_next + 1) % Voices;
            src.pitch = 1f + Random.Range(-0.05f, 0.05f);
            src.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        // ------------------------------------------------------------------ synthesis

        static AudioClip Make(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static float Envelope(float t, float length) => Mathf.Exp(-6f * t / length) * Mathf.Clamp01(t * 400f);

        static AudioClip Thunk(float freq, float length)
        {
            int n = (int)(SampleRate * length);
            var s = new float[n];
            var rng = new System.Random(1);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float f = freq * (1f - 0.5f * t / length);
                s[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.8f + ((float)rng.NextDouble() * 2f - 1f) * 0.25f) * Envelope(t, length) * 0.6f;
            }
            return Make("Thunk", s);
        }

        static AudioClip Sweep(float from, float to, float length, float gain)
        {
            int n = (int)(SampleRate * length);
            var s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                phase += 2f * Mathf.PI * Mathf.Lerp(from, to, t / length) / SampleRate;
                s[i] = Mathf.Sin(phase) * Envelope(t, length) * gain;
            }
            return Make("Sweep", s);
        }

        static AudioClip Noise(float length, float gain)
        {
            int n = (int)(SampleRate * length);
            var s = new float[n];
            var rng = new System.Random(2);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                lp += (((float)rng.NextDouble() * 2f - 1f) - lp) * 0.3f;
                s[i] = lp * Envelope(t, length) * gain * 2f;
            }
            return Make("Noise", s);
        }

        static AudioClip Chime(float[] notes, float length)
        {
            int n = (int)(SampleRate * length);
            var s = new float[n];
            float step = length / notes.Length;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                for (int k = 0; k < notes.Length; k++)
                {
                    float local = t - k * step * 0.5f;
                    if (local < 0f) continue;
                    s[i] += Mathf.Sin(2f * Mathf.PI * notes[k] * local) * Envelope(local, length) * 0.22f;
                }
            }
            return Make("Chime", s);
        }

        static AudioClip Ratchet(float length)
        {
            int n = (int)(SampleRate * length);
            var s = new float[n];
            var rng = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                // Clicks at 24 Hz, each a short noise burst.
                float within = (t * 24f) % 1f;
                float click = within < 0.15f ? 1f - within / 0.15f : 0f;
                s[i] = ((float)rng.NextDouble() * 2f - 1f) * click * 0.4f * Mathf.Clamp01(1f - t / length);
            }
            return Make("Ratchet", s);
        }

        static AudioClip Square(float freq, float length, float gain)
        {
            int n = (int)(SampleRate * length);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                s[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) > 0f ? 1f : -1f) * Envelope(t, length) * gain;
            }
            return Make("Square", s);
        }
    }
}
