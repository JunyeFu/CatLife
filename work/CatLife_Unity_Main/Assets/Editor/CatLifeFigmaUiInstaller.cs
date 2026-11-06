using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CatLifeFigmaUiInstaller
{
    public const string ScenePath = "Assets/Editor/CatProneReview/FigmaUiCandidate/CatLifeFigmaUiReview.unity";
    private const string Art = "Assets/MobileRuntime/UI/Art/Figma/S_CL_UI_Figma_";
    private static readonly Color Cream = new Color32(255, 248, 234, 255);
    private static readonly Color Muted = new Color32(131, 115, 92, 255);
    private static readonly Color Orange = new Color32(255, 138, 0, 255);
    private static Transform[] nodes;
    private static Font font;

    [MenuItem("CatLife/Mobile/Apply Figma Frontend")]
    public static void Install()
    {
        ImportSkin();
        string report = Path.GetFullPath("../../Reports/figma-ui-20261019");
        Directory.CreateDirectory(report);
        foreach (string path in new[] { CatLifeMobileUiPrefabBuilder.PrefabPath, "Assets/Scenes/CatLifeMobile.unity" })
        {
            string backup = Path.Combine(report, "before-" + Path.GetFileName(path));
            if (!File.Exists(backup)) File.Copy(path, backup);
        }
        var prefab = PrefabUtility.LoadPrefabContents(CatLifeMobileUiPrefabBuilder.PrefabPath);
        try { Apply(prefab); PrefabUtility.SaveAsPrefabAsset(prefab, CatLifeMobileUiPrefabBuilder.PrefabPath); }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        var main = EditorSceneManager.OpenScene("Assets/Scenes/CatLifeMobile.unity");
        Apply(ViewRoot());
        EditorSceneManager.MarkSceneDirty(main);
        EditorSceneManager.SaveScene(main);

        // Use the last stable town/exit candidate, not the unaccepted r28 locomotion experiment.
        EditorSceneManager.OpenScene(CatLife.Editor.CatLifeTownAtlasReview.ScenePath);
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        AssetDatabase.Refresh();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath, true);
        var review = EditorSceneManager.OpenScene(ScenePath);
        var view = ViewRoot();
        if (PrefabUtility.IsPartOfPrefabInstance(view))
            PrefabUtility.UnpackPrefabInstance(view, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Apply(view);
        string uiPath = Path.GetDirectoryName(ScenePath).Replace('\\', '/') + "/PF_CL_UI_FigmaReview.prefab";
        var uiPrefab = PrefabUtility.SaveAsPrefabAsset(view, uiPath);
        var replacement = (GameObject)PrefabUtility.InstantiatePrefab(uiPrefab, review);
        replacement.name = "CatLifeMobileView";
        var appData = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<CatLifeMobileApp>());
        appData.FindProperty("viewRoot").objectReferenceValue = replacement;
        appData.ApplyModifiedPropertiesWithoutUndo();
        UnityEngine.Object.DestroyImmediate(view);
        EditorSceneManager.MarkSceneDirty(review);
        EditorSceneManager.SaveScene(review);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(report, "installed.txt"),
            "Figma=HjZp8L3sjI0radKgNQEIjv; reviewed 2026-10-19\n" +
            "Main and serialized UI prefab updated; review scene=" + ScenePath + "\n" +
            "World, cat, camera, session and private credentials not replaced.\n");
        Debug.Log("CATLIFE_FIGMA_UI_INSTALLED " + ScenePath);
    }

    private static GameObject ViewRoot()
    {
        var app = UnityEngine.Object.FindFirstObjectByType<CatLifeMobileApp>();
        return (GameObject)new SerializedObject(app).FindProperty("viewRoot").objectReferenceValue;
    }

    public static void ImportSkin()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/MobileRuntime/UI/Art/Figma" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.spritePixelsPerUnit = 100;
            importer.maxTextureSize = 1024;
            float border = path.Contains("Shadow") ? (path.Contains("Primary") ? 122 : 64)
                : path.Contains("SkinPrimary") ? 88 : path.Contains("SkinSheet") ? 56
                : path.Contains("SkinIconCircle") ? 70 : path.Contains("SkinTrack") ? 88
                : path.Contains("SkinProgress") ? 10 : path.Contains("Skin") ? 32 : 0;
            importer.spriteBorder = new Vector4(border, border, border, border);
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = importer.maxTextureSize;
            android.format = TextureImporterFormat.ASTC_4x4;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }
    }

    public static void Apply(GameObject root)
    {
        nodes = root.GetComponentsInChildren<Transform>(true);
        font = Named("TodayText").GetComponent<Text>().font;
        int buttonsBefore = root.GetComponentsInChildren<Button>(true).Length;
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            Color c = text.color;
            if (c.g > c.r + .02f && c.g > c.b + .02f) text.color = new Color(0, 0, 0, c.a);
        }
        var ui = root.GetComponent<CatLifeMobileUiPresentation>();
        if (ui == null) ui = root.AddComponent<CatLifeMobileUiPresentation>();
        ui.chipNormal = Skin("Chip"); ui.chipSelected = Skin("ChipSelected"); ui.secondary = Skin("Secondary");
        ui.toggleOff = Sprite("ToggleOff"); ui.toggleOn = Sprite("ToggleOn"); ui.chartBar = Skin("Progress");
        Home();
        Setup(ui);
        Focus();
        Reward(ui);
        PageHeader("Records", "专注记录"); PageHeader("Growth", "猫咪成长"); PageHeader("Settings", "设置");
        Records(ui); Growth(ui); Settings(ui, root);
        ApplyPageRepairs(root);
        foreach (string name in new[] { "AdoptionCard", "ExitConfirmCard", "AiConsentPanelCard" }) Surface(Named(name), "Card");
        foreach (string name in new[] { "AdoptionConfirm", "InterruptConfirm", "AiConsentAccept", "ReviewerMinute", "ReviewTail", "ReviewWave", "ReviewRest", "ReviewStretch" })
            StyleButton(Named(name), true, name.StartsWith("Review") ? 36 : 44);
        foreach (string name in new[] { "AdoptionDuration", "InterruptCancel", "AiConsentCancel", "AutoFocusCancel", "DebugClose" })
            StyleButton(Named(name), false, 40);
        if (root.GetComponentsInChildren<Button>(true).Length < buttonsBefore)
            throw new InvalidOperationException("The frontend must preserve every existing button.");
        CatLifeUiStyleInstaller.Apply(root);
        EditorUtility.SetDirty(root);
    }

    public static void ApplyPageRepairs(GameObject root)
    {
        nodes = root.GetComponentsInChildren<Transform>(true);
        font = Named("TodayText").GetComponent<Text>().font;
        var ui = root.GetComponent<CatLifeMobileUiPresentation>();
        ui.rewardDuration.fontSize = 96;
        ui.rewardBehavior.fontSize = 32;
        Fill(ui.rewardBehavior.rectTransform, 8);
        ui.rewardPaws.fontSize = 36;
        Named("TimerText").GetComponent<Text>().fontSize = 96;
        Named("FocusTitle").GetComponent<Text>().fontSize = 36;
        Named("BrandTitle").GetComponent<Text>().fontSize = 56;
        Named("FigmaSetupBrand").GetComponent<Text>().fontSize = 56;
        Named("SetupTitle").GetComponent<Text>().fontSize = 56;
        Named("SelectedMinutes").GetComponent<Text>().fontSize = 24;
        Box(Named("SelectedMinutes"), Named("SetupCard"), 64, 252, 952, 48, 1080, 1104);
        Named("RewardTitle").GetComponent<Text>().fontSize = 52;
        Named("RewardHome").GetComponentInChildren<Text>(true).fontSize = 40;
        Named("RewardAgain").GetComponentInChildren<Text>(true).fontSize = 40;
        // Reserve the left side for the cat's contact paws, without relocating the gesture track.
        Box(Named("SwipeLabel"), Named("SwipeTrack"), 540, 0, 308, 180, 888, 180);
        Type("SwipeLabel", 36, Color.black, TextAnchor.MiddleCenter);
        var hint = NewText(Named("RewardCard"), "RewardScrollHint", 20, Color.black);
        Box(hint.transform, Named("RewardCard"), 64, 840, 952, 40, 1080, 1104);
        hint.text = "上下滑动查看完整建议";
        hint.gameObject.SetActive(false);
        ui.rewardScrollHint = hint;
        EditorUtility.SetDirty(root);
    }

    private static void Home()
    {
        var home = Named("HomeHudLayer");
        Box(Named("TitleButton"), home, 64, 100, 700, 120);
        Box(Named("BrandDot"), Named("TitleButton"), 8, 56, 32, 32, 700, 120);
        Box(Named("BrandTitle"), Named("TitleButton"), 64, 12, 600, 108, 700, 120);
        Type("BrandTitle", 72, Color.black, TextAnchor.MiddleLeft, true);
        Box(Named("TodayText"), home, 96, 234, 800, 72);
        Type("TodayText", 36, Color.black, TextAnchor.MiddleLeft);
        string[] names = { "Growth", "Records", "Settings" };
        string[] icons = { "IconGrowth", "IconRecord", "IconSettings" };
        for (int i = 0; i < names.Length; i++)
        {
            var button = Named(names[i] + "Button");
            Box(button, home, 888, 1530 + i * 170, 144, 144);
            Surface(button, "IconCircle");
            var icon = Named(names[i] + "Icon");
            Icon(icon, Sprite(icons[i]), button, 144, 144);
        }
        Box(Named("StartButton"), home, 96, 2030, 888, 180);
        StyleButton(Named("StartButton"), true, 52);
        Named("StartSpark").gameObject.SetActive(false);
        Box(Named("BubbleText").parent, home, 680, 1230, 330, 220);
        Surface(Named("BubbleText").parent, "Card");
        Fill((RectTransform)Named("BubbleText"), 32);
        Type("BubbleText", 36, Color.black, TextAnchor.MiddleCenter);
    }

    private static void Setup(CatLifeMobileUiPresentation ui)
    {
        var panel = Named("SetupPanel");
        panel.GetComponent<Image>().color = new Color(0, 0, 0, .10f);
        var brand = NewText(panel, "FigmaSetupBrand", 72, Color.black); brand.fontStyle = FontStyle.Bold; brand.alignment = TextAnchor.MiddleLeft;
        Box(brand.transform, panel, 128, 112, 600, 108); brand.text = "CatLife";
        var dot = Child(panel, "FigmaSetupBrandDot"); var dotImage = dot.GetComponent<Image>() ?? dot.gameObject.AddComponent<Image>();
        dotImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/CatLifeHome/dot.png"); dotImage.color = Orange; dotImage.raycastTarget = false; Box(dot, panel, 72, 156, 32, 32);
        ui.setupToday = NewText(panel, "FigmaSetupToday", 36, Color.black); ui.setupToday.supportRichText = true; ui.setupToday.alignment = TextAnchor.MiddleLeft;
        Box(ui.setupToday.transform, panel, 96, 234, 800, 72);
        var card = Named("SetupCard"); Box(card, panel, 0, 1320, 1080, 1104); Surface(card, "Sheet");
        Named("SetupBottomBacking").GetComponent<Image>().color = Color.clear;
        Box(Named("SetupGrip"), card, 480, 34, 120, 12, 1080, 1104);
        Box(Named("SetupTitle"), card, 64, 88, 952, 104, 1080, 1104); Type("SetupTitle", 60, Color.black, TextAnchor.MiddleCenter, true);
        Box(Named("SetupSubtitle"), card, 64, 180, 952, 72, 1080, 1104); Type("SetupSubtitle", 36, Muted, TextAnchor.MiddleCenter);
        Box(Named("SelectedMinutes"), card, 64, 252, 952, 36, 1080, 1104); Type("SelectedMinutes", 24, Muted, TextAnchor.MiddleCenter);
        ui.minuteImages = new Image[3]; ui.minuteTexts = new Text[3];
        string[] minutes = { "Minutes15", "Minutes25", "Minutes45", "CustomMinutes" };
        for (int i = 0; i < minutes.Length; i++)
        {
            var choice = Named(minutes[i]); Box(choice, card, 64 + 244 * i, 300, 220, 128, 1080, 1104);
            Surface(choice, "Chip");
            foreach (var text in choice.GetComponentsInChildren<Text>(true)) { SetType(text, 40, Color.black, TextAnchor.MiddleCenter); Fill(text.rectTransform, 14); }
            if (i < 3) { ui.minuteImages[i] = choice.GetComponent<Image>(); ui.minuteTexts[i] = choice.GetComponentInChildren<Text>(true); }
        }
        Box(Named("ReminderButton"), card, 64, 470, 464, 128, 1080, 1104);
        Box(Named("ReminderGentleButton"), card, 552, 470, 464, 128, 1080, 1104);
        StyleButton(Named("ReminderButton"), false, 44); StyleButton(Named("ReminderGentleButton"), false, 44);
        ui.quietImage = Named("ReminderButton").GetComponent<Image>(); ui.gentleImage = Named("ReminderGentleButton").GetComponent<Image>();
        ui.quietText = Named("ReminderButtonText").GetComponent<Text>(); ui.gentleText = Named("ReminderGentleButtonText").GetComponent<Text>();
        var ai = Named("AiButton"); Box(ai, card, 64, 640, 952, 132, 1080, 1104); Surface(ai, "Card");
        Box(Named("AiLabel"), ai, 32, 0, 700, 132, 952, 132); Type("AiLabel", 36, Color.black, TextAnchor.MiddleLeft);
        Named("AiLabel").GetComponent<Text>().text = "智能建议 · 会后一次";
        Named("AiButtonText").gameObject.SetActive(false);
        Box(Named("AiToggleTrack"), ai, 772, 28, 132, 76, 952, 132);
        ui.setupAiToggle = Named("AiToggleTrack").GetComponent<Image>(); ui.setupAiToggle.sprite = ui.toggleOff; ui.setupAiToggle.type = Image.Type.Simple;
        Named("AiToggleKnob").gameObject.SetActive(false);
        Box(Named("SetupCancel"), card, 64, 860, 300, 148, 1080, 1104); StyleButton(Named("SetupCancel"), false, 44);
        Box(Named("SetupStart"), card, 396, 860, 620, 148, 1080, 1104); StyleButton(Named("SetupStart"), true, 52);
        ui.focusMode = Named("FocusCompanionText").GetComponent<Text>();
    }

    private static void Focus()
    {
        var panel = Named("FocusPanel"); panel.GetComponent<Image>().color = new Color(23 / 255f, 74 / 255f, 42 / 255f, .14f);
        Box(Named("FocusTitle"), panel, 240, 160, 600, 72); Type("FocusTitle", 44, Color.white, TextAnchor.MiddleCenter);
        Box(Named("TimerText"), panel, 180, 292, 720, 200); Type("TimerText", 128, Color.white, TextAnchor.MiddleCenter);
        var chip = Named("FocusCompanionChip"); Box(chip, panel, 300, 528, 480, 112); Surface(chip, "Card");
        Named("FocusCompanionIcon").gameObject.SetActive(false);
        Fill((RectTransform)Named("FocusCompanionText"), 20); Type("FocusCompanionText", 36, Color.black, TextAnchor.MiddleCenter);
        var track = Named("SwipeTrack"); Box(track, panel, 96, 2030, 888, 180); Surface(track, "Track");
        var contact = track.GetComponent<CatLifePawContactView>();
        if (contact != null) contact.SetTrackSize(888f / 1080f, 180);
        track.GetComponent<Image>().color = new Color(1, 1, 1, .87f);
        var handle = Named("SwipeHandle"); handle.GetComponent<Image>().sprite = Skin("IconCircle"); handle.GetComponent<Image>().color = Color.white;
        Named("SwipePaw").GetComponent<Image>().sprite = Sprite("IconPaw"); Named("SwipePaw").GetComponent<Image>().color = Color.white;
        Box(Named("SwipeLabel"), track, 180, 0, 528, 180, 888, 180); Type("SwipeLabel", 44, Color.black, TextAnchor.MiddleCenter);
        var outline = Named("SwipeLabel").GetComponent<Outline>(); if (outline != null) UnityEngine.Object.DestroyImmediate(outline);
    }

    private static void Reward(CatLifeMobileUiPresentation ui)
    {
        var panel = Named("RewardPanel"); var card = Named("RewardCard");
        Box(card, panel, 0, 1320, 1080, 1104); Surface(card, "Sheet");
        Named("RewardBottomBacking").GetComponent<Image>().color = Color.clear;
        Box(Named("RewardTitle"), card, 64, 56, 952, 96, 1080, 1104); Type("RewardTitle", 60, Color.black, TextAnchor.MiddleCenter, true);
        Box(Named("RewardLeafLeft"), card, 318, 80, 64, 60, 1080, 1104);
        Box(Named("RewardLeafRight"), card, 702, 80, 64, 60, 1080, 1104);
        foreach (string leaf in new[] { "RewardLeafLeft", "RewardLeafRight" }) { Named(leaf).GetComponent<Image>().sprite = Sprite("IconLeaf"); Named(leaf).GetComponent<Image>().color = new Color(1, 1, 1, .32f); }
        var stats = Child(card, "FigmaRewardStats"); Fill((RectTransform)stats, 0); ui.rewardStats = stats.gameObject;
        ui.rewardDuration = NewText(stats, "RewardDurationText", 128, Orange); Box(ui.rewardDuration.transform, stats, 160, 140, 760, 180, 1080, 1104);
        var behavior = NewSurface(stats, "RewardAggregateCard", "Card"); Box(behavior, stats, 64, 324, 952, 92, 1080, 1104);
        ui.rewardBehavior = NewText(behavior, "RewardBehaviorText", 36, Color.black); Fill(ui.rewardBehavior.rectTransform, 20);
        var progress = NewSurface(stats, "RewardGrowthCard", "Card"); Box(progress, stats, 64, 440, 952, 140, 1080, 1104);
        ui.rewardGrowth = NewText(progress, "RewardGrowthText", 34, Color.black); Box(ui.rewardGrowth.transform, progress, 32, 8, 888, 82, 952, 140);
        ui.rewardProgress = Progress(progress, "RewardGrowthProgress", 952, 140, 102);
        ui.rewardPaws = NewText(stats, "RewardPawsText", 44, Orange); Box(ui.rewardPaws.transform, stats, 64, 594, 952, 72, 1080, 1104);
        var viewport = NewSurface(card, "RewardInsightViewport", "Card");
        if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>(); Box(viewport, card, 64, 670, 952, 190, 1080, 1104);
        var content = Child(viewport, "RewardInsightContent"); TopContent((RectTransform)content, 190);
        var insight = Named("RewardText").GetComponent<Text>(); insight.transform.SetParent(content, false); Fill(insight.rectTransform, 24);
        SetType(insight, 34, Color.black, TextAnchor.UpperLeft); insight.supportRichText = false;
        var scroll = viewport.GetComponent<ScrollRect>() ?? viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = (RectTransform)viewport; scroll.content = (RectTransform)content; scroll.horizontal = false; scroll.inertia = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        viewport.GetComponent<Image>().raycastTarget = true;
        ui.rewardInsight = insight; ui.rewardViewport = (RectTransform)viewport; ui.rewardContent = (RectTransform)content; ui.rewardScroll = scroll;
        Box(Named("RewardHome"), card, 64, 880, 460, 128, 1080, 1104); StyleButton(Named("RewardHome"), false, 44);
        Box(Named("RewardAgain"), card, 556, 880, 460, 128, 1080, 1104); StyleButton(Named("RewardAgain"), true, 52);
    }

    private static void PageHeader(string pageName, string title)
    {
        var page = Named(pageName + "Panel"); page.GetComponent<Image>().sprite = null; page.GetComponent<Image>().color = Cream;
        var header = Named(title + "Header"); Box(header, page, 0, 0, 1080, 232); header.GetComponent<Image>().sprite = null;
        var bannerViewport = Child(page, pageName + "FigmaBannerViewport");
        Box(bannerViewport, page, 0, 232, 1080, 460);
        bannerViewport.SetAsFirstSibling();
        if (bannerViewport.GetComponent<RectMask2D>() == null) bannerViewport.gameObject.AddComponent<RectMask2D>();
        var banner = Named(pageName + "SceneBanner"); banner.SetParent(bannerViewport, false);
        Fill((RectTransform)banner, 0); banner.GetComponent<Image>().preserveAspect = false;
        var aspect = banner.GetComponent<AspectRatioFitter>() ?? banner.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspect.aspectRatio = 1080f / 460f;
        var sheet = Named(pageName + "Sheet"); Box(sheet, page, 0, 652, 1080, 1676); Surface(sheet, "Sheet");
        var back = Named(pageName + "Back"); Box(back, header, 40, 80, 120, 128, 1080, 232); Surface(back, "IconCircle");
        back.GetComponentInChildren<Text>(true).gameObject.SetActive(false);
        var icon = Child(back, "FigmaBackIcon"); var image = icon.gameObject.GetComponent<Image>() ?? icon.gameObject.AddComponent<Image>();
        Icon(icon, Sprite("IconBack"), back, 120, 128);
        Named(title + "Icon").gameObject.SetActive(false);
        Box(Named(title + "Title"), header, 210, 102, 660, 72, 1080, 232); Type(title + "Title", 44, Color.black, TextAnchor.MiddleCenter);
    }

    private static void Records(CatLifeMobileUiPresentation ui)
    {
        var sheet = Named("RecordsSheet");
        Box(Named("RecordsSummaryCard"), sheet, 64, 64, 952, 236, 1080, 1676); Surface(Named("RecordsSummaryCard"), "Card");
        Fill((RectTransform)Named("RecordsText"), 32); Type("RecordsText", 36, Color.black, TextAnchor.MiddleLeft);
        var chart = Named("RecordsChart"); Box(chart, sheet, 64, 336, 952, 650, 1080, 1676); Surface(chart, "Card");
        Box(Named("RecordsChartTitle"), chart, 32, 32, 600, 72, 952, 650); Type("RecordsChartTitle", 44, Color.black, TextAnchor.MiddleLeft);
        for (int i = 0; i < 4; i++)
        {
            Box(Named("ChartGrid" + i), chart, 72, 480 - i * 100, 808, 2, 952, 650);
            var line = Named("ChartGrid" + i).GetComponent<Image>(); line.sprite = Sprite("VisualChartGrid1"); line.color = Color.white; line.type = Image.Type.Simple;
            Box(Named("ChartAxis" + i), chart, 2, 454 - i * 100, 64, 52, 952, 650); Type("ChartAxis" + i, 18, Muted, TextAnchor.MiddleRight);
        }
        Box(Named("RecordsWeekTotal"), chart, 32, 564, 888, 72, 952, 650); Type("RecordsWeekTotal", 36, Muted, TextAnchor.MiddleLeft);
        ui.dayBars = Enumerable.Range(0, 7).Select(i => Named("DayBar" + i).GetComponent<Image>()).ToArray();
        ui.dayLabels = Enumerable.Range(0, 7).Select(i => Named("DayLabel" + i).GetComponent<Text>()).ToArray();
        foreach (Text label in ui.dayLabels) SetType(label, 28, Muted, TextAnchor.MiddleCenter);
        var list = Named("RecordsListCard"); Box(list, sheet, 64, 1024, 952, 652, 1080, 1676); list.GetComponent<Image>().color = Color.clear;
        Named("RecordsListTitle").gameObject.SetActive(false);
        var viewport = Child(list, "FigmaRecordsViewport"); Fill((RectTransform)viewport, 0);
        if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>(); var hit = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        var content = Child(viewport, "FigmaRecordsContent"); TopContent((RectTransform)content, 972);
        var scroll = viewport.GetComponent<ScrollRect>() ?? viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = (RectTransform)viewport; scroll.content = (RectTransform)content; scroll.horizontal = false; scroll.inertia = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        ui.recordRows = new Text[6]; ui.recordCards = new GameObject[6];
        for (int i = 0; i < 6; i++)
        {
            var row = NewSurface(content, "FigmaRecordCard" + i, "Card"); Box(row, content, 0, i * 162, 952, 138, 952, 972);
            var text = i == 0 ? Named("RecordsListText").GetComponent<Text>() : NewText(row, "FigmaRecordText" + i, 32, Color.black);
            text.transform.SetParent(row, false); Fill(text.rectTransform, 24); SetType(text, 32, Color.black, TextAnchor.MiddleLeft);
            ui.recordRows[i] = text; ui.recordCards[i] = row.gameObject;
        }
    }

    private static void Growth(CatLifeMobileUiPresentation ui)
    {
        var sheet = Named("GrowthSheet"); var stats = Named("GrowthStatsCard");
        Box(stats, sheet, 64, 64, 952, 264, 1080, 1676); Surface(stats, "Card");
        Box(Named("GrowthText"), stats, 32, 24, 888, 160, 952, 264); Type("GrowthText", 36, Color.black, TextAnchor.MiddleLeft);
        ui.growthProgress = Progress(stats, "FigmaGrowthProgress", 952, 264, 212);
        string[] cards = { "GrowthActionWave", "GrowthActionTail", "GrowthActionLie", "GrowthActionStretch" };
        ui.growthCards = new Image[4]; ui.growthIcons = new Image[4]; ui.growthLabels = new Text[4];
        for (int i = 0; i < cards.Length; i++)
        {
            var card = Named(cards[i]); Box(card, sheet, i % 2 == 0 ? 64 : 572, i < 2 ? 368 : 940, 444, 540, 1080, 1676); Surface(card, "Card");
            var halo = Child(card, "FigmaActionHalo"); var image = halo.gameObject.GetComponent<Image>() ?? halo.gameObject.AddComponent<Image>(); image.sprite = Sprite("VisualActionHalo"); image.raycastTarget = false;
            Box(halo, card, 62, 54, 320, 280, 444, 540); halo.SetAsFirstSibling();
            Box(Named(cards[i] + "Icon"), card, 74, 45, 296, 296, 444, 540);
            Box(Named(cards[i] + "Text"), card, 32, 352, 380, 166, 444, 540); Type(cards[i] + "Text", 36, Color.black, TextAnchor.MiddleCenter);
            ui.growthCards[i] = card.GetComponent<Image>(); ui.growthIcons[i] = Named(cards[i] + "Icon").GetComponent<Image>(); ui.growthLabels[i] = Named(cards[i] + "Text").GetComponent<Text>();
        }
        string[] buttons = { "VisitFishShop", "VisitRewardTree", "VisitPlaza" };
        for (int i = 0; i < buttons.Length; i++) { Box(Named(buttons[i]), sheet, 64 + 324 * i, 1548, 304, 128, 1080, 1676); StyleButton(Named(buttons[i]), false, 40); }
    }

    private static void Settings(CatLifeMobileUiPresentation ui, GameObject root)
    {
        var sheet = Named("SettingsSheet");
        var viewport = Child(sheet, "ExitSettingsViewport"); Fill((RectTransform)viewport, 0);
        if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.raycastTarget = true;
        var content = Child(viewport, "ExitSettingsContent"); TopContent((RectTransform)content, 1676);
        var scroll = sheet.GetComponent<ScrollRect>() ?? sheet.gameObject.AddComponent<ScrollRect>(); scroll.viewport = (RectTransform)viewport; scroll.content = (RectTransform)content; scroll.horizontal = false; scroll.inertia = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        var preference = Named("SettingsPreferenceCard"); Box(preference, content, 64, 64, 952, 506, 1080, 1676); Surface(preference, "Card");
        var privacy = Named("SettingsPrivacyCard"); Box(privacy, content, 64, 600, 952, 506, 1080, 1676); Surface(privacy, "Card");
        var local = Named("SettingsLocalDataCard"); Box(local, content, 64, 1136, 952, 298, 1080, 1676); Surface(local, "Card");
        foreach (string title in new[] { "SettingsPreferenceTitle", "SettingsPrivacyTitle", "SettingsLocalTitle" })
        { Box(Named(title), Named(title).parent, 32, 24, 888, 72, 952, title == "SettingsLocalTitle" ? 298 : 506); Type(title, 44, Color.black, TextAnchor.MiddleLeft); }
        var exit = nodes.SingleOrDefault(t => t.name == "SettingsExitIntervention");
        if (exit == null)
        {
            exit = UnityEngine.Object.Instantiate(Named("SettingsAutoFocus").gameObject, preference).transform;
            exit.name = "SettingsExitIntervention";
            exit.GetComponentInChildren<Text>(true).name = "SettingsExitInterventionText";
        }
        var rows = new[] { "SettingsDuration", "SettingsReminder", "SettingsAutoFocus", "SettingsExitIntervention", "SettingsAi", "SettingsBehavior", "SettingsRecords", "SettingsClear" };
        nodes = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < rows.Length; i++)
        {
            var parent = i < 4 ? preference : i < 6 ? privacy : local;
            float top = i < 4 ? 108 + 104 * i : i < 6 ? 108 + 104 * (i - 4) : 108 + 104 * (i - 6);
            var row = Named(rows[i]); Box(row, parent, 32, top, 888, 100, 952, i < 6 ? 506 : 298);
            row.GetComponent<Image>().sprite = null; row.GetComponent<Image>().color = Color.clear;
            var label = row.GetComponentsInChildren<Text>(true).First(t => t.name == rows[i] + "Text");
            Box(label.transform, row, 0, 0, 510, 100, 888, 100); SetType(label, 36, i == 7 ? new Color32(239, 62, 54, 255) : Color.black, TextAnchor.MiddleLeft);
        }
        ui.settingsRowLabels = new[] { "SettingsDurationText", "SettingsReminderText", "SettingsAutoFocusText", "SettingsAiText", "SettingsBehaviorText", "SettingsRecordsText" }.Select(n => Named(n).GetComponent<Text>()).ToArray();
        ui.durationValue = Value(Named("SettingsDuration"), "FigmaDurationValue"); ui.reminderValue = Value(Named("SettingsReminder"), "FigmaReminderValue");
        ui.autoFocusValue = Value(Named("SettingsAutoFocus"), "FigmaAutoFocusValue"); ui.recordsCountValue = Value(Named("SettingsRecords"), "FigmaRecordsValue"); ui.clearValue = Value(Named("SettingsClear"), "FigmaClearValue");
        ui.settingsAiToggle = Toggle(Named("SettingsAi")); ui.settingsBehaviorToggle = Toggle(Named("SettingsBehavior")); ui.settingsExitToggle = Toggle(exit);
        foreach (var text in exit.GetComponentsInChildren<Text>(true)) if (text.name != "SettingsExitInterventionText") text.gameObject.SetActive(false);
        Named("SettingsExitInterventionText").GetComponent<Text>().text = "猫咪挽留";
        var source = Named("SettingsInsightSource"); Box(source, privacy, 32, 316, 510, 100, 952, 506); Type("SettingsInsightSource", 36, Color.black, TextAnchor.MiddleLeft); source.GetComponent<Text>().text = "洞察来源";
        var sourceValue = NewText(privacy, "FigmaInsightSourceValue", 36, Muted); Box(sourceValue.transform, privacy, 562, 316, 330, 100, 952, 506); sourceValue.alignment = TextAnchor.MiddleRight; sourceValue.text = "本地规则";
        var details = NewSurface(privacy, "SettingsPrivacyDetails", "Secondary"); Box(details, privacy, 32, 420, 888, 100, 952, 506); details.GetComponent<Image>().color = Color.clear; details.GetComponent<Image>().raycastTarget = true; if (details.GetComponent<Button>() == null) details.gameObject.AddComponent<Button>();
        details.Find("FigmaSoftShadow").gameObject.SetActive(false);
        Box(Named("SettingsPrivacyCopy"), details, 0, 0, 510, 100, 888, 100); Type("SettingsPrivacyCopy", 36, Color.black, TextAnchor.MiddleLeft); Named("SettingsPrivacyCopy").GetComponent<Text>().text = "隐私说明";
        var detailsValue = Value(details, "FigmaPrivacyDetailsValue"); detailsValue.text = "查看 ›";
        Box(Named("SettingsText"), content, 80, 1486, 920, 72, 1080, 1676); Type("SettingsText", 28, Muted, TextAnchor.MiddleLeft);
        var footer = NewText(content, "FigmaStatisticsPrivacyCopy", 28, Muted); Box(footer.transform, content, 80, 1542, 920, 72, 1080, 1676); footer.alignment = TextAnchor.MiddleLeft; footer.text = "关闭行为统计不影响计时、记录和奖励。";
        PrivacyPanel(Named("TransientLayer"));
        var sizing = root.GetComponent<CatLifeMobileTouchLayout>() ?? root.AddComponent<CatLifeMobileTouchLayout>();
        sizing.Configure(scroll, rows.Select(n => (RectTransform)Named(n)).ToArray(), Named("SwipeTrack").GetComponent<CatLifeSwipeToEnd>(), (RectTransform)Named("SwipeTrack"),
            (RectTransform)Named("ExitConfirmCard"), new[] { (RectTransform)Named("InterruptConfirm"), (RectTransform)Named("InterruptCancel") }, (RectTransform)Named("ExitConfirmText"));
        sizing.SetVisualTrackHeight(180);
    }

    private static void PrivacyPanel(Transform transient)
    {
        var panel = NewSurface(transient, "FigmaPrivacyPanel", "Card"); Fill((RectTransform)panel, 0); panel.GetComponent<Image>().sprite = null; panel.GetComponent<Image>().color = new Color(0, 0, 0, .35f); panel.GetComponent<Image>().raycastTarget = true;
        var card = NewSurface(panel, "FigmaPrivacyCard", "Card"); Box(card, panel, 64, 600, 952, 1200);
        var title = NewText(card, "FigmaPrivacyTitle", 52, Color.black); Box(title.transform, card, 32, 48, 888, 96, 952, 1200); title.text = "隐私说明";
        var copy = NewText(card, "FigmaPrivacyCopy", 34, Color.black); Box(copy.transform, card, 48, 180, 856, 720, 952, 1200); copy.alignment = TextAnchor.UpperLeft;
        copy.text = "计时、记录和奖励保存在本机。\n\n关闭行为统计不会关闭计时、记录或奖励。\n\n智能建议默认关闭。\n你同意并开启后，仅在会话完成时请求一次。\n\n仅发送本次会话的聚合值：\n时长、触控次数、后台次数和稳定度。\n不发送输入文字、触控轨迹或屏幕内容。\n\n查看本页不会开启智能建议。";
        var close = NewSurface(card, "FigmaPrivacyClose", "Primary"); Box(close, card, 96, 996, 760, 148, 952, 1200); if (close.GetComponent<Button>() == null) close.gameObject.AddComponent<Button>(); close.GetComponent<Image>().raycastTarget = true;
        var label = NewText(close, "FigmaPrivacyCloseText", 44, Color.white); Fill(label.rectTransform, 24); label.text = "知道了";
        panel.gameObject.SetActive(false);
    }

    private static Transform Named(string name) => nodes.Single(t => t != null && t.name == name);
    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png") ?? throw new InvalidOperationException("Missing Figma sprite: " + name);
    private static Sprite Skin(string name) => Sprite("Skin" + name);
    private static void Box(Transform node, Transform parent, float x, float y, float width, float height, float parentWidth = 1080, float parentHeight = 2424)
    {
        node.SetParent(parent, false); var r = (RectTransform)node;
        r.anchorMin = new Vector2(x / parentWidth, 1 - (y + height) / parentHeight); r.anchorMax = new Vector2((x + width) / parentWidth, 1 - y / parentHeight);
        r.offsetMin = r.offsetMax = Vector2.zero; r.localScale = Vector3.one;
    }
    private static void Fill(RectTransform rect, float inset) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset); }
    private static void TopContent(RectTransform r, float height) { r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(0, height); }
    private static void Type(string name, int size, Color color, TextAnchor align, bool bold = false) => SetType(Named(name).GetComponent<Text>(), size, color, align, bold);
    private static void SetType(Text text, int size, Color color, TextAnchor align, bool bold = false)
    { text.font = font; text.fontSize = size; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.color = color; text.alignment = align; text.resizeTextForBestFit = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; text.lineSpacing = 1f; }
    private static Transform Child(Transform parent, string name)
    { var child = parent.Find(name); if (child != null) return child; var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go.transform; }
    private static Transform NewSurface(Transform parent, string name, string skin)
    { var child = Child(parent, name); if (child.GetComponent<Image>() == null) child.gameObject.AddComponent<Image>(); Surface(child, skin); child.GetComponent<Image>().raycastTarget = false; return child; }
    private static Text NewText(Transform parent, string name, int size, Color color)
    { var child = Child(parent, name); var text = child.GetComponent<Text>() ?? child.gameObject.AddComponent<Text>(); SetType(text, size, color, TextAnchor.MiddleCenter); text.raycastTarget = false; return text; }
    private static Text Value(Transform parent, string name)
    { var text = NewText(parent, name, 36, Muted); Box(text.transform, parent, 530, 0, 330, 100, 888, 100); text.alignment = TextAnchor.MiddleRight; return text; }
    private static Image Toggle(Transform parent)
    { var child = Child(parent, "FigmaToggle"); var image = child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>(); image.sprite = Sprite("ToggleOff"); image.color = Color.white; image.raycastTarget = false; Box(child, parent, 716, 12, 132, 76, 888, 100); return image; }
    private static Image Progress(Transform parent, string name, float width, float height, float y)
    { var track = NewSurface(parent, name + "Track", "ProgressTrack"); Box(track, parent, 32, y, width - 64, 16, width, height); var fill = NewSurface(track, name, "Progress").GetComponent<Image>(); Fill(fill.rectTransform, 0); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; return fill; }
    private static void Surface(Transform target, string skin)
    {
        var image = target.GetComponent<Image>(); image.sprite = Skin(skin); image.type = Image.Type.Sliced; image.color = Color.white;
        if (skin != "Primary" && skin != "Secondary" && skin != "Card" && skin != "IconCircle") return;
        var child = Child(target, "FigmaSoftShadow"); var shadow = child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>();
        shadow.sprite = Skin(skin == "Primary" || skin == "IconCircle" ? "PrimaryShadow" : "CardShadow"); shadow.type = Image.Type.Sliced; shadow.raycastTarget = false; shadow.color = Color.white;
        var r = (RectTransform)child; Fill(r, -32); child.SetAsFirstSibling();
    }
    private static void StyleButton(Transform target, bool primary, int size)
    {
        Surface(target, primary ? "Primary" : "Secondary"); var button = target.GetComponent<Button>();
        if (primary) { button.transition = Selectable.Transition.SpriteSwap; button.spriteState = new SpriteState { pressedSprite = Skin("PrimaryPressed"), disabledSprite = Skin("PrimaryDisabled") }; }
        else { button.transition = Selectable.Transition.ColorTint; var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1, .98f, .94f); colors.pressedColor = new Color(.96f, .92f, .84f); button.colors = colors; }
        var text = target.GetComponentInChildren<Text>(true); Fill(text.rectTransform, 20); SetType(text, size, primary ? Color.white : Color.black, TextAnchor.MiddleCenter, true);
    }
    private static void Icon(Transform target, Sprite sprite, Transform parent, float width, float height)
    { var image = target.GetComponent<Image>(); image.sprite = sprite; image.color = Color.white; image.preserveAspect = true; image.raycastTarget = false; float w = sprite.rect.width / 2, h = sprite.rect.height / 2; Box(target, parent, (width - w) / 2, (height - h) / 2, w, h, width, height); }
}
