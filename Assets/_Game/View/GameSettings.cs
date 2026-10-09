using System;
using UnityEngine;

namespace Cogworks.View
{
    /// <summary>Player preferences, stored per machine in PlayerPrefs.</summary>
    public static class GameSettings
    {
        const string MasterKey = "cw.volume.master";
        const string SfxKey = "cw.volume.sfx";
        const string FullscreenKey = "cw.fullscreen";

        public static event Action Changed;

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(MasterKey, 0.8f);
            set => Set(MasterKey, Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
            set => Set(SfxKey, Mathf.Clamp01(value));
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            set
            {
                PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
                Screen.fullScreen = value;
                Changed?.Invoke();
            }
        }

        static void Set(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            AudioListener.volume = MasterVolume;
            Changed?.Invoke();
        }

        /// <summary>Applies stored settings at startup.</summary>
        public static void Apply()
        {
            AudioListener.volume = MasterVolume;
            if (PlayerPrefs.HasKey(FullscreenKey)) Screen.fullScreen = Fullscreen;
        }
    }
}
