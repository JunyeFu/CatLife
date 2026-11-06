using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Incremental styling only: existing controls, coordinates and business bindings stay intact.
public static class CatLifeUiStyleInstaller
{
    public const string ScenePath = "Assets/Editor/CatProneReview/UiStyleCandidate/CatLifeUiStyleReview.unity";
    private const string Art = "Assets/MobileRuntime/UI/Art/Style/S_CL_UI_Style_";
    private const string TokensPath = "Assets/MobileRuntime/UI/Style/CL_UI_StyleTokens.json";
    [Serializable] private sealed class Colors
    {
        public string lightBase, lightCard, lightInset, darkBase, darkSurface, darkCard, darkRaised;
        public string ink, mutedInk, onDark, brandOrange, brandGreen, danger;
    }
    [Serializable] private sealed class Typography { public string softwareDarkFont; public string[] softwareDarkObjects; }
    [Serializable] private sealed class Reveal { public float characterSeconds, staggerSeconds, blurDesignPx; public float[] bezier; }
    [Serializable] private sealed class Spring { public float stiffness, damping; }
    [Serializable] private sealed class Tokens { public Colors colors; public Typography typography; public Reveal textReveal; public Spring exitGesture; }
    private static Color Parse(string value) { ColorUtility.TryParseHtmlString(value, out Color color); return color; }
    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png")
        ?? throw new InvalidOperationException("Missing prepared UI skin: " + name);
    private static Sprite StyleSprite(Sprite original)
    {
        const string prefix = "S_CL_UI_Figma_";
        if (original == null || !original.name.StartsWith(prefix)) return original;
        string name = original.name.Substring(prefix.Length);
        return name.StartsWith("Skin") || name == "ToggleOn" || name == "ToggleOff" ? Sprite(name) : original;
    }

