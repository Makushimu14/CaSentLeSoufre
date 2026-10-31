using System;
using System.IO;
using CaSentLeSoufre.Core;
using UnityEngine;

namespace CaSentLeSoufre.App
{
    /// <summary>
    /// Sauvegarde de la partie en cours (un seul emplacement), pour la reprendre plus tard là où on s'est arrêté.
    /// Écrite à chaque coup, supprimée quand la partie se termine ou est abandonnée.
    /// </summary>
    public static class GameSaveStorage
    {
        private const string FileName = "savedgame.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>
        /// Charge la partie enregistrée, après avoir vérifié qu'elle peut vraiment être reprise (cartes cohérentes,
        /// réglages valides). Retourne null s'il n'y en a pas, ou si le fichier est abîmé : il est alors mis de côté
        /// (« savedgame.invalid.json ») pour ne pas bloquer le menu à chaque lancement.
        /// </summary>
        public static MatchData LoadValid()
        {
            var data = JsonFile.Load<MatchData>(FilePath);
            if (data == null) return null;

            try
            {
                Match.FromData(data); // simple vérification : l'objet n'est pas conservé
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Sauvegarde] Partie enregistrée inutilisable, elle est mise de côté : {e.Message}");
                try
                {
                    File.Copy(FilePath, Path.ChangeExtension(FilePath, ".invalid.json"), true);
                }
                catch (Exception copyError)
                {
                    Debug.LogWarning($"[Sauvegarde] Copie impossible : {copyError.Message}");
                }
                Delete();
                return null;
            }
        }

        /// <summary>Enregistre sans jamais faire échouer le jeu : une erreur d'écriture est seulement signalée.</summary>
        public static void Save(MatchData data)
        {
            try
            {
                JsonFile.Save(FilePath, data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Sauvegarde] Enregistrement impossible : {e.Message}");
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Sauvegarde] Suppression impossible : {e.Message}");
            }
        }
    }
}
