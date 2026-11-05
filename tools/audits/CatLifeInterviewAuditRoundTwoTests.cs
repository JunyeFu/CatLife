using System;
using System.Collections;
using System.Reflection;
using CatLife.Mobile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Opt-in counterexamples for interview questions 6–9. Keep outside Assets after an audit.
public sealed class CatLifeInterviewAuditRoundTwoTests
{
    private const string Key = "CatLife.Mobile.Data.v1";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private bool hadData;
    private string saved;
    private GameObject host;

    [SetUp] public void Preserve()
    {
        hadData = PlayerPrefs.HasKey(Key);
        saved = PlayerPrefs.GetString(Key, "");
    }
    [TearDown] public void Restore()
    {
        if (host != null) UnityEngine.Object.DestroyImmediate(host);
        if (hadData) PlayerPrefs.SetString(Key, saved); else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
    private static Type Runtime(string name) => Type.GetType("CatLife.LLM." + name + ", Assembly-CSharp");
    private static object Field(object obj, string name) => obj.GetType().GetField(name, Private).GetValue(obj);
    private static void Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
    private static object Suggestion(string line)
    {
        Type type = Runtime("LLMBehaviorSuggestion");
        object raw = Activator.CreateInstance(type);
        type.GetField("gentleReminder").SetValue(raw, line);
        type.GetField("returnSuggestion").SetValue(raw, "写下下一步");
        type.GetField("catReactionReason").SetValue(raw, "安静陪伴");
        return raw;
    }
    private static void Seed()
    {
        var data = new CatLifeAppData();
        data.settings.catAdopted = true;
        PlayerPrefs.SetString(Key, CatLifeDataJson.Serialize(data));
    }

    [UnityTest]
    public IEnumerator LateResultKeepsOriginalRecordWithoutInterruptingNextFocus()
    {
        Seed(); SceneManager.LoadScene("CatLifeMobile"); yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var data = (CatLifeAppData)Field(app, "data");
        data.settings.aiEnabled = true; data.settings.aiConsent = true;
        var old = new CatLifeSessionRecord { completed = true, actualSeconds = 60, plannedSeconds = 60,
            aiRequestAttempted = true, aiRequestStatus = "requesting", aiRequestCount = 1 };
        data.records.Add(old);
        var session = (CatLifeSessionController)Field(app, "session");
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        session.BeginTransition(60, now); session.EnterFocus(now);
        Call(app, "ShowFocus", true);
        long deadline = data.activeSession.targetEndAt;
        Component client = app.GetComponent("MockCatLLMClient");
        client.GetType().GetField("<LastSource>k__BackingField", Private).SetValue(client, "mimo_cloud");
        Call(app, "ApplyAiSuggestion", data, old, Suggestion("旧会话已完成"));
        Assert.That(old.aiRequestStatus, Is.EqualTo("succeeded"));
        Assert.That(data.records.Count, Is.EqualTo(1));
        Assert.That(data.activeSession.targetEndAt, Is.EqualTo(deadline));
        Assert.That(session.Phase, Is.EqualTo(CatLifeSessionPhase.Focus));
        Assert.That(app.GetType().GetProperty("CurrentView").GetValue(app), Is.EqualTo("FocusPanel"));
    }

    private Component NewLocalClient(float latency)
    {
        host = new GameObject("RoundTwoLocalClient");
        Component client = host.AddComponent(Runtime("MockCatLLMClient"));
        client.GetType().GetField("preferGenericCloudWhenConfigured", Private).SetValue(client, false);
        client.GetType().GetField("simulatedLatencySeconds", Private).SetValue(client, latency);
        return client;
    }
    private static void Request(Component client, Action<string> error)
    {
        client.GetType().GetMethod("RequestSuggestion").Invoke(client, new object[] { null, null, null, error });
    }
    private static bool Busy(Component client) => (bool)client.GetType().GetProperty("IsBusy").GetValue(client);

    [UnityTest]
    public IEnumerator SecondConcurrentRequestIsRejectedAndFirstStillFinishes()
    {
        Component client = NewLocalClient(.15f);
        string error = null; int errors = 0;
        Request(client, _ => Assert.Fail("First local request failed."));
        Request(client, value => { error = value; errors++; });
        Assert.That(error, Does.Contain("busy")); Assert.That(errors, Is.EqualTo(1));
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(Busy(client), Is.False);
        Assert.That(client.GetType().GetProperty("LastSource").GetValue(client), Is.EqualTo("local_template"));
    }

    [UnityTest]
    public IEnumerator UserCatNameMustNotBecomeGrowthRichTextMarkup()
    {
        Seed(); SceneManager.LoadScene("CatLifeMobile"); yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        InputField input = GameObject.Find("CatLifeMobileView").transform.Find("TransientLayer/AdoptionPanel/AdoptionCard/CatNameInput")?.GetComponent<InputField>();
        if (input == null)
            foreach (InputField candidate in UnityEngine.Object.FindObjectsByType<InputField>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.name == "CatNameInput") input = candidate;
        Assert.That(input, Is.Not.Null);
        input.text = "<b>橘</b>";
        Call(app, "AdoptCat"); Call(app, "ShowGrowth");
        Text text = (Text)Field(app, "growthText");
        Assert.That(text.gameObject.activeInHierarchy, Is.True, "Check a visible growth label.");
        Assert.That(text.supportRichText && text.text.Contains("<b>橘</b>"), Is.False,
            "User-controlled nickname was interpolated into an enabled rich-text label without escaping.");
    }

    [Test]
    public void SuggestedTextTruncationMustNotSplitEmojiSurrogatePair()
    {
        Type type = Runtime("LLMBehaviorSuggestion");
        object raw = Suggestion(new string('学', 23) + "\U0001F431");
        object[] args = { raw, null, null };
        Assert.That((bool)type.GetMethod("TryBuildSafe").Invoke(null, args), Is.True);
        string result = (string)type.GetField("gentleReminder").GetValue(args[1]);
        Assert.That(result.Length > 0 && char.IsHighSurrogate(result[result.Length - 1]), Is.False,
            "The 24-code-unit truncation produced an unpaired UTF-16 high surrogate.");
    }

    [UnityTest]
    public IEnumerator ModelRewardTextMustRemainPlainText()
    {
        Seed(); SceneManager.LoadScene("CatLifeMobile"); yield return null;
        Component app = GameObject.Find("CatLifeMobileApp").GetComponent("CatLifeMobileApp");
        var record = new CatLifeSessionRecord { completed = true, plannedSeconds = 60, actualSeconds = 60,
            aiRequestStatus = "succeeded", aiGentleReminder = "<size=200>文本</size>",
            aiReturnSuggestion = "写下下一步", aiReactionReason = "安静陪伴" };
        Call(app, "RenderReward", record);
        Component ui = GameObject.Find("CatLifeMobileView").GetComponent("CatLifeMobileUiPresentation");
        Text visible = (Text)ui.GetType().GetField("rewardInsight").GetValue(ui);
        Assert.That(visible.supportRichText, Is.False, "The actual serialized insight label must render model text literally.");
    }

    [TestCase(59)]
    [TestCase(90)]
    [TestCase(119)]
    public void DailyAndWeeklyTotalsMustRoundAfterSummingSeconds(int seconds)
    {
        long now = DateTimeOffset.Now.ToUnixTimeSeconds();
        var data = new CatLifeAppData();
        for (int i = 0; i < 2; i++) data.records.Add(new CatLifeSessionRecord {
            endedAt = now - i * 200, actualSeconds = seconds, plannedSeconds = 180, completed = false });
        var stats = CatLifeStats.Derive(data, now);
        int expected = 2 * seconds / 60;
        Assert.That(stats.TodayMinutes, Is.EqualTo(expected), "Sub-minute remainders were lost before aggregation.");
        Assert.That(stats.SevenDayMinutesByDay[6], Is.EqualTo(expected));
    }

    [Test]
    public void CrossMidnightUsesCompletionDayAndInterruptionsNeverUnlockActions()
    {
        DateTimeOffset today = new DateTimeOffset(DateTime.Now.Date.AddHours(12));
        long end = new DateTimeOffset(DateTime.Now.Date.AddSeconds(10)).ToUnixTimeSeconds();
        var data = new CatLifeAppData();
        data.records.Add(new CatLifeSessionRecord { startedAt = end - 60, endedAt = end,
            actualSeconds = 60, plannedSeconds = 60, completed = false });
        var stats = CatLifeStats.Derive(data, today.ToUnixTimeSeconds());
        Assert.That(stats.TodayMinutes, Is.EqualTo(1));
        Assert.That(stats.SevenDayMinutesByDay[5], Is.Zero);
        Assert.That(stats.Paws + stats.TotalGrowth, Is.Zero);
        Assert.That(CatLifeUnlocks.GetUnlockedReactions(stats.Paws), Is.Empty);
    }

    [UnityTest]
    public IEnumerator DeactivatingHostMustReleaseBusyBeforeReuse()
    {
        Component client = NewLocalClient(.15f); Request(client, null);
        Assert.That(Busy(client), Is.True);
        host.SetActive(false); yield return new WaitForSecondsRealtime(.3f); host.SetActive(true);
        string error = null; Request(client, value => error = value);
        Assert.That(error, Is.Null, "Deactivated coroutine left the client permanently busy on reuse.");
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(Busy(client), Is.False);
    }

    [UnityTest]
    public IEnumerator DisablingOnlyBehaviourDoesNotStopItsCoroutine()
    {
        Component client = NewLocalClient(.15f); Request(client, null);
        ((Behaviour)client).enabled = false;
        yield return new WaitForSecondsRealtime(.3f);
        ((Behaviour)client).enabled = true;
        Assert.That(Busy(client), Is.False);
    }
}
