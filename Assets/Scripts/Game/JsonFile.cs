using System;
using System.IO;
using UnityEngine;

namespace CaSentLeSoufre.App
{
    /// <summary>Lecture et écriture JSON sûres d'un fichier de données (préférences, statistiques...).</summary>
    public static class JsonFile
    {
        /// <summary>
        /// Lit le fichier. Retourne null s'il n'existe pas. S'il est illisible, une copie « .corrupt.json » est
        /// conservée (pour ne pas être écrasée par la prochaine sauvegarde) et null est retourné.
        /// </summary>
        public static T Load<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;

            try
            {
                var value = JsonUtility.FromJson<T>(File.ReadAllText(path));
                if (value == null) throw new InvalidDataException("Fichier vide.");
                return value;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Données] Fichier illisible ({Path.GetFileName(path)}), une copie est conservée : {e.Message}");
                try
                {
                    File.Copy(path, Path.ChangeExtension(path, ".corrupt.json"), true);
                }
                catch (Exception copyError)
                {
                    Debug.LogWarning($"[Données] Copie impossible : {copyError.Message}");
                }
                return null;
            }
        }

        /// <summary>Écrit dans un fichier temporaire puis le substitue : jamais de fichier à moitié écrit.</summary>
        public static void Save<T>(string path, T value) where T : class
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(value, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
    }
}