    public static AsyncOperation LoadReviewForCapture() => EditorSceneManager.LoadSceneAsyncInPlayMode(
        ScenePath, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));

    public static void Install()
    {
        ImportSkin();
        foreach (string path in new[] { CatLifeMobileUiPrefabBuilder.PrefabPath,
            "Assets/Editor/CatProneReview/FigmaUiCandidate/PF_CL_UI_FigmaReview.prefab" })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { Apply(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        if (!File.Exists(ScenePath))
        {
            var source = EditorSceneManager.OpenScene("Assets/Editor/CatProneReview/NavigationTurnCandidate/CatLifeNavigationTurnReview.unity");
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(source, ScenePath, true);
        }
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var app = UnityEngine.Object.FindFirstObjectByType<CatLifeMobileApp>();
        var view = (GameObject)new SerializedObject(app).FindProperty("viewRoot").objectReferenceValue;
        Apply(view);
        foreach (var component in view.GetComponentsInChildren<Component>(true))
        {
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("CATLIFE_UI_STYLE_INSTALLED " + ScenePath + "; original world/camera/control geometry retained; no APK built.");
    }

    public static void ImportSkin()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/MobileRuntime/UI/Art/Style" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.isReadable = false;
            importer.spritePixelsPerUnit = 100; importer.maxTextureSize = 512;
            float border = path.Contains("PrimaryShadow") ? 122 : path.Contains("CardShadow") ? 64
                : path.Contains("SkinPrimary") || path.Contains("SkinTrack") ? 88
                : path.Contains("SkinSheet") ? 56 : path.Contains("SkinIconCircle") ? 70
                : path.Contains("SkinProgress") ? 10 : path.Contains("Toggle") || path.Contains("SkinSwipeFill") ? 0 : 32;
            importer.spriteBorder = new Vector4(border, border, border, border);
            var android = importer.GetPlatformTextureSettings("Android"); android.overridden = true;
            android.maxTextureSize = 512; android.format = TextureImporterFormat.ASTC_4x4;
            importer.SetPlatformTextureSettings(android); importer.SaveAndReimport();
        }
    }

    public static void Apply(GameObject root)
    {
        var tokens = JsonUtility.FromJson<Tokens>(File.ReadAllText(TokensPath));
        var colors = tokens.colors;
        var ui = root.GetComponent<CatLifeMobileUiPresentation>();
        root.GetComponent<Canvas>().additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
        var nodes = root.GetComponentsInChildren<Transform>(true);
        ui.ink = Parse(colors.ink); ui.onDark = Parse(colors.onDark); ui.mutedInk = Parse(colors.mutedInk);
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            image.sprite = StyleSprite(image.sprite);
            // These three pre-Figma surfaces were still using the original outlined artwork.
            if (image.name == "SwipeFill") { image.sprite = Sprite("SkinSwipeFill"); image.color = Color.white; }
            if (image.name == "DebugPanel") { image.sprite = Sprite("SkinCard"); image.type = Image.Type.Sliced; }
            if (image.name == "ExitInterventionPrompt" || image.name == "BubbleTextCard")
            { image.sprite = Sprite("SkinCard"); image.type = Image.Type.Sliced; image.color = Color.white; }
            Color c = image.color;
            if (image.name == "FocusPanel" || image.name == "TransitionPanel" || c.r == 0 && c.g == 0 && c.b == 0)
            { Color neutral = Parse(colors.darkBase); neutral.a = c.a; image.color = neutral; }
            else if (image.name == "DebugPanel") { Color neutral = Parse(colors.darkSurface); neutral.a = c.a; image.color = neutral; }
            else if (image.sprite == null && c.a > .98f && c.r > .93f && c.g > .8f && c.b > .65f)
                image.color = Parse(colors.lightBase);
        }
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            var state = button.spriteState;
            state.pressedSprite = StyleSprite(state.pressedSprite); state.disabledSprite = StyleSprite(state.disabledSprite);
            state.highlightedSprite = StyleSprite(state.highlightedSprite); state.selectedSprite = StyleSprite(state.selectedSprite);
            button.spriteState = state;
            if (button.transition == Selectable.Transition.ColorTint)
            {
                var block = button.colors;
                block.normalColor = block.highlightedColor = block.selectedColor = Color.white;
                block.pressedColor = Parse(colors.lightBase); button.colors = block;
            }
        }
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            Color c = text.color;
            if (c.a == 0) continue;
            Color replacement = c.r > .95f && c.g > .95f && c.b > .95f ? ui.onDark
                : c.r < .2f && c.g < .2f && c.b < .2f ? ui.ink
                : c.g > c.r + .02f && c.g > c.b + .02f ? ui.ink
                : c.r > c.g * 1.8f ? c : ui.mutedInk;
            replacement.a = c.a; text.color = replacement;
        }
        foreach (Outline outline in root.GetComponentsInChildren<Outline>(true)) UnityEngine.Object.DestroyImmediate(outline);
        ui.chipNormal = Sprite("SkinChip"); ui.chipSelected = Sprite("SkinChipSelected"); ui.secondary = Sprite("SkinSecondary");
        ui.toggleOff = Sprite("ToggleOff"); ui.toggleOn = Sprite("ToggleOn"); ui.chartBar = Sprite("SkinProgress");
        foreach (Image toggle in new[] { ui.setupAiToggle, ui.settingsAiToggle, ui.settingsBehaviorToggle, ui.settingsExitToggle })
            toggle.preserveAspect = true;
        ui.darkBodyFont = AssetDatabase.LoadAssetAtPath<Font>(tokens.typography.softwareDarkFont);
        ui.darkBodyTexts = nodes.Where(t => tokens.typography.softwareDarkObjects.Contains(t.name)).Select(t => t.GetComponent<Text>()).ToArray();
        ui.ApplyTypography();
        foreach (Text dark in ui.darkBodyTexts) dark.color = ui.onDark;
        var reveal = ui.rewardInsight.GetComponent<CatLifeInsightReveal>() ?? ui.rewardInsight.gameObject.AddComponent<CatLifeInsightReveal>();
        var timing = tokens.textReveal;
        reveal.Configure(AssetDatabase.LoadAssetAtPath<Shader>("Assets/MobileRuntime/UI/Style/CL_UI_InsightReveal.shader"),
            timing.characterSeconds, timing.staggerSeconds, timing.blurDesignPx,
            new Vector4(timing.bezier[0], timing.bezier[1], timing.bezier[2], timing.bezier[3]));
        ui.insightReveal = reveal;
        root.GetComponentInChildren<CatLifeSwipeToEnd>(true).ConfigureSpring(tokens.exitGesture.stiffness, tokens.exitGesture.damping);
        foreach (var field in root.GetComponentsInChildren<InputField>(true))
        {
            field.transition = Selectable.Transition.None;
            var background = field.GetComponent<Image>(); background.sprite = Sprite("SkinChip"); background.color = Color.white; background.type = Image.Type.Sliced;
            var rings = field.transform.Find("StyleFocusRings")?.GetComponent<Image>();
            if (rings == null)
            {
                var child = new GameObject("StyleFocusRings", typeof(RectTransform), typeof(Image)); child.transform.SetParent(field.transform, false);
                rings = child.GetComponent<Image>(); var r = rings.rectTransform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
            }
            rings.sprite = Sprite("InputFocus"); rings.type = Image.Type.Sliced; rings.color = Color.white;
            rings.raycastTarget = false; rings.enabled = false;
            var focus = field.GetComponent<CatLifeInputFocus>() ?? field.gameObject.AddComponent<CatLifeInputFocus>(); focus.Configure(field, rings);
            var keyboard = field.GetComponent<CatLifeKeyboardAvoidance>() ?? field.gameObject.AddComponent<CatLifeKeyboardAvoidance>();
            keyboard.Configure(field, (RectTransform)field.transform.parent);
        }
        EditorUtility.SetDirty(ui);
    }
}
