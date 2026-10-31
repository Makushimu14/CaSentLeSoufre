using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaSentLeSoufre.App.UI
{
    /// <summary>
    /// Affiche le menu de configuration au lancement de la scène de départ, sans objet à placer à la main.
    /// À remplacer par une vraie scène « MainMenu » quand la table de jeu aura sa propre scène.
    /// </summary>
    internal static class MenuBootstrap
    {
        private const string MenuSceneName = "SampleScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ShowMenu()
        {
            if (SceneManager.GetActiveScene().name != MenuSceneName) return;
            if (Object.FindAnyObjectByType<MainMenuController>() != null) return;

            new GameObject("MainMenu").AddComponent<MainMenuController>();
        }
    }
}
