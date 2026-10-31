using UnityEditor;
using UnityEngine;

namespace CaSentLeSoufre.EditorTools
{
    /// <summary>
    /// Réglages d'import des images de cartes (Assets/Resources/Cards), appliqués automatiquement à chaque import :
    /// pas de redimensionnement à une puissance de deux (les cartes feraient 256 x 512 et seraient floues),
    /// mipmaps pour rester nettes une fois réduites, compression de haute qualité.
    /// </summary>
    public sealed class CardTextureSettings : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Cards/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder, System.StringComparison.Ordinal)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
