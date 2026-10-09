using System;
using System.IO;
using Cogworks.Sim;
using UnityEngine;

namespace Cogworks.Game
{
    /// <summary>Single save slot on disk, using the same snapshot format as multiplayer late-join.</summary>
    public static class SaveSystem
    {
        public static string SavePath => Path.Combine(Application.persistentDataPath, "world.cwsave");

        public static bool HasSave => File.Exists(SavePath);

        public static void Save(Simulation sim)
        {
            // Write then swap, so a crash mid-save never destroys the previous save.
            string temp = SavePath + ".tmp";
            File.WriteAllBytes(temp, Snapshot.Save(sim));
            if (File.Exists(SavePath)) File.Replace(temp, SavePath, null);
            else File.Move(temp, SavePath);
        }

        public static bool TryLoad(out Simulation sim, out string error)
        {
            sim = null;
            error = null;
            try
            {
                sim = Snapshot.Load(File.ReadAllBytes(SavePath));
                return true;
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
