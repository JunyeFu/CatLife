using System.Collections;
using System.Linq;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CatLifeFigmaUiTests
{
    private const string Key = "CatLife.Mobile.Data.v1";
    private bool hadData;
    private string savedData;
    [SetUp] public void PreserveData() { hadData = PlayerPrefs.HasKey(Key); savedData = PlayerPrefs.GetString(Key); }
    [TearDown] public void RestoreData() { if (hadData) PlayerPrefs.SetString(Key, savedData); else PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    private static CatLifeAppData Seed(int records = 0)
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.aiEnabled = false;
        data.settings.aiConsent = false;
        data.settings.autoFocusAdaptationSeconds = 0;
        for (int i = 0; i < records; i++) data.records.Add(new CatLifeSessionRecord { completed = true, plannedSeconds = 60, actualSeconds = 60 });
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        return data;
    }
    private static Transform View(string name) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == name);
    private static void Click(string name) => View(name).GetComponent<Button>().onClick.Invoke();
    private static Component Presentation() => GameObject.Find("CatLifeMobileView").GetComponent("CatLifeMobileUiPresentation");
    private static T Field<T>(Component component, string name) => (T)component.GetType().GetField(name).GetValue(component);

    [UnityTest]
    public IEnumerator NeutralInkAndCenteredButtonsSurviveSetupSelection()
    {
        Seed(); yield return null;
        var ui = Presentation();
        Assert.That(ui, Is.Not.Null);
        Color ink = new Color32(32, 32, 30, 255), onDark = new Color32(252, 252, 251, 255);
        Assert.That(View("BrandTitle").GetComponent<Text>().color, Is.EqualTo(ink));
        foreach (string name in new[] { "StartButton", "SetupStart", "RewardAgain" })
        {
            var button = View(name).GetComponent<Button>();
            var text = button.GetComponentInChildren<Text>(true);
            Assert.That(button.spriteState.pressedSprite.name, Does.StartWith("S_CL_UI_Style_"));
            Assert.That(button.spriteState.disabledSprite.name, Does.StartWith("S_CL_UI_Style_"));
            Assert.That(text.fontSize, Is.EqualTo(name == "RewardAgain" ? 40 : 52));
            Assert.That(text.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(text.resizeTextForBestFit, Is.False);
            Assert.That(text.rectTransform.offsetMin, Is.EqualTo(-text.rectTransform.offsetMax));
        }
        Click("StartButton"); Click("Minutes45"); Click("ReminderGentleButton"); yield return null;
        Assert.That(View("Minutes45Text").GetComponent<Text>().color, Is.EqualTo(onDark));
        Assert.That(View("Minutes15Text").GetComponent<Text>().color, Is.EqualTo(ink));
        Assert.That(View("ReminderGentleButtonText").GetComponent<Text>().color, Is.EqualTo(ink));
        Assert.That(View("Minutes45").GetComponent<Image>().sprite.name, Does.EndWith("SkinChipSelected"));
        foreach (var text in ui.GetComponentsInChildren<Text>(true))
        {
            var c = text.color;
            Assert.That(c.g > c.r + .02f && c.g > c.b + .02f, Is.False, text.name);
            if (c.a > 0) Assert.That(c.r == 0 && c.g == 0 && c.b == 0, Is.False, text.name + " pure black");
            Assert.That(text.font.name, Does.Contain("NotoSansSC"), text.name);
        }
    }

    [UnityTest]
    public IEnumerator DarkFontSurvivesStartupAndHasCompleteEqualWidthDigits()
    {
        Seed(); yield return null;
        Font expected = Field<Font>(Presentation(), "darkBodyFont");
        Assert.That(expected, Is.Not.Null);
        foreach (string name in new[] { "FocusTitle", "TimerText", "TransitionText", "DebugText", "DebugTitle" })
            Assert.That(View(name).GetComponent<Text>().font, Is.SameAs(expected), name);
        expected.RequestCharactersInTexture("专注中0123456789:", 96, FontStyle.Normal);
        int? advance = null;
        foreach (char c in "0123456789")
        {
            Assert.That(expected.GetCharacterInfo(c, out CharacterInfo info, 96, FontStyle.Normal), Is.True, c.ToString());
            if (advance.HasValue) Assert.That(info.advance, Is.EqualTo(advance.Value));
            advance = info.advance;
        }
        foreach (char c in "专注中:") Assert.That(expected.HasCharacter(c), Is.True, c.ToString());
        for (int tap = 0; tap < 5; tap++) Click("TitleButton");
        foreach (char c in View("DebugText").GetComponent<Text>().text + View("TransitionText").GetComponent<Text>().text)
            if (!char.IsWhiteSpace(c)) Assert.That(expected.HasCharacter(c), Is.True, c.ToString());
        Click("DebugClose");
        Click("StartButton"); Click("Minutes45"); yield return null;
        Assert.That(View("TimerText").GetComponent<Text>().font, Is.SameAs(expected));
    }

    [UnityTest]
    public IEnumerator BothInputsShowPassiveInnerRingsOnlyWhileFocused()
    {
        Seed(); yield return null;
        foreach (string name in new[] { "CatNameInput", "CustomMinutes" })
        {
            if (name == "CatNameInput") View("AdoptionPanel").gameObject.SetActive(true);
            else { View("AdoptionPanel").gameObject.SetActive(false); Click("StartButton"); }
            var input = View(name).GetComponent<InputField>();
            var rings = input.transform.Find("StyleFocusRings").GetComponent<Image>();
            Assert.That(rings.raycastTarget, Is.False);
            Assert.That(input.transition, Is.EqualTo(Selectable.Transition.None));
            input.ActivateInputField(); yield return null; yield return null;
            Assert.That(input.isFocused, Is.True, name);
            Assert.That(rings.enabled, Is.True, name);
            input.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
            yield return null; yield return null;
            Assert.That(rings.enabled, Is.False, name);
        }
    }

    [UnityTest]
    public IEnumerator BothInputsAvoidTheKeyboardAndRestoreTheirCardsAtBothSizes()
    {
        foreach (Vector2Int size in new[] { new Vector2Int(1080, 2424), new Vector2Int(720, 1280) })
        {
            System.Type builder = System.Type.GetType("CatLifePreviewSceneBuilder, Assembly-CSharp-Editor");
            builder.GetMethod("SetGameViewFixedResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { size.x, size.y });
            Seed(); yield return null; yield return null;
            foreach (string name in new[] { "CatNameInput", "CustomMinutes" })
            {
                if (name == "CatNameInput") View("AdoptionPanel").gameObject.SetActive(true);
                else { View("AdoptionPanel").gameObject.SetActive(false); Click("StartButton"); }
                var field = View(name).GetComponent<InputField>();
                var card = (RectTransform)field.transform.parent; Vector2 resting = card.anchoredPosition;
                var avoidance = field.GetComponent("CatLifeKeyboardAvoidance");
                Assert.That(avoidance, Is.Not.Null);
                field.ActivateInputField(); yield return null; yield return null;
                Assert.That(field.isFocused, Is.True);
                foreach (float portion in new[] { .36f, .5f })
                {
                    float inset = Screen.height * portion;
                    avoidance.GetType().GetMethod("SetReviewInset").Invoke(avoidance, new object[] { inset });
                    yield return null; yield return null;
                    var corners = new Vector3[4]; ((RectTransform)field.transform).GetWorldCorners(corners);
                    Assert.That(RectTransformUtility.WorldToScreenPoint(null, corners[0]).y,
                        Is.GreaterThanOrEqualTo(inset + 16 * field.GetComponentInParent<Canvas>().scaleFactor - .5f), name);
                    Vector2 lifted = card.anchoredPosition;
                    for (int frame = 0; frame < 5; frame++) yield return null;
                    Assert.That(Vector2.Distance(card.anchoredPosition, lifted), Is.LessThan(.001f), "Avoidance must not accumulate every frame.");
                }
                field.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
                yield return null; yield return null;
                Assert.That(card.anchoredPosition, Is.EqualTo(resting), name + " normal layout restore");
            }
        }
    }

    [UnityTest]
    public IEnumerator SingleCharacterTailBalancesWithoutChangingOriginalTextOrExplicitBreaks()
    {
        Seed(); yield return null;
        Text label = Field<Text>(Presentation(), "rewardInsight");
        label.fontSize = 34;
        var generator = new TextGenerator();
        float width = generator.GetPreferredWidth("甲乙丙丁", label.GetGenerationSettings(Vector2.zero)) / label.pixelsPerUnit;
        label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + .1f);
        Canvas.ForceUpdateCanvases();
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var method = app.GetType().Assembly.GetType("CatLifeTextWrap").GetMethod("WithoutSingleCharacterTail");
        const string original = "甲乙丙丁戊";
        string display = (string)method.Invoke(null, new object[] { label, original });
        Assert.That(display, Is.EqualTo("甲乙丙\n丁戊"));
        Assert.That(display.Replace("\n", ""), Is.EqualTo(original));
        const string intentional = "甲乙丙丁\n戊";
        Assert.That((string)method.Invoke(null, new object[] { label, intentional }), Is.EqualTo(intentional));
    }

    [UnityTest]
    public IEnumerator ReapplyingStyleKeepsInkAndControlGeometry()
    {
        Seed(); yield return null;
        GameObject root = GameObject.Find("CatLifeMobileView");
        var buttons = root.GetComponentsInChildren<Button>(true);
        var rectangles = buttons.Select(b => (RectTransform)b.transform).ToArray();
        var before = rectangles.Select(r => new[] { r.anchorMin, r.anchorMax, r.anchoredPosition, r.sizeDelta }).ToArray();
        System.Type installer = System.Type.GetType("CatLifeUiStyleInstaller, Assembly-CSharp-Editor");
        var apply = installer.GetMethod("Apply");
        apply.Invoke(null, new object[] { root }); apply.Invoke(null, new object[] { root });
        Assert.That(View("BrandTitle").GetComponent<Text>().color, Is.EqualTo((Color)new Color32(32, 32, 30, 255)));
        Assert.That(root.GetComponentsInChildren<Button>(true).Length, Is.EqualTo(buttons.Length));
        for (int i = 0; i < rectangles.Length; i++)
        {
            var r = rectangles[i];
            CollectionAssert.AreEqual(before[i], new[] { r.anchorMin, r.anchorMax, r.anchoredPosition, r.sizeDelta }, r.name);
        }
    }

    [UnityTest]
    public IEnumerator SettingsPrivacyAndSwitchesPreserveConsentAndRecords()
    {
        Seed(1); yield return null; Click("SettingsButton"); yield return null;
        var ui = Presentation();
        Assert.That(Field<Text>(ui, "recordsCountValue").text, Does.StartWith("1 条"));
        Click("SettingsAi");
        Assert.That(View("AiConsentPanel").gameObject.activeSelf, Is.True);
        Click("AiConsentCancel");
        Click("SettingsPrivacyDetails");
        Assert.That(View("FigmaPrivacyPanel").gameObject.activeSelf, Is.True);
        Click("FigmaPrivacyClose");
        Click("SettingsBehavior");
        var saved = CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key));
        Assert.That(saved.settings.aiEnabled, Is.False);
        Assert.That(saved.settings.aiConsent, Is.False);
        Assert.That(saved.records.Count, Is.EqualTo(1));
        Assert.That(Field<Image>(ui, "settingsBehaviorToggle").sprite.name, Does.EndWith("ToggleOff"));
        Click("SettingsDuration");
        Assert.That(Field<Text>(ui, "durationValue").text, Is.EqualTo("45 分钟"));
        Click("SettingsClear");
        Assert.That(View("SettingsClearText").GetComponent<Text>().text, Does.Contain("再次"));
        Click("SettingsBack"); Click("SettingsButton");
        Assert.That(View("SettingsClearText").GetComponent<Text>().text, Is.EqualTo("清除本地数据"));
        Assert.That(CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key)).records.Count, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator RewardSeparatesRealAggregatesFromScrollableAdvice()
    {
        Seed(); yield return null;
        View("RewardPanel").gameObject.SetActive(true);
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var record = new CatLifeSessionRecord { completed = true, plannedSeconds = 60, actualSeconds = 60, behaviorStatsDisabled = true, aiRequestAttempted = true, aiRequestCount = 1, aiRequestStatus = "succeeded", aiSource = "mimo_cloud", aiGentleReminder = "这一段已完成", aiReturnSuggestion = string.Concat(Enumerable.Repeat("先完成一个小步骤。", 30)), aiReaction = "paw_wave" };
        app.GetType().GetMethod("RenderReward", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, new object[] { record });
        yield return null;
        var ui = Presentation();
        Assert.That(Field<Text>(ui, "rewardDuration").text, Is.EqualTo("1 分钟"));
        Assert.That(Field<Text>(ui, "rewardBehavior").text, Is.EqualTo("行为统计未采集"));
        Assert.That(Field<Text>(ui, "rewardGrowth").text, Does.Contain("成长 +1"));
        Assert.That(Field<Text>(ui, "rewardInsight").text, Does.Contain("MiMo 生成").And.Contain("小步骤"));
        Assert.That(Field<RectTransform>(ui, "rewardContent").rect.height, Is.GreaterThan(Field<RectTransform>(ui, "rewardViewport").rect.height));
        Assert.That(Field<Text>(ui, "rewardScrollHint").gameObject.activeSelf, Is.True);
        var scroll = Field<ScrollRect>(ui, "rewardScroll");
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(null, Field<RectTransform>(ui, "rewardViewport").position) };
        scroll.OnBeginDrag(pointer);
        pointer.position += Vector2.up * 100; pointer.delta = Vector2.up * 100;
        scroll.OnDrag(pointer); scroll.OnEndDrag(pointer);
        Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1), "The full advice must be reachable by dragging.");
        Assert.That(record.aiRequestCount, Is.EqualTo(1));
        Assert.That(record.PawsAwarded, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator RewardAggregatesFitActualFontAndGenerateVisibleGlyphs()
    {
        Seed(); yield return null;
        // Measure the serialized layout in its approved portrait reference frame, not the batch Editor's landscape Game view.
        Canvas canvas = GameObject.Find("CatLifeMobileView").GetComponent<Canvas>();
        canvas.GetComponent<CanvasScaler>().enabled = false;
        canvas.renderMode = RenderMode.WorldSpace;
        var frame = (RectTransform)canvas.transform;
        frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1080);
        frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2424);
        View("RewardPanel").gameObject.SetActive(true);
        View("RewardCard").gameObject.SetActive(true);
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var record = new CatLifeSessionRecord { completed = true, plannedSeconds = 60, actualSeconds = 60 };
        app.GetType().GetMethod("RenderReward", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, new object[] { record });
        yield return null; Canvas.ForceUpdateCanvases();
        foreach (string field in new[] { "rewardDuration", "rewardBehavior", "rewardGrowth", "rewardPaws" })
        {
            Text text = Field<Text>(Presentation(), field);
            text.cachedTextGenerator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size));
            Debug.Log($"GAP20_FONT {field} rect={text.rectTransform.rect.height} preferred={text.preferredHeight} vertices={text.cachedTextGenerator.vertexCount}");
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .1f), field);
            Assert.That(text.cachedTextGenerator.vertexCount, Is.GreaterThan(4), field);
        }
    }

    [UnityTest]
    public IEnumerator RewardWorldExtendsUnderActualSheetEdge()
    {
        Seed(); yield return null;
        View("RewardPanel").gameObject.SetActive(true); View("RewardCard").gameObject.SetActive(true);
        var director = Camera.main.GetComponent("CatLifeCameraDirector");
        director.GetType().GetMethod("Show").Invoke(director, new object[] { CatLifeSessionPhase.Reward, true });
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4]; ((RectTransform)View("RewardCard")).GetWorldCorners(corners);
        Assert.That(Camera.main.pixelRect.y, Is.LessThanOrEqualTo(corners[1].y - 24f * Screen.height / 2424f),
            "The world render ends above the sheet edge, exposing a black seam.");
    }

    [UnityTest]
    public IEnumerator PageLabelsFitShorterPortraitWithoutLosingGlyphs()
    {
        Seed(); yield return null;
        var ui = Presentation();
        ui.GetComponent<CanvasScaler>().enabled = false;
        ui.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)ui.transform).sizeDelta = new Vector2(1080, 1920);
        foreach (string name in new[] { "HomeHudLayer", "SetupPanel", "SetupCard", "FocusPanel", "RewardPanel", "RewardCard" })
            View(name).gameObject.SetActive(true);
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var record = new CatLifeSessionRecord { completed = true, plannedSeconds = 60, actualSeconds = 60, longestStableSeconds = 60 };
        app.GetType().GetMethod("RenderReward", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, new object[] { record });
        Canvas.ForceUpdateCanvases();
        var labels = new[] { "BrandTitle", "FigmaSetupBrand", "SetupTitle", "SelectedMinutes", "FocusTitle", "TimerText", "RewardTitle", "RewardDurationText", "RewardBehaviorText", "RewardGrowthText", "RewardPawsText", "RewardScrollHint", "RewardHomeText", "RewardAgainText" };
        foreach (string name in labels)
        {
            var text = View(name).GetComponent<Text>();
            text.cachedTextGenerator.Populate(text.text, text.GetGenerationSettings(text.rectTransform.rect.size));
            Debug.Log($"GAP20_SHORT_FONT {name} rect={text.rectTransform.rect.height} preferred={text.preferredHeight} vertices={text.cachedTextGenerator.vertexCount}");
        }
        foreach (string name in labels)
        {
            var text = View(name).GetComponent<Text>();
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + .1f), name);
            Assert.That(text.cachedTextGenerator.vertexCount, Is.GreaterThan(4), name);
        }
    }

    [UnityTest]
    public IEnumerator RecordRowsAndGrowthProgressUseExistingData()
    {
        Seed(3); yield return null; Click("RecordsButton"); yield return null;
        var ui = Presentation();
        Assert.That(Field<GameObject[]>(ui, "recordCards").Count(go => go.activeSelf), Is.EqualTo(3));
        Assert.That(Field<Text[]>(ui, "recordRows")[0].text, Does.Contain("完成"));
        Click("RecordsBack"); Click("GrowthButton"); yield return null;
        Assert.That(Field<Image>(ui, "growthProgress").fillAmount, Is.EqualTo(.03f).Within(.0001f));
        Assert.That(View("GrowthActionTailText").GetComponent<Text>().text, Does.Contain("已解锁"));
        Assert.That(View("GrowthActionStretchText").GetComponent<Text>().text, Does.Contain("8 枚"));
        Assert.That(View("GrowthActionWaveIcon").GetComponent<Image>().sprite.name, Is.EqualTo("T_CL_CatAction_Wave_512"));
    }

    [UnityTest]
    public IEnumerator SerializedPagesRetainSafeAreaAndNonInteractiveDecorations()
    {
        Seed(); yield return null;
        var ui = Presentation();
        Assert.That(ui.GetComponent<CanvasScaler>().referenceResolution, Is.EqualTo(new Vector2(1080, 2400)));
        Assert.That(ui.GetComponentsInChildren<MonoBehaviour>().Any(c => c != null && c.GetType().Name == "CatLifeSafeArea"), Is.True);
        foreach (var image in ui.GetComponentsInChildren<Image>(true).Where(i => i.name.Contains("SoftShadow") || i.name.Contains("ActionHalo") || i.name.Contains("FigmaToggle")))
            Assert.That(image.raycastTarget, Is.False, image.name);
        foreach (string page in new[] { "Records", "Growth", "Settings" })
        {
            var banner = View(page + "SceneBanner");
            Assert.That(banner.GetComponent<Image>().sprite.name, Is.EqualTo("T_CL_UI_" + page + "Runtime_1080x460"));
            Assert.That(banner.GetComponent<AspectRatioFitter>().aspectMode, Is.EqualTo(AspectRatioFitter.AspectMode.EnvelopeParent));
            Assert.That(banner.parent.GetComponent<RectMask2D>(), Is.Not.Null);
        }
        Click("SettingsButton"); yield return null;
        var scroll = View("SettingsSheet").GetComponent<ScrollRect>();
        Assert.That(scroll.content.rect.height, Is.GreaterThanOrEqualTo(scroll.viewport.rect.height));
        Assert.That(View("CatLifeMobileTown").GetComponentsInChildren<MeshRenderer>().Length, Is.GreaterThan(10));
    }
}
