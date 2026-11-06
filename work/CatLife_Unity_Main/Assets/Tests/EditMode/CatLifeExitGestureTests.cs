using CatLife.Mobile;
using NUnit.Framework;

public sealed class CatLifeExitGestureTests
{
    [Test]
    public void FastDragMustContactThenContinueWithoutJump()
    {
        var gesture = new CatLifeExitGesture(1200, 80);
        gesture.Begin(0, 0, true, false);
        gesture.Step(.9f, .05);
        Assert.That(gesture.Progress, Is.EqualTo(.35f));
        Assert.That(gesture.InterventionShown, Is.True);
        gesture.Step(.9f, .451);
        Assert.That(gesture.Phase, Is.EqualTo(CatLifeExitGesturePhase.Holding));
        gesture.Step(1.01f, .55);
        Assert.That(gesture.Phase, Is.EqualTo(CatLifeExitGesturePhase.Released));
        Assert.That(gesture.Progress, Is.EqualTo(.35f));
        gesture.Step(1.35f, .75);
        Assert.That(gesture.Progress, Is.GreaterThan(.65f));
        Assert.That(gesture.End(), Is.True);
        Assert.That(gesture.End(), Is.False);
    }

    [Test]
    public void HoldExpiresAtTotalOnePointTwoSecondsEvenWithLateFrame()
    {
        var gesture = new CatLifeExitGesture(1200, 80);
        gesture.Begin(0, 10, true, false);
        gesture.Step(.9f, 10.05);
        gesture.Step(.9f, 11.251);
        Assert.That(gesture.Phase, Is.EqualTo(CatLifeExitGesturePhase.Released));
        Assert.That(gesture.Progress, Is.EqualTo(.35f));
        Assert.That(gesture.End(), Is.False);
    }

    [Test]
    public void CancelAfterContactRetainsSingleInterventionButEarlyProbeDoesNot()
    {
        var gesture = new CatLifeExitGesture(1200, 80);
        gesture.Begin(0, 0, true, false);
        gesture.Step(.2f, .1);
        Assert.That(gesture.Phase, Is.EqualTo(CatLifeExitGesturePhase.Reaching));
        gesture.Cancel();
        Assert.That(gesture.InterventionShown, Is.False);
        gesture.Begin(0, 1, true, false);
        gesture.Step(.9f, 1.05);
        Assert.That(gesture.End(), Is.False);
        gesture.Step(0, 1.9);
        gesture.Begin(0, 2, true, gesture.InterventionShown);
        gesture.Step(.7f, 2.5);
        Assert.That(gesture.Phase, Is.EqualTo(CatLifeExitGesturePhase.Dragging));
        Assert.That(gesture.End(), Is.True);
    }

    [Test]
    public void DisabledInterventionTracksRelativeDistanceAndCancelsOnBackDrag()
    {
        var gesture = new CatLifeExitGesture(1200, 80);
        gesture.Begin(.8f, 0, false, false);
        gesture.Step(.8f, 0);
        Assert.That(gesture.Progress, Is.Zero);
        gesture.Step(1.5f, 1);
        Assert.That(gesture.Progress, Is.GreaterThan(.65f));
        gesture.Step(1f, 2);
        Assert.That(gesture.End(), Is.False);
        Assert.That(gesture.InterventionShown, Is.False);
    }

    [TestCase(10)] [TestCase(30)] [TestCase(60)]
    public void StepAndReturnAreMonotonicAtDifferentFrameRates(int fps)
    {
        var gesture = new CatLifeExitGesture(1200, 80);
        gesture.Begin(0, 0, false, false);
        gesture.Step(.9f, 0);
        Assert.That(gesture.Progress, Is.Zero, "A same-frame target update must not teleport the handle.");
        float last = 0;
        for (int frame = 1; frame <= fps; frame++)
        {
            gesture.Step(.9f, (double)frame / fps);
            Assert.That(gesture.Progress, Is.InRange(last, .9f)); last = gesture.Progress;
        }
        Assert.That(gesture.End(), Is.True); Assert.That(gesture.IsSettling, Is.True);
        for (int frame = 1; frame <= fps; frame++)
        {
            gesture.Step(99, 1 + (double)frame / fps);
            Assert.That(gesture.Progress, Is.InRange(0, last)); last = gesture.Progress;
        }
        Assert.That(gesture.Progress, Is.Zero); Assert.That(gesture.IsSettling, Is.False);
    }

    [TestCase(.649f, false)] [TestCase(.65f, true)]
    public void EndUsesTheVisibleThresholdWithoutAdvancingToAnUnseenTarget(float target, bool expected)
    {
        var gesture = new CatLifeExitGesture(1200, 80);
        gesture.Begin(0, 0, false, false); gesture.Step(target, 10);
        Assert.That(gesture.Progress, Is.EqualTo(target));
        Assert.That(gesture.End(), Is.EqualTo(expected));
        Assert.That(gesture.Progress, Is.EqualTo(target), "Return starts from what was visible at release.");
        Assert.That(gesture.End(), Is.False, "A second UP cannot confirm again.");
    }

    [Test]
    public void RegrabbingTheReturningHandleKeepsPositionAndCancelClearsVelocity()
    {
        var gesture = new CatLifeExitGesture(1200, 80);
        gesture.Begin(0, 0, false, false); gesture.Step(.6f, .3); gesture.End(); gesture.Step(0, .32);
        float visible = gesture.Progress;
        gesture.Begin(.4f, .32, false, false); gesture.Step(.4f, .32);
        Assert.That(gesture.Progress, Is.EqualTo(visible));
        gesture.Step(-10, .34); Assert.That(gesture.Progress, Is.InRange(0, visible));
        gesture.Cancel(); gesture.Step(100, 50);
        Assert.That(gesture.Progress, Is.Zero); Assert.That(gesture.IsSettling, Is.False);
        Assert.That(gesture.Phase, Is.EqualTo(CatLifeExitGesturePhase.Idle));
    }
}
