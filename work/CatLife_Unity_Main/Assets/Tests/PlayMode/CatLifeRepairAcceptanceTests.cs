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

public sealed class CatLifeRepairAcceptanceTests
{
    const string Key = "CatLife.Mobile.Data.v1";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    string original; bool had;
    [SetUp] public void Preserve() { had = PlayerPrefs.HasKey(Key); original = PlayerPrefs.GetString(Key); }
    [TearDown] public void Restore() { if (had) PlayerPrefs.SetString(Key, original); else PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    static void Call(Component app, string name, params object[] args) => app.GetType().GetMethod(name, Private).Invoke(app, args);
    static object Field(Component app, string name) => app.GetType().GetField(name, Private).GetValue(app);
    static Component App() => GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
    static Text Text(string name) => UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == name);

    [TestCase("你\u200b患有抑郁症")]
    [TestCase("你患有焦 虑 症")]
    [TestCase("建议你自行停药")]
    [TestCase("不继续学习我就不理你")]
    [TestCase("再退出我就离开你")]
    [TestCase("先打开材料，写一行", true)]
    [TestCase("休息一下也可以", true)]
    public void FullOutputBoundary(string line, bool accepted = false)
    {
        Type t = Type.GetType("CatLife.LLM.LLMBehaviorSuggestion, Assembly-CSharp");
        foreach (string field in new[] { "suggestedLine", "gentleReminder", "returnSuggestion", "catReactionReason" })
        {
            object raw = Activator.CreateInstance(t); t.GetField(field).SetValue(raw, line);
            object[] args = { raw, null, null };
            Assert.That((bool)t.GetMethod("TryBuildSafe").Invoke(null, args), Is.EqualTo(accepted), field);
        }
        if (!accepted)
        {
            object raw = Activator.CreateInstance(t); t.GetField("suggestedLine").SetValue(raw, new string('好', 49) + line);
            Assert.That((bool)t.GetMethod("TryBuildSafe").Invoke(null, new object[] { raw, null, null }), Is.False, "Check before truncation");
        }
    }

    [TestCase("🐱")]
    [TestCase("abcdefghijklmnopqrstuvw🐱")]
    [TestCase("猫猫猫猫猫猫猫猫猫猫猫猫猫猫猫猫猫猫猫猫猫猫猫🐱")]
    [TestCase("")]
    public void TextCutKeepsUtf16Pairs(string line)
    {
        Type t = Type.GetType("CatLife.LLM.LLMBehaviorSuggestion, Assembly-CSharp");
        object raw = Activator.CreateInstance(t); t.GetField("gentleReminder").SetValue(raw, line);
        object[] args = { raw, null, null }; Assert.That((bool)t.GetMethod("TryBuildSafe").Invoke(null, args), Is.True);
        string output = (string)t.GetField("gentleReminder").GetValue(args[1]);
        Assert.That(output.Length, Is.LessThanOrEqualTo(24));
        for (int i = 0; i < output.Length; i++)
            if (char.IsSurrogate(output[i])) { Assert.That(char.IsSurrogatePair(output, i), Is.True); i++; }
    }

