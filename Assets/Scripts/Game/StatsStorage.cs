using System;
using System.IO;
using CaSentLeSoufre.Core;
using UnityEngine;

namespace CaSentLeSoufre.App
{
    /// <summary>Enregistrement du carnet de statistiques en JSON dans le dossier de données persistantes.</summary>
    public static class StatsStorage
    {
        private const string FileName = "stats.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// Charge le carnet. Si le fichier est illisible, il est conservé sous « stats.corrupt.json »
        /// (pour ne pas être écrasé par la prochaine sauvegarde) et un carnet vide est renvoyé.
        /// </summary>
        public static StatsBook Load() => Load(FilePath);

        public static StatsBook Load(string path)
        {
            if (!File.Exists(path)) return new StatsBook();

            try
            {
                var snapshot = JsonUtility.FromJson<StatsSnapshot>(File.ReadAllText(path));
                if (snapshot == null) throw new InvalidDataException("Fichier de statistiques vide.");
                return StatsBook.FromSnapshot(snapshot);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Statistiques] Fichier illisible, une copie est conservée : {e.Message}");
                try
                {
                    File.Copy(path, Path.ChangeExtension(path, ".corrupt.json"), true);
                }
                catch (Exception copyError)
                {
                    Debug.LogWarning($"[Statistiques] Copie impossible : {copyError.Message}");
                }
                return new StatsBook();
            }
        }

        public static void Save(StatsBook book) => Save(book, FilePath);

        /// <summary>Écrit dans un fichier temporaire puis le substitue, pour ne jamais laisser un fichier à moitié écrit.</summary>
        public static void Save(StatsBook book, string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(book.ToSnapshot(), true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }
}
