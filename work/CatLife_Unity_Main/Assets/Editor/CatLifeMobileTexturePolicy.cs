using System;
using System.Collections.Generic;
using UnityEditor;

public static class CatLifeMobileTexturePolicy
{
    private const string TownTextureRoot = "Assets/MobileRuntime/Art/Town/Textures";
    private const string CatTextureRoot = "Assets/MobileRuntime/Art/Cat/Textures";

    [MenuItem("CatLife/Mobile Rebuild/Apply Android Texture Policy")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void ApplyBatch()
    {
        Apply();
        EditorApplication.Exit(0);
    }

    public static void Apply()
    {
        foreach (string path in FindRuntimeTextures())
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            bool isNormal = path.EndsWith("_Normal_1024.png", StringComparison.Ordinal);
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isNormal;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.isReadable = false;

            bool isCat = path.StartsWith(CatTextureRoot, StringComparison.Ordinal);
            TextureImporterPlatformSettings settings = new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 1024,
                format = isCat ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.CompressedHQ,
                compressionQuality = isCat ? 90 : 80
            };
            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    private static IEnumerable<string> FindRuntimeTextures()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TownTextureRoot, CatTextureRoot }))
        {
            yield return AssetDatabase.GUIDToAssetPath(guid);
        }
    }
}
