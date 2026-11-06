using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CatLifeExitPriorityTests
{
    const string Key = "CatLife.Mobile.Data.v1";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Reach = "CL_CAT_ProneExitReach_v01_24f";
    const string Hold = "CL_CAT_ProneExitHold_v01_loop_48f";
    const string Rest = "CL_CAT_ProneBreath_v11_loop_240f";
    const string Wake = "CL_CAT_LieToStand_v11_120f";
    string saved;
    bool hadData;
    Component app, presenter, swipe;
    CatLifeAppData data;
    CatLifeSessionController session;
    Dictionary<string, GameObject> views;
    Animator animator;
    RectTransform track;
    PointerEventData pointer;
    Vector2 start, delta;
    [SetUp] public void Preserve() { hadData = PlayerPrefs.HasKey(Key); saved = PlayerPrefs.GetString(Key); }
    [UnityTearDown] public IEnumerator Restore()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.isLoaded)
        {
            SceneManager.SetActiveScene(SceneManager.CreateScene("ExitPriorityCleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        if (hadData) PlayerPrefs.SetString(Key, saved); else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
    IEnumerator Load(bool candidate = true)
    {
        var seedData = new CatLifeAppData();
        seedData.settings.catAdopted = true;
        seedData.settings.aiEnabled = false;
        var seed = new CatLifeSessionController(seedData);
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        seed.BeginTransition(900, now); seed.EnterFocus(now);
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(seedData));
        if (candidate)
        {
            var installer = Type.GetType("CatLifeUiStyleInstaller, Assembly-CSharp-Editor");
            yield return (AsyncOperation)installer.GetMethod("LoadReviewForCapture").Invoke(null, null);
        }
        else SceneManager.LoadScene("CatLifeMobile");
        yield return new WaitForSeconds(3);
        app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        data = (CatLifeAppData)app.GetType().GetField("data", Private).GetValue(app);
        session = (CatLifeSessionController)app.GetType().GetField("session", Private).GetValue(app);
        views = (Dictionary<string, GameObject>)app.GetType().GetField("views", Private).GetValue(app);
        var cat = GameObject.Find("CatLifeMobileCat");
        presenter = cat.GetComponent("CatLifeMobileCatPresenter");
        animator = cat.GetComponent<Animator>();
        track = (RectTransform)views["SwipeTrack"].transform;
        swipe = track.GetComponent("CatLifeSwipeToEnd");
        Canvas.ForceUpdateCanvases();
        start = RectTransformUtility.WorldToScreenPoint(null, views["SwipeHandle"].transform.position);
        delta = RectTransformUtility.WorldToScreenPoint(null, track.TransformPoint(Vector3.right * track.rect.width * .84f))
            - RectTransformUtility.WorldToScreenPoint(null, track.position);
        pointer = new PointerEventData(EventSystem.current) { pointerId = 73, position = start };
    }
    void Down() { pointer.position = start; ExecuteEvents.Execute(track.gameObject, pointer, ExecuteEvents.pointerDownHandler); }
    void Drag(float progress) { pointer.position = start + delta * progress; ExecuteEvents.Execute(track.gameObject, pointer, ExecuteEvents.dragHandler); }
    void Up(float progress) { pointer.position = start + delta * progress; ExecuteEvents.Execute(track.gameObject, pointer, ExecuteEvents.pointerUpHandler); }
    CatLifeExitGesturePhase Gesture => (CatLifeExitGesturePhase)swipe.GetType().GetProperty("GesturePhase").GetValue(swipe);
    float Progress => (float)swipe.GetType().GetProperty("Progress").GetValue(swipe);
    IEnumerator WaitForPhase(CatLifeExitGesturePhase expected)
    {
        double until = Time.realtimeSinceStartupAsDouble + 1.6;
        while (Gesture != expected && Time.realtimeSinceStartupAsDouble < until) yield return null;
        Assert.That(Gesture, Is.EqualTo(expected));
    }
    IEnumerator WaitForProgress(float minimum)
    {
        double until = Time.realtimeSinceStartupAsDouble + .5;
        while (Progress < minimum && Time.realtimeSinceStartupAsDouble < until) yield return null;
        Assert.That(Progress, Is.GreaterThanOrEqualTo(minimum));
    }
    bool Playing => (bool)presenter.GetType().GetProperty("IsExitGesturePlaying").GetValue(presenter);
    void InvokePrivate(Component component, string method, params object[] args) => component.GetType().GetMethod(method, Private).Invoke(component, args);

    [UnityTest] public IEnumerator NaturalCompletionWinsDuringPreparation() => CompletionWins(CatLifeExitGesturePhase.Preparing);
    [UnityTest] public IEnumerator NaturalCompletionWinsDuringHolding() => CompletionWins(CatLifeExitGesturePhase.Holding);
    [UnityTest] public IEnumerator NaturalCompletionWinsDuringRelease() => CompletionWins(CatLifeExitGesturePhase.Released);
    IEnumerator CompletionWins(CatLifeExitGesturePhase phase)
    {
        yield return Load();
        Down(); Drag(.5f);
        yield return WaitForPhase(CatLifeExitGesturePhase.Preparing);
        Assert.That(data.activeSession.exitInterventionShown, Is.True, "Must use the actual App binding, not a test-only subscriber.");
        if (phase != CatLifeExitGesturePhase.Preparing) yield return WaitForPhase(phase);
        Assert.That(Gesture, Is.EqualTo(phase));
        // Synthetic clock arrangement only: make a one-minute session due in the actual App.Update.
        long now = (long)app.GetType().GetMethod("SessionNow", Private).Invoke(app, null);
        data.activeSession.plannedSeconds = 60;
        data.activeSession.focusStartedAt = now - 60;
        data.activeSession.stableSegmentStartedAt = now - 60;
        data.activeSession.targetEndAt = now;
        InvokePrivate(app, "Update");
        Assert.That(session.Phase, Is.EqualTo(CatLifeSessionPhase.Reward));
        Assert.That(Gesture, Is.EqualTo(CatLifeExitGesturePhase.Idle));
        Assert.That(Playing, Is.False);
        Assert.That(views["ExitConfirm"].activeSelf, Is.False);
        Assert.That(views["RewardCard"].activeSelf, Is.False);
        Assert.That(animator.speed, Is.EqualTo(1));
        yield return new WaitForSeconds(.25f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(Wake), Is.True);
        // Old input and the old confirmation button cannot turn a completed session into an interruption.
        Up(1);
        views["InterruptConfirm"].GetComponent<Button>().onClick.Invoke();
        float until = Time.realtimeSinceStartup + 4;
        while (!views["RewardCard"].activeSelf && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(views["RewardCard"].activeSelf, Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(Rest), Is.False, "No late release coroutine may restore Focus over Reward.");
        Assert.That(data.records.Count, Is.EqualTo(1));
        Assert.That(data.records[0].completed, Is.True);
        Assert.That(data.records[0].GrowthAwarded, Is.EqualTo(1));
        Assert.That(data.records[0].PawsAwarded, Is.EqualTo(1));
        Assert.That(data.records[0].aiRequestCount, Is.Zero);
        var reloaded = CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key));
        Assert.That(reloaded.activeSession, Is.Null);
        Assert.That(reloaded.records.Count, Is.EqualTo(1));
        Debug.Log($"EXIT_PRIORITY phase={phase} completion=1 growth=1 paws=1 lateInputIgnored=true aiRequests=0");
    }

    [UnityTest]
    public IEnumerator PauseCancelsThePoseAndOldPointerWithoutResettingTheSession()
    {
        yield return Load();
        long deadline = data.activeSession.targetEndAt;
        Down(); Drag(.5f);
        yield return WaitForPhase(CatLifeExitGesturePhase.Holding);
        yield return null; // Observe the Animator after it has evaluated the new phase.
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(Hold), Is.True);
        InvokePrivate(app, "OnApplicationPause", true);
        InvokePrivate(swipe, "OnApplicationPause", true);
        Up(1);
        InvokePrivate(app, "OnApplicationPause", false);
        InvokePrivate(swipe, "OnApplicationPause", false);
        yield return new WaitForSeconds(.25f);
        Assert.That(Gesture, Is.EqualTo(CatLifeExitGesturePhase.Idle));
        Assert.That(Playing, Is.False);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(Rest), Is.True);
        Assert.That(views["ExitConfirm"].activeSelf, Is.False);
        Assert.That(data.activeSession.targetEndAt, Is.EqualTo(deadline));
        Assert.That(data.activeSession.backgroundCount, Is.EqualTo(1));
        Assert.That(data.activeSession.exitInterventionShown, Is.True);
        Assert.That(data.records.Count, Is.Zero);
        var reloaded = CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key));
        Assert.That(new CatLifeSessionController(reloaded).CanShowExitIntervention, Is.False);
        Assert.That(reloaded.activeSession.targetEndAt, Is.EqualTo(deadline));
        Down(); Drag(.75f); yield return WaitForProgress(.65f); Up(.75f);
        Assert.That(views["ExitConfirm"].activeSelf, Is.True, "The next attempt must remain available without another block.");
        Debug.Log("EXIT_PAUSE cancelled=true deadlineUnchanged=true consumedAfterReload=true nextAttemptDirect=true");
    }

    [UnityTest]
    public IEnumerator DraggingBackThenForwardCancelsTheOldReleaseOwner()
    {
        yield return Load();
        Down(); Drag(.25f);
        yield return WaitForPhase(CatLifeExitGesturePhase.Reaching);
        var probe = new GameObject("Exit preparation ownership probe").AddComponent<CatTerrainLateProbe>();
        int ownershipLosses = 0;
        probe.sample = () =>
        {
            if (Gesture == CatLifeExitGesturePhase.Preparing && (!Playing ||
                (string)presenter.GetType().GetProperty("LastAnimationCommand").GetValue(presenter) != Reach)) ownershipLosses++;
        };
        Drag(.1f); yield return WaitForPhase(CatLifeExitGesturePhase.Dragging);
        Drag(.5f); yield return WaitForPhase(CatLifeExitGesturePhase.Preparing);
        Assert.That(Gesture, Is.EqualTo(CatLifeExitGesturePhase.Preparing));
        yield return new WaitForSeconds(.12f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(Reach), Is.True,
            "The old release coroutine must not restore Rest after a new preparation owns the Animator.");
        Assert.That(Playing, Is.True);
        yield return new WaitForSecondsRealtime(.34f);
        probe.sample = null;
        UnityEngine.Object.Destroy(probe.gameObject);
        Assert.That(ownershipLosses, Is.Zero, "No actual late frame may lose the new preparation to the old release owner.");
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(Hold), Is.True);
        Assert.That(data.activeSession.exitInterventionShown, Is.True);
        Up(.5f);
        yield return new WaitForSeconds(.6f);
        Assert.That(Playing, Is.False);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(Rest), Is.True);
    }

    [UnityTest]
    public IEnumerator SameFrameReleaseDoesNotJumpToTheFullyExtendedPose()
    {
        yield return Load();
        Down(); Drag(.25f); Up(.25f);
        yield return new WaitForSeconds(.03f);
        var state = animator.GetCurrentAnimatorStateInfo(0);
        Assert.That(state.IsName(Rest), Is.True, "A zero-time drag has no visible reach to reverse.");
        Assert.That(Playing, Is.False);
        Down(); Drag(.3f); yield return WaitForProgress(.24f);
        float releaseStart = 1 - Mathf.InverseLerp(.15f, .35f, Progress);
        Up(.3f); yield return null;
        state = animator.GetCurrentAnimatorStateInfo(0);
        Assert.That(state.IsName("CL_CAT_ProneExitRelease_v01_24f"), Is.True);
        Assert.That(state.normalizedTime, Is.GreaterThanOrEqualTo(releaseStart - .01f),
            "Visible reaching must reverse from its matching release phase.");
        yield return new WaitForSeconds(.5f);
        Assert.That(Playing, Is.False);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(Rest), Is.True);
        Assert.That(data.activeSession.exitInterventionShown, Is.False);
        Down(); Drag(.5f); Up(.5f);
        yield return new WaitForSeconds(.05f);
        var skin = animator.GetComponentInChildren<SkinnedMeshRenderer>();
        Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("CL_CAT_ProneExitReachCorrective_v01")),
            Is.LessThan(.01f), "An immediately cancelled fast drag must not render a fully reached paw first.");
        Debug.Log("EXIT_SAME_FRAME halfReleaseMatched=true immediateFastCancelNoExtension=true");
    }

    [UnityTest]
    public IEnumerator SceneUnloadDuringAnActiveGestureDoesNotCallDestroyedPresenter()
    {
        yield return Load();
        Down(); Drag(.5f);
        yield return WaitForPhase(CatLifeExitGesturePhase.Preparing);
        Assert.That(Playing, Is.True);
        // UnityTearDown unloads the actual scene while the gesture is still active.
    }

    [UnityTest]
    public IEnumerator EarlyReleaseReturnsToRestAndTheOriginalModelKeepsDirectSliding()
    {
        yield return Load();
        Down(); Drag(.25f);
        yield return WaitForPhase(CatLifeExitGesturePhase.Reaching);
        yield return null;
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(Reach), Is.True);
        Assert.That(animator.speed, Is.Zero);
        Up(.25f);
        yield return new WaitForSeconds(.6f);
        Assert.That(Playing, Is.False);
        Assert.That(animator.speed, Is.EqualTo(1));
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName(Rest), Is.True);
        Assert.That(session.CanShowExitIntervention, Is.True);
        Assert.That(data.activeSession.exitInterventionShown, Is.False);
        yield return Load(false);
        Assert.That((bool)presenter.GetType().GetProperty("CanBeginExitGesture").GetValue(presenter), Is.False);
        Down(); Drag(.75f); yield return WaitForProgress(.65f); Up(.75f);
        Assert.That(views["ExitConfirm"].activeSelf, Is.True);
        Assert.That(data.activeSession.exitInterventionShown, Is.False);
        Debug.Log("EXIT_EARLY_RETURN rest=true markerNotConsumed=true originalModelDirect=true");
    }
}
