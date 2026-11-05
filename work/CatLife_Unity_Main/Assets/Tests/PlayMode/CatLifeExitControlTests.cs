using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CatLifeExitControlTests
{
    const string Key = "CatLife.Mobile.Data.v1";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    string saved;
    bool hadData;
    Component app;
    CatLifeAppData data;
    CatLifeSessionController session;
    Dictionary<string, GameObject> views;
    RenderTexture target;
    PointerEventData pointer;
    RectTransform track;
    Vector2 start, delta;
    [SetUp] public void Preserve() { hadData = PlayerPrefs.HasKey(Key); saved = PlayerPrefs.GetString(Key); }
    [UnityTearDown] public IEnumerator Restore()
    {
        var scene = SceneManager.GetSceneByName("CatLifeExitCandidate");
        if (scene.isLoaded)
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("ExitControlsCleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
        if (hadData) PlayerPrefs.SetString(Key, saved); else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
    void Click(string name) => views[name].GetComponent<Button>().onClick.Invoke();
    IEnumerator LoadCandidate()
    {
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/Editor/CatProneReview/ExitCandidate/CatLifeExitCandidate.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return new WaitForSeconds(1);
        app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        data = (CatLifeAppData)app.GetType().GetField("data", Private).GetValue(app);
        session = (CatLifeSessionController)app.GetType().GetField("session", Private).GetValue(app);
        var cat = GameObject.Find("CatLifeMobileCat");
        var skin = cat.GetComponentInChildren<SkinnedMeshRenderer>();
        var root = (GameObject)app.GetType().GetField("viewRoot", Private).GetValue(app);
        views = (Dictionary<string, GameObject>)app.GetType().GetField("views", Private).GetValue(app);
        var canvas = root.GetComponent<Canvas>();
        root.GetComponent("CatLifeMobileTouchLayout").GetType().GetMethod("SetReviewDensity").Invoke(root.GetComponent("CatLifeMobileTouchLayout"), new object[] { 2.625f });
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera.main; canvas.planeDistance = .5f;
        root.GetComponent<CanvasScaler>().enabled = false; canvas.scaleFactor = Mathf.Sqrt(2424f / 2400);
        var safe = (RectTransform)views["SafeArea"].transform;
        ((MonoBehaviour)safe.GetComponent("CatLifeSafeArea")).enabled = false;
        safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
        if (target == null) target = new RenderTexture(1080, 2424, 24);
        Camera.main.targetTexture = target; Camera.main.aspect = 1080f / 2424;
        if (session.Phase == CatLifeSessionPhase.Focus)
        {
            app.GetType().GetMethod("ShowFocus", Private).Invoke(app, new object[] { true });
        }
        else app.GetType().GetMethod("ShowHome", Private, null, new[] { typeof(bool) }, null).Invoke(app, new object[] { true });
        yield return new WaitForSeconds(.3f);
        if (session.Phase == CatLifeSessionPhase.Focus)
            Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("CL_CAT_ProneJointCorrective_v11")),
                Is.GreaterThan(99), "A cold-started candidate must restore the full accepted Focus corrective.");
#else
        yield break;
#endif
    }
    IEnumerator StartFocus()
    {
        Click("StartButton"); Click("SetupStart");
        float until = Time.realtimeSinceStartup + 8;
        while (session.Phase != CatLifeSessionPhase.Focus && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(session.Phase, Is.EqualTo(CatLifeSessionPhase.Focus));
        yield return new WaitForSeconds(.5f);
    }
    void Down()
    {
        Canvas.ForceUpdateCanvases();
        track = (RectTransform)views["SwipeTrack"].transform;
        start = RectTransformUtility.WorldToScreenPoint(Camera.main, views["SwipeHandle"].transform.position);
        delta = RectTransformUtility.WorldToScreenPoint(Camera.main, track.TransformPoint(Vector3.right * track.rect.width * .84f))
            - RectTransformUtility.WorldToScreenPoint(Camera.main, track.position);
        pointer = new PointerEventData(EventSystem.current) { pointerId = 81, position = start,
            pointerPressRaycast = new RaycastResult { module = track.GetComponentInParent<GraphicRaycaster>() } };
        ExecuteEvents.Execute(track.gameObject, pointer, ExecuteEvents.pointerDownHandler);
    }
    void Drag(float amount) { pointer.position = start + delta * amount; ExecuteEvents.Execute(track.gameObject, pointer, ExecuteEvents.dragHandler); }
    void Up(float amount) { pointer.position = start + delta * amount; ExecuteEvents.Execute(track.gameObject, pointer, ExecuteEvents.pointerUpHandler); }
    IEnumerator Snap(string tag)
    {
        var probe = new GameObject("Exit controls late snapshot").AddComponent<CatTerrainLateProbe>();
        bool pending = true;
        probe.sample = () =>
        {
            if (!pending) return;
            pending = false;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp04-controls-cold-20261017/" + tag));
            CatLifeExitActionTests.Capture(Camera.main, folder, 0, 1080, 2424);
        };
        while (pending) yield return null;
        probe.sample = null; UnityEngine.Object.Destroy(probe.gameObject);
    }
    [UnityTest]
    public IEnumerator NewExitControlsMeet48DpAcrossReviewProfiles()
    {
        var seed = new CatLifeAppData();
        seed.settings.catAdopted = true; seed.settings.defaultMinutes = 15;
        seed.settings.aiEnabled = false; seed.settings.localBehaviorStatsEnabled = false;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(seed));
        yield return LoadCandidate();
        var root = (GameObject)app.GetType().GetField("viewRoot", Private).GetValue(app);
        var canvas = root.GetComponent<Canvas>();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp04-touch-matrix-fixed-20261017"));
        Directory.CreateDirectory(output);
        var rows = new StringBuilder("profile,control,widthPx,heightPx,widthDp,heightDp\n");
        int small = 0;
        foreach (var profile in new[] { (name:"Portrait420", width:1080, height:2424, density:2.625f), (name:"Wide320", width:720, height:1280, density:2f) })
        {
            var oldTarget = target; target = new RenderTexture(profile.width, profile.height, 24);
            Camera.main.targetTexture = target; Camera.main.aspect = (float)profile.width / profile.height;
            oldTarget.Release(); UnityEngine.Object.Destroy(oldTarget);
            canvas.scaleFactor = Mathf.Sqrt((profile.width/1080f)*(profile.height/2400f));
            root.GetComponent("CatLifeMobileTouchLayout").GetType().GetMethod("SetReviewDensity").Invoke(root.GetComponent("CatLifeMobileTouchLayout"), new object[] { profile.density });
            Click("SettingsButton");
            if (data.settings.catExitInterventionEnabled) Click("SettingsExitIntervention");
            yield return new WaitForSeconds(.15f);
            Measure("SettingsExitIntervention");
            CatLifeExitActionTests.Capture(Camera.main, Path.Combine(output, profile.name + "-settings"), 0, profile.width, profile.height);
            Click("SettingsBack"); yield return StartFocus();
            Down(); Drag(.75f); Up(.75f);
            yield return new WaitForSeconds(.1f);
            foreach (string name in new[] { "SwipeHandle", "InterruptConfirm", "InterruptCancel" }) Measure(name);
            CatLifeExitActionTests.Capture(Camera.main, Path.Combine(output, profile.name + "-confirm"), 0, profile.width, profile.height);
            Click("InterruptConfirm");
            void Measure(string name)
            {
                var corners = new Vector3[4]; ((RectTransform)views[name].transform).GetWorldCorners(corners);
                Vector2 min = Camera.main.WorldToScreenPoint(corners[0]), max = Camera.main.WorldToScreenPoint(corners[2]);
                Vector2 size = max-min;
                float widthDp = size.x/profile.density, heightDp = size.y/profile.density;
                if (widthDp < 47.9f || heightDp < 47.9f) small++;
                rows.AppendLine(FormattableString.Invariant($"{profile.name},{name},{size.x:F3},{size.y:F3},{widthDp:F3},{heightDp:F3}"));
            }
        }
        File.WriteAllText(Path.Combine(output, "touch-sizes.csv"), rows.ToString());
        Assert.That(small, Is.Zero, "The new setting, start handle and confirmation buttons must each provide at least 48dp.");
    }
    [UnityTest]
    public IEnumerator ExitSettingsScrollKeepsControlsReachable()
    {
        var seed = new CatLifeAppData();
        seed.settings.catAdopted = true; seed.settings.aiEnabled = false; seed.settings.localBehaviorStatsEnabled = false;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(seed));
        yield return LoadCandidate();
        var root = (GameObject)app.GetType().GetField("viewRoot", Private).GetValue(app);
        var canvas = root.GetComponent<Canvas>();
        var scroll = views["SettingsSheet"].GetComponent<ScrollRect>();
        var safe = (RectTransform)views["SafeArea"].transform;
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp04-settings-scroll-20261017"));
        Directory.CreateDirectory(output);
        foreach (var profile in new[] { (name:"Portrait420", width:1080, height:2424, density:2.625f), (name:"Wide320", width:720, height:1280, density:2f) })
        {
            var previous = target; target = new RenderTexture(profile.width, profile.height, 24);
            Camera.main.targetTexture = target; Camera.main.aspect = (float)profile.width / profile.height;
            previous.Release(); UnityEngine.Object.Destroy(previous);
            canvas.scaleFactor = Mathf.Sqrt((profile.width/1080f)*(profile.height/2400f));
            root.GetComponent("CatLifeMobileTouchLayout").GetType().GetMethod("SetReviewDensity").Invoke(root.GetComponent("CatLifeMobileTouchLayout"), new object[] { profile.density });
            // Synthetic insets exercise the existing SafeArea parent, not an Android safe-area claim.
            safe.offsetMin = new Vector2(0, 24*profile.density/canvas.scaleFactor);
            safe.offsetMax = new Vector2(0, -32*profile.density/canvas.scaleFactor);
            Click("SettingsButton"); yield return new WaitForSeconds(.2f);
            scroll.verticalNormalizedPosition = 1; yield return null;
            foreach (string name in new[] { "SettingsDuration", "SettingsReminder", "SettingsAutoFocus", "SettingsExitIntervention", "SettingsAi", "SettingsBehavior", "SettingsRecords", "SettingsClear" })
            {
                var rect = (RectTransform)views[name].transform;
                Assert.That(rect.rect.height*canvas.scaleFactor/profile.density, Is.GreaterThanOrEqualTo(47.9f), name);
            }
            bool enabled = data.settings.catExitInterventionEnabled;
            Tap("SettingsExitIntervention");
            Assert.That(data.settings.catExitInterventionEnabled, Is.EqualTo(!enabled));
            var scrollEvent = new PointerEventData(EventSystem.current) { position = Camera.main.WorldToScreenPoint(scroll.viewport.position), scrollDelta = new Vector2(0, -scroll.content.rect.height*2) };
            ExecuteEvents.ExecuteHierarchy(scroll.viewport.gameObject, scrollEvent, ExecuteEvents.scrollHandler);
            yield return new WaitForSeconds(.15f);
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0).Within(.001f));
            var clearRect = (RectTransform)views["SettingsClear"].transform;
            var corners = new Vector3[4]; clearRect.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var local = (Vector2)scroll.viewport.InverseTransformPoint(corner);
                Assert.That(scroll.viewport.rect.Contains(local), Is.True, "The complete clear button must be visible after scrolling.");
            }
            Tap("SettingsClear");
            Assert.That(views["SettingsClearText"].GetComponent<Text>().text, Is.EqualTo("再次点击确认清除"));
            Assert.That(data, Is.SameAs(app.GetType().GetField("data", Private).GetValue(app)), "One click only arms clearing.");
            CatLifeExitActionTests.Capture(Camera.main, Path.Combine(output, profile.name + "-bottom"), 0, profile.width, profile.height);
            Tap("SettingsBack");
            Assert.That(data.settings.catAdopted, Is.True);
        }
        void Tap(string name)
        {
            Canvas.ForceUpdateCanvases();
            var input = new PointerEventData(EventSystem.current) { pointerId = 83, position = Camera.main.WorldToScreenPoint(views[name].transform.position), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(input, hits);
            Assert.That(hits.Count, Is.GreaterThan(0), name);
            var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(receiver, Is.EqualTo(views[name]), "Visible controls must receive the actual top raycast: " + name);
            ExecuteEvents.Execute(receiver, input, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(receiver, input, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(receiver, input, ExecuteEvents.pointerClickHandler);
        }
    }
    [UnityTest]
    public IEnumerator ContinuousExitGestureReview()
    {
        var seed = new CatLifeAppData();
        seed.settings.catAdopted = true; seed.settings.defaultMinutes = 15;
        seed.settings.aiEnabled = false; seed.settings.localBehaviorStatsEnabled = false;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(seed));
        yield return LoadCandidate(); yield return StartFocus();
        long deadline = data.activeSession.targetEndAt;
        var root = (GameObject)app.GetType().GetField("viewRoot", Private).GetValue(app);
        var canvas = root.GetComponent<Canvas>();
        var oldTarget = target;
        target = new RenderTexture(540, 1212, 24);
        Camera.main.targetTexture = target; Camera.main.aspect = 540f / 1212;
        oldTarget.Release(); UnityEngine.Object.Destroy(oldTarget);
        canvas.scaleFactor = Mathf.Sqrt((540f / 1080) * (1212f / 2400));
        root.GetComponent("CatLifeMobileTouchLayout").GetType().GetMethod("SetReviewDensity").Invoke(root.GetComponent("CatLifeMobileTouchLayout"), new object[] { 1.3125f });
        var marker = new GameObject("SyntheticPointerForReviewOnly", typeof(RectTransform), typeof(Image));
        marker.transform.SetParent(views["TransientLayer"].transform, false);
        var markerRect = (RectTransform)marker.transform;
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(.5f, .5f); markerRect.sizeDelta = new Vector2(64, 64);
        var markerImage = marker.GetComponent<Image>();
        markerImage.sprite = views["SwipeHandle"].GetComponent<Image>().sprite;
        markerImage.color = new Color(.15f, .7f, 1f, .7f); markerImage.raycastTarget = false; marker.SetActive(false);
        var caption = new GameObject("ReviewScopeCaption", typeof(RectTransform), typeof(Text));
        caption.transform.SetParent(views["TransientLayer"].transform, false);
        var captionRect = (RectTransform)caption.transform;
        captionRect.anchorMin = new Vector2(.04f, .965f); captionRect.anchorMax = new Vector2(.96f, .995f);
        captionRect.offsetMin = captionRect.offsetMax = Vector2.zero;
        var captionText = caption.GetComponent<Text>();
        captionText.font = views["FocusTitle"].GetComponent<Text>().font; captionText.fontSize = 22;
        captionText.text = "Unity 候选 · 模拟触控（非模拟器）"; captionText.color = Color.white;
        captionText.alignment = TextAnchor.MiddleLeft; captionText.raycastTarget = false;
        yield return new WaitForSeconds(.1f);
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Reports/wp04-continuous-hint-20261017"));
        Directory.CreateDirectory(Path.Combine(output, "frames"));
        var timestamps = new List<double>();
        var rows = new StringBuilder("frame,seconds,progress,gesture,animation,prompt,confirm,hint\n");
        int releaseHintFrames = 0, hintMismatches = 0;
        var image = new Texture2D(540, 1212, TextureFormat.RGB24, false);
        var probe = new GameObject("Continuous exit late-frame capture").AddComponent<CatTerrainLateProbe>();
        var swipe = views["SwipeTrack"].GetComponent("CatLifeSwipeToEnd");
        var presenter = GameObject.Find("CatLifeMobileCat").GetComponent("CatLifeMobileCatPresenter");
        double started = Time.realtimeSinceStartupAsDouble;
        probe.sample = () =>
        {
            double elapsed = Time.realtimeSinceStartupAsDouble - started;
            Canvas.ForceUpdateCanvases(); Camera.main.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 540, 1212), 0, 0); image.Apply(); RenderTexture.active = previous;
            int frame = timestamps.Count;
            File.WriteAllBytes(Path.Combine(output, "frames", $"{frame:D6}.png"), image.EncodeToPNG());
            timestamps.Add(elapsed);
            float progress = (float)swipe.GetType().GetProperty("Progress").GetValue(swipe);
            string hint = views["SwipeLabel"].GetComponent<Text>().text;
            if (progress >= .65f) releaseHintFrames++;
            if (hint != (progress >= .65f ? "松开确认结束" : "继续右滑可结束")) hintMismatches++;
            rows.AppendLine(FormattableString.Invariant($"{frame},{elapsed:F6},{progress},{swipe.GetType().GetProperty("GesturePhase").GetValue(swipe)},{presenter.GetType().GetProperty("LastAnimationCommand").GetValue(presenter)},{views["ExitInterventionPrompt"].activeSelf},{views["ExitConfirm"].activeSelf},{hint}"));
        };
        bool pressed = false, lifted = false, cancelled = false, second = false, secondCancelled = false;
        double duration = 0;
        try
        {
            while ((duration = Time.realtimeSinceStartupAsDouble - started) < 6)
            {
                float t = (float)duration;
                if (t >= .5f && !pressed) { Down(); marker.SetActive(true); pressed = true; }
                if (pressed && !lifted)
                {
                    float amount = t < 1.3f ? .5f * Mathf.Clamp01((t-.5f)/.8f) :
                        t < 1.7f ? .5f : Mathf.Lerp(.5f, 1f, Mathf.Clamp01((t-1.7f)/1f));
                    Drag(amount);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)marker.transform.parent, pointer.position, Camera.main, out var markerPoint);
                    markerRect.anchoredPosition = markerPoint;
                    if (t >= 2.8f) { Up(1); marker.SetActive(false); lifted = true; Assert.That(views["ExitConfirm"].activeSelf, Is.True); }
                }
                if (t >= 3.7f && !cancelled) { Click("InterruptCancel"); cancelled = true; }
                if (t >= 4.1f && !second)
                {
                    Down(); Drag(.75f); Up(.75f); second = true;
                    Assert.That(views["ExitConfirm"].activeSelf, Is.True); Assert.That(views["ExitInterventionPrompt"].activeSelf, Is.False);
                }
                if (t >= 4.8f && !secondCancelled) { Click("InterruptCancel"); secondCancelled = true; }
                yield return null;
            }
            Assert.That(timestamps.Count, Is.GreaterThan(20));
            Assert.That(secondCancelled, Is.True);
            Assert.That(data.activeSession.targetEndAt, Is.EqualTo(deadline));
            Assert.That(data.activeSession.exitInterventionShown, Is.True);
            Assert.That(data.records.Count, Is.Zero);
            Assert.That(releaseHintFrames, Is.GreaterThan(0)); Assert.That(hintMismatches, Is.Zero);
            Debug.Log($"EXIT_CONTINUOUS frames={timestamps.Count} duration={duration:F3} realLateFrames=true input=synthetic noAwards=true releaseHintFrames={releaseHintFrames} hintMismatches={hintMismatches}");
        }
        finally
        {
            probe.sample = null; UnityEngine.Object.Destroy(probe.gameObject); UnityEngine.Object.Destroy(image);
            File.WriteAllText(Path.Combine(output, "frames.csv"), rows.ToString());
            var concat = new StringBuilder("ffconcat version 1.0\n");
            for (int frame = 0; frame < timestamps.Count; frame++)
            {
                double end = frame + 1 < timestamps.Count ? timestamps[frame + 1] : duration;
                concat.Append(FormattableString.Invariant($"file 'frames/{frame:D6}.png'\noption framerate 1000\nduration {end-timestamps[frame]:F6}\n"));
            }
            if (timestamps.Count > 0) concat.Append($"file 'frames/{timestamps.Count-1:D6}.png'\noption framerate 1000\n");
            File.WriteAllText(Path.Combine(output, "frames.ffconcat"), concat.ToString());
        }
    }
    [UnityTest]
    public IEnumerator SettingPersistsAndPromptOnlyAppearsForTheFirstEnabledContact()
    {
        var seed = new CatLifeAppData();
        seed.settings.catAdopted = true; seed.settings.defaultMinutes = 15;
        seed.settings.aiEnabled = false; seed.settings.localBehaviorStatsEnabled = false;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(seed));
        yield return LoadCandidate();
        Click("SettingsButton");
        Assert.That(views["SettingsExitInterventionValue"].GetComponent<Text>().text, Is.EqualTo("开启"));
        Click("SettingsExitIntervention");
        Assert.That(views["SettingsExitInterventionValue"].GetComponent<Text>().text, Is.EqualTo("关闭"));
        Assert.That(CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key)).settings.catExitInterventionEnabled, Is.False);
        Assert.That(data.settings.aiEnabled || data.settings.localBehaviorStatsEnabled, Is.False);
        yield return Snap("settings-off");
        Click("SettingsBack"); yield return StartFocus();
        Down(); Drag(.75f); Up(.75f);
        Assert.That(views["ExitConfirm"].activeSelf, Is.True);
        Assert.That(views["ExitInterventionPrompt"].activeSelf, Is.False);
        Assert.That(data.activeSession.exitInterventionShown, Is.False);
        Click("InterruptConfirm");
        Assert.That(data.records.Count, Is.EqualTo(1));
        Assert.That(data.records[0].GrowthAwarded + data.records[0].PawsAwarded, Is.Zero);
        Click("SettingsButton"); Click("SettingsExitIntervention");
        Assert.That(views["SettingsExitInterventionValue"].GetComponent<Text>().text, Is.EqualTo("开启"));
        Assert.That(CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key)).settings.catExitInterventionEnabled, Is.True);
        Assert.That(data.settings.aiEnabled || data.settings.localBehaviorStatsEnabled, Is.False);
        yield return Snap("settings-on");
        Click("SettingsBack"); yield return StartFocus();
        long deadline = data.activeSession.targetEndAt;
        Down(); Drag(.5f);
        Assert.That(data.activeSession.exitInterventionShown, Is.True);
        Assert.That(views["ExitInterventionPrompt"].activeSelf, Is.True);
        Assert.That(views["ExitInterventionPromptText"].GetComponent<Text>().text, Is.EqualTo("再陪你专注一会儿？"));
        Assert.That(views["ExitInterventionPrompt"].GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), Is.True);
        yield return new WaitForSecondsRealtime(.45f);
        yield return Snap("prompt");
        yield return new WaitForSecondsRealtime(2.05f);
        Assert.That(views["ExitInterventionPrompt"].activeSelf, Is.False);
        Assert.That(data.activeSession.targetEndAt, Is.EqualTo(deadline));
        Up(.5f);
        yield return new WaitForSeconds(.5f);
        Down(); Drag(.75f); Up(.75f);
        Assert.That(views["ExitConfirm"].activeSelf, Is.True);
        Assert.That(views["ExitInterventionPrompt"].activeSelf, Is.False);
        Click("InterruptCancel");
        yield return Snap("prompt-ended");
        yield return LoadCandidate();
        Assert.That(data.activeSession.targetEndAt, Is.EqualTo(deadline));
        Assert.That(data.activeSession.exitInterventionShown, Is.True);
        Assert.That(views["ExitInterventionPrompt"].activeSelf, Is.False);
        Down(); Drag(.75f); Up(.75f);
        Assert.That(views["ExitConfirm"].activeSelf, Is.True);
        Assert.That(views["ExitInterventionPrompt"].activeSelf, Is.False);
        Click("InterruptCancel");
        yield return Snap("reloaded");
        Assert.That(data.records.Count, Is.EqualTo(1));
        Assert.That(data.records[0].completed, Is.False);
        Assert.That(data.records[0].aiRequestCount, Is.Zero);
        Debug.Log("EXIT_CONTROLS offDirect=true settingSaved=true aiAndStatsUnchanged=true promptOnce=true promptExpired=true reloadDirect=true interruptedAwards=0");
    }
}
