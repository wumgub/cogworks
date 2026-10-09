using UnityEditor;
using UnityEngine;

namespace Cogworks.Editor
{
    /// <summary>Pixel-art import settings for everything under Assets/_Game/Art: crisp, uncompressed, 16 px per tile.</summary>
    public sealed class ArtImporter : AssetPostprocessor
    {
        const string ArtRoot = "Assets/_Game/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            // World sprites: 16 px per tile. UI skin: 100, UI Toolkit's reference, so 9-slice borders scale
            // exactly by -unity-slice-scale instead of by 100/16.
            importer.spritePixelsPerUnit = assetPath.StartsWith(ArtRoot + "UI/") ? 100 : 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            // Terrain blending reads terrain pixels at runtime to build soft borders.
            importer.isReadable = System.IO.Path.GetFileName(assetPath).StartsWith("terrain_");

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // tiles and footprints must stay exact squares
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
        }
    }
}
