using System;
using System.Collections;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

public sealed class CatLifeSwipeLifecycleTests
{
    private GameObject root;
    private Component swipe;
    private int confirmations;
    private int interactions;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("SwipeLifecycleTest", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(1000, 200);
        swipe = root.AddComponent(Type.GetType("CatLifeSwipeToEnd, Assembly-CSharp"));
        confirmations = interactions = 0;
        swipe.GetType().GetEvent("ConfirmRequested").AddEventHandler(swipe, (Action)(() => confirmations++));
        swipe.GetType().GetEvent("InteractionRecorded").AddEventHandler(swipe, (Action)(() => interactions++));
    }

    [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(root); }
    private void Send(string method, int id, float x) => swipe.GetType().GetMethod(method).Invoke(swipe,
        new object[] { new PointerEventData(null) { pointerId = id, position = new Vector2(x, 0) } });
    private float Progress => (float)swipe.GetType().GetProperty("Progress").GetValue(swipe);
    private IEnumerator WaitForProgress(float minimum)
    {
        double until = Time.realtimeSinceStartupAsDouble + .5;
        while (Progress < minimum && Time.realtimeSinceStartupAsDouble < until) yield return null;
        Assert.That(Progress, Is.GreaterThanOrEqualTo(minimum));
    }

    [UnityTest]
    public IEnumerator SecondPointerCannotTakeOverOrConfirmAndReleaseConfirmsOnlyOnce()
    {
        Send("OnPointerDown", 1, 0);
        Send("OnPointerDown", 2, 500);
        Send("OnDrag", 2, 1000);
        Send("OnPointerUp", 2, 1000);
        Assert.That(Progress, Is.Zero);
        Assert.That(interactions, Is.Zero);
        Send("OnDrag", 1, 600);
        yield return WaitForProgress(.65f);
        Assert.That(Progress, Is.GreaterThan(.65f));
        Assert.That(confirmations, Is.Zero);
        Send("OnPointerUp", 1, 600);
        Send("OnPointerUp", 1, 600);
        Assert.That(confirmations, Is.EqualTo(1));
        Assert.That(interactions, Is.EqualTo(1));
        Assert.That((bool)swipe.GetType().GetProperty("IsSettling").GetValue(swipe), Is.True);
        yield return new WaitForSecondsRealtime(.6f);
        Assert.That(Progress, Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisablePauseAndFocusLossCancelWithoutConfirmation()
    {
        foreach (string method in new[] { "OnDisable", "OnApplicationPause", "OnApplicationFocus" })
        {
            Send("OnPointerDown", 1, 0);
            Send("OnDrag", 1, 600);
            yield return WaitForProgress(.2f);
            swipe.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(swipe,
                method == "OnDisable" ? null : new object[] { method == "OnApplicationPause" });
            Send("OnPointerUp", 1, 600);
            Assert.That(Progress, Is.Zero);
            Assert.That(confirmations, Is.Zero);
            Assert.That(interactions, Is.Zero);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator ReleaseUsesTheVisibleProgressAndNeverAnUnseenPointerPosition()
    {
        Send("OnPointerDown", 1, 0);
        Send("OnDrag", 1, .649f * 840);
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(Progress, Is.LessThan(.65f));
        Send("OnPointerUp", 1, 900);
        Assert.That(confirmations, Is.Zero);
        Assert.That(interactions, Is.EqualTo(1));
        yield return new WaitForSecondsRealtime(.6f);
        Send("OnPointerDown", 1, 0); Send("OnDrag", 1, 600);
        yield return WaitForProgress(.65f);
        Send("OnPointerUp", 1, 100);
        Assert.That(confirmations, Is.EqualTo(1), "An UP event without a rendered reverse drag must use the last visible handle.");
    }

    [UnityTest]
    public IEnumerator StationaryPointerReleasesAtTheLimitAndOnlyConsumesOnce()
    {
        int interventions = 0;
        swipe.GetType().GetMethod("ConfigureIntervention").Invoke(swipe, new object[] { (Func<bool>)(() => interventions == 0) });
        swipe.GetType().GetEvent("InterventionStarted").AddEventHandler(swipe, (Action)(() => interventions++));
        Send("OnPointerDown", 1, 0);
        Send("OnDrag", 1, 420);
        yield return WaitForProgress(.35f);
        Assert.That(Progress, Is.EqualTo(.35f).Within(.0001));
        Assert.That((CatLifeExitGesturePhase)swipe.GetType().GetProperty("GesturePhase").GetValue(swipe), Is.EqualTo(CatLifeExitGesturePhase.Preparing));
        yield return new WaitForSecondsRealtime(.45f);
        Assert.That((CatLifeExitGesturePhase)swipe.GetType().GetProperty("GesturePhase").GetValue(swipe), Is.EqualTo(CatLifeExitGesturePhase.Holding));
        yield return new WaitForSecondsRealtime(.85f);
        Assert.That((CatLifeExitGesturePhase)swipe.GetType().GetProperty("GesturePhase").GetValue(swipe), Is.EqualTo(CatLifeExitGesturePhase.Released));
        Assert.That(Progress, Is.EqualTo(.35f).Within(.0001));
        Assert.That(interventions, Is.EqualTo(1));
        Send("OnPointerUp", 1, 420);
        Assert.That(confirmations, Is.Zero);
        Send("OnPointerDown", 1, 0);
        Send("OnDrag", 1, 600);
        yield return WaitForProgress(.65f);
        Send("OnPointerUp", 1, 600);
        Assert.That(confirmations, Is.EqualTo(1));
        Assert.That(interventions, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator FocusLossCancelsAnActiveInterventionWithoutLateConfirmation()
    {
        int cancelled = 0;
        swipe.GetType().GetMethod("ConfigureIntervention").Invoke(swipe, new object[] { (Func<bool>)(() => true) });
        swipe.GetType().GetEvent("GestureCancelled").AddEventHandler(swipe, (Action)(() => cancelled++));
        Send("OnPointerDown", 7, 0); Send("OnDrag", 7, 420);
        yield return WaitForProgress(.35f);
        swipe.GetType().GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(swipe, new object[] { false });
        Send("OnPointerUp", 7, 1000);
        Assert.That(cancelled, Is.EqualTo(1));
        Assert.That(confirmations, Is.Zero);
        Assert.That(interactions, Is.Zero);
        Assert.That(Progress, Is.Zero);
        yield return null;
    }
}
