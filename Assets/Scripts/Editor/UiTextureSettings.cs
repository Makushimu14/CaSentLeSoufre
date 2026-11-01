using UnityEditor;
using UnityEngine;

namespace CaSentLeSoufre.EditorTools
{
    /// <summary>
    /// Réglages d'import des images d'interface (Assets/Resources/UI, comme le fond du menu) : taille d'origine
    /// conservée, sans mipmaps (l'image s'affiche plein écran), compression de haute qualité.
    /// </summary>
    public sealed class UiTextureSettings : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/UI/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
