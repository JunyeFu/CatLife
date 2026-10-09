using CatLife.Mobile;
using NUnit.Framework;

public sealed class CatLifeExitGestureTests
{
    [Test]
    public void FastDragMustContactThenContinueWithoutJump()
    {
        var gesture = new CatLifeExitGesture();
        gesture.Begin(0, true, false);
        gesture.Step(.9f, 0);
        Assert.That(gesture.Progress, Is.EqualTo(.35f));
        Assert.That(gesture.InterventionShown, Is.True);
        gesture.Step(.9f, .4);
        Assert.That(gesture.Phase, Is.EqualTo(CatLifeExitGesturePhase.Holding));
        gesture.Step(1.01f, .5);
        Assert.That(gesture.Phase, Is.EqualTo(CatLifeExitGesturePhase.Released));
        Assert.That(gesture.Progress, Is.EqualTo(.35f));
        gesture.Step(1.32f, .6);
        Assert.That(gesture.Progress, Is.GreaterThan(.65f));
        Assert.That(gesture.End(), Is.True);
        Assert.That(gesture.End(), Is.False);
    }

    [Test]
    public void HoldExpiresAtTotalOnePointTwoSecondsEvenWithLateFrame()
    {
        var gesture = new CatLifeExitGesture();
        gesture.Begin(0, true, false);
        gesture.Step(.4f, 10);
        gesture.Step(.9f, 11.21);
        Assert.That(gesture.Phase, Is.EqualTo(CatLifeExitGesturePhase.Released));
        Assert.That(gesture.Progress, Is.EqualTo(.35f));
        Assert.That(gesture.End(), Is.False);
    }

    [Test]
    public void CancelAfterContactRetainsSingleInterventionButEarlyProbeDoesNot()
    {
        var gesture = new CatLifeExitGesture();
        gesture.Begin(0, true, false);
        gesture.Step(.2f, 0);
        Assert.That(gesture.Phase, Is.EqualTo(CatLifeExitGesturePhase.Reaching));
        gesture.Cancel();
        Assert.That(gesture.InterventionShown, Is.False);
        gesture.Begin(0, true, false);
        gesture.Step(.4f, 1);
        Assert.That(gesture.End(), Is.False);
        gesture.Begin(0, true, gesture.InterventionShown);
        gesture.Step(.7f, 2);
        Assert.That(gesture.Phase, Is.EqualTo(CatLifeExitGesturePhase.Dragging));
        Assert.That(gesture.End(), Is.True);
    }

    [Test]
    public void DisabledInterventionTracksRelativeDistanceAndCancelsOnBackDrag()
    {
        var gesture = new CatLifeExitGesture();
        gesture.Begin(.8f, false, false);
        gesture.Step(.8f, 0);
        Assert.That(gesture.Progress, Is.Zero);
        gesture.Step(1.5f, 1);
        Assert.That(gesture.Progress, Is.GreaterThan(.65f));
        gesture.Step(1f, 2);
        Assert.That(gesture.End(), Is.False);
        Assert.That(gesture.InterventionShown, Is.False);
    }
}
