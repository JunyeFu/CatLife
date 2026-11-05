using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.U2D;

public static class CatLifeMobileUiArtInstaller
{
    private const string ArtRoot = "Assets/MobileRuntime/UI/Art";
    private const string FontSourcePath = "Assets/MobileRuntime/UI/Fonts/NotoSansSC-CatLifeSubset.ttf";
    private const string CharacterSetPath = "Assets/MobileRuntime/UI/Fonts/CatLife_UI_Characters.txt";
    private const string FontAssetPath = "Assets/MobileRuntime/UI/Fonts/F_CL_UI_NotoSansSC_SDF.asset";
    private const string AtlasPath = "Assets/MobileRuntime/UI/Art/CL_UI_Runtime.spriteatlas";

    [MenuItem("CatLife/Mobile/Install UI Art And Rebuild Prefab")]
    public static void InstallAndBuild()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureSpriteImports();
        CreateRuntimeAtlas();
        EnsureTmpSettings();
        CreateTmpFontAsset();
        CatLifeMobileUiPrefabBuilder.Build();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("CATLIFE_UI_ART_INSTALL_OK");
    }

    private static void EnsureTmpSettings()
    {
        const string temporarySettingsPath = "Assets/Resources/TMP Settings.asset";
        if (Shader.Find("TextMeshPro/Distance Field") == null ||
            AssetDatabase.FindAssets("t:TMP_Settings", new[] { "Assets" }).Length == 0)
        {
            AssetDatabase.DeleteAsset(temporarySettingsPath);
            string[] packages = Directory.GetFiles(
                Path.GetFullPath("Library/PackageCache"),
                "TMP Essential Resources.unitypackage",
                SearchOption.AllDirectories);
            if (packages.Length == 0)
                throw new FileNotFoundException("TMP Essential Resources.unitypackage was not found");
            AssetDatabase.ImportPackage(packages[0], false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        if (TMP_Settings.LoadDefaultSettings() == null)
            throw new FileNotFoundException("TMP Settings asset was not installed");
    }

    private static void ConfigureSpriteImports()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".png")) continue;
            // These generated sprites keep the dedicated Figma installer's sliced borders.
            if (path.Contains("/Figma/")) continue;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool banner = path.Contains("/Banners/");
            bool action = path.Contains("/Actions/");
            bool icon = path.Contains("/Icons/");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = !banner;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.maxTextureSize = banner ? 1024 : action ? 512 : icon ? 256 : 1024;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = BorderFor(path);

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = importer.maxTextureSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }
    }

    private static Vector4 BorderFor(string path)
    {
        if (path.Contains("CardWide")) return new Vector4(36, 36, 36, 36);
        if (path.Contains("HalfSheet") || path.Contains("FullSheet")) return new Vector4(56, 56, 56, 56);
        if (path.Contains("IconCircle")) return new Vector4(64, 64, 64, 64);
        if (path.Contains("Pill")) return new Vector4(48, 48, 48, 48);
        if (path.Contains("PrimaryButton") || path.Contains("SwipeTrack")) return new Vector4(72, 72, 72, 72);
        return Vector4.zero;
    }

    private static void CreateRuntimeAtlas()
    {
        AssetDatabase.DeleteAsset(AtlasPath);
        SpriteAtlas atlas = new SpriteAtlas();
        SpriteAtlasPackingSettings packing = atlas.GetPackingSettings();
        packing.enableRotation = false;
        packing.enableTightPacking = false;
        packing.padding = 4;
        atlas.SetPackingSettings(packing);

        SpriteAtlasTextureSettings texture = atlas.GetTextureSettings();
        texture.generateMipMaps = false;
        texture.readable = false;
        texture.sRGB = true;
        atlas.SetTextureSettings(texture);

        TextureImporterPlatformSettings android = atlas.GetPlatformSettings("Android");
        android.overridden = true;
        android.maxTextureSize = 2048;
        android.format = TextureImporterFormat.ASTC_6x6;
        atlas.SetPlatformSettings(android);

        AssetDatabase.CreateAsset(atlas, AtlasPath);
        var packables = new List<Object>();
        foreach (string folder in new[] { ArtRoot + "/Components", ArtRoot + "/Icons", ArtRoot + "/Actions" })
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(folder);
            if (asset != null) packables.Add(asset);
        }
        SpriteAtlasExtensions.Add(atlas, packables.ToArray());
        EditorUtility.SetDirty(atlas);
    }

    private static void CreateTmpFontAsset()
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
        if (source == null) throw new FileNotFoundException("Missing CatLife font subset", FontSourcePath);

        string characters = File.ReadAllText(CharacterSetPath);
        AssetDatabase.DeleteAsset(FontAssetPath);
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            source, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        fontAsset.name = "F_CL_UI_NotoSansSC_SDF";
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
            if (atlasTexture != null && !AssetDatabase.Contains(atlasTexture)) AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material)) AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        if (!fontAsset.TryAddCharacters(characters, out string missing) && !string.IsNullOrEmpty(missing))
            Debug.LogWarning("CatLife TMP missing characters: " + missing);
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(fontAsset);
    }
}
