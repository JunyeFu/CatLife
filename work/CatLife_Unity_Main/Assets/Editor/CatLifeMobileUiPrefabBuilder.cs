using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CatLifeMobileUiPrefabBuilder
{
    public const string PrefabPath = "Assets/MobileRuntime/UI/PF_CL_UI_Mobile.prefab";
    private static readonly Color Cream = new Color(1f, .965f, .86f, .98f);
    private static readonly Color Orange = new Color(.96f, .49f, .08f, 1f);
    private static readonly Color Green = new Color(.17f, .38f, .20f, 1f);
    private static readonly Color Sky = new Color(.30f, .66f, .82f, 1f);
    private static readonly Color Alert = new Color(.72f, .20f, .13f, 1f);
    private static readonly Color Ink = new Color(.16f, .20f, .13f, 1f);
    private static Sprite sprite;
    private static Font font;

    public static GameObject Build()
    {
        EnsureFolder("Assets/MobileRuntime/UI");
        sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject canvas = new GameObject("CatLifeMobileView", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 2400); scaler.matchWidthOrHeight = .5f;
        GameObject safe = Panel("SafeArea", canvas.transform, Color.clear); safe.AddComponent<CatLifeSafeArea>();
        Transform root = safe.transform;

        GameObject home = Panel("HomeHudLayer", root, Color.clear);
        ButtonAt("TitleButton", "CatLife", home.transform, new Vector2(.06f, .91f), new Vector2(.52f, .98f), Green, 58);
        TextAt("TodayText", "今日专注 0 分钟", home.transform, new Vector2(.06f, .85f), new Vector2(.72f, .91f), 32, TextAnchor.MiddleLeft, Color.white);
        ButtonAt("GrowthButton", "成长", home.transform, new Vector2(.79f, .64f), new Vector2(.95f, .70f), Green, 30);
        ButtonAt("RecordsButton", "记录", home.transform, new Vector2(.79f, .55f), new Vector2(.95f, .61f), Orange, 30);
        ButtonAt("SettingsButton", "设置", home.transform, new Vector2(.79f, .46f), new Vector2(.95f, .52f), Sky, 30);
        ButtonAt("StartButton", "开始专注", home.transform, new Vector2(.15f, .05f), new Vector2(.85f, .12f), Orange, 42);
        TextAt("BubbleText", "先不用急，我在这里。", home.transform, new Vector2(.15f, .25f), new Vector2(.55f, .33f), 30, TextAnchor.MiddleCenter, Ink, Cream);

        GameObject session = Panel("SessionLayer", root, Color.clear);
        GameObject setup = Panel("SetupPanel", session.transform, new Color(0, 0, 0, .12f));
        GameObject setupCard = PanelAt("SetupCard", setup.transform, new Vector2(.04f, .02f), new Vector2(.96f, .48f), Cream);
        TextAt("SetupTitle", "准备专注", setupCard.transform, new Vector2(.08f, .82f), new Vector2(.92f, .96f), 44, TextAnchor.MiddleCenter, Ink);
        TextAt("SelectedMinutes", "本次 25 分钟", setupCard.transform, new Vector2(.18f, .70f), new Vector2(.82f, .82f), 34, TextAnchor.MiddleCenter, Green);
        ButtonAt("Minutes15", "15 分钟", setupCard.transform, new Vector2(.06f, .55f), new Vector2(.30f, .68f), Green, 28);
        ButtonAt("Minutes25", "25 分钟", setupCard.transform, new Vector2(.38f, .55f), new Vector2(.62f, .68f), Orange, 28);
        ButtonAt("Minutes45", "45 分钟", setupCard.transform, new Vector2(.70f, .55f), new Vector2(.94f, .68f), Green, 28);
        InputAt("CustomMinutes", "1-180", setupCard.transform, new Vector2(.06f, .40f), new Vector2(.58f, .51f));
        ButtonAt("CustomApply", "使用自定义", setupCard.transform, new Vector2(.62f, .40f), new Vector2(.94f, .51f), Sky, 26);
        ButtonAt("ReminderButton", "安静陪伴", setupCard.transform, new Vector2(.06f, .25f), new Vector2(.46f, .36f), Green, 26, "ReminderButtonText");
        ButtonAt("AiButton", "AI 建议：关", setupCard.transform, new Vector2(.54f, .25f), new Vector2(.94f, .36f), Sky, 26, "AiButtonText");
        ButtonAt("SetupCancel", "取消", setupCard.transform, new Vector2(.06f, .07f), new Vector2(.46f, .19f), Green, 30);
        ButtonAt("SetupStart", "开始", setupCard.transform, new Vector2(.54f, .07f), new Vector2(.94f, .19f), Orange, 30);

        GameObject transition = Panel("TransitionPanel", session.transform, new Color(.08f, .16f, .14f, .20f));
        TextAt("TransitionText", "慢慢趴好，准备开始……", transition.transform, new Vector2(.12f, .74f), new Vector2(.88f, .82f), 38, TextAnchor.MiddleCenter, Color.white);
        ButtonAt("AutoFocusCancel", "暂不进入", transition.transform, new Vector2(.30f, .08f), new Vector2(.70f, .14f), Green, 28);
        GameObject focus = Panel("FocusPanel", session.transform, new Color(.04f, .12f, .13f, .20f));
        TextAt("FocusTitle", "专注中", focus.transform, new Vector2(.20f, .88f), new Vector2(.80f, .94f), 38, TextAnchor.MiddleCenter, Color.white);
        TextAt("TimerText", "25:00", focus.transform, new Vector2(.12f, .75f), new Vector2(.88f, .88f), 106, TextAnchor.MiddleCenter, Color.white);
        GameObject swipe = PanelAt("SwipeTrack", focus.transform, new Vector2(.12f, .05f), new Vector2(.88f, .11f), new Color(.25f, .20f, .13f, .88f));
        swipe.GetComponent<Image>().raycastTarget = true;
        CatLifeSwipeToEnd swipeControl = swipe.AddComponent<CatLifeSwipeToEnd>();
        Image fill = Panel("SwipeFill", swipe.transform, Orange).GetComponent<Image>(); fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = 0;
        RectTransform handle = PanelAt("SwipeHandle", swipe.transform, new Vector2(0, .05f), new Vector2(.12f, .95f), Cream).GetComponent<RectTransform>();
        TextAt("SwipeLabel", "向右滑动结束", swipe.transform, new Vector2(.15f, 0), new Vector2(.85f, 1), 28, TextAnchor.MiddleCenter, Color.white);
        swipeControl.Configure(handle, fill);

        GameObject reward = Panel("RewardPanel", session.transform, new Color(0, 0, 0, .12f));
        GameObject rewardCard = PanelAt("RewardCard", reward.transform, new Vector2(.05f, .08f), new Vector2(.95f, .55f), Cream);
        TextAt("RewardTitle", "专注完成", rewardCard.transform, new Vector2(.1f, .82f), new Vector2(.9f, .96f), 46, TextAnchor.MiddleCenter, Orange);
        TextAt("RewardText", "", rewardCard.transform, new Vector2(.08f, .25f), new Vector2(.92f, .82f), 31, TextAnchor.UpperCenter, Ink);
        ButtonAt("RewardHome", "返回小镇", rewardCard.transform, new Vector2(.06f, .06f), new Vector2(.46f, .20f), Green, 29);
        ButtonAt("RewardAgain", "再来一次", rewardCard.transform, new Vector2(.54f, .06f), new Vector2(.94f, .20f), Orange, 29);

        GameObject pages = Panel("PageLayer", root, Color.clear);
        Page(pages.transform, "RecordsPanel", "专注记录", "RecordsText", "RecordsBack");
        Page(pages.transform, "GrowthPanel", "猫咪成长", "GrowthText", "GrowthBack");
        GameObject settings = Page(pages.transform, "SettingsPanel", "设置与隐私", "SettingsText", "SettingsBack");
        ButtonAt("SettingsDuration", "切换默认时长", settings.transform, new Vector2(.08f, .28f), new Vector2(.48f, .34f), Green, 25);
        ButtonAt("SettingsReminder", "切换提醒", settings.transform, new Vector2(.52f, .28f), new Vector2(.92f, .34f), Sky, 25);
        ButtonAt("SettingsBehavior", "行为统计开关", settings.transform, new Vector2(.08f, .20f), new Vector2(.48f, .26f), Green, 25);
        ButtonAt("SettingsAi", "AI 开关", settings.transform, new Vector2(.52f, .20f), new Vector2(.92f, .26f), Orange, 25);
        ButtonAt("SettingsAutoFocus", "自动专注时长", settings.transform, new Vector2(.08f, .12f), new Vector2(.48f, .18f), Sky, 23);
        ButtonAt("SettingsClear", "清除本地数据", settings.transform, new Vector2(.52f, .12f), new Vector2(.92f, .18f), Alert, 23, "SettingsClearText");

        GameObject transient = Panel("TransientLayer", root, Color.clear);
        ConfirmPanel(transient.transform, "ExitConfirm", "确认结束本次专注？\n中断会保留记录，但成长和爪印为 0。", "InterruptConfirm", "确认结束", "InterruptCancel", "继续专注");
        ConfirmPanel(transient.transform, "AiConsentPanel", "AI 只接收本次时长、触控、后台次数和稳定度等聚合值；不会发送文字、轨迹、屏幕或其他应用内容。", "AiConsentAccept", "同意并开启", "AiConsentCancel", "取消");
        GameObject debug = PanelAt("DebugPanel", transient.transform, new Vector2(.08f, .20f), new Vector2(.92f, .80f), new Color(.08f, .12f, .10f, .98f));
        TextAt("DebugTitle", "评审信息", debug.transform, new Vector2(.1f, .82f), new Vector2(.9f, .95f), 42, TextAnchor.MiddleCenter, Cream);
        TextAt("DebugText", "", debug.transform, new Vector2(.1f, .34f), new Vector2(.9f, .80f), 30, TextAnchor.UpperLeft, Color.white);
        ButtonAt("ReviewerMinute", "开始 1 分钟评审会话", debug.transform, new Vector2(.10f, .17f), new Vector2(.90f, .29f), Orange, 28);
        ButtonAt("DebugClose", "关闭", debug.transform, new Vector2(.25f, .04f), new Vector2(.75f, .14f), Green, 27);

        setup.SetActive(false); transition.SetActive(false); focus.SetActive(false); reward.SetActive(false);
        foreach (Transform child in pages.transform) child.gameObject.SetActive(false);
        foreach (Transform child in transient.transform) child.gameObject.SetActive(false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvas, PrefabPath);
        Object.DestroyImmediate(canvas);
        return prefab;
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
        Text text = go.GetComponent<Text>(); text.text = value; text.font = font; text.fontSize = size; text.alignment = anchor; text.color = color; text.resizeTextForBestFit = true; text.resizeTextMinSize = 18; text.raycastTarget = false; return text;
    }
    private static Button ButtonAt(string name, string value, Transform parent, Vector2 min, Vector2 max, Color color, int size, string textName = null)
    {
        GameObject go = PanelAt(name, parent, min, max, color); go.GetComponent<Image>().raycastTarget = true; Button button = go.AddComponent<Button>(); TextAt(textName ?? name + "Text", value, go.transform, Vector2.zero, Vector2.one, size, TextAnchor.MiddleCenter, Color.white); return button;
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