    [UnityTest] public IEnumerator CorruptSaveRecoveryPreservesOriginalUntilConfirmed()
    {
        foreach (string broken in new[] { "{", "null" })
        {
            PlayerPrefs.SetString(Key, broken); SceneManager.LoadScene("CatLifeMobile"); yield return null;
            Component app = App();
            Assert.That(Field(app, "saveRecovery"), Is.True);
            Assert.That(app.GetType().GetProperty("CurrentView").GetValue(app), Is.EqualTo("SaveRecoveryPanel"));
            Assert.That(Text("SaveRecoveryText").transform.parent.parent.gameObject.activeInHierarchy, Is.True);
            Call(app, "Save"); Call(app, "ConfirmRecoveryClear");
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(broken));
            Call(app, "RetrySavedData"); Call(app, "ExportSavedData");
            Assert.That(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.persistentDataPath, "CatLife-recovery-original.txt")), Is.EqualTo(broken));
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(broken));
            Call(app, "ConfirmRecoveryClear"); Call(app, "ConfirmRecoveryClear");
            Assert.That(Field(app, "saveRecovery"), Is.False);
            Assert.That(CatLifeDataJson.TryDeserialize(PlayerPrefs.GetString(Key), out var data), Is.True);
            Assert.That(data.records, Is.Empty); Assert.That(data.settings.aiConsent, Is.False);
        }
    }

    [UnityTest] public IEnumerator CancelledClientCanImmediatelyRequestWithoutOldCallback()
    {
        Type type = Type.GetType("CatLife.LLM.MockCatLLMClient, Assembly-CSharp");
        var host = new GameObject("RepairCancellation");
        Component client = host.AddComponent(type);
        type.GetField("preferGenericCloudWhenConfigured", Private).SetValue(client, false);
        type.GetField("simulatedLatencySeconds", Private).SetValue(client, .15f);
        int cancelled = 0;
        Action<string> onError = reason => { Assert.That(reason, Is.EqualTo("cancelled")); cancelled++; };
        type.GetMethod("RequestSuggestion").Invoke(client, new object[] { null, null, null, onError });
        type.GetMethod("CancelPending").Invoke(client, new object[] { "cancelled" });
        string error = null;
        type.GetMethod("RequestSuggestion").Invoke(client, new object[] { null, null, null, (Action<string>)(s => error = s) });
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(error, Is.Null); Assert.That(cancelled, Is.EqualTo(1));
        Assert.That(type.GetProperty("IsBusy").GetValue(client), Is.False);
        UnityEngine.Object.DestroyImmediate(host);
    }

    [UnityTest] public IEnumerator RetryRecoveryRestoresValidFocusAndDoesNotRetryOldAi()
    {
        PlayerPrefs.SetString(Key, "{"); SceneManager.LoadScene("CatLifeMobile"); yield return null;
        Component app = App();
        var repaired = new CatLifeAppData(); repaired.settings.catAdopted = true; repaired.settings.localBehaviorStatsEnabled = false;
        repaired.records.Add(new CatLifeSessionRecord { aiRequestAttempted = true, aiRequestCount = 1, aiRequestStatus = "requesting" });
        var controller = new CatLifeSessionController(repaired); long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        controller.BeginTransition(60, now); controller.EnterFocus(now); long deadline = repaired.activeSession.targetEndAt;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(repaired)); Call(app, "RetrySavedData");
        Assert.That(Field(app, "saveRecovery"), Is.False);
        var data = (CatLifeAppData)Field(app, "data");
        Assert.That(data.activeSession.targetEndAt, Is.EqualTo(deadline)); Assert.That(data.activeSession.behaviorStatsDisabled, Is.True);
        Assert.That(data.records[0].aiRequestStatus, Is.EqualTo("failed")); Assert.That(data.records[0].aiRequestCount, Is.EqualTo(1));
        Assert.That(app.GetType().GetProperty("CurrentView").GetValue(app), Is.EqualTo("FocusPanel"));
    }

    [UnityTest] public IEnumerator DestroyingClientDoesNotCallDestroyedUi()
    {
        Type type = Type.GetType("CatLife.LLM.MockCatLLMClient, Assembly-CSharp");
        var host = new GameObject("RepairDestroy"); Component client = host.AddComponent(type);
        type.GetField("preferGenericCloudWhenConfigured", Private).SetValue(client, false);
        type.GetField("simulatedLatencySeconds", Private).SetValue(client, 1f);
        int callbacks = 0;
        type.GetMethod("RequestSuggestion").Invoke(client, new object[] { null, null, null, (Action<string>)(_ => callbacks++) });
        UnityEngine.Object.DestroyImmediate(host); yield return new WaitForSecondsRealtime(.2f);
        Assert.That(callbacks, Is.Zero);
    }

    [UnityTest] public IEnumerator AiOffOnCannotReviveCancelledRecord()
    {
        var initial = new CatLifeAppData(); initial.settings.catAdopted = true;
        initial.settings.aiEnabled = initial.settings.aiConsent = true;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(initial));
        SceneManager.LoadScene("CatLifeMobile"); yield return null;
        Component app = App(); var data = (CatLifeAppData)Field(app, "data");
        var record = new CatLifeSessionRecord { aiRequestStatus = "requesting", aiRequestAttempted = true, aiRequestCount = 1 };
        data.records.Add(record); Call(app, "ToggleAi"); Call(app, "ToggleAi");
        Type t = Type.GetType("CatLife.LLM.LLMBehaviorSuggestion, Assembly-CSharp");
        Call(app, "ApplyAiSuggestion", data, record, Activator.CreateInstance(t));
        Assert.That(record.aiRequestStatus, Is.EqualTo("cancelled")); Assert.That(record.aiRequestCount, Is.EqualTo(1));
        Assert.That(record.aiGentleReminder, Is.Null.Or.Empty);
    }

    [TestCase(-3600)] [TestCase(3600)]
    public void ElapsedClockIgnoresWallClockChanges(int wallChange)
    {
        var clock = new CatLifeSessionClock(); clock.Anchor(10000, 500);
        var data = new CatLifeAppData(); var session = new CatLifeSessionController(data);
        session.BeginTransition(60, 10000); session.EnterFocus(10000);
        long wall = 10030 + wallChange; // calendar may change, interval progress must not.
        Assert.That(session.RemainingSeconds(clock.Now(530)), Is.EqualTo(30));
        Assert.That(session.TryComplete(clock.Now(530), out _), Is.False);
        Assert.That(session.TryComplete(clock.Now(560), out _), Is.True);
        Assert.That(data.records.Single().actualSeconds, Is.EqualTo(60));
        Assert.That(wall, Is.Not.EqualTo(clock.Now(530)));
    }

    [Test] public void WeeklyTotalSumsUnroundedSecondsAcrossDays()
    {
        long now = new DateTimeOffset(DateTime.Now.Date.AddHours(12)).ToUnixTimeSeconds();
        var data = new CatLifeAppData();
        for (int day = 0; day < 2; day++) data.records.Add(new CatLifeSessionRecord { endedAt = now - day * 86400, actualSeconds = 59 });
        var stats = CatLifeStats.Derive(data, now);
        Assert.That(stats.SevenDayMinutes, Is.EqualTo(1)); Assert.That(stats.SevenDayMinutesByDay.Sum(), Is.Zero);
        Assert.That(stats.Paws + stats.TotalGrowth, Is.Zero);
    }

    [UnityTest] public IEnumerator ClockRollbackAfterRestartRequiresExplicitChoice()
    {
        foreach (bool resume in new[] { true, false })
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var data = new CatLifeAppData(); data.settings.catAdopted = true;
            var session = new CatLifeSessionController(data); session.BeginTransition(60, now + 3600); session.EnterFocus(now + 3600);
            string json = CatLifeDataJson.Serialize(data); PlayerPrefs.SetString(Key, json);
            SceneManager.LoadScene("CatLifeMobile"); yield return null; Component app = App();
            Assert.That(Field(app, "clockRecovery"), Is.True); Call(app, "Save");
            Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(json));
            Call(app, resume ? "ContinueAfterClockChange" : "InterruptAfterClockChange");
            Assert.That(Field(app, "clockRecovery"), Is.False);
            var loaded = CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key));
            if (resume) Assert.That(loaded.activeSession.targetEndAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Is.InRange(58, 60));
            else { Assert.That(loaded.records.Count, Is.EqualTo(1)); Assert.That(loaded.records[0].GrowthAwarded + loaded.records[0].PawsAwarded, Is.Zero); }
        }
    }

    [UnityTest] public IEnumerator NicknameStaysLiteralAndSurvivesReload()
    {
        var initial = new CatLifeAppData(); initial.settings.catAdopted = true; initial.settings.catName = "<size=99>橘";
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(initial)); SceneManager.LoadScene("CatLifeMobile"); yield return null;
        Component app = App(); Call(app, "ShowGrowth");
        Assert.That(Text("GrowthNameText").supportRichText, Is.False);
        Assert.That(Text("GrowthNameText").text, Does.Contain(initial.settings.catName));
        Assert.That(Text("CatNameText").supportRichText, Is.False);
        Assert.That(Text("GrowthText").supportRichText, Is.True);
        Call(app, "Save"); Assert.That(CatLifeDataJson.Deserialize(PlayerPrefs.GetString(Key)).settings.catName, Is.EqualTo(initial.settings.catName));
        float scale = Time.timeScale; Time.timeScale = 0;
        try { yield return new WaitForSecondsRealtime(.1f); Assert.That(Time.realtimeSinceStartupAsDouble, Is.GreaterThan(0)); }
        finally { Time.timeScale = scale; }
    }

