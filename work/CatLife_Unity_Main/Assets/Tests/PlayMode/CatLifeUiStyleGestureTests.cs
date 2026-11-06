using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Current serialized r42 scene, with the real App/paw view/Presenter and synthetic pointer events.
public sealed class CatLifeUiStyleGestureTests
{
    private const string Key = "CatLife.Mobile.Data.v1";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private bool had;
    private string saved;
    private Component swipe, app;
    private RectTransform track, handle;
    private Image fill;
    private PointerEventData pointer;
    private Vector2 start, delta;
    private CatLifeAppData data;
    private static Transform View(string name) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(t => t.name == name);
    private float Progress => (float)swipe.GetType().GetProperty("Progress").GetValue(swipe);
    private bool Settling => (bool)swipe.GetType().GetProperty("IsSettling").GetValue(swipe);
    private CatLifeExitGesturePhase Phase => (CatLifeExitGesturePhase)swipe.GetType().GetProperty("GesturePhase").GetValue(swipe);
    [SetUp] public void Preserve() { had = PlayerPrefs.HasKey(Key); saved = PlayerPrefs.GetString(Key); }
    [TearDown] public void Restore()
    { if (had) PlayerPrefs.SetString(Key, saved); else PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    private IEnumerator Load()
    {
        var seed = new CatLifeAppData(); seed.settings.catAdopted = true; seed.settings.aiEnabled = false;
        seed.settings.autoFocusAdaptationSeconds = 0;
        var session = new CatLifeSessionController(seed);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        session.BeginTransition(900, now); session.EnterFocus(now);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(seed));
        var installer = Type.GetType("CatLifeUiStyleInstaller, Assembly-CSharp-Editor");
        yield return (AsyncOperation)installer.GetMethod("LoadReviewForCapture").Invoke(null, null);
        yield return new WaitForSecondsRealtime(1);
        app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        data = (CatLifeAppData)app.GetType().GetField("data", Private).GetValue(app);
        track = (RectTransform)View("SwipeTrack"); handle = (RectTransform)View("SwipeHandle");
        swipe = track.GetComponent("CatLifeSwipeToEnd");
        fill = (Image)swipe.GetType().GetField("fill", Private).GetValue(swipe);
        Assert.That(fill.sprite.name, Is.EqualTo("S_CL_UI_Style_SkinSwipeFill"));
        Assert.That(fill.color, Is.EqualTo(Color.white));
        Assert.That(View("ExitInterventionPrompt").GetComponent<Image>().sprite.name, Is.EqualTo("S_CL_UI_Style_SkinCard"));
        Canvas.ForceUpdateCanvases(); yield return null;
        start = RectTransformUtility.WorldToScreenPoint(null, handle.position);
        delta = RectTransformUtility.WorldToScreenPoint(null, track.TransformPoint(Vector3.right * track.rect.width * .84f))
            - RectTransformUtility.WorldToScreenPoint(null, track.position);
        pointer = new PointerEventData(EventSystem.current) { pointerId = 271, position = start };
        Assert.That(swipe.GetType().GetField("springStiffness", Private).GetValue(swipe), Is.EqualTo(1200f));
        Assert.That(swipe.GetType().GetField("springDamping", Private).GetValue(swipe), Is.EqualTo(80f));
    }
    private void Down() => ExecuteEvents.Execute(track.gameObject, pointer, ExecuteEvents.pointerDownHandler);
    private void Drag(float amount) { pointer.position = start + delta * amount; ExecuteEvents.Execute(track.gameObject, pointer, ExecuteEvents.dragHandler); }
    private void Up(float amount) { pointer.position = start + delta * amount; ExecuteEvents.Execute(track.gameObject, pointer, ExecuteEvents.pointerUpHandler); }
    private IEnumerator UntilPhase(CatLifeExitGesturePhase phase)
    {
        double until = Time.realtimeSinceStartupAsDouble + 1.7;
        while (Phase != phase && Time.realtimeSinceStartupAsDouble < until) yield return null;
        Assert.That(Phase, Is.EqualTo(phase));
    }

