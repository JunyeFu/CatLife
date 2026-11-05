using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CatLifeProductGapTests
{
    [UnityTest] public IEnumerator GrowthUsesApprovedTailNameWithoutChangingUnlocks()
    {
        var data=new CatLifeAppData(); data.settings.catAdopted=true;
        data.settings.aiEnabled=false; data.settings.localBehaviorStatsEnabled=false;
        PlayerPrefs.SetString(Key,CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        Click("GrowthButton");
        Assert.That(View("GrowthActionTailText").GetComponent<Text>().text,
            Is.EqualTo("轻摆尾\n3 枚爪印解锁"));
        Assert.That(CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key)).records.Count,Is.Zero);
    }

    [UnityTest] public IEnumerator LeavingSettingsCancelsPendingDataClear()
    {
        var data = new CatLifeAppData(); data.settings.catAdopted = true;
        data.records.Add(new CatLifeSessionRecord {completed=true,actualSeconds=60,plannedSeconds=60});
        PlayerPrefs.SetString(Key,CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        Click("SettingsButton"); Click("SettingsClear");
        Assert.That(View("SettingsClearText").GetComponent<Text>().text,Does.Contain("再次"));
        Click("SettingsBack"); Click("SettingsButton");
        Assert.That(View("SettingsClearText").GetComponent<Text>().text,Is.EqualTo("清除本地数据"));
        Assert.That(CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key)).records.Count,Is.EqualTo(1));
    }

    [UnityTest] public IEnumerator AiWithoutConsentOrWithAiDisabledDoesNotAttemptRequest()
    {
        var initial = new CatLifeAppData(); initial.settings.catAdopted = true;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(initial));
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var data = (CatLifeAppData)app.GetType().GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
        var request = app.GetType().GetMethod("RequestAiOnce", BindingFlags.Instance | BindingFlags.NonPublic);
        var record = new CatLifeSessionRecord { completed = true, actualSeconds = 60, plannedSeconds = 60 };
        data.settings.aiEnabled = true; data.settings.aiConsent = false;
        request.Invoke(app, new object[] { record });
        Assert.That(record.aiRequestAttempted, Is.False);
        data.settings.aiConsent = true; data.settings.aiEnabled = false;
        request.Invoke(app, new object[] { record });
        Assert.That(record.aiRequestCount, Is.Zero);
        data.settings.aiEnabled = true; record.completed = false;
        request.Invoke(app, new object[] { record });
        Assert.That(record.aiRequestCount, Is.Zero, "Interrupted sessions must not call the model.");
    }
    [Test] public void EmptyOrUnsafeAiOutputCannotBecomeExecutableSuggestion()
    {
        var type = Type.GetType("CatLife.LLM.MockCatLLMClient, Assembly-CSharp");
        var parse = type.GetMethod("TryParseGenericCloudSuggestion");
        foreach (string content in new[] { "", "{\"transformCommandIncluded\":true}", "{\"privacyInferenceIncluded\":true}" })
        {
            object[] values = { content, null, null };
            Assert.That((bool)parse.Invoke(null, values), Is.False, content);
        }
        object[] incomplete = { "{}", null, null };
        parse.Invoke(null, incomplete);
        Assert.That((string)incomplete[1].GetType().GetField("returnSuggestion").GetValue(incomplete[1]), Is.Null.Or.Empty,
            "An incomplete response cannot satisfy the reward's three-field success requirement.");
    }
    [UnityTest]
    public IEnumerator RestoredFocusInitializesProneAnimationAfterAllComponentsAwake()
    {
        var data = new CatLifeAppData(); data.settings.catAdopted = true;
        var controller = new CatLifeSessionController(data);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        controller.BeginTransition(60, now); controller.EnterFocus(now);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(.5f);
        var presenter = UnityEngine.Object.FindFirstObjectByType(Type.GetType("CatLifeMobileCatPresenter, Assembly-CSharp")) as Component;
        var animator = presenter.GetComponentInChildren<Animator>();
        Assert.That(View("FocusPanel").activeSelf, Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_ProneBreath_v11_loop_240f"), Is.True);
    }
    [UnityTest]
    public IEnumerator ExitHandleIsVisibleAndRequiresActualDrag()
    {
        var data = new CatLifeAppData(); data.settings.catAdopted = true;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        View("FocusPanel").SetActive(true); yield return null;
        var rect = View("SwipeTrack").GetComponent<RectTransform>();
        var swipe = rect.GetComponent("CatLifeSwipeToEnd");
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.width * .40f, 0, 0)));
        swipe.GetType().GetMethod("OnPointerDown").Invoke(swipe, new object[] { pointer });
        swipe.GetType().GetMethod("OnPointerUp").Invoke(swipe, new object[] { pointer });
        Assert.That(View("ExitConfirm").activeSelf, Is.False, "A tap at the right edge must not count as a swipe");
        Assert.That(View("SwipeHandle").GetComponent<RectTransform>().rect.width, Is.EqualTo(128));
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.width * -.40f, 0, 0)));
        swipe.GetType().GetMethod("OnPointerDown").Invoke(swipe, new object[] { pointer });
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.width * .30f, 0, 0)));
        swipe.GetType().GetMethod("OnDrag").Invoke(swipe, new object[] { pointer });
        swipe.GetType().GetMethod("OnPointerUp").Invoke(swipe, new object[] { pointer });
        Assert.That(View("ExitConfirm").activeSelf, Is.True);
    }
    [TestCase(1080, 2424)]
    [TestCase(720, 1280)]
    public void RoundedSheetRendersWorldBehindItsEdgeWithoutMovingVisibleWorld(int width, int height)
    {
        var go = new GameObject("SheetEdgeTest", typeof(Camera));
        var referenceGo = new GameObject("OriginalSheetFraming", typeof(Camera));
        var texture = new RenderTexture(width, height, 0);
        var camera = go.GetComponent<Camera>();
        var reference = referenceGo.GetComponent<Camera>();
        camera.targetTexture = reference.targetTexture = texture;
        Type type = Type.GetType("CatLifeCameraDirector, Assembly-CSharp");
        var director = go.AddComponent(type);
        type.GetMethod("Configure").Invoke(director, new object[] { camera });
        type.GetMethod("Show").Invoke(director, new object[] { CatLifeSessionPhase.Reward, true });
        reference.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
        reference.fieldOfView = 54f;
        reference.rect = new Rect(0, .48f, 1, .52f);
        Assert.That(camera.rect.yMin, Is.LessThan(.48f - 36f / 2400f),
            "The 36-unit rounded backing needs world pixels behind its transparent edge.");
        foreach (var point in new[] { new Vector3(.15f,.15f,4), new Vector3(.5f,.5f,6), new Vector3(.85f,.85f,8) })
        {
            Vector3 world = reference.ViewportToWorldPoint(point);
            Assert.That(Vector2.Distance(camera.WorldToScreenPoint(world), reference.WorldToScreenPoint(world)),
                Is.LessThan(1f), $"Visible framing must stay within one physical pixel. actual={camera.WorldToScreenPoint(world):F5}, expected={reference.WorldToScreenPoint(world):F5}");
        }
        type.GetMethod("Show").Invoke(director, new object[] { CatLifeSessionPhase.Normal, true });
        reference.transform.SetPositionAndRotation(go.transform.position, go.transform.rotation);
        reference.fieldOfView = 74f; reference.rect = new Rect(0,0,1,1);
        Vector3 homePoint = reference.ViewportToWorldPoint(new Vector3(.25f,.7f,5));
        Assert.That(Vector2.Distance(camera.WorldToScreenPoint(homePoint), reference.WorldToScreenPoint(homePoint)), Is.LessThan(.05f));
        UnityEngine.Object.DestroyImmediate(go);
        UnityEngine.Object.DestroyImmediate(referenceGo);
        UnityEngine.Object.DestroyImmediate(texture);
    }
    [Test]
    public void RewardFramingReservesVisibleWorldAndHomeRestoresFullViewport()
    {
        var go = new GameObject("FramingTest", typeof(Camera));
        var camera = go.GetComponent<Camera>(); Color day = camera.backgroundColor;
        Type type = Type.GetType("CatLifeCameraDirector, Assembly-CSharp");
        var director = go.AddComponent(type);
        type.GetMethod("Configure").Invoke(director, new object[] { camera });
        type.GetMethod("Show").Invoke(director, new object[] { CatLifeSessionPhase.Reward, true });
        Assert.That(camera.rect.yMin, Is.InRange(.45f, .48f));
        Assert.That(camera.rect.yMax, Is.EqualTo(1f).Within(.001f));
        type.GetMethod("Show").Invoke(director, new object[] { CatLifeSessionPhase.Focus, true });
        Assert.That(camera.backgroundColor, Is.Not.EqualTo(day));
        type.GetMethod("Show").Invoke(director, new object[] { CatLifeSessionPhase.Normal, true });
        Assert.That(camera.rect, Is.EqualTo(new Rect(0, 0, 1, 1)));
        Assert.That(camera.backgroundColor, Is.EqualTo(day));
        UnityEngine.Object.DestroyImmediate(go);
    }
    [Test]
    public void DisablingBehaviorStatsStopsCollectionAndClearsExistingWindow()
    {
        var go = new GameObject("StatsOptOutTest");
        Type type = Type.GetType("CatLife.Recognition.RealtimeFeatureEngine, Assembly-CSharp");
        Component engine = go.AddComponent(type);
        type.GetMethod("RecordUiTap").Invoke(engine, new object[] { "first" });
        type.GetMethod("SetCollectionEnabled").Invoke(engine, new object[] { false });
        type.GetMethod("RecordUiTap").Invoke(engine, new object[] { "should_not_collect" });
        type.GetMethod("Tick").Invoke(engine, new object[] { 10f });
        Assert.That(type.GetProperty("LastAcceptedBehaviorEvent").GetValue(engine), Is.EqualTo("disabled"));
        object snapshot = type.GetProperty("Latest").GetValue(engine);
        Assert.That(snapshot.GetType().GetField("tapRate1s").GetValue(snapshot), Is.EqualTo(0f));
        UnityEngine.Object.DestroyImmediate(go);
    }
    private const string Key = "CatLife.Mobile.Data.v1";
    private string saved;
    [SetUp] public void SavePreferences() { saved = PlayerPrefs.GetString(Key, ""); }
    [TearDown] public void RestorePreferences() { if (saved.Length == 0) PlayerPrefs.DeleteKey(Key); else PlayerPrefs.SetString(Key, saved); }
    private static GameObject View(string name) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == name).gameObject;
    private static void Click(string name) => View(name).GetComponent<Button>().onClick.Invoke();

    [UnityTest]
    public IEnumerator AdoptionPersistsWithoutErasingRecordsAndUnlockedCardsFollowPaws()
    {
        var data = new CatLifeAppData();
        for (int i = 0; i < 3; i++) data.records.Add(new CatLifeSessionRecord { completed = true, plannedSeconds = 60, actualSeconds = 60, endedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        Assert.That(View("AdoptionPanel").activeSelf, Is.True);
        Assert.That(Camera.main.backgroundColor, Is.EqualTo(new Color(.35f, .75f, .95f, 1f)), "Home sky must not depend on Awake ordering");
        View("CatNameInput").GetComponent<InputField>().text = "团子";
        Click("AdoptionDuration"); Click("AdoptionConfirm");
        data = CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key));
        Assert.That(data.settings.catAdopted, Is.True);
        Assert.That(data.settings.catName, Is.EqualTo("团子"));
        Assert.That(data.records.Count, Is.EqualTo(3));
        Assert.That(data.settings.defaultMinutes, Is.EqualTo(45));
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        Assert.That(View("AdoptionPanel").activeSelf, Is.False);
        Click("GrowthButton");
        Assert.That(View("GrowthText").GetComponent<Text>().text, Does.Contain("团子").And.Contain("爪印 3"));
        Assert.That(View("GrowthActionTailText").GetComponent<Text>().text, Does.Contain("已解锁"));
        Assert.That(View("GrowthActionStretchText").GetComponent<Text>().text, Does.Contain("8 枚"));
        Click("GrowthBack"); Click("RecordsButton");
        Assert.That(View("DayLabel6").GetComponent<Text>().text, Is.EqualTo(DateTime.Now.ToString("M/d")));
        Assert.That(View("DayLabel0").GetComponent<Text>().text, Is.EqualTo(DateTime.Now.AddDays(-6).ToString("M/d")));
        Assert.That(View("DayBar0").activeSelf, Is.False, "Zero-minute days must not draw positive bars");
        Assert.That(View("DayBar6").GetComponent<RectTransform>().anchorMax.y, Is.EqualTo(.24f + .48f * 3f / 45f).Within(.001f));
        Assert.That(View("ChartAxis3").GetComponent<Text>().text, Is.EqualTo("45 分"));
    }

    [UnityTest]
    public IEnumerator AllLandmarksHaveConsumersAndFocusRejectsPageEntry()
    {
        var data = new CatLifeAppData(); data.settings.catAdopted = true;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        var app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        Type action = Type.GetType("CatLifeLandmarkAction, Assembly-CSharp");
        Type landmark = Type.GetType("CatLifeLandmark, Assembly-CSharp");
        var components = UnityEngine.Object.FindObjectsByType(landmark, FindObjectsSortMode.None);
        foreach (string name in new[] { "Records", "Growth", "FishShop", "RewardTree", "Plaza" })
            Assert.That(components.Any(c => landmark.GetField("action").GetValue(c).ToString() == name), Is.True, name);
        var open = app.GetType().GetMethod("OpenLandmark");
        open.Invoke(app, new[] { Enum.Parse(action, "Records") });
        Assert.That(View("RecordsPanel").activeSelf, Is.True);
        Click("RecordsBack");
        open.Invoke(app, new[] { Enum.Parse(action, "Growth") });
        Assert.That(View("GrowthPanel").activeSelf, Is.True);
        Click("GrowthBack");
        app.GetType().GetMethod("BeginReviewerMinute", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(app, null);
        open.Invoke(app, new[] { Enum.Parse(action, "Records") });
        Assert.That(View("RecordsPanel").activeSelf, Is.False);
    }
}