#if UNITY_EDITOR
    [UnityTest] public IEnumerator ForegroundCallbackBeforeStartUsesInitializedSessionClock()
    {
        foreach (bool rollback in new[] { false, true })
        {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var initial = new CatLifeAppData(); initial.settings.catAdopted = true;
        long start = rollback ? now + 3600 : now - 20;
        var controller = new CatLifeSessionController(initial); controller.BeginTransition(60, start); controller.EnterFocus(start);
        if (rollback) initial.records.Add(new CatLifeSessionRecord { aiRequestAttempted = true, aiRequestCount = 1, aiRequestStatus = "requesting" });
        string originalJson = CatLifeDataJson.Serialize(initial);
        PlayerPrefs.SetString(Key, originalJson);
        long foregroundAt = 0;
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> loaded = (scene, mode) =>
        {
            Component app = App(); Call(app, "OnApplicationPause", false);
            foregroundAt = ((CatLifeAppData)Field(app, "data")).activeSession.stableSegmentStartedAt;
        };
        SceneManager.sceneLoaded += loaded;
        try { SceneManager.LoadScene("CatLifeMobile"); yield return null; }
        finally { SceneManager.sceneLoaded -= loaded; }
        if (rollback) Assert.That(PlayerPrefs.GetString(Key), Is.EqualTo(originalJson), "Rollback recovery must block startup writes before a choice.");
        else Assert.That(foregroundAt, Is.InRange(now - 1, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1), "Startup foreground callback used an unanchored elapsed clock.");
        }
    }

    [UnityTest] public IEnumerator CandidatePreparationActuallyEntersFocus()
    {
        var initial = new CatLifeAppData(); initial.settings.catAdopted = true; initial.settings.defaultMinutes = 1;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(initial));
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/Editor/CatProneReview/NavigationTurnCandidate/CatLifeNavigationTurnReview.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return null; Camera.main.aspect = 1080f / 2424f;
        Component app = App(); Call(app, "BeginTransition");
        double until = Time.realtimeSinceStartupAsDouble + 35;
        while ((CatLifeSessionPhase)app.GetType().GetProperty("CurrentPhase").GetValue(app) == CatLifeSessionPhase.Transition && Time.realtimeSinceStartupAsDouble < until) yield return null;
        var animator = GameObject.Find("CatLifeMobileCat").GetComponent<Animator>();
        var presenter = animator.GetComponent("CatLifeMobileCatPresenter");
        string diagnostics = "state=" + animator.GetCurrentAnimatorStateInfo(0).fullPathHash + " normalized=" + animator.GetCurrentAnimatorStateInfo(0).normalizedTime.ToString("R")
            + " speed=" + animator.speed + " clip=" + string.Join(",", animator.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name))
            + " ready=" + presenter.GetType().GetProperty("SessionPoseReady").GetValue(presenter)
            + " command=" + presenter.GetType().GetProperty("LastAnimationCommand").GetValue(presenter)
            + " coroutine=" + (presenter.GetType().GetField("sitRecoveryRoutine", Private).GetValue(presenter) != null)
            + " cameraStarted=" + Field(app, "transitionCameraStarted");
        Assert.That(app.GetType().GetProperty("CurrentPhase").GetValue(app), Is.EqualTo(CatLifeSessionPhase.Focus), diagnostics);
    }
#endif
}