    [UnityTest]
    public IEnumerator CurrentSceneUsesOneProgressAndFreezesTheTrackThroughReturn()
    {
        var builder = Type.GetType("CatLifePreviewSceneBuilder, Assembly-CSharp-Editor");
        builder.GetMethod("SetGameViewFixedResolution", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { 1080, 2424 });
        yield return Load();
        string output = Environment.GetEnvironmentVariable("CATLIFE_UI_CAPTURE_DIR");
        var rows = new StringBuilder("seconds,phase,progress,fill,handleAnchor,trackX,trackY,settling\n");
        var probe = new GameObject("Current exit layout observer").AddComponent<CatTerrainLateProbe>();
        double started = Time.unscaledTimeAsDouble;
        Vector3 frozen = track.position;
        int mismatches = 0, observed = 0;
        probe.sample = () =>
        {
            float progress = Progress;
            if (Mathf.Abs(fill.fillAmount - progress) > .00001f || Mathf.Abs(handle.anchorMin.x - Mathf.Lerp(.08f, .92f, progress)) > .00001f) mismatches++;
            if (Phase != CatLifeExitGesturePhase.Idle || Settling)
                if (Vector3.Distance(track.position, frozen) > .0001f) mismatches++;
            rows.AppendLine(FormattableString.Invariant($"{Time.unscaledTimeAsDouble-started:F6},{Phase},{progress:R},{fill.fillAmount:R},{handle.anchorMin.x:R},{track.position.x:R},{track.position.y:R},{Settling}"));
            observed++;
        };
        try
        {
            long deadline = data.activeSession.targetEndAt;
            Down(); Drag(.5f); yield return UntilPhase(CatLifeExitGesturePhase.Preparing);
            Assert.That(Progress, Is.EqualTo(.35f));
            Assert.That(data.activeSession.exitInterventionShown, Is.True);
            double contactAt = Time.unscaledTimeAsDouble;
            yield return UntilPhase(CatLifeExitGesturePhase.Holding);
            Assert.That(Progress, Is.EqualTo(.35f));
            yield return Capture(output, "gesture-contact");
            yield return UntilPhase(CatLifeExitGesturePhase.Released);
            Assert.That(Time.unscaledTimeAsDouble - contactAt, Is.InRange(1.15, 1.4), "Total includes preparation; it must not become 1.6 seconds.");
            Assert.That(Progress, Is.EqualTo(.35f));
            Drag(.98f);
            double until = Time.unscaledTimeAsDouble + .5;
            while (Progress < .65f && Time.unscaledTimeAsDouble < until) yield return null;
            Assert.That(Progress, Is.GreaterThanOrEqualTo(.65f));
            yield return Capture(output, "gesture-visible-confirm");
            float beforeUp = Progress;
            Up(2); Assert.That(Progress, Is.EqualTo(beforeUp));
            Assert.That(View("ExitConfirm").gameObject.activeSelf, Is.True);
            Assert.That(Settling, Is.True);
            yield return Capture(output, "gesture-return");
            View("InterruptCancel").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(Progress, Is.Zero); Assert.That(Settling, Is.False);
            Assert.That(data.activeSession.targetEndAt, Is.EqualTo(deadline));
            Assert.That(data.records.Count, Is.Zero);
            Assert.That(observed, Is.GreaterThan(10)); Assert.That(mismatches, Is.Zero);
            Debug.Log($"UI_STYLE_GESTURE actualFrames={observed} progressMismatches={mismatches} deadlineUnchanged=true records=0 input=synthetic");
        }
        finally
        {
            probe.sample = null; UnityEngine.Object.Destroy(probe.gameObject);
            if (!string.IsNullOrEmpty(output)) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "gesture-frames.csv"), rows.ToString()); }
        }
    }

    [UnityTest]
    public IEnumerator CurrentSceneDeadlineCancelsAVisibleInterventionAndSettlesOnlyOnce()
    {
        yield return Load();
        Down(); Drag(.5f); yield return UntilPhase(CatLifeExitGesturePhase.Holding);
        long now = (long)app.GetType().GetMethod("SessionNow", Private).Invoke(app, null);
        data.activeSession.plannedSeconds = 60; data.activeSession.focusStartedAt = now - 60;
        data.activeSession.stableSegmentStartedAt = now - 60; data.activeSession.targetEndAt = now;
        app.GetType().GetMethod("Update", Private).Invoke(app, null);
        Up(2); View("InterruptConfirm").GetComponent<Button>().onClick.Invoke();
        Assert.That(Progress, Is.Zero); Assert.That(Settling, Is.False);
        Assert.That(View("ExitConfirm").gameObject.activeSelf, Is.False);
        Assert.That(data.records.Count, Is.EqualTo(1));
        Assert.That(data.records[0].completed, Is.True);
        Assert.That(data.records[0].GrowthAwarded, Is.EqualTo(1));
        Assert.That(data.records[0].PawsAwarded, Is.EqualTo(1));
        Assert.That(data.records[0].aiRequestCount, Is.Zero);
        Assert.That(data.activeSession, Is.Null);
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(data.records.Count, Is.EqualTo(1));
    }
    private static IEnumerator Capture(string output, string name)
    {
        if (string.IsNullOrEmpty(output)) yield break;
        Directory.CreateDirectory(output); string path = Path.Combine(output, name + ".png");
        ScreenCapture.CaptureScreenshot(path);
        double until = Time.realtimeSinceStartupAsDouble + 10;
        while (!File.Exists(path) && Time.realtimeSinceStartupAsDouble < until) yield return null;
        Assert.That(File.Exists(path), Is.True); yield return null;
    }
}
