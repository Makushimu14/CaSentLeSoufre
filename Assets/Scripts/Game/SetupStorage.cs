using System;
using System.IO;
using CaSentLeSoufre.Core;
using UnityEngine;

namespace CaSentLeSoufre.App
{
    /// <summary>Enregistrement des préférences du joueur (nom, adversaires, règles, langue...).</summary>
    public static class SetupStorage
    {
        private const string FileName = "settings.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Charge les préférences ; null si le fichier est absent ou illisible.</summary>
        public static SetupPreferences Load() => JsonFile.Load<SetupPreferences>(FilePath);

        /// <summary>Enregistre sans jamais faire échouer le jeu : une erreur d'écriture est seulement signalée.</summary>
        public static void Save(SetupPreferences preferences)
        {
            try
            {
                JsonFile.Save(FilePath, preferences);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Préférences] Enregistrement impossible : {e.Message}");
            }
        }
    }
}
