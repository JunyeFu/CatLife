using System.Collections;
using System.Linq;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
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
    public IEnumerator BlackTextAndCenteredButtonsSurviveSetupSelection()
    {
        Seed(); yield return null;
        var ui = Presentation();
        Assert.That(ui, Is.Not.Null);
        Assert.That(View("BrandTitle").GetComponent<Text>().color, Is.EqualTo(Color.black));
        foreach (string name in new[] { "StartButton", "SetupStart", "RewardAgain" })
        {
            var text = View(name).GetComponentInChildren<Text>(true);
            Assert.That(text.fontSize, Is.EqualTo(52));
            Assert.That(text.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(text.resizeTextForBestFit, Is.False);
            Assert.That(text.rectTransform.offsetMin, Is.EqualTo(-text.rectTransform.offsetMax));
        }
        Click("StartButton"); Click("Minutes45"); Click("ReminderGentleButton"); yield return null;
        Assert.That(View("Minutes45Text").GetComponent<Text>().color, Is.EqualTo(Color.white));
        Assert.That(View("Minutes15Text").GetComponent<Text>().color, Is.EqualTo(Color.black));
        Assert.That(View("ReminderGentleButtonText").GetComponent<Text>().color, Is.EqualTo(Color.black));
        Assert.That(View("Minutes45").GetComponent<Image>().sprite.name, Does.EndWith("SkinChipSelected"));
        foreach (var text in ui.GetComponentsInChildren<Text>(true))
        {
            var c = text.color;
            Assert.That(c.g > c.r + .02f && c.g > c.b + .02f, Is.False, text.name);
            Assert.That(text.font.name, Does.Contain("NotoSansSC"), text.name);
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
        Assert.That(record.aiRequestCount, Is.EqualTo(1));
        Assert.That(record.PawsAwarded, Is.EqualTo(1));
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
