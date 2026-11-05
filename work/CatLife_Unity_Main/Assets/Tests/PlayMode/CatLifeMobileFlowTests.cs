using System.Collections;
using System.Linq;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CatLifeMobileFlowTests
{
    [UnityTest]
    public IEnumerator HomePointerActivityResetsDwellWithoutDuplicatingSemanticTaps()
    {
        foreach (int duration in new[] { 30, 60, 120 })
        {
        SeedAutoFocus();
        var configured = CatLifeDataJson.Deserialize(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"));
        configured.settings.autoFocusAdaptationSeconds = duration;
        PlayerPrefs.SetString("CatLife.Mobile.Data.v1", CatLifeDataJson.Serialize(configured));
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        Component features = GameObject.Find("CatLifeRuntimeSystems").GetComponent("RealtimeFeatureEngine");
        MethodInfo evaluate = app.GetType().GetMethod("EvaluateAutoFocus");
        MethodInfo observe = app.GetType().GetMethod("ObserveHomePointerActivity", BindingFlags.Instance | BindingFlags.NonPublic);
        System.Type snapshotType = System.Type.GetType("CatLife.Recognition.RecognitionSnapshot, Assembly-CSharp");
        object snapshot = snapshotType.GetMethod("CreateDefault").Invoke(null, null);
        snapshotType.GetField("focusConfidence").SetValue(snapshot, .82f);
        snapshotType.GetField("attentionBand").SetValue(snapshot, System.Enum.Parse(snapshotType.GetField("attentionBand").FieldType, "Stable"));
        evaluate.Invoke(app, new object[] { snapshot, duration - 1f });
        Click("TitleButton");
        features.GetType().GetMethod("Tick").Invoke(features, new object[] { 0f });
        object before = features.GetType().GetProperty("Latest").GetValue(features);
        float taps = (float)before.GetType().GetField("tapRate1s").GetValue(before);
        for (int i = 0; i < 60; i++) observe.Invoke(app, new object[] { true });
        object after = features.GetType().GetProperty("Latest").GetValue(features);
        Assert.That((float)after.GetType().GetField("tapRate1s").GetValue(after), Is.EqualTo(taps));
        Assert.That((float)after.GetType().GetField("secondsSinceLastInteraction").GetValue(after), Is.LessThan(.1f));
        evaluate.Invoke(app, new object[] { snapshot, 1f });
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Normal));
        observe.Invoke(app, new object[] { false });
        evaluate.Invoke(app, new object[] { snapshot, duration - 1f });
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Transition));
        }
    }

    [UnityTest]
    public IEnumerator DisabledStatisticsRewardAndPromptDoNotInventMeasurements()
    {
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var record = new CatLifeSessionRecord
        {
            completed = true, plannedSeconds = 60, actualSeconds = 60,
            behaviorStatsDisabled = true
        };
        record.localInsight = CatLifeInsightEngine.Create(record);
        app.GetType().GetMethod("RenderReward", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(app, new object[] { record });
        Text text = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .First(t => t.name == "RewardText");
        var rewardUi = GameObject.Find("CatLifeMobileView").GetComponent("CatLifeMobileUiPresentation");
        string rewardSummary = ((Text)rewardUi.GetType().GetField("rewardBehavior").GetValue(rewardUi)).text
            + ((Text)rewardUi.GetType().GetField("rewardGrowth").GetValue(rewardUi)).text + text.text;
        Assert.That(rewardSummary, Does.Contain("行为统计未采集").And.Contain("成长 +1"));
        Assert.That(rewardSummary, Does.Not.Contain("后台返回").And.Not.Contain("稳定度"));
        Assembly runtimeAssembly = app.GetType().Assembly;
        System.Type contextType = runtimeAssembly.GetType("CatLife.LLM.CatPromptContext");
        object context = System.Activator.CreateInstance(contextType);
        contextType.GetField("behaviorStatsDisabled").SetValue(context, true);
        contextType.GetField("userIntent").SetValue(context, "PostSession");
        contextType.GetField("safeLocalSummary").SetValue(context, "时长=60s;行为统计未采集");
        contextType.GetField("focusScore01").SetValue(context, .9f);
        contextType.GetField("distraction01").SetValue(context, .8f);
        contextType.GetField("tapRate1s").SetValue(context, 4f);
        System.Type builderType = runtimeAssembly.GetType("CatLife.LLM.CatPromptBuilder");
        string prompt = (string)builderType.GetMethod("BuildUserContextPrompt")
            .Invoke(System.Activator.CreateInstance(builderType), new[] { context });
        Assert.That(prompt, Does.Contain("时长=60s").And.Contain("not collected"));
        Assert.That(prompt, Does.Not.Contain("focusScore01").And.Not.Contain("distraction01").And.Not.Contain("tapRate1s"));
    }

    private string savedData;
    [SetUp] public void SaveData() { savedData = PlayerPrefs.GetString("CatLife.Mobile.Data.v1", ""); }
    [TearDown] public void RestoreData()
    {
        if (savedData.Length == 0) PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        else PlayerPrefs.SetString("CatLife.Mobile.Data.v1", savedData);
    }
    [UnityTest]
    public IEnumerator RewardShowsRequestingStructuredSuccessAndLocalFailure()
    {
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        MethodInfo render = app.GetType().GetMethod("RenderReward", BindingFlags.Instance | BindingFlags.NonPublic);
        Text text = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "RewardText");
        var record = new CatLifeSessionRecord { completed = true, plannedSeconds = 60, actualSeconds = 60, aiRequestAttempted = true, aiRequestStatus = "requesting", localInsight = "本地记录已保存" };
        render.Invoke(app, new object[] { record });
        Assert.That(text.text, Does.Contain("正在生成陪伴建议"));
        record.aiRequestStatus = "succeeded";
        record.aiGentleReminder = "完成这一段啦";
        record.aiReturnSuggestion = "先写下下一步";
        record.aiReactionReason = "挥爪为你加油";
        record.aiReaction = "paw_wave";
        render.Invoke(app, new object[] { record });
        Assert.That(text.text, Does.Contain("MiMo 生成").And.Contain("先写下下一步").And.Contain("挥爪回应"));
        Assert.That(text.resizeTextForBestFit, Is.False);
        record.aiRequestStatus = "local_fallback";
        render.Invoke(app, new object[] { record });
        Assert.That(text.text, Does.Contain("本地记录已保存").And.Not.Contain("MiMo 生成"));
        Assert.That(record.GrowthAwarded, Is.EqualTo(1));
        Assert.That(record.PawsAwarded, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator AiReactionPlaysUnlockedAnimatorStateOnlyInReward()
    {
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        MethodInfo play = coordinator.GetType().GetMethod("PlayAiReaction");
        Assert.That((bool)play.Invoke(coordinator, new object[] { "paw_wave" }), Is.False);
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Reward });
        Assert.That((bool)play.Invoke(coordinator, new object[] { "unknown" }), Is.False);
        Assert.That((bool)play.Invoke(coordinator, new object[] { "paw_wave" }), Is.True);
        yield return new WaitForSeconds(.5f);
        Animator animator = GameObject.Find("CatLifeMobileCat").GetComponentInChildren<Animator>();
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_CrouchPawWave_v01_loop_96f"), Is.True);
    }

    [UnityTest]
    public IEnumerator RewardNoneActionRendersAndPersistsWithoutInventingReaction()
    {
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var record = new CatLifeSessionRecord
        {
            completed = true, plannedSeconds = 60, actualSeconds = 60,
            aiRequestAttempted = true, aiRequestCount = 1, aiRequestStatus = "succeeded",
            aiSource = "mimo_cloud", aiRequestedAction = "none",
            aiReaction = CatLifeUnlocks.ResolveAiReaction("none", 8), aiActionStatus = "no_action",
            aiGentleReminder = "这一段已完成", aiReturnSuggestion = "写下下一步",
            aiReactionReason = "安静陪着你"
        };
        var data = new CatLifeAppData();
        data.records.Add(record);
        var restored = CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(data)).records[0];
        app.GetType().GetMethod("RenderReward", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, new object[] { restored });
        Text text = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "RewardText");
        Assert.That(text.text, Does.Contain("猫咪回应：暂无动作").And.Contain("写下下一步"));
        Assert.That(text.text, Does.Not.Contain("挥爪回应").And.Not.Contain("轻摆尾"));
        Assert.That(restored.aiReaction, Is.Empty);
        Assert.That(restored.aiActionStatus, Is.EqualTo("no_action"));
        Assert.That(restored.aiRequestCount, Is.EqualTo(1));
        Assert.That(restored.GrowthAwarded, Is.EqualTo(1));
        Assert.That(restored.PawsAwarded, Is.EqualTo(1));
    }

    [Test]
    public void StructuredAiOutputPreservesSectionsAndRejectsUnsafeText()
    {
        System.Type type = System.Type.GetType("CatLife.LLM.LLMBehaviorSuggestion, Assembly-CSharp");
        object value = System.Activator.CreateInstance(type);
        type.GetField("gentleReminder").SetValue(value, "这一小段已经完成啦");
        type.GetField("returnSuggestion").SetValue(value, "先写下下一步");
        type.GetField("catReactionReason").SetValue(value, "轻轻挥爪为你加油");
        object[] args = { value, null, null };
        Assert.That((bool)type.GetMethod("TryBuildSafe").Invoke(null, args), Is.True);
        Assert.That(type.GetField("returnSuggestion").GetValue(args[1]), Is.EqualTo("先写下下一步"));
        type.GetField("returnSuggestion").SetValue(value, "https://example.com");
        Assert.That((bool)type.GetMethod("TryBuildSafe").Invoke(null, args), Is.False);
    }

    [Test]
    public void RecognitionSpectrumClassifiesDistractedTransitioningAndStableWithTrend()
    {
        System.Type spectrumType = System.Type.GetType("CatLife.Recognition.AttentionSpectrum, Assembly-CSharp");
        Assert.That(spectrumType, Is.Not.Null);
        MethodInfo evaluate = spectrumType.GetMethod("Evaluate", BindingFlags.Static | BindingFlags.Public);

        object distracted = evaluate.Invoke(null, new object[] { .30f, .80f, .70f, .65f });
        object transitioning = evaluate.Invoke(null, new object[] { .55f, .35f, .30f, .55f });
        object stable = evaluate.Invoke(null, new object[] { .82f, .18f, .10f, .60f });

        Assert.That(distracted.GetType().GetField("band").GetValue(distracted).ToString(), Is.EqualTo("Distracted"));
        Assert.That(distracted.GetType().GetField("trend").GetValue(distracted).ToString(), Is.EqualTo("Falling"));
        Assert.That(transitioning.GetType().GetField("band").GetValue(transitioning).ToString(), Is.EqualTo("Transitioning"));
        Assert.That(transitioning.GetType().GetField("trend").GetValue(transitioning).ToString(), Is.EqualTo("Steady"));
        Assert.That(stable.GetType().GetField("band").GetValue(stable).ToString(), Is.EqualTo("Stable"));
        Assert.That(stable.GetType().GetField("trend").GetValue(stable).ToString(), Is.EqualTo("Rising"));
    }

    [UnityTest]
    public IEnumerator StableRecognitionStartsCancelableAutoTransitionWithoutRetriggering()
    {
        SeedAutoFocus();
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        MethodInfo evaluate = app.GetType().GetMethod("EvaluateAutoFocus", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(evaluate, Is.Not.Null);
        System.Type snapshotType = System.Type.GetType("CatLife.Recognition.RecognitionSnapshot, Assembly-CSharp");
        object snapshot = snapshotType.GetMethod("CreateDefault", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        snapshotType.GetField("focusConfidence").SetValue(snapshot, .82f);
        snapshotType.GetField("attentionBand").SetValue(snapshot, System.Enum.Parse(snapshotType.GetField("attentionBand").FieldType, "Stable"));

        evaluate.Invoke(app, new object[] { snapshot, 30f });
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Transition));
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("TransitionPanel"));
        Assert.That(GameObject.Find("AutoFocusCancel"), Is.Not.Null);

        Click("AutoFocusCancel");
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Normal));
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("HomeHudLayer"));
        evaluate.Invoke(app, new object[] { snapshot, 30f });
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Normal));
    }

    [UnityTest]
    public IEnumerator StableRecognitionAutomaticallyEntersFocusAfterTransition()
    {
        SeedAutoFocus();
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        MethodInfo evaluate = app.GetType().GetMethod("EvaluateAutoFocus", BindingFlags.Instance | BindingFlags.Public);
        System.Type snapshotType = System.Type.GetType("CatLife.Recognition.RecognitionSnapshot, Assembly-CSharp");
        object snapshot = snapshotType.GetMethod("CreateDefault", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        snapshotType.GetField("focusConfidence").SetValue(snapshot, .82f);
        snapshotType.GetField("attentionBand").SetValue(snapshot, System.Enum.Parse(snapshotType.GetField("attentionBand").FieldType, "Stable"));

        evaluate.Invoke(app, new object[] { snapshot, 30f });
        yield return new WaitForSecondsRealtime(2.9f);

        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Focus));
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("FocusPanel"));
    }

    [UnityTest]
    public IEnumerator QuietDwellBecomesExplicitLocalAggregateEvent()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component features = GameObject.Find("CatLifeRuntimeSystems").GetComponent("RealtimeFeatureEngine");
        features.GetType().GetMethod("RecordUiEvent").Invoke(features, new object[] { "page_enter_quiet_test" });
        features.GetType().GetMethod("Tick").Invoke(features, new object[] { 6f });

        Assert.That((string)features.GetType().GetProperty("LastAcceptedBehaviorEvent").GetValue(features), Is.EqualTo("quiet_dwell"));
        object latest = features.GetType().GetProperty("Latest").GetValue(features);
        string summary = (string)latest.GetType().GetField("localEventSummary").GetValue(latest);
        Assert.That(summary, Does.Not.Contain("raw"));
        Assert.That(summary, Does.Not.Contain("package"));
    }

    [UnityTest]
    public IEnumerator MobileUiButtonsFeedTheAggregateTapRate()
    {
        var enabledStats = new CatLifeAppData();
        enabledStats.settings.catAdopted = true;
        enabledStats.settings.localBehaviorStatsEnabled = true;
        PlayerPrefs.SetString("CatLife.Mobile.Data.v1", CatLifeDataJson.Serialize(enabledStats));
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component features = GameObject.Find("CatLifeRuntimeSystems").GetComponent("RealtimeFeatureEngine");

        Click("TitleButton");
        features.GetType().GetMethod("Tick").Invoke(features, new object[] { 0f });

        // Observe the one-second window immediately; a scene-loading frame can exceed it.
        object latest = features.GetType().GetProperty("Latest").GetValue(features);
        Assert.That((float)latest.GetType().GetField("tapRate1s").GetValue(latest), Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator ApprovedHomeAndSetupUseSerializedUiKitSprites()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;

        Image start = GameObject.Find("StartButton").GetComponent<Image>();
        Assert.That(start.sprite, Is.Not.Null);
        Assert.That(start.sprite.name, Is.EqualTo("S_CL_UI_Figma_SkinPrimary"));
        Image growthIcon = GameObject.Find("GrowthButton").transform.Find("GrowthIcon").GetComponent<Image>();
        Assert.That(growthIcon.sprite.name, Is.EqualTo("S_CL_UI_Figma_IconGrowth"));

        Click("StartButton");
        yield return null;
        Image setupCard = GameObject.Find("SetupCard").GetComponent<Image>();
        Assert.That(setupCard.sprite.name, Is.EqualTo("S_CL_UI_Figma_SkinSheet"));
        Behaviour bridge = GameObject.Find("SafeArea").GetComponent("CatUiInteractionBridge") as Behaviour;
        Assert.That(bridge.enabled, Is.False);
        Component feedback = Object.FindAnyObjectByType(System.Type.GetType("CatLife.UI.CatBubblePresenter, Assembly-CSharp")) as Component;
        Transform feedbackBubble = feedback.GetComponentsInChildren<Transform>(true).First(item => item.name == "CatFeedbackBubble");
        Assert.That(feedbackBubble.gameObject.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator ApprovedFunctionalPagesExposeRealVisualComponents()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.That(all.Count(item => item.name.StartsWith("DayBar")), Is.EqualTo(7));
        Assert.That(all.Count(item => item.name.StartsWith("ChartGrid")), Is.EqualTo(4));
        Assert.That(all.Any(item => item.name == "FocusCompanionChip"), Is.True);
        Assert.That(all.Any(item => item.name == "GrowthActionWave"), Is.True);
        Assert.That(all.Any(item => item.name == "SettingsPreferenceCard"), Is.True);
        Assert.That(all.Any(item => item.name == "SettingsRecords"), Is.True);
        Assert.That(all.First(item => item.name == "GrowthActionWaveIcon").GetComponent<Image>().sprite.name, Is.EqualTo("T_CL_CatAction_Wave_512"));
        Assert.That(all.First(item => item.name == "GrowthActionLieIcon").GetComponent<Image>().sprite.name, Is.EqualTo("T_CL_CatAction_Loaf_512"));
        Assert.That(GameObject.Find("CL_ENV_CloudLayer_01").GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator FocusSwipeProducesSanitizedAggregateEvent()
    {
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        app.GetType().GetMethod("BeginReviewerMinute", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, null);
        float deadline = Time.realtimeSinceStartup + 6f;
        while (Property<CatLifeSessionPhase>(app, "CurrentPhase") != CatLifeSessionPhase.Focus && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Focus));
        Canvas.ForceUpdateCanvases();
        Component features = GameObject.Find("CatLifeRuntimeSystems").GetComponent("RealtimeFeatureEngine");
        Transform swipeTransform = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(item => item.name == "SwipeTrack");
        Component swipe = swipeTransform.GetComponent("CatLifeSwipeToEnd");
        var handle = (RectTransform)swipe.GetType().GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(swipe);
        var pointer = new PointerEventData(EventSystem.current) { pointerId = 1, position = RectTransformUtility.WorldToScreenPoint(null, handle.position) };
        swipe.GetType().GetMethod("OnPointerDown").Invoke(swipe, new object[] { pointer });
        swipe.GetType().GetMethod("OnPointerUp").Invoke(swipe, new object[] { pointer });

        Assert.That((string)features.GetType().GetProperty("LastAcceptedBehaviorEvent").GetValue(features), Is.EqualTo("ui_scroll"));
    }

    [UnityTest]
    public IEnumerator ReviewerPanelRendersCurrentRecognitionInsteadOfPresetValues()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        for (int i = 0; i < 5; i++) Click("TitleButton");
        GameObject cat = GameObject.Find("CatLifeMobileCat");
        for (int i = 0; i < 3; i++) cat.GetComponent("CatBehaviorDriver").GetType().GetMethod("NotifyCatTapped").Invoke(cat.GetComponent("CatBehaviorDriver"), null);
        yield return new WaitForSecondsRealtime(.6f);

        Component coordinator = GameObject.Find("CatLifeRuntimeSystems").GetComponent("CatLifeMobileRuntimeCoordinator");
        object recognition = coordinator.GetType().GetProperty("LatestRecognition").GetValue(coordinator);
        string band = recognition.GetType().GetField("attentionBand").GetValue(recognition).ToString();
        string debug = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(item => item.name == "DebugText").text;
        Assert.That(debug, Does.Contain("注意 " + band));
        Assert.That(debug, Does.Contain("事件 features:"));
        Assert.That(debug, Does.Not.Contain("演示"));
    }

    [Test]
    public void BehaviorEventBoundaryRejectsRawTouchAndPackagePayloads()
    {
        System.Type sanitizer = System.Type.GetType("CatLife.Recognition.BehaviorEventSanitizer, Assembly-CSharp");
        MethodInfo parse = sanitizer.GetMethod("TryParseAndSanitize", BindingFlags.Static | BindingFlags.Public);
        object[] rawTouch = { "{\"eventType\":\"UiTap\",\"raw_touch_path\":\"secret\"}", null, null };
        object[] package = { "{\"eventType\":\"UiTap\",\"package_name\":\"other.app\"}", null, null };
        Assert.That((bool)parse.Invoke(null, rawTouch), Is.False);
        Assert.That((bool)parse.Invoke(null, package), Is.False);
    }

    [Test]
    public void CameraDirectorExposesApprovedFixedPresets()
    {
        System.Type type = System.Type.GetType("CatLifeCameraDirector, Assembly-CSharp");
        GameObject go = new GameObject("Camera", typeof(Camera));
        Component director = go.AddComponent(type);
        type.GetMethod("Configure").Invoke(director, new object[] { go.GetComponent<Camera>() });
        object home = type.GetMethod("GetPreset").Invoke(director, new object[] { CatLifeSessionPhase.Normal });
        object focus = type.GetMethod("GetPreset").Invoke(director, new object[] { CatLifeSessionPhase.Focus });
        object transition = type.GetMethod("GetPreset").Invoke(director, new object[] { CatLifeSessionPhase.Transition });
        Assert.That((Vector3)home.GetType().GetProperty("Position").GetValue(home), Is.EqualTo(new Vector3(0f, 2.2f, 9f)));
        Assert.That((Vector3)home.GetType().GetProperty("Euler").GetValue(home), Is.EqualTo(new Vector3(12f, 180f, 0f)));
        Assert.That((float)home.GetType().GetProperty("Fov").GetValue(home), Is.EqualTo(74f));
        Assert.That((float)focus.GetType().GetProperty("Fov").GetValue(focus), Is.EqualTo(74f));
        Assert.That((float)transition.GetType().GetProperty("Duration").GetValue(transition), Is.EqualTo(2f));
        Object.DestroyImmediate(go);
    }

    [Test]
    public void GenericCloudConfigUsesMiMoDefaults()
    {
        System.Type configType = System.Type.GetType("CatLife.LLM.GenericCloudConfig, Assembly-CSharp");
        Assert.That(configType, Is.Not.Null);
        object config = System.Activator.CreateInstance(configType);
        Assert.That((string)configType.GetField("provider").GetValue(config), Is.EqualTo("mimo"));
        Assert.That((string)configType.GetField("apiEndpoint").GetValue(config), Is.EqualTo("https://api.xiaomimimo.com/v1/chat/completions"));
        Assert.That((string)configType.GetField("model").GetValue(config), Is.EqualTo("mimo-v2.5"));
        Assert.That(configType.GetField("apiKey"), Is.Not.Null);
    }

    [Test]
    public void GenericCloudParserAcceptsPlainTextWithoutRetry()
    {
        System.Type clientType = System.Type.GetType("CatLife.LLM.MockCatLLMClient, Assembly-CSharp");
        MethodInfo parse = clientType.GetMethod("TryParseGenericCloudSuggestion", BindingFlags.Static | BindingFlags.Public);
        object[] args = { "今天也专注得很好，继续保持。", null, null };
        bool accepted = (bool)parse.Invoke(null, args);
        Assert.That(accepted, Is.True);
        Assert.That(args[1], Is.Not.Null);
        Assert.That((string)args[2], Does.StartWith("passed"));
    }

    [Test]
    public void MiMoRequestDisablesThinkingAndRequestsJson()
    {
        System.Type clientType = System.Type.GetType("CatLife.LLM.MockCatLLMClient, Assembly-CSharp");
        MethodInfo build = clientType.GetMethod("BuildGenericCloudRequestJson", BindingFlags.Static | BindingFlags.Public);
        Assert.That(build, Is.Not.Null);
        string json = (string)build.Invoke(null, new object[] { "mimo-v2.5", null });
        StringAssert.Contains("\"thinking\":{\"type\":\"disabled\"}", json);
        StringAssert.Contains("\"response_format\":{\"type\":\"json_object\"}", json);
    }

    [UnityTest]
    public IEnumerator RestoredTransitionFinishesLieDownBeforeStartingClock()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        new CatLifeSessionController(data).BeginTransition(900, System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()-20);
        PlayerPrefs.SetString("CatLife.Mobile.Data.v1", CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        Assert.That(Property<CatLifeSessionPhase>(app,"CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Transition));
        Assert.That(Property<string>(app,"CurrentView"), Is.EqualTo("TransitionPanel"));
        yield return new WaitForSecondsRealtime(1);
        var stored = CatLifeDataJson.Deserialize(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"));
        Assert.That(stored.activeSession.targetEndAt, Is.Zero);
        Assert.That(stored.activeSession.plannedSeconds, Is.EqualTo(900));
        yield return new WaitForSecondsRealtime(2.5f);
        Assert.That(Property<CatLifeSessionPhase>(app,"CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Focus));
        stored = CatLifeDataJson.Deserialize(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"));
        Assert.That(stored.activeSession.targetEndAt-stored.activeSession.focusStartedAt, Is.EqualTo(900));
        Assert.That(stored.records.Count, Is.Zero);
    }

    [UnityTest]
    public IEnumerator MobileSceneRoutesPagesAndStartsAfterTransition()
    {
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        Assert.That(app, Is.Not.Null);
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("HomeHudLayer"));
        Click("RecordsButton");
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("RecordsPanel"));
        Click("RecordsBack");
        Click("StartButton");
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("SetupPanel"));
        Click("SetupStart");
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Transition));
        yield return new WaitForSecondsRealtime(3f);
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Focus));
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("FocusPanel"));
    }

    [UnityTest]
    public IEnumerator SceneContainsStableLandmarkEntrypointsAndAnimatorStates()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        var landmarks = Object.FindObjectsByType<Component>(FindObjectsSortMode.None).Where(c => c.GetType().Name == "CatLifeLandmark").ToArray();
        foreach (string action in new[] { "Records", "Growth", "FishShop", "RewardTree", "Plaza" })
            Assert.That(landmarks.Any(c => c.GetType().GetField("action").GetValue(c).ToString() == action), Is.True, action + " stable semantic entrypoint is missing.");
        Animator animator = GameObject.Find("CatLifeMobileCat").GetComponentInChildren<Animator>();
        Assert.That(animator.applyRootMotion, Is.False);
        Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
        var skin = GameObject.Find("CatLifeMobileCat").GetComponentsInChildren<SkinnedMeshRenderer>().Single();
        var baked = new Mesh(); skin.BakeMesh(baked, false);
        Bounds visibleBounds = skin.bounds; visibleBounds.Expand(.001f);
        // Match v12's verified terrain probe: bone-scaled BakeMesh vertices are
        // converted by rotation/translation, not scaled a second time.
        foreach (Vector3 vertex in baked.vertices)
        {
            Vector3 world = skin.transform.position + skin.transform.rotation * vertex;
            Assert.That(visibleBounds.Contains(world), Is.True, $"Runtime world bounds {skin.bounds} miss standing vertex {world:R}.");
        }
        Object.Destroy(baked);
        Assert.That(animator.HasState(0, Animator.StringToHash("Base Layer.CL_CAT_StandIdle_v01_loop_96f")), Is.True);
        Assert.That(animator.HasState(0, Animator.StringToHash("Base Layer.CL_CAT_LieFocusRest_v01_loop_96f")), Is.True);
    }

    [UnityTest]
    public IEnumerator MobileSceneWiresRecognitionThroughSingleRuntimeSystemsRoot()
    {
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;

        GameObject systems = GameObject.Find("CatLifeRuntimeSystems");
        Assert.That(systems, Is.Not.Null);
        Assert.That(systems.GetComponent("RealtimeFeatureEngine"), Is.Not.Null);
        Assert.That(systems.GetComponent("MockRecognitionProvider"), Is.Not.Null);
        Component coordinator = systems.GetComponent("CatLifeMobileRuntimeCoordinator");
        Assert.That(coordinator, Is.Not.Null);
        Assert.That(GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileRuntimeCoordinator"), Is.Null);

        Component features = systems.GetComponent("RealtimeFeatureEngine");
        coordinator.GetType().GetMethod("RecordUiEvent").Invoke(coordinator, new object[] { "page_enter_contract_test" });
        Assert.That((string)features.GetType().GetProperty("LastAcceptedBehaviorEvent").GetValue(features), Is.EqualTo("page_enter_contract_test"));
        GameObject cat = GameObject.Find("CatLifeMobileCat");
        Rigidbody catBody = cat.GetComponent<Rigidbody>();
        float groundedY = catBody.position.y;
        Assert.That(NavMesh.SamplePosition(new Vector3(.75f, .02f, 4.45f), out var relocation, 2f, NavMesh.AllAreas), Is.True);
        Assert.That(cat.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(relocation.position), Is.True);
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Focus });
        object snapshot = features.GetType().GetProperty("Latest").GetValue(features);
        Assert.That((bool)snapshot.GetType().GetField("isFocusSessionActive").GetValue(snapshot), Is.True);
        Assert.That(Vector3.Distance(catBody.position, new Vector3(0f, groundedY, 4.5f)), Is.LessThan(.03f));
        var presenter = cat.GetComponent("CatLifeMobileCatPresenter");
        var configuredRotation = (Quaternion)presenter.GetType().GetField("sessionAnchorRotation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);
        Assert.That(Quaternion.Angle(catBody.rotation, configuredRotation), Is.LessThan(.5f));
        Assert.That(cat.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped, Is.True);
    }

    [UnityTest]
    public IEnumerator MobileCatCanNavigateFromSpawnToApprovedInterestPoint()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        Component navigation = cat.GetComponent("CatNavigationAgent");
        Assert.That(navigation, Is.Not.Null);
        Assert.That((bool)navigation.GetType().GetProperty("IsOnNavMesh").GetValue(navigation), Is.True);

        GameObject destination = GameObject.Find("Interest_Left_Garden");
        Assert.That(destination, Is.Not.Null);
        Vector3 start = cat.transform.position;
        bool accepted = (bool)navigation.GetType().GetMethod("TryMoveTo").Invoke(navigation, new object[] { destination.transform.position });
        Assert.That(accepted, Is.True);

        float deadline = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < deadline && Vector3.Distance(start, cat.transform.position) < .2f)
            yield return null;

        Assert.That(Vector3.Distance(start, cat.transform.position), Is.GreaterThanOrEqualTo(.2f));
    }

    [UnityTest]
    public IEnumerator MobileNavigationDefinesSemanticBuildingForbiddenZones()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;

        GameObject root = GameObject.Find("CatForbiddenZones");
        Assert.That(root, Is.Not.Null);
        Component[] zones = root.GetComponentsInChildren(System.Type.GetType("CatLife.Cat.CatForbiddenZone, Assembly-CSharp"));
        Assert.That(zones.Length, Is.GreaterThanOrEqualTo(5));
        Assert.That(zones.Any(zone => ((string)zone.GetType().GetProperty("SourceObjectName").GetValue(zone)).Contains("FocusHouse")), Is.True);

        Component focusZone = zones.First(zone => ((string)zone.GetType().GetProperty("SourceObjectName").GetValue(zone)).Contains("FocusHouse"));
        bool containsCenter = (bool)focusZone.GetType().GetMethod("ContainsProjectedPoint").Invoke(
            focusZone,
            new object[] { focusZone.transform.position, 0f });
        Assert.That(containsCenter, Is.True);
    }

    [UnityTest]
    public IEnumerator MobileCatTraversesThreeApprovedInterestPointsContinuously()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        Behaviour behavior = (Behaviour)cat.GetComponent("CatBehaviorDriver");
        behavior.enabled = false;
        Component navigation = cat.GetComponent("CatNavigationAgent");
        Component planner = cat.GetComponent("CatDestinationPlanner");
        System.Type snapshotType = System.Type.GetType("CatLife.Recognition.RecognitionSnapshot, Assembly-CSharp");
        object snapshot = snapshotType.GetMethod("CreateDefault", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        MethodInfo requestedPlan = planner.GetType().GetMethods().Single(method => method.Name == "TryPlanRequestedPoint");

        foreach (string pointName in new[] { "Interest_Left_Garden", "Interest_Front_Path", "Interest_Right_Garden" })
        {
            Vector3 requested = GameObject.Find(pointName).transform.position;
            object[] planArgs = { snapshot, requested, cat.transform.position, Vector3.zero };
            Assert.That((bool)requestedPlan.Invoke(planner, planArgs), Is.True, pointName + " was rejected by the planner.");
            Assert.That((bool)navigation.GetType().GetMethod("TryMoveTo").Invoke(navigation, new[] { planArgs[3] }), Is.True);
            yield return null;

            float deadline = Time.realtimeSinceStartup + 9f;
            while (Time.realtimeSinceStartup < deadline && !(bool)navigation.GetType().GetMethod("HasArrived").Invoke(navigation, null))
                yield return null;
            Assert.That((bool)navigation.GetType().GetMethod("HasArrived").Invoke(navigation, null), Is.True, pointName + " was not reached.");
        }
    }

    [UnityTest]
    public IEnumerator MobileCatRoamsWithoutUserMoveCommandAndHasWalkAnimation()
    {
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        Component behavior = cat.GetComponent("CatBehaviorDriver");
        Assert.That(behavior, Is.Not.Null);
        Assert.That(((Behaviour)behavior).enabled, Is.True);
        NavMeshAgent navMeshAgent = cat.GetComponent<NavMeshAgent>();
        Assert.That(navMeshAgent.enabled && navMeshAgent.isOnNavMesh, Is.True, "Normal phase must attach the cat to the runtime NavMesh.");
        Animator animator = cat.GetComponentInChildren<Animator>();
        Assert.That(animator.HasState(0, Animator.StringToHash("Base Layer.CL_CAT_Walk_v01_loop_60f")), Is.True);

        Component planner = cat.GetComponent("CatDestinationPlanner");
        System.Type snapshotType = System.Type.GetType("CatLife.Recognition.RecognitionSnapshot, Assembly-CSharp");
        object snapshot = snapshotType.GetMethod("CreateDefault", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        MethodInfo requestedPlan = planner.GetType().GetMethods().Single(method => method.Name == "TryPlanRequestedPoint");
        object[] planArgs = { snapshot, GameObject.Find("Interest_Left_Garden").transform.position, cat.transform.position, Vector3.zero };
        Assert.That((bool)requestedPlan.Invoke(planner, planArgs), Is.True, "Approved interest point was rejected by CatDestinationPlanner.");

        Vector3 start = cat.transform.position;
        float deadline = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < deadline && HorizontalDistance(start, cat.transform.position) < .2f)
            yield return null;

        float moved = HorizontalDistance(start, cat.transform.position);
        Component driver = cat.GetComponent("CatBehaviorDriver");
        string state = driver.GetType().GetProperty("CurrentState").GetValue(driver).ToString();
        string path = (string)cat.GetComponent("CatNavigationAgent").GetType().GetProperty("PathStatusText").GetValue(cat.GetComponent("CatNavigationAgent"));
        Assert.That(moved, Is.GreaterThanOrEqualTo(.2f), $"state={state}; path={path}; moved={moved:F3}");
        yield return new WaitForSeconds(.2f);
        bool currentWalk = animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.CL_CAT_Walk_v01_loop_60f");
        bool nextWalk = animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.CL_CAT_Walk_v01_loop_60f");
        Assert.That(currentWalk || nextWalk, Is.True,
            $"The cat moved {moved:F3}m without current/next Walk locomotion.");
        var walkSkin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        Assert.That(walkSkin.sharedMesh.blendShapeCount, Is.EqualTo(39));
        for (int i = 0; i < walkSkin.sharedMesh.blendShapeCount; i++)
            Assert.That(walkSkin.GetBlendShapeWeight(i), Is.LessThan(1f), "Walk must release " + walkSkin.sharedMesh.GetBlendShapeName(i));
    }

    [UnityTest]
    public IEnumerator MobileCatUsesGravityAndWalkableTownCollidersWithoutFallingThrough()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;

        GameObject cat = GameObject.Find("CatLifeMobileCat");
        Rigidbody body = cat.GetComponent<Rigidbody>();
        CapsuleCollider bodyCollider = cat.GetComponent<CapsuleCollider>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.useGravity && !body.isKinematic, Is.True);
        Assert.That(bodyCollider, Is.Not.Null);
        Assert.That(bodyCollider.isTrigger, Is.False);

        foreach (string surfaceName in new[]
        {
            "CL_ENV_IslandBase_01", "CL_ENV_IslandGrass_01", "CL_ROAD_CentralGardenRing_01",
            "CL_ROAD_PlazaPath_01", "CL_ROAD_PlazaRing_01", "CL_PROP_CenterStone_01"
        })
        {
            MeshCollider surface = GameObject.Find(surfaceName).GetComponent<MeshCollider>();
            Assert.That(surface, Is.Not.Null, surfaceName + " has no physical surface.");
            Assert.That(surface.isTrigger, Is.False, surfaceName + " cannot hold the cat.");
        }

        float startY = body.position.y;
        yield return new WaitForSeconds(1f);
        Assert.That(body.position.y, Is.GreaterThan(-.5f), "Gravity cat fell through the island.");
        Assert.That(Mathf.Abs(body.position.y - startY), Is.LessThan(.25f), "Gravity grounding is vertically unstable.");
    }

    [UnityTest]
    public IEnumerator MobileCatTapProducesVisibleLowDistractionFeedback()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;

        System.Type presenterType = System.Type.GetType("CatLife.UI.CatBubblePresenter, Assembly-CSharp");
        Component presenter = Object.FindAnyObjectByType(presenterType) as Component;
        Assert.That(presenter, Is.Not.Null, "The mobile scene must provide a visible cat feedback presenter.");
        GameObject cat = GameObject.Find("CatLifeMobileCat");
        Component mapper = cat.GetComponent("CatInteractionMapper");
        Assert.That(mapper, Is.Not.Null, "The mobile cat must map real pointer input.");
        Assert.That(cat.GetComponent<BoxCollider>(), Is.Not.Null, "The mobile cat must expose a raycast collider.");
        GameObject safeArea = GameObject.Find("SafeArea");
        Assert.That(safeArea.GetComponent("CatUiInteractionBridge"), Is.Not.Null, "uGUI must bridge Android pointer clicks to the moving cat.");
        Assert.That(safeArea.GetComponent<Image>().raycastTarget, Is.True, "The serialized SafeArea must receive Android pointer clicks.");
        Vector3 center = cat.GetComponentsInChildren<Renderer>().Aggregate(
            new Bounds(cat.GetComponentsInChildren<Renderer>()[0].bounds.center, Vector3.zero),
            (bounds, renderer) => { bounds.Encapsulate(renderer.bounds); return bounds; }).center;
        Vector3 screenCenter = Camera.main.WorldToScreenPoint(center);
        MethodInfo screenHit = mapper.GetType().GetMethod("IsWithinCatScreenHitArea", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That((bool)screenHit.Invoke(mapper, new object[] { new Vector2(screenCenter.x, screenCenter.y) }), Is.True);
        Component driver = cat.GetComponent("CatBehaviorDriver");
        driver.GetType().GetMethod("NotifyCatTapped").Invoke(driver, null);
        yield return null;

        GameObject bubble = GameObject.Find("CatFeedbackBubble");
        Assert.That(bubble, Is.Not.Null);
        Assert.That(bubble.activeInHierarchy, Is.True);
        Assert.That(bubble.GetComponentInChildren<Text>().text, Is.Not.Empty);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    [UnityTest]
    public IEnumerator MobileUiBackgroundsDoNotBlockWorldInteractionRaycasts()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;

        foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (image.name == "AdoptionPanel" || image.name == "FigmaPrivacyPanel") { Assert.That(image.raycastTarget, Is.True, "An open modal must prevent click-through to the town."); continue; }
            var scroll = image.GetComponent<ScrollRect>() ?? image.GetComponentInParent<ScrollRect>(true);
            if (scroll != null && scroll.viewport == image.rectTransform)
            { Assert.That(image.raycastTarget, Is.True, "The scroll viewport must receive real drag events."); continue; }
            if (image.GetComponent<Selectable>() == null && image.GetComponent("CatLifeSwipeToEnd") == null && image.GetComponent("CatUiInteractionBridge") == null)
                Assert.That(image.raycastTarget, Is.False, image.name + " blocks world interaction without being selectable.");
        }
    }

    [UnityTest]
    public IEnumerator NormalRoamingPrefersTheApprovedFixedCameraRange()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component planner = GameObject.Find("CatLifeMobileCat").GetComponent("CatDestinationPlanner");
        FieldInfo preference = planner.GetType().GetField("preferCameraRangeWhenNonFocused", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo radius = planner.GetType().GetField("nonFocusSampleRadius", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That((bool)preference.GetValue(planner), Is.True);
        Assert.That((float)radius.GetValue(planner), Is.LessThanOrEqualTo(2.5f));

        Vector3 home = GameObject.Find("Interest_HomeFront").transform.position;
        foreach (string pointName in new[] { "Interest_Left_Garden", "Interest_Front_Path", "Interest_Right_Garden" })
        {
            GameObject pointObject = GameObject.Find(pointName);
            Vector3 point = pointObject.transform.position;
            Assert.That(Vector3.Distance(home, point), Is.LessThanOrEqualTo(2.25f));
            Assert.That((bool)planner.GetType().GetMethod("IsPointInPreferredCameraRange").Invoke(planner, new object[] { point }), Is.True, pointName + " is outside the fixed camera safe range.");
            Vector3 viewport = Camera.main.WorldToViewportPoint(point + Vector3.up * .28f);
            Assert.That(viewport.x, Is.InRange(.15f, .75f), pointName + " is outside the visible horizontal activity band.");
            Assert.That(viewport.y, Is.InRange(.25f, .7f), pointName + " is outside the visible vertical activity band.");

            Component interest = pointObject.GetComponent("CatInterestPoint");
            float sampleRadius = (float)interest.GetType().GetProperty("SampleRadius").GetValue(interest);
            foreach (float zOffset in new[] { -sampleRadius, sampleRadius })
            {
                Vector3 sampleViewport = Camera.main.WorldToViewportPoint(point + new Vector3(0f, .28f, zOffset));
                Assert.That(sampleViewport.y, Is.InRange(.3f, .65f), pointName + " sample edge can move the cat outside the visible vertical activity band.");
            }
        }
    }

    [UnityTest]
    public IEnumerator AutoFocusPageExitResetsDwellForEveryConfiguredDuration()
    {
        foreach (int duration in new[] { 30, 60, 120 })
        {
            var data = new CatLifeAppData();
            data.settings.catAdopted = true;
            data.settings.localBehaviorStatsEnabled = true;
            data.settings.autoFocusAdaptationSeconds = duration;
            PlayerPrefs.SetString("CatLife.Mobile.Data.v1", CatLifeDataJson.Serialize(data));
            SceneManager.LoadScene("CatLifeMobile");
            yield return null;
            Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
            var evaluate = app.GetType().GetMethod("EvaluateAutoFocus");
            var snapshotType = System.Type.GetType("CatLife.Recognition.RecognitionSnapshot, Assembly-CSharp");
            object snapshot = snapshotType.GetMethod("CreateDefault").Invoke(null, null);
            snapshotType.GetField("focusConfidence").SetValue(snapshot, .82f);
            snapshotType.GetField("attentionBand").SetValue(snapshot, System.Enum.Parse(snapshotType.GetField("attentionBand").FieldType, "Stable"));
            foreach (string page in new[] { "SetupPanel", "RecordsPanel", "GrowthPanel", "SettingsPanel" })
            {
                evaluate.Invoke(app, new object[] { snapshot, duration - 1f });
                app.GetType().GetMethod("ShowOnly", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, new object[] { page });
                app.GetType().GetMethod("ShowOnly", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, new object[] { "HomeHudLayer" });
                evaluate.Invoke(app, new object[] { snapshot, 1f });
                Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Normal), $"{duration}s: {page} must break continuous home dwell");
                app.GetType().GetMethod("ShowOnly", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, new object[] { page });
                app.GetType().GetMethod("ShowOnly", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, new object[] { "HomeHudLayer" });
            }
            evaluate.Invoke(app, new object[] { snapshot, duration });
            Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Transition));
        }
    }

    [UnityTest]
    public IEnumerator AutoFocusRespectsEligibilityAndStatsOffStillAllowsManualSession()
    {
        SeedAutoFocus();
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var data = (CatLifeAppData)app.GetType().GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
        var evaluate = app.GetType().GetMethod("EvaluateAutoFocus");
        var type = System.Type.GetType("CatLife.Recognition.RecognitionSnapshot, Assembly-CSharp");
        object snapshot = type.GetMethod("CreateDefault").Invoke(null, null);
        type.GetField("focusConfidence").SetValue(snapshot, .82f);
        type.GetField("attentionBand").SetValue(snapshot, System.Enum.Parse(type.GetField("attentionBand").FieldType, "Stable"));
        foreach (string page in new[] { "SetupPanel", "RecordsPanel", "GrowthPanel", "SettingsPanel" })
        {
            app.GetType().GetMethod("ShowOnly", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, new object[] { page });
            evaluate.Invoke(app, new object[] { snapshot, 120f });
            Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Normal), page);
        }
        app.GetType().GetMethod("ShowOnly", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, new object[] { "HomeHudLayer" });
        data.settings.catAdopted = false;
        evaluate.Invoke(app, new object[] { snapshot, 120f });
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Normal), "not adopted");
        data.settings.catAdopted = true;
        data.settings.autoFocusAdaptationSeconds = 0;
        evaluate.Invoke(app, new object[] { snapshot, 120f });
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Normal), "auto focus off");
        data.settings.autoFocusAdaptationSeconds = 30;
        data.settings.localBehaviorStatsEnabled = false;
        evaluate.Invoke(app, new object[] { snapshot, 120f });
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Normal), "statistics off");
        app.GetType().GetMethod("BeginTransition", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, null);
        float deadline = Time.realtimeSinceStartup + 6f;
        while (Property<CatLifeSessionPhase>(app, "CurrentPhase") == CatLifeSessionPhase.Transition && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Focus), "manual focus remains available");
    }

    [UnityTest]
    public IEnumerator InterruptConfirmationAtDeadlineCompletesExactlyOnce()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var controller = new CatLifeSessionController(data);
        controller.BeginTransition(60, now);
        controller.EnterFocus(now);
        PlayerPrefs.SetString("CatLife.Mobile.Data.v1", CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        data = (CatLifeAppData)app.GetType().GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
        data.activeSession.targetEndAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        data.activeSession.focusStartedAt = data.activeSession.targetEndAt - 60;
        data.activeSession.stableSegmentStartedAt = data.activeSession.focusStartedAt;
        var confirm = app.GetType().GetMethod("ConfirmInterrupt", BindingFlags.Instance | BindingFlags.NonPublic);
        confirm.Invoke(app, null);
        Assert.That(data.records.Count, Is.EqualTo(1));
        Assert.That(data.records[0].completed, Is.True, "Natural deadline wins over an exit confirmation in the same frame");
        Assert.That(data.records[0].ActualSeconds, Is.EqualTo(60));
        Assert.That(data.records[0].GrowthAwarded, Is.EqualTo(1));
        Assert.That(data.records[0].PawsAwarded, Is.EqualTo(1));
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Reward));
        confirm.Invoke(app, null);
        Assert.That(data.records.Count, Is.EqualTo(1), "A stale confirmation must not settle twice");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        confirm.Invoke(app,null);
        data = CatLifeDataJson.Deserialize(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"));
        Assert.That(data.records.Count, Is.EqualTo(1), "Reload and stale confirmation must not add a reward record");
        var stats = CatLifeStats.Derive(data,System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.That(stats.TotalGrowth, Is.EqualTo(1));
        Assert.That(stats.Paws, Is.EqualTo(1));
        Assert.That(data.activeSession, Is.Null);
    }

    [UnityTest]
    public IEnumerator ConfirmedClearResetsAdoptionNameAndPersistsEmptyData()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.catName = "旧名字";
        data.settings.aiConsent = true;
        data.settings.aiEnabled = true;
        data.records.Add(new CatLifeSessionRecord { completed = true, actualSeconds = 60, plannedSeconds = 60 });
        PlayerPrefs.SetString("CatLife.Mobile.Data.v1", CatLifeDataJson.Serialize(data));
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Click("SettingsButton"); Click("SettingsClear"); Click("SettingsClear");
        data = CatLifeDataJson.Deserialize(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"));
        Assert.That(data.records.Count, Is.Zero);
        Assert.That(data.activeSession, Is.Null);
        Assert.That(data.settings.catAdopted, Is.False);
        Assert.That(data.settings.aiConsent, Is.False);
        Assert.That(data.settings.aiEnabled, Is.False);
        InputField input = Object.FindObjectsByType<InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.name == "CatNameInput");
        Assert.That(input.gameObject.activeInHierarchy, Is.True);
        Assert.That(input.text, Is.EqualTo(new CatLifeAppSettings().catName), "Clearing must not leave the old cat name in the new adoption form");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        data = CatLifeDataJson.Deserialize(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"));
        Assert.That(data.records.Count, Is.Zero);
        Assert.That(data.settings.catAdopted, Is.False);
        Assert.That(data.settings.aiConsent, Is.False);
        Assert.That(data.settings.aiEnabled, Is.False);
        Click("AdoptionConfirm");
        Click("SettingsButton"); Click("SettingsAi");
        Assert.That(GameObject.Find("AiConsentPanel"), Is.Not.Null, "Clearing must require fresh consent before AI can be enabled again.");
        Click("AiConsentCancel");
        data = CatLifeDataJson.Deserialize(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"));
        Assert.That(data.settings.aiConsent, Is.False);
        Assert.That(data.settings.aiEnabled, Is.False);
    }

    [UnityTest]
    public IEnumerator ClearedDataIgnoresLateLocalAiCallback()
    {
        SeedAutoFocus();
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var data = (CatLifeAppData)app.GetType().GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
        Component client = app.GetComponent("MockCatLLMClient");
        client.GetType().GetField("preferGenericCloudWhenConfigured", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(client, false);
        client.GetType().GetField("simulatedLatencySeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(client, 1f);
        data.settings.aiEnabled = true; data.settings.aiConsent = true;
        var record = new CatLifeSessionRecord { completed = true, plannedSeconds = 60, actualSeconds = 60 };
        data.records.Add(record);
        app.GetType().GetMethod("RequestAiOnce", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(app, new object[] { record });
        Assert.That(record.aiRequestStatus, Is.EqualTo("requesting"));
        Click("SettingsButton"); Click("SettingsClear"); Click("SettingsClear");
        string clearedJson = PlayerPrefs.GetString("CatLife.Mobile.Data.v1");
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That((bool)client.GetType().GetProperty("IsBusy").GetValue(client), Is.False, "Local callback has finished, no cloud request was made");
        Assert.That(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"), Is.EqualTo(clearedJson));
        Assert.That(record.aiRequestStatus, Is.EqualTo("requesting"), "A discarded record must not be processed after data clear");
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Normal));
    }

    [UnityTest]
    public IEnumerator LeavingRewardKeepsLateLocalAdviceWithoutReopeningPageOrRetrying()
    {
        SeedAutoFocus();
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var data = (CatLifeAppData)app.GetType().GetField("data", flags).GetValue(app);
        var controller = (CatLifeSessionController)app.GetType().GetField("session", flags).GetValue(app);
        Component client = app.GetComponent("MockCatLLMClient");
        client.GetType().GetField("preferGenericCloudWhenConfigured", flags).SetValue(client, false);
        client.GetType().GetField("simulatedLatencySeconds", flags).SetValue(client, 1f);
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        controller.BeginTransition(60, now - 60);
        controller.EnterFocus(now - 60);
        Assert.That(controller.TryComplete(now, out var record), Is.True);
        data.settings.aiEnabled = true; data.settings.aiConsent = true;
        app.GetType().GetMethod("Complete", flags).Invoke(app, new object[] { record });
        Assert.That(record.aiRequestStatus, Is.EqualTo("requesting"));
        app.GetType().GetMethod("ReturnToTown", flags).Invoke(app, null);
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That((bool)client.GetType().GetProperty("IsBusy").GetValue(client), Is.False);
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("HomeHudLayer"));
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Normal));
        Assert.That(record.aiRequestStatus, Is.EqualTo("local_fallback"));
        Assert.That(record.aiSource, Is.EqualTo("local_template"));
        app.GetType().GetMethod("RequestAiOnce", flags).Invoke(app, new object[] { record });
        Assert.That(record.aiRequestCount, Is.EqualTo(1));
        Assert.That((bool)client.GetType().GetProperty("IsBusy").GetValue(client), Is.False);
        var persisted = CatLifeDataJson.Deserialize(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"));
        Assert.That(persisted.records.Count, Is.EqualTo(1));
        Assert.That(persisted.records[0].aiRequestStatus, Is.EqualTo("local_fallback"));
        Assert.That(persisted.records[0].aiRequestCount, Is.EqualTo(1));
        Assert.That(persisted.records[0].GrowthAwarded, Is.EqualTo(1));
        Assert.That(persisted.records[0].PawsAwarded, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator FailedAiClientPersistsSingleAttemptWithoutChangingReward()
    {
        SeedAutoFocus();
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var data = (CatLifeAppData)app.GetType().GetField("data", flags).GetValue(app);
        var controller = (CatLifeSessionController)app.GetType().GetField("session", flags).GetValue(app);
        Component client = app.GetComponent("MockCatLLMClient");
        client.GetType().GetField("enableClient", flags).SetValue(client, false);
        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        controller.BeginTransition(60, now - 60);
        controller.EnterFocus(now - 60);
        Assert.That(controller.TryComplete(now, out var record), Is.True);
        data.settings.aiEnabled = true;
        data.settings.aiConsent = true;
        app.GetType().GetMethod("Complete", flags).Invoke(app, new object[] { record });
        Assert.That(record.aiRequestStatus, Is.EqualTo("failed"));
        Assert.That(record.aiSource, Is.EqualTo("local_template"));
        Assert.That(record.aiReaction, Is.Null.Or.Empty);
        Assert.That((bool)client.GetType().GetProperty("IsBusy").GetValue(client), Is.False);
        app.GetType().GetMethod("RequestAiOnce", flags).Invoke(app, new object[] { record });
        Assert.That(record.aiRequestCount, Is.EqualTo(1));
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        var persisted = CatLifeDataJson.Deserialize(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"));
        Assert.That(persisted.records.Count, Is.EqualTo(1));
        Assert.That(persisted.records[0].aiRequestStatus, Is.EqualTo("failed"));
        Assert.That(persisted.records[0].aiRequestCount, Is.EqualTo(1));
        Assert.That(persisted.records[0].GrowthAwarded, Is.EqualTo(1));
        Assert.That(persisted.records[0].PawsAwarded, Is.EqualTo(1));
        Assert.That(CatLifeUnlocks.ReactionLabel("tail_wag"), Is.EqualTo("轻摆尾"));
    }

    [UnityTest]
    public IEnumerator SyntheticAiCallbackRejectsIncompleteAndUsesOnlyUnlockedActions()
    {
        // Synthetic parser/callback integration only; never a cloud-success claim.
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var clientType = System.Type.GetType("CatLife.LLM.MockCatLLMClient, Assembly-CSharp");
        string complete = "\"suggestedLine\":\"完成了这段专注\",\"gentleReminder\":\"休息一下\",\"returnSuggestion\":\"准备好再继续\",\"catReactionReason\":\"用动作回应你的完成\"";
        string response = "{"+complete+",\"recommendedLocalAction\":\"soft_roam\"}";
        string[] payloads = { "{}", "{"+complete+",\"recommendedLocalAction\":\"teleport\"}", response, response, response };
        for (int example = 0; example < payloads.Length; example++)
        {
            SeedAutoFocus();
            SceneManager.LoadScene("CatLifeMobile");
            yield return null;
            Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
            var data = (CatLifeAppData)app.GetType().GetField("data", flags).GetValue(app);
            var session = (CatLifeSessionController)app.GetType().GetField("session", flags).GetValue(app);
            data.settings.aiEnabled = false;
            long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            session.BeginTransition(60, now-60); session.EnterFocus(now-60);
            Assert.That(session.TryComplete(now, out var record), Is.True);
            app.GetType().GetMethod("Complete", flags).Invoke(app, new object[]{record});
            yield return new WaitForSeconds(5);
            Assert.That(session.TryMarkAiRequestAttempted(record, "synthetic_test_pending"), Is.True);
            // The synthetic transport callback still requires the user's current consent.
            data.settings.aiEnabled = true; data.settings.aiConsent = true;
            string clearedJson = null;
            if (example >= 3) app.GetType().GetMethod("ReturnToTown",flags).Invoke(app,null);
            if (example == 4)
            {
                Click("SettingsButton"); Click("SettingsClear"); Click("SettingsClear");
                clearedJson = PlayerPrefs.GetString("CatLife.Mobile.Data.v1");
            }
            Component client = app.GetComponent("MockCatLLMClient");
            clientType.GetProperty("LastSource").GetSetMethod(true).Invoke(client,new object[]{"mimo_cloud"});
            clientType.GetProperty("LastCloudRequestId").GetSetMethod(true).Invoke(client,new object[]{"synthetic-test-not-cloud"});
            object[] parsed = { payloads[example], null, null };
            Assert.That((bool)clientType.GetMethod("TryParseGenericCloudSuggestion").Invoke(null,parsed), Is.True);
            app.GetType().GetMethod("ApplyAiSuggestion",flags).Invoke(app,new object[]{data,record,parsed[1]});
            yield return new WaitForSeconds(6);
            if (example == 4)
            {
                Assert.That(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"), Is.EqualTo(clearedJson));
                Assert.That(record.aiRequestStatus, Is.EqualTo("requesting"));
                Assert.That(Property<CatLifeSessionPhase>(app,"CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Normal));
                Assert.That(Property<string>(app,"CurrentView"), Is.Not.EqualTo("RewardPanel"));
                continue;
            }
            Assert.That(record.aiRequestStatus, Is.EqualTo(example == 0 ? "local_fallback" : "succeeded"));
            if (example < 2) Assert.That(record.aiReaction, Is.Null.Or.Empty);
            else
            {
                Assert.That(record.aiRequestedAction, Is.EqualTo("soft_roam"));
                Assert.That(record.aiReaction, Is.EqualTo("paw_wave"), "Stretch is still locked at one paw.");
                Assert.That(record.aiActionStatus, Is.EqualTo(example == 3 ? "skipped_page_left" : "played"));
            }
            data.settings.aiEnabled = true; data.settings.aiConsent = true;
            app.GetType().GetMethod("RequestAiOnce",flags).Invoke(app,new object[]{record});
            Assert.That(record.aiRequestCount, Is.EqualTo(1));
            Assert.That((bool)clientType.GetProperty("IsBusy").GetValue(client), Is.False);
            var savedData = CatLifeDataJson.Deserialize(PlayerPrefs.GetString("CatLife.Mobile.Data.v1"));
            Assert.That(savedData.records.Count, Is.EqualTo(1));
            Assert.That(savedData.records[0].GrowthAwarded, Is.EqualTo(1));
            Assert.That(savedData.records[0].PawsAwarded, Is.EqualTo(1));
            Assert.That(savedData.records[0].aiReaction ?? "", Is.EqualTo(record.aiReaction ?? ""));
            Assert.That(Property<string>(app,"CurrentView"), Is.EqualTo(example == 3 ? "HomeHudLayer" : "RewardPanel"));
            if (example < 3)
            {
                string rendered = ((Text)app.GetType().GetField("rewardText",flags).GetValue(app)).text;
                Assert.That(rendered, Does.Contain(example == 0 ? "本地洞察 · 本次 AI 建议未生成" : "MiMo 生成"));
                if (example == 0) Assert.That(rendered, Does.Not.Contain("MiMo 生成"));
                if (example == 2) Assert.That(rendered, Does.Contain("挥爪回应").And.Not.Contain("伸个懒腰"));
            }
        }
    }

    [UnityTest]
    public IEnumerator LocalTailReviewUsesRealPresenterWithoutRecordsRewardsOrCloud()
    {
        SeedAutoFocus();
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var data = (CatLifeAppData)app.GetType().GetField("data", flags).GetValue(app);
        string before = CatLifeDataJson.Serialize(data);
        var views = (System.Collections.Generic.Dictionary<string,GameObject>)app.GetType().GetField("views",flags).GetValue(app);
        views["ReviewTail"].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Assert.That(Property<string>(app,"CurrentView"), Is.EqualTo("RewardPanel"));
        Assert.That(views["RewardTitle"].GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("本地动作预览"));
        var animator = GameObject.Find("CatLifeMobileCat").GetComponent<Animator>();
        bool seen = false;
        for (int i=0;i<60;i++)
        {
            yield return new WaitForSeconds(.2f);
            seen |= animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_RewardSideTailReturn_v01_384f");
        }
        Assert.That(seen, Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CL_CAT_StandIdle_v01_loop_96f"), Is.True);
        Assert.That(CatLifeDataJson.Serialize(data), Is.EqualTo(before));
        Assert.That((bool)app.GetComponent("MockCatLLMClient").GetType().GetProperty("IsBusy").GetValue(app.GetComponent("MockCatLLMClient")), Is.False);
        views["RewardHome"].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Assert.That(Property<string>(app,"CurrentView"), Is.EqualTo("HomeHudLayer"));
        Assert.That(views["RewardTitle"].GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("专注完成"));
    }

    [UnityTest]
    public IEnumerator AiConsentOffAndInterruptedSessionsNeverRequest()
    {
        const string key = "CatLife.Mobile.Data.v1";
        bool hadData = PlayerPrefs.HasKey(key);
        string original = PlayerPrefs.GetString(key);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            for (int scenario=0; scenario<4; scenario++)
            {
                SeedAutoFocus();
                SceneManager.LoadScene("CatLifeMobile");
                yield return null;
                Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
                var data = (CatLifeAppData)app.GetType().GetField("data",flags).GetValue(app);
                var controller = (CatLifeSessionController)app.GetType().GetField("session",flags).GetValue(app);
                data.settings.aiEnabled = scenario != 0 && scenario != 2;
                data.settings.aiConsent = scenario != 1 && scenario != 2;
                long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                controller.BeginTransition(60,now-60); controller.EnterFocus(now-60);
                CatLifeSessionRecord record;
                if (scenario == 3) record = controller.Interrupt(now-30);
                else
                {
                    Assert.That(controller.TryComplete(now,out record), Is.True);
                    app.GetType().GetMethod("Complete",flags).Invoke(app,new object[]{record});
                }
                // Even an erroneous repeated caller must not send an ineligible session.
                app.GetType().GetMethod("RequestAiOnce",flags).Invoke(app,new object[]{record});
                yield return new WaitForSeconds(.2f);
                var client = app.GetComponent("MockCatLLMClient");
                Assert.That((string)client.GetType().GetProperty("LastSource").GetValue(client), Is.EqualTo("not_requested"), $"scenario {scenario}");
                Assert.That((bool)client.GetType().GetProperty("IsBusy").GetValue(client), Is.False);
                Assert.That(record.aiRequestCount, Is.Zero);
                Assert.That(record.aiRequestAttempted, Is.False);
                Assert.That(record.aiReaction, Is.Null.Or.Empty);
                Assert.That(record.GrowthAwarded, Is.EqualTo(scenario==3 ? 0 : 1));
                Assert.That(record.PawsAwarded, Is.EqualTo(scenario==3 ? 0 : 1));
            }
        }
        finally
        {
            if (hadData) PlayerPrefs.SetString(key,original); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    private static void SeedAutoFocus()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        data.settings.localBehaviorStatsEnabled = true;
        data.settings.autoFocusAdaptationSeconds = 30;
        PlayerPrefs.SetString("CatLife.Mobile.Data.v1", CatLifeDataJson.Serialize(data));
    }

    private static void Click(string name)
    {
        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (button.name == name && button.gameObject.scene == SceneManager.GetActiveScene()) { button.onClick.Invoke(); return; }
        Assert.Fail("Button not found: " + name);
    }

    private static T Property<T>(Component component, string name) { return (T)component.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public).GetValue(component); }
}
