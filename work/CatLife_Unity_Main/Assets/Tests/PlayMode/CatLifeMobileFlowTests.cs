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

public sealed class CatLifeMobileFlowTests
{
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
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        MethodInfo evaluate = app.GetType().GetMethod("EvaluateAutoFocus", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(evaluate, Is.Not.Null);
        System.Type snapshotType = System.Type.GetType("CatLife.Recognition.RecognitionSnapshot, Assembly-CSharp");
        object snapshot = snapshotType.GetMethod("CreateDefault", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        snapshotType.GetField("focusConfidence").SetValue(snapshot, .82f);
        snapshotType.GetField("attentionBand").SetValue(snapshot, System.Enum.Parse(snapshotType.GetField("attentionBand").FieldType, "Stable"));

        evaluate.Invoke(app, new object[] { snapshot, 15f });
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
        PlayerPrefs.DeleteKey("CatLife.Mobile.Data.v1");
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        MethodInfo evaluate = app.GetType().GetMethod("EvaluateAutoFocus", BindingFlags.Instance | BindingFlags.Public);
        System.Type snapshotType = System.Type.GetType("CatLife.Recognition.RecognitionSnapshot, Assembly-CSharp");
        object snapshot = snapshotType.GetMethod("CreateDefault", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        snapshotType.GetField("focusConfidence").SetValue(snapshot, .82f);
        snapshotType.GetField("attentionBand").SetValue(snapshot, System.Enum.Parse(snapshotType.GetField("attentionBand").FieldType, "Stable"));

        evaluate.Invoke(app, new object[] { snapshot, 15f });
        yield return new WaitForSecondsRealtime(2.1f);

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
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component features = GameObject.Find("CatLifeRuntimeSystems").GetComponent("RealtimeFeatureEngine");

        Click("TitleButton");
        features.GetType().GetMethod("Tick").Invoke(features, new object[] { 0f });
        yield return null;

        object latest = features.GetType().GetProperty("Latest").GetValue(features);
        Assert.That((float)latest.GetType().GetField("tapRate1s").GetValue(latest), Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator FocusSwipeProducesSanitizedAggregateEvent()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Component features = GameObject.Find("CatLifeRuntimeSystems").GetComponent("RealtimeFeatureEngine");
        Transform swipeTransform = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(item => item.name == "SwipeTrack");
        Component swipe = swipeTransform.GetComponent("CatLifeSwipeToEnd");
        swipe.GetType().GetMethod("OnPointerUp").Invoke(swipe, new object[] { new PointerEventData(EventSystem.current) });

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
        Assert.That((Vector3)home.GetType().GetProperty("Position").GetValue(home), Is.EqualTo(new Vector3(.1f, 1.9f, 1.2f)));
        Assert.That((float)focus.GetType().GetProperty("Fov").GetValue(focus), Is.EqualTo(72f));
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
        yield return new WaitForSecondsRealtime(2.1f);
        Assert.That(Property<CatLifeSessionPhase>(app, "CurrentPhase"), Is.EqualTo(CatLifeSessionPhase.Focus));
        Assert.That(Property<string>(app, "CurrentView"), Is.EqualTo("FocusPanel"));
    }

    [UnityTest]
    public IEnumerator SceneContainsStableLandmarkEntrypointsAndAnimatorStates()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;
        Assert.That(GameObject.Find("CL_BLD_TomatoClockTower_01").GetComponent("CatLifeLandmark"), Is.Not.Null);
        Assert.That(GameObject.Find("CL_BLD_CatHouse_01").GetComponent("CatLifeLandmark"), Is.Not.Null);
        Animator animator = GameObject.Find("CatLifeMobileCat").GetComponentInChildren<Animator>();
        Assert.That(animator.applyRootMotion, Is.False);
        Assert.That(GameObject.Find("CatLifeMobileCat").GetComponentsInChildren<SkinnedMeshRenderer>().Single().updateWhenOffscreen, Is.True);
        Assert.That(animator.HasState(0, Animator.StringToHash("Base Layer.CL_CAT_IdleBreath_v06_headsync_loop_108f")), Is.True);
        Assert.That(animator.HasState(0, Animator.StringToHash("Base Layer.CL_CAT_FocusRest_v01_loop_96f")), Is.True);
    }

    [UnityTest]
    public IEnumerator MobileSceneWiresRecognitionThroughSingleRuntimeSystemsRoot()
    {
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
        coordinator.GetType().GetMethod("ApplyPhase").Invoke(coordinator, new object[] { CatLifeSessionPhase.Focus });
        object snapshot = features.GetType().GetProperty("Latest").GetValue(features);
        Assert.That((bool)snapshot.GetType().GetField("isFocusSessionActive").GetValue(snapshot), Is.True);
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
        Animator animator = cat.GetComponentInChildren<Animator>();
        Assert.That(animator.HasState(0, Animator.StringToHash("Base Layer.CL_CAT_SRC_Walk_60fps")), Is.True);

        Component planner = cat.GetComponent("CatDestinationPlanner");
        System.Type snapshotType = System.Type.GetType("CatLife.Recognition.RecognitionSnapshot, Assembly-CSharp");
        object snapshot = snapshotType.GetMethod("CreateDefault", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
        MethodInfo requestedPlan = planner.GetType().GetMethods().Single(method => method.Name == "TryPlanRequestedPoint");
        object[] planArgs = { snapshot, GameObject.Find("Interest_Left_Garden").transform.position, cat.transform.position, Vector3.zero };
        Assert.That((bool)requestedPlan.Invoke(planner, planArgs), Is.True, "Approved interest point was rejected by CatDestinationPlanner.");

        Vector3 start = cat.transform.position;
        float deadline = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < deadline && Vector3.Distance(start, cat.transform.position) < .2f)
            yield return null;

        float moved = Vector3.Distance(start, cat.transform.position);
        Component driver = cat.GetComponent("CatBehaviorDriver");
        string state = driver.GetType().GetProperty("CurrentState").GetValue(driver).ToString();
        string path = (string)cat.GetComponent("CatNavigationAgent").GetType().GetProperty("PathStatusText").GetValue(cat.GetComponent("CatNavigationAgent"));
        Assert.That(moved, Is.GreaterThanOrEqualTo(.2f), $"state={state}; path={path}; moved={moved:F3}");
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

    [UnityTest]
    public IEnumerator MobileUiBackgroundsDoNotBlockWorldInteractionRaycasts()
    {
        SceneManager.LoadScene("CatLifeMobile");
        yield return null;

        foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
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
            Vector3 point = GameObject.Find(pointName).transform.position;
            Assert.That(Vector3.Distance(home, point), Is.LessThanOrEqualTo(2.25f));
            Assert.That((bool)planner.GetType().GetMethod("IsPointInPreferredCameraRange").Invoke(planner, new object[] { point }), Is.True, pointName + " is outside the fixed camera safe range.");
            Vector3 viewport = Camera.main.WorldToViewportPoint(point + Vector3.up * .28f);
            Assert.That(viewport.x, Is.InRange(.15f, .75f), pointName + " is outside the visible horizontal activity band.");
            Assert.That(viewport.y, Is.InRange(.25f, .7f), pointName + " is outside the visible vertical activity band.");
        }
    }

    private static void Click(string name)
    {
        foreach (Button button in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (button.name == name) { button.onClick.Invoke(); return; }
        Assert.Fail("Button not found: " + name);
    }

    private static T Property<T>(Component component, string name) { return (T)component.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public).GetValue(component); }
}
