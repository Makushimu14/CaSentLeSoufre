using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CaSentLeSoufre.EditorTools
{
    /// <summary>
    /// Génère le jeu pour Windows : menu « Build > Windows », ou en ligne de commande
    /// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod CaSentLeSoufre.EditorTools.BuildScript.BuildWindows</c>.
    /// Le résultat est dans Builds/Windows (dossier ignoré par git).
    /// </summary>
    public static class BuildScript
    {
        private const string OutputFolder = "Builds/Windows";

        [MenuItem("Build/Windows")]
        public static void BuildWindows()
        {
            // Les tables de traduction passent par Addressables : leur contenu doit être construit avant le jeu.
            AddressableAssetSettings.BuildPlayerContent();

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("Aucune scène dans Build Settings.");

            Directory.CreateDirectory(OutputFolder);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputFolder, "CaSentLeSoufre.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[Build] {summary.result} : {summary.totalSize / (1024 * 1024)} Mo, {summary.totalErrors} erreur(s), " +
                      $"{summary.totalTime.TotalSeconds:0} s -> {summary.outputPath}");

            if (summary.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw new Exception("La génération a échoué.");
            }
        }
    }
}
