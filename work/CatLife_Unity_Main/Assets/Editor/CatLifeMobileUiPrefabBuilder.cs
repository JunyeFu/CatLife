using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CatLifeMobileUiPrefabBuilder
{
    public const string PrefabPath = "Assets/MobileRuntime/UI/PF_CL_UI_Mobile.prefab";
    public static void InstallLocalReviewButtons()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var debug = System.Array.Find(root.GetComponentsInChildren<Transform>(true), t => t.name == "DebugPanel");
        AddLocalReviewButtons(debug);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void AddLocalReviewButtons(Transform debug)
    {
        var text = debug.Find("DebugText").GetComponent<RectTransform>();
        text.anchorMin = new Vector2(.1f,.49f);
        string[] names = { "ReviewTail", "ReviewWave", "ReviewRest", "ReviewStretch" };
        string[] labels = { "本地：轻摆尾", "本地：挥爪", "本地：休息", "本地：伸展" };
        for (int i=0;i<4;i++)
        {
            var button = debug.Find(names[i]);
            if (button == null)
            {
                button = Object.Instantiate(debug.Find("ReviewerMinute"),debug);
                button.name = names[i];
                button.GetComponentInChildren<Text>().text = labels[i];
            }
            var rect = button.GetComponent<RectTransform>();
            float x = i%2 == 0 ? .10f : .52f;
            float y = i<2 ? .39f : .30f;
            rect.anchorMin = new Vector2(x,y); rect.anchorMax = new Vector2(x+.38f,y+.08f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
    private static readonly Color Cream = new Color32(255, 248, 234, 255);
    private static readonly Color Orange = new Color32(255, 138, 0, 255);
    private static readonly Color Green = new Color32(23, 74, 42, 255);
    private static readonly Color Sky = new Color32(54, 169, 232, 255);
    private static readonly Color Alert = new Color32(239, 62, 54, 255);
    private static readonly Color Ink = new Color(.16f, .20f, .13f, 1f);
    private static readonly Color MutedInk = new Color(.32f, .29f, .24f, 1f);
    private const string UiKitPath = "Assets/UI/CatLifeHome/";
    private const string UiArtPath = "Assets/MobileRuntime/UI/Art/";
    private const string ActionThumbPath = UiArtPath + "Actions/";
    private const string UiFontPath = "Assets/MobileRuntime/UI/Fonts/NotoSansSC-Medium.ttf";
    private static Sprite sprite;
    private static Sprite roundedSolid;
    private static Sprite roundedOutline;
    private static Sprite startButton;
    private static Sprite circleOutline;
    private static Sprite circleSolid;
    private static Sprite dot;
    private static Sprite iconCat;
    private static Sprite iconGrowth;
    private static Sprite iconRecord;
    private static Sprite iconSettings;
    private static Sprite iconSpark;
    private static Sprite iconPaw;
    private static Sprite iconClock;
    private static Sprite iconLeaf;
    private static Sprite iconMoon;
    private static Sprite actionWave;
    private static Sprite actionTail;
    private static Sprite actionLoaf;
    private static Sprite actionStretch;
    private static Sprite cardWide;
    private static Sprite halfSheet;
    private static Sprite fullSheet;
    private static Sprite primaryButton;
    private static Sprite swipeTrack;
    private static Sprite pill;
    private static Sprite iconCircle;
    private static Font font;

    public static GameObject Build()
    {
        EnsureFolder("Assets/MobileRuntime/UI");
        NormalizeUiKitImports();
        NormalizeActionThumbnailImports();
        roundedSolid = LoadSprite("rounded_solid");
        roundedOutline = LoadSprite("rounded_outline");
        startButton = LoadSprite("start_button");
        circleOutline = LoadSprite("circle_outline");
        circleSolid = LoadSprite("circle_solid");
        dot = LoadSprite("dot");
        iconCat = LoadSprite("icon_cat");
        iconRecord = LoadSprite("icon_record");
        iconSettings = LoadSprite("icon_settings");
        iconSpark = LoadSprite("icon_spark");
        iconPaw = LoadSprite("icon_paw");
        iconClock = LoadSprite("icon_clock");
        iconLeaf = LoadSprite("icon_leaf");
        iconMoon = LoadSprite("icon_moon");
        actionWave = LoadActionSprite("T_CL_CatAction_Wave_512");
        actionTail = LoadActionSprite("T_CL_CatAction_Tail_512");
        actionLoaf = LoadActionSprite("T_CL_CatAction_Loaf_512");
        actionStretch = LoadActionSprite("T_CL_CatAction_Stretch_512");
        cardWide = LoadArtSprite("Components", "S_CL_UI_CardWide_v02_952x240");
        halfSheet = LoadArtSprite("Components", "S_CL_UI_HalfSheet_v02_1080x1104");
        fullSheet = LoadArtSprite("Components", "S_CL_UI_FullSheet_v02_1080x1748");
        primaryButton = LoadArtSprite("Components", "S_CL_UI_PrimaryButton_v02_888x180");
        swipeTrack = LoadArtSprite("Components", "S_CL_UI_SwipeTrack_v02_888x180");
        pill = LoadArtSprite("Components", "S_CL_UI_Pill_v02_480x96");
        iconCircle = LoadArtSprite("Components", "S_CL_UI_IconCircle_v02_128");
        iconRecord = LoadArtSprite("Icons", "S_CL_UI_record_v1_256");
        iconGrowth = LoadArtSprite("Icons", "S_CL_UI_growth_v1_256");
        iconSettings = LoadArtSprite("Icons", "S_CL_UI_settings_v1_256");
        iconSpark = LoadArtSprite("Icons", "S_CL_UI_spark_v1_256");
        iconPaw = LoadArtSprite("Icons", "S_CL_UI_paw_v1_256");
        iconClock = LoadArtSprite("Icons", "S_CL_UI_clock_v1_256");
        iconLeaf = LoadArtSprite("Icons", "S_CL_UI_leaf_v1_256");
        iconMoon = LoadArtSprite("Icons", "S_CL_UI_moon_v1_256");
        sprite = cardWide;
        font = AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);
        if (font == null) throw new System.InvalidOperationException("Missing CatLife UI font subset: " + UiFontPath);
        GameObject canvas = new GameObject("CatLifeMobileView", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 2400); scaler.matchWidthOrHeight = .5f;
        GameObject safe = Panel("SafeArea", canvas.transform, Color.clear); safe.AddComponent<CatLifeSafeArea>();
        Transform root = safe.transform;

        GameObject home = Panel("HomeHudLayer", root, Color.clear);
        BrandButton(home.transform);
        TextAt("TodayText", "今日专注 <color=#FF8A00>0</color> 分钟", home.transform, new Vector2(.075f, .855f), new Vector2(.72f, .91f), 32, TextAnchor.MiddleLeft, Green);
        IconButtonAt("GrowthButton", "GrowthIcon", iconGrowth, home.transform, new Vector2(.82f, .313f), new Vector2(.94f, .367f));
        IconButtonAt("RecordsButton", "RecordsIcon", iconRecord, home.transform, new Vector2(.82f, .243f), new Vector2(.94f, .297f));
        IconButtonAt("SettingsButton", "SettingsIcon", iconSettings, home.transform, new Vector2(.82f, .173f), new Vector2(.94f, .227f));
        GameObject homeStart = ButtonAt("StartButton", "开始专注", home.transform, new Vector2(.15f, .088f), new Vector2(.85f, .163f), Color.white, 44).gameObject;
        homeStart.GetComponent<Image>().sprite = primaryButton;
        homeStart.transform.Find("StartButtonText").GetComponent<RectTransform>().offsetMin = new Vector2(92, 4);
        ImageAt("StartSpark", iconSpark, homeStart.transform, new Vector2(.18f, .23f), new Vector2(.29f, .77f), Color.white);
        TextAt("BubbleText", "先不用急，\n我在这里。", home.transform, new Vector2(.14f, .385f), new Vector2(.52f, .455f), 30, TextAnchor.MiddleCenter, Ink, Color.white);

        GameObject session = Panel("SessionLayer", root, Color.clear);
        GameObject setup = Panel("SetupPanel", session.transform, new Color(0, 0, 0, .12f));
        PanelAt("SetupBottomBacking", setup.transform, Vector2.zero, new Vector2(1f, .46f), Cream);
        GameObject setupCard = PanelAt("SetupCard", setup.transform, new Vector2(.02f, .01f), new Vector2(.98f, .46f), Cream);
        setupCard.GetComponent<Image>().sprite = halfSheet;
        PanelAt("SetupGrip", setupCard.transform, new Vector2(.43f, .94f), new Vector2(.57f, .955f), new Color(.72f, .66f, .56f, .55f));
        TextAt("SetupTitle", "准备专注", setupCard.transform, new Vector2(.08f, .82f), new Vector2(.92f, .94f), 46, TextAnchor.MiddleCenter, Ink);
        TextAt("SetupSubtitle", "选一个适合现在的节奏", setupCard.transform, new Vector2(.12f, .755f), new Vector2(.88f, .82f), 26, TextAnchor.MiddleCenter, MutedInk);
        TextAt("SelectedMinutes", "本次 25 分钟", setupCard.transform, new Vector2(.25f, .69f), new Vector2(.75f, .75f), 24, TextAnchor.MiddleCenter, Orange);
        ButtonAt("Minutes15", "15 分钟", setupCard.transform, new Vector2(.05f, .57f), new Vector2(.27f, .675f), Color.white, 27, null, MutedInk);
        ButtonAt("Minutes25", "25 分钟", setupCard.transform, new Vector2(.29f, .57f), new Vector2(.51f, .675f), Orange, 27);
        ButtonAt("Minutes45", "45 分钟", setupCard.transform, new Vector2(.53f, .57f), new Vector2(.75f, .675f), Color.white, 27, null, MutedInk);
        InputAt("CustomMinutes", "自定义", setupCard.transform, new Vector2(.77f, .57f), new Vector2(.95f, .675f));
        ButtonAt("ReminderButton", "安静陪伴", setupCard.transform, new Vector2(.05f, .405f), new Vector2(.49f, .51f), Color.white, 27, "ReminderButtonText", Ink).gameObject.GetComponent<Image>().sprite = roundedOutline;
        ButtonAt("ReminderGentleButton", "轻提醒", setupCard.transform, new Vector2(.51f, .405f), new Vector2(.95f, .51f), Color.white, 27, null, MutedInk);
        ToggleRowAt("AiButton", setupCard.transform, new Vector2(.05f, .25f), new Vector2(.95f, .375f));
        ButtonAt("SetupCancel", "取消", setupCard.transform, new Vector2(.05f, .07f), new Vector2(.34f, .19f), Color.white, 30, null, MutedInk);
        GameObject setupStart = ButtonAt("SetupStart", "开始 25 分钟", setupCard.transform, new Vector2(.37f, .07f), new Vector2(.95f, .19f), Color.white, 30).gameObject;
        setupStart.GetComponent<Image>().sprite = primaryButton;

        GameObject transition = Panel("TransitionPanel", session.transform, new Color(.08f, .16f, .14f, .20f));
        TextAt("TransitionText", "慢慢趴好，准备开始……", transition.transform, new Vector2(.12f, .74f), new Vector2(.88f, .82f), 38, TextAnchor.MiddleCenter, Color.white);
        ButtonAt("AutoFocusCancel", "暂不进入", transition.transform, new Vector2(.30f, .08f), new Vector2(.70f, .14f), Green, 28);
        GameObject focus = Panel("FocusPanel", session.transform, new Color(.04f, .08f, .15f, .28f));
        TextAt("FocusTitle", "专注中", focus.transform, new Vector2(.20f, .88f), new Vector2(.80f, .94f), 38, TextAnchor.MiddleCenter, Color.white);
        TextAt("TimerText", "25:00", focus.transform, new Vector2(.12f, .75f), new Vector2(.88f, .88f), 106, TextAnchor.MiddleCenter, Color.white);
        GameObject focusChip = PanelAt("FocusCompanionChip", focus.transform, new Vector2(.33f, .70f), new Vector2(.67f, .75f), new Color(1f, .97f, .89f, .92f));
        focusChip.GetComponent<Image>().sprite = pill;
        ImageAt("FocusCompanionIcon", iconCat, focusChip.transform, new Vector2(.08f, .18f), new Vector2(.25f, .82f), Orange);
        TextAt("FocusCompanionText", "安静陪伴", focusChip.transform, new Vector2(.27f, 0), new Vector2(.94f, 1), 25, TextAnchor.MiddleCenter, MutedInk);
        GameObject swipe = PanelAt("SwipeTrack", focus.transform, new Vector2(.12f, .05f), new Vector2(.88f, .11f), new Color(.25f, .20f, .13f, .88f));
        swipe.GetComponent<Image>().sprite = swipeTrack;
        swipe.GetComponent<Image>().raycastTarget = true;
        CatLifeSwipeToEnd swipeControl = swipe.AddComponent<CatLifeSwipeToEnd>();
        Image fill = Panel("SwipeFill", swipe.transform, Orange).GetComponent<Image>(); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0;
        RectTransform handle = PanelAt("SwipeHandle", swipe.transform, new Vector2(0, .05f), new Vector2(.12f, .95f), Cream).GetComponent<RectTransform>();
        handle.GetComponent<Image>().sprite = circleSolid;
        ImageAt("SwipePaw", iconPaw, handle, new Vector2(.20f, .20f), new Vector2(.80f, .80f), Orange);
        TextAt("SwipeLabel", "向右滑动结束", swipe.transform, new Vector2(.15f, 0), new Vector2(.85f, 1), 28, TextAnchor.MiddleCenter, Color.white);
        swipeControl.Configure(handle, fill);

        GameObject reward = Panel("RewardPanel", session.transform, new Color(0, 0, 0, .12f));
        PanelAt("RewardBottomBacking", reward.transform, Vector2.zero, new Vector2(1f, .48f), Cream);
        GameObject rewardCard = PanelAt("RewardCard", reward.transform, new Vector2(.05f, .03f), new Vector2(.95f, .48f), Cream);
        rewardCard.GetComponent<Image>().sprite = halfSheet;
        ImageAt("RewardLeafLeft", iconLeaf, rewardCard.transform, new Vector2(.25f, .84f), new Vector2(.33f, .94f), Green);
        ImageAt("RewardLeafRight", iconLeaf, rewardCard.transform, new Vector2(.67f, .84f), new Vector2(.75f, .94f), Green).rectTransform.localScale = new Vector3(-1, 1, 1);
        TextAt("RewardTitle", "专注完成", rewardCard.transform, new Vector2(.1f, .82f), new Vector2(.9f, .96f), 46, TextAnchor.MiddleCenter, Green);
        TextAt("RewardText", "", rewardCard.transform, new Vector2(.08f, .25f), new Vector2(.92f, .82f), 31, TextAnchor.UpperCenter, Ink);
        ButtonAt("RewardHome", "返回小镇", rewardCard.transform, new Vector2(.06f, .06f), new Vector2(.46f, .20f), Color.white, 29, null, Green);
        GameObject rewardAgain = ButtonAt("RewardAgain", "再来一次", rewardCard.transform, new Vector2(.54f, .06f), new Vector2(.94f, .20f), Color.white, 29).gameObject;
        rewardAgain.GetComponent<Image>().sprite = primaryButton;

        GameObject pages = Panel("PageLayer", root, Color.clear);
        RecordsPage(pages.transform);
        GrowthPage(pages.transform);
        SettingsPage(pages.transform);

        GameObject transient = Panel("TransientLayer", root, Color.clear);
        GameObject adoption = Panel("AdoptionPanel", transient.transform, new Color(0, 0, 0, .28f));
        adoption.GetComponent<Image>().raycastTarget = true;
        GameObject adoptionCard = PanelAt("AdoptionCard", adoption.transform, new Vector2(.06f, .12f), new Vector2(.94f, .53f), Cream);
        TextAt("AdoptionTitle", "认识你的猫咪", adoptionCard.transform, new Vector2(.08f, .80f), new Vector2(.92f, .96f), 42, TextAnchor.MiddleCenter, Green);
        TextAt("AdoptionCopy", "给它一个名字，开始一起生活。\n名字仅保存在本机，不发送给 AI。", adoptionCard.transform, new Vector2(.08f, .60f), new Vector2(.92f, .80f), 28, TextAnchor.MiddleCenter, Ink);
        GameObject nameBox = PanelAt("CatNameInput", adoptionCard.transform, new Vector2(.12f, .40f), new Vector2(.88f, .57f), Color.white);
        nameBox.GetComponent<Image>().raycastTarget = true;
        InputField nameInput = nameBox.AddComponent<InputField>();
        nameInput.textComponent = TextAt("CatNameText", "小橘", nameBox.transform, Vector2.zero, Vector2.one, 34, TextAnchor.MiddleCenter, Ink);
        nameInput.characterLimit = 12;
        nameInput.text = "小橘";
        ButtonAt("AdoptionDuration", "默认专注 25 分钟 · 点此切换", adoptionCard.transform, new Vector2(.10f, .23f), new Vector2(.90f, .37f), Cream, 27, null, Green);
        ButtonAt("AdoptionConfirm", "领取并进入小镇", adoptionCard.transform, new Vector2(.10f, .05f), new Vector2(.90f, .21f), Orange, 32);
        ConfirmPanel(transient.transform, "ExitConfirm", "确认结束本次专注？\n中断会保留记录，但成长和爪印为 0。", "InterruptConfirm", "确认结束", "InterruptCancel", "继续专注");
        ConfirmPanel(transient.transform, "AiConsentPanel", "AI 只接收本次时长、触控、后台次数和稳定度等聚合值；不会发送文字、轨迹、屏幕或其他应用内容。", "AiConsentAccept", "同意并开启", "AiConsentCancel", "取消");
        GameObject debug = PanelAt("DebugPanel", transient.transform, new Vector2(.08f, .20f), new Vector2(.92f, .80f), new Color(.08f, .12f, .10f, .98f));
        TextAt("DebugTitle", "评审信息", debug.transform, new Vector2(.1f, .82f), new Vector2(.9f, .95f), 42, TextAnchor.MiddleCenter, Cream);
        TextAt("DebugText", "", debug.transform, new Vector2(.1f, .34f), new Vector2(.9f, .80f), 30, TextAnchor.UpperLeft, Color.white);
        ButtonAt("ReviewerMinute", "开始 1 分钟评审会话", debug.transform, new Vector2(.10f, .17f), new Vector2(.90f, .29f), Orange, 28);
        ButtonAt("DebugClose", "关闭", debug.transform, new Vector2(.25f, .04f), new Vector2(.75f, .14f), Green, 27);
        AddLocalReviewButtons(debug.transform);

        setup.SetActive(false); transition.SetActive(false); focus.SetActive(false); reward.SetActive(false);
        foreach (Transform child in pages.transform) child.gameObject.SetActive(false);
        foreach (Transform child in transient.transform) child.gameObject.SetActive(false);
        CatLifeFigmaUiInstaller.Apply(canvas);
        CatLifeRepairUiInstaller.Apply(canvas);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath);
        Object.DestroyImmediate(canvas);
        return prefab;
    }

    private static void RecordsPage(Transform parent)
    {
        GameObject page = Panel("RecordsPanel", parent, Cream);
        PageHeader(page.transform, "专注记录", "RecordsBack", iconRecord);
        GameObject sheet = PanelAt("RecordsSheet", page.transform, new Vector2(.03f, 0f), new Vector2(.97f, .7583f), Cream);
        sheet.GetComponent<Image>().sprite = fullSheet;
        GameObject summary = PanelAt("RecordsSummaryCard", sheet.transform, new Vector2(.04f, .75f), new Vector2(.96f, .91f), Color.white);
        TextAt("RecordsText", "今日 0 分钟  ·  完成 0 次\n最长稳定 00:00", summary.transform, new Vector2(.06f, .08f), new Vector2(.94f, .92f), 27, TextAnchor.MiddleLeft, Ink);
        GameObject chart = PanelAt("RecordsChart", sheet.transform, new Vector2(.04f, .34f), new Vector2(.96f, .71f), Color.white);
        TextAt("RecordsChartTitle", "近 7 天", chart.transform, new Vector2(.05f, .83f), new Vector2(.45f, .96f), 28, TextAnchor.MiddleLeft, Green);
        for (int line = 0; line < 4; line++)
        {
            float y = .24f + line * .16f;
            PanelAt("ChartGrid" + line, chart.transform, new Vector2(.14f, y), new Vector2(.95f, y + .004f), new Color(.79f, .74f, .64f, .50f));
            TextAt("ChartAxis" + line, (line * 15) + " 分", chart.transform, new Vector2(.01f, y - .035f), new Vector2(.13f, y + .035f), 16, TextAnchor.MiddleRight, MutedInk);
        }
        for (int i = 0; i < 7; i++)
        {
            float x0 = .07f + i * .13f;
            PanelAt("DayBar" + i, chart.transform, new Vector2(x0, .20f), new Vector2(x0 + .065f, .38f), i == 6 ? Orange : new Color(1f, .67f, .12f));
            TextAt("DayLabel" + i, "一二三四五六日"[i].ToString(), chart.transform, new Vector2(x0 - .01f, .04f), new Vector2(x0 + .075f, .17f), 20, TextAnchor.MiddleCenter, MutedInk);
        }
        TextAt("RecordsWeekTotal", "本周 0 分钟", chart.transform, new Vector2(.25f, .73f), new Vector2(.95f, .93f), 20, TextAnchor.MiddleRight, Orange);
        GameObject list = PanelAt("RecordsListCard", sheet.transform, new Vector2(.04f, .08f), new Vector2(.96f, .30f), Color.white);
        TextAt("RecordsListTitle", "最近会话", list.transform, new Vector2(.05f, .72f), new Vector2(.45f, .96f), 25, TextAnchor.MiddleLeft, Green);
        TextAt("RecordsListText", "还没有记录", list.transform, new Vector2(.05f, .08f), new Vector2(.95f, .72f), 23, TextAnchor.UpperLeft, MutedInk);
    }

    private static void GrowthPage(Transform parent)
    {
        GameObject page = Panel("GrowthPanel", parent, Cream);
        PageHeader(page.transform, "猫咪成长", "GrowthBack", iconPaw);
        GameObject sheet = PanelAt("GrowthSheet", page.transform, new Vector2(.03f, 0f), new Vector2(.97f, .7583f), Cream);
        sheet.GetComponent<Image>().sprite = fullSheet;
        GameObject stats = PanelAt("GrowthStatsCard", sheet.transform, new Vector2(.04f, .80f), new Vector2(.96f, .96f), Color.white);
        TextAt("GrowthText", "", stats.transform, new Vector2(.06f, .04f), new Vector2(.94f, .96f), 26, TextAnchor.MiddleLeft, Ink);
        ButtonAt("VisitFishShop", "鱼店闻香", sheet.transform, new Vector2(.04f, .71f), new Vector2(.33f, .79f), Color.white, 26, null, Green);
        ButtonAt("VisitRewardTree", "奖励树摇尾", sheet.transform, new Vector2(.355f, .71f), new Vector2(.645f, .79f), Color.white, 26, null, Green);
        ButtonAt("VisitPlaza", "广场伸展", sheet.transform, new Vector2(.67f, .71f), new Vector2(.96f, .79f), Color.white, 26, null, Green);
        GrowthActionCard("GrowthActionWave", "挥爪", actionWave, sheet.transform, new Vector2(.04f, .40f), new Vector2(.48f, .69f), false);
        GrowthActionCard("GrowthActionTail", CatLife.Mobile.CatLifeUnlocks.ReactionLabel("tail_wag"), actionTail, sheet.transform, new Vector2(.52f, .40f), new Vector2(.96f, .69f), false);
        GrowthActionCard("GrowthActionLie", "趴卧", actionLoaf, sheet.transform, new Vector2(.04f, .08f), new Vector2(.48f, .37f), false);
        GrowthActionCard("GrowthActionStretch", "伸懒腰\n完成更多专注解锁", actionStretch, sheet.transform, new Vector2(.52f, .08f), new Vector2(.96f, .37f), true);
    }

    private static void SettingsPage(Transform parent)
    {
        GameObject page = Panel("SettingsPanel", parent, Cream);
        PageHeader(page.transform, "设置", "SettingsBack", iconSettings);
        GameObject sheet = PanelAt("SettingsSheet", page.transform, new Vector2(.03f, 0f), new Vector2(.97f, .7583f), Cream);
        sheet.GetComponent<Image>().sprite = fullSheet;
        TextAt("SettingsText", "本地模式 · 数据留在设备", sheet.transform, new Vector2(.06f, .87f), new Vector2(.94f, .96f), 25, TextAnchor.MiddleCenter, Green);
        GameObject preference = PanelAt("SettingsPreferenceCard", sheet.transform, new Vector2(.04f, .56f), new Vector2(.96f, .84f), Color.white);
        TextAt("SettingsPreferenceTitle", "专注偏好", preference.transform, new Vector2(.05f, .80f), new Vector2(.55f, .97f), 28, TextAnchor.MiddleLeft, Green);
        SettingsRow("SettingsDuration", "默认时长", preference.transform, .55f, .79f);
        SettingsRow("SettingsReminder", "提醒模式", preference.transform, .30f, .54f);
        SettingsRow("SettingsAutoFocus", "自动专注", preference.transform, .05f, .29f);
        GameObject privacy = PanelAt("SettingsPrivacyCard", sheet.transform, new Vector2(.04f, .27f), new Vector2(.96f, .53f), Color.white);
        TextAt("SettingsPrivacyTitle", "智能建议与隐私", privacy.transform, new Vector2(.05f, .79f), new Vector2(.72f, .97f), 27, TextAnchor.MiddleLeft, Green);
        SettingsRow("SettingsAi", "AI 会后建议", privacy.transform, .56f, .78f);
        SettingsRow("SettingsBehavior", "行为数据", privacy.transform, .33f, .55f);
        TextAt("SettingsInsightSource", "洞察来源     本地规则", privacy.transform, new Vector2(.06f, .17f), new Vector2(.94f, .33f), 21, TextAnchor.MiddleLeft, MutedInk);
        TextAt("SettingsPrivacyCopy", "隐私说明     只发送会话聚合值", privacy.transform, new Vector2(.06f, .01f), new Vector2(.94f, .17f), 20, TextAnchor.MiddleLeft, MutedInk);
        GameObject local = PanelAt("SettingsLocalDataCard", sheet.transform, new Vector2(.04f, .05f), new Vector2(.96f, .24f), Color.white);
        TextAt("SettingsLocalTitle", "本地数据", local.transform, new Vector2(.05f, .70f), new Vector2(.45f, .96f), 26, TextAnchor.MiddleLeft, Green);
        ButtonAt("SettingsRecords", "专注记录", local.transform, new Vector2(.04f, .38f), new Vector2(.96f, .68f), Cream, 21, "SettingsRecordsText", Ink);
        ButtonAt("SettingsClear", "清除本地数据", local.transform, new Vector2(.04f, .06f), new Vector2(.96f, .35f), new Color(1f, .91f, .88f), 21, "SettingsClearText", Alert);
    }

    private static void PageHeader(Transform parent, string title, string backName, Sprite icon)
    {
        string banner = backName == "RecordsBack" ? "Records" : backName == "GrowthBack" ? "Growth" : "Settings";
        ImageAt(banner + "SceneBanner", LoadArtSprite("Banners", "T_CL_UI_" + banner + "Runtime_1080x460"), parent, new Vector2(0, .7417f), new Vector2(1, .9333f), Color.white);
        GameObject header = PanelAt(title + "Header", parent, new Vector2(0, .9333f), Vector2.one, Cream);
        header.GetComponent<Image>().sprite = LoadArtSprite("Components", "S_CL_UI_NavBar_v02_1080x160");
        ButtonAt(backName, "返回", header.transform, new Vector2(.035f, .20f), new Vector2(.20f, .80f), Color.white, 23, null, Green);
        ImageAt(title + "Icon", icon, header.transform, new Vector2(.35f, .25f), new Vector2(.42f, .75f), Orange);
        TextAt(title + "Title", title, header.transform, new Vector2(.42f, 0), new Vector2(.82f, 1), 38, TextAnchor.MiddleLeft, Green);
    }

    private static void GrowthActionCard(string name, string label, Sprite icon, Transform parent, Vector2 min, Vector2 max, bool locked)
    {
        GameObject card = PanelAt(name, parent, min, max, locked ? new Color(.88f, .86f, .81f) : Color.white);
        ImageAt(name + "Icon", icon, card.transform, new Vector2(.12f, .38f), new Vector2(.88f, .94f), locked ? new Color(.48f, .47f, .44f, .55f) : Color.white);
        TextAt(name + "Text", label, card.transform, new Vector2(.08f, .06f), new Vector2(.92f, .43f), locked ? 20 : 27, TextAnchor.MiddleCenter, locked ? new Color(.42f, .41f, .38f) : Ink);
    }

    private static void SettingsRow(string name, string label, Transform parent, float minY, float maxY)
    {
        ButtonAt(name, label, parent, new Vector2(.04f, minY), new Vector2(.96f, maxY), Cream, 22, null, Ink);
    }

    private static GameObject Page(Transform parent, string name, string title, string bodyName, string backName)
    {
        GameObject page = Panel(name, parent, Cream);
        TextAt(name + "Title", title, page.transform, new Vector2(.08f, .87f), new Vector2(.92f, .96f), 46, TextAnchor.MiddleCenter, Green);
        TextAt(bodyName, "", page.transform, new Vector2(.08f, .18f), new Vector2(.92f, .84f), 30, TextAnchor.UpperLeft, Ink);
        ButtonAt(backName, "返回小镇", page.transform, new Vector2(.25f, .05f), new Vector2(.75f, .12f), Green, 28);
        return page;
    }
    private static void ConfirmPanel(Transform parent, string name, string message, string okName, string ok, string cancelName, string cancel)
    {
        GameObject overlay = Panel(name, parent, new Color(0, 0, 0, .55f));
        GameObject card = PanelAt(name + "Card", overlay.transform, new Vector2(.08f, .33f), new Vector2(.92f, .67f), Cream);
        TextAt(name + "Text", message, card.transform, new Vector2(.08f, .35f), new Vector2(.92f, .90f), 30, TextAnchor.MiddleCenter, Ink);
        ButtonAt(cancelName, cancel, card.transform, new Vector2(.06f, .08f), new Vector2(.46f, .28f), Green, 26);
        ButtonAt(okName, ok, card.transform, new Vector2(.54f, .08f), new Vector2(.94f, .28f), Orange, 26);
    }
    private static GameObject Panel(string name, Transform parent, Color color) { return PanelAt(name, parent, Vector2.zero, Vector2.one, color); }
    private static GameObject PanelAt(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); RectTransform rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>(); image.color = color; image.sprite = sprite; image.type = Image.Type.Sliced; image.raycastTarget = false; return go;
    }
    private static Text TextAt(string name, string value, Transform parent, Vector2 min, Vector2 max, int size, TextAnchor anchor, Color color, Color? background = null)
    {
        Transform actualParent = parent;
        if (background.HasValue) actualParent = PanelAt(name + "Card", parent, min, max, background.Value).transform;
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(actualParent, false); RectTransform rect = (RectTransform)go.transform; rect.anchorMin = background.HasValue ? Vector2.zero : min; rect.anchorMax = background.HasValue ? Vector2.one : max; rect.offsetMin = new Vector2(12, 6); rect.offsetMax = new Vector2(-12, -6);
        Text text = go.GetComponent<Text>(); text.text = value; text.font = font; text.fontSize = size; text.fontStyle = FontStyle.Bold; text.alignment = anchor; text.color = color; text.resizeTextForBestFit = true; text.resizeTextMinSize = 18; text.raycastTarget = false; return text;
    }
    private static Button ButtonAt(string name, string value, Transform parent, Vector2 min, Vector2 max, Color color, int size, string textName = null, Color? textColor = null)
    {
        GameObject go = PanelAt(name, parent, min, max, color); go.GetComponent<Image>().raycastTarget = true; Button button = go.AddComponent<Button>(); TextAt(textName ?? name + "Text", value, go.transform, Vector2.zero, Vector2.one, size, TextAnchor.MiddleCenter, textColor ?? Color.white); return button;
    }

    private static void BrandButton(Transform parent)
    {
        GameObject go = PanelAt("TitleButton", parent, new Vector2(.06f, .91f), new Vector2(.52f, .98f), Color.clear);
        go.GetComponent<Image>().raycastTarget = true;
        go.AddComponent<Button>();
        ImageAt("BrandDot", dot, go.transform, new Vector2(.01f, .34f), new Vector2(.08f, .66f), Orange);
        TextAt("BrandTitle", "CatLife", go.transform, new Vector2(.10f, 0), Vector2.one, 58, TextAnchor.MiddleLeft, Color.white);
    }

    private static Button IconButtonAt(string name, string iconName, Sprite icon, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = PanelAt(name, parent, min, max, new Color(1f, .82f, .30f, 1f));
        Image background = go.GetComponent<Image>();
        background.sprite = iconCircle;
        background.raycastTarget = true;
        Button button = go.AddComponent<Button>();
        ImageAt(iconName, icon, go.transform, new Vector2(.23f, .23f), new Vector2(.77f, .77f), Color.white);
        return button;
    }

    private static void ToggleRowAt(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = PanelAt(name, parent, min, max, Color.white);
        go.GetComponent<Image>().raycastTarget = true;
        go.AddComponent<Button>();
        TextAt("AiLabel", "智能建议", go.transform, new Vector2(.04f, 0), new Vector2(.66f, 1), 27, TextAnchor.MiddleLeft, Ink);
        TextAt("AiButtonText", "关", go.transform, new Vector2(.66f, 0), new Vector2(.78f, 1), 23, TextAnchor.MiddleRight, MutedInk);
        GameObject track = PanelAt("AiToggleTrack", go.transform, new Vector2(.80f, .27f), new Vector2(.95f, .73f), new Color(.72f, .70f, .66f, 1f));
        Image knob = ImageAt("AiToggleKnob", circleSolid, track.transform, new Vector2(.04f, .10f), new Vector2(.44f, .90f), Color.white);
        knob.raycastTarget = false;
    }

    private static Image ImageAt(string name, Sprite source, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>(); image.sprite = source; image.color = color; image.preserveAspect = true; image.raycastTarget = false;
        return image;
    }

    private static Sprite LoadSprite(string name)
    {
        Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(UiKitPath + name + ".png");
        if (loaded == null) throw new System.InvalidOperationException("Missing CatLife UI kit sprite: " + name);
        return loaded;
    }

    private static Sprite LoadActionSprite(string name)
    {
        Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(ActionThumbPath + name + ".png");
        if (loaded == null) throw new System.InvalidOperationException("Missing CatLife action thumbnail: " + name);
        return loaded;
    }

    private static Sprite LoadArtSprite(string folder, string name)
    {
        Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(UiArtPath + folder + "/" + name + ".png");
        if (loaded == null) throw new System.InvalidOperationException("Missing CatLife UI art sprite: " + folder + "/" + name);
        return loaded;
    }

    private static void NormalizeActionThumbnailImports()
    {
        string[] names = { "T_CL_CatAction_Wave_512", "T_CL_CatAction_Tail_512", "T_CL_CatAction_Loaf_512", "T_CL_CatAction_Stretch_512" };
        foreach (string name in names)
        {
            string path = ActionThumbPath + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 512;
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 256;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }
    }

    private static void NormalizeUiKitImports()
    {
        string[] names = { "rounded_solid", "rounded_outline", "start_button", "circle_outline", "circle_solid", "dot", "icon_cat", "icon_record", "icon_settings", "icon_spark", "icon_paw", "icon_clock", "icon_leaf", "icon_moon" };
        foreach (string name in names)
        {
            string path = UiKitPath + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 512;
            android.format = TextureImporterFormat.Automatic;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }
    }
    private static InputField InputAt(string name, string placeholder, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = PanelAt(name, parent, min, max, Color.white); go.GetComponent<Image>().raycastTarget = true; InputField input = go.AddComponent<InputField>(); Text text = TextAt(name + "Text", "", go.transform, new Vector2(.05f, 0), new Vector2(.95f, 1), 28, TextAnchor.MiddleLeft, Ink); Text hint = TextAt(name + "Placeholder", placeholder, go.transform, new Vector2(.05f, 0), new Vector2(.95f, 1), 28, TextAnchor.MiddleLeft, new Color(.3f, .3f, .3f, .6f)); input.textComponent = text; input.placeholder = hint; input.contentType = InputField.ContentType.IntegerNumber; return input;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return; string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/"); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
