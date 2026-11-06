using CatLife.Mobile;
using NUnit.Framework;

public sealed class CatLifeExitPersistenceTests
{
    [TestCase(0d, CatLifeExitGesturePhase.Preparing)]
    [TestCase(.4d, CatLifeExitGesturePhase.Holding)]
    [TestCase(1.21d, CatLifeExitGesturePhase.Released)]
    public void DeadlineWinsDuringEveryInterventionPhase(double elapsed, CatLifeExitGesturePhase expected)
    {
        var data = new CatLifeAppData();
        var session = new CatLifeSessionController(data);
        session.BeginTransition(60, 100); session.EnterFocus(102);
        var gesture = new CatLifeExitGesture(1200, 80);
        gesture.Begin(0, 0, session.CanShowExitIntervention, false);
        gesture.Step(.9f, .1);
        Assert.That(session.MarkExitInterventionShown(), Is.True);
        if (elapsed > 0) gesture.Step(.9f, .1 + elapsed);
        Assert.That(gesture.Phase, Is.EqualTo(expected));
        Assert.That(data.activeSession.targetEndAt, Is.EqualTo(162));
        Assert.That(session.TryComplete(162, out var record), Is.True);
        gesture.Cancel(); // Required when the future view leaves Focus at the deadline.
        Assert.That(gesture.End(), Is.False);
        Assert.That(session.TryComplete(163, out _), Is.False);
        Assert.That(record.GrowthAwarded, Is.EqualTo(1));
        Assert.That(record.PawsAwarded, Is.EqualTo(1));
        data = CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(data));
        Assert.That(data.records.Count, Is.EqualTo(1));
        Assert.That(data.activeSession, Is.Null);
    }

    [Test]
    public void LegacySettingsDefaultEnabledAndExplicitDisabledSurvivesReload()
    {
        var data = CatLifeDataJson.Deserialize("{\"records\":[],\"settings\":{\"defaultMinutes\":15,\"aiEnabled\":false}}");
        Assert.That(data.settings.catExitInterventionEnabled, Is.True);
        Assert.That(data.settings.defaultMinutes, Is.EqualTo(15));
        data.settings.catExitInterventionEnabled = false;
        data = CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(data));
        Assert.That(data.settings.catExitInterventionEnabled, Is.False);
    }

    [Test]
    public void ContactPersistsAcrossReloadWithoutChangingDeadlineOrReward()
    {
        var data = new CatLifeAppData();
        var session = new CatLifeSessionController(data);
        Assert.That(session.MarkExitInterventionShown(), Is.False);
        session.BeginTransition(60, 100);
        Assert.That(session.MarkExitInterventionShown(), Is.False);
        session.EnterFocus(102);
        Assert.That(session.MarkExitInterventionShown(), Is.True);
        Assert.That(session.MarkExitInterventionShown(), Is.False);
        data = CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(data));
        session = new CatLifeSessionController(data);
        Assert.That(data.activeSession.exitInterventionShown, Is.True);
        Assert.That(session.CanShowExitIntervention, Is.False);
        Assert.That(session.RemainingSeconds(130), Is.EqualTo(32));
        Assert.That(session.TryComplete(162, out var record), Is.True);
        Assert.That(record.GrowthAwarded, Is.EqualTo(1));
        Assert.That(record.PawsAwarded, Is.EqualTo(1));
        Assert.That(data.records.Count, Is.EqualTo(1));
        session.BeginTransition(60, 200);
        session.EnterFocus(202);
        Assert.That(session.CanShowExitIntervention, Is.True);
    }

    [Test]
    public void DisabledInterventionDoesNotConsumeContactAndIgnoresAiAndStatisticsSwitches()
    {
        var data = new CatLifeAppData();
        data.settings.aiEnabled = false;
        data.settings.localBehaviorStatsEnabled = false;
        data.settings.catExitInterventionEnabled = false;
        var session = new CatLifeSessionController(data);
        session.BeginTransition(60, 0);
        session.EnterFocus(2);
        Assert.That(session.MarkExitInterventionShown(), Is.False);
        Assert.That(data.activeSession.exitInterventionShown, Is.False);
        data.settings.catExitInterventionEnabled = true;
        Assert.That(session.MarkExitInterventionShown(), Is.True);
    }
}
