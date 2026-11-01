using UnityEditor;
using UnityEngine;

namespace CaSentLeSoufre.EditorTools
{
    /// <summary>
    /// Réglages d'import de l'icône du jeu (Assets/Icons) : pleine taille, transparence conservée, sans mipmaps
    /// (Unity en tire lui-même les différentes tailles d'icône).
    /// </summary>
    public sealed class IconTextureSettings : AssetPostprocessor
    {
        private const string Folder = "Assets/Icons/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
        }
    }
}
