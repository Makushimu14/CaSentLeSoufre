using System.IO;
using CaSentLeSoufre.Core;
using UnityEngine;

namespace CaSentLeSoufre.App
{
    /// <summary>Enregistrement du carnet de statistiques dans le dossier de données persistantes.</summary>
    public static class StatsStorage
    {
        private const string FileName = "stats.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Charge le carnet ; un carnet vide si le fichier est absent ou illisible.</summary>
        public static StatsBook Load() => Load(FilePath);

        public static StatsBook Load(string path) => StatsBook.FromSnapshot(JsonFile.Load<StatsSnapshot>(path));

        public static void Save(StatsBook book) => Save(book, FilePath);

        public static void Save(StatsBook book, string path) => JsonFile.Save(path, book.ToSnapshot());
    }
}
